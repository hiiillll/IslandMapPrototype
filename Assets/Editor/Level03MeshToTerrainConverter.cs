using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Converts Level03's generated land Mesh into a seamless 4x4 Terrain grid.
/// The original Mesh remains in the scene as a disabled rollback source.
/// </summary>
[InitializeOnLoad]
public static class Level03MeshToTerrainConverter
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string EnvironmentName = "ENVIRONMENT_Level03";
    private const string LandMeshName = "ENV_Level03_FlatIslands_And_MainMountain";
    private const string ConvertedRootName = "ENV_Level03_ConvertedTerrain";
    private const string OutputFolder = "Assets/Level03/ConvertedTerrain";
    private const string FlatLandLayerPath = "Assets/Level03/FlatTerrain/TL_Level03_FlatLand.terrainlayer";
    private const string BeachLayerPath = "Assets/Level03/TerrainLayers/TL_Level03_Beach.terrainlayer";
    private const string RequestAssetPath = "Assets/Editor/Level03MeshToTerrainConverter.request";
    private const string ReportPath = "Library/CodexLevel03MeshToTerrainReport.json";

    private const int TileCountX = 4;
    private const int TileCountZ = 4;
    private const float TileSize = 1000f;
    private const float TerrainHeight = 400f;
    private const int HeightmapResolution = 513;
    private const int AlphamapResolution = 512;

    [Serializable]
    private sealed class ConversionReport
    {
        public bool success;
        public string message;
        public int tileCount;
        public int heightmapResolutionPerTile;
        public int visibleHoleCells;
        public int totalHoleCells;
        public float minimumSampledHeight;
        public float maximumSampledHeight;
        public bool originalMeshRendererDisabled;
        public bool originalMeshColliderDisabled;
        public string outputFolder;
        public string completedAt;
    }

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    private static string RequestFilePath => Path.Combine(ProjectRoot, RequestAssetPath);
    private static string ReportFilePath => Path.Combine(ProjectRoot, ReportPath);

    static Level03MeshToTerrainConverter()
    {
        if (File.Exists(RequestFilePath))
        {
            EditorApplication.delayCall += ConvertOnce;
        }
    }

    [MenuItem("Tools/Island Map/Level03/Convert Generated Land Mesh To Terrain")]
    public static void ConvertFromMenu()
    {
        try
        {
            ConversionReport report = Convert();
            WriteReport(report);
            EditorUtility.DisplayDialog("Level03 Mesh To Terrain", report.message, "OK");
        }
        catch (Exception exception)
        {
            WriteFailureReport(exception);
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Level03 Mesh To Terrain", exception.Message, "OK");
        }
    }

    private static void ConvertOnce()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ConvertOnce;
            return;
        }

        try
        {
            ConversionReport report = Convert();
            WriteReport(report);
            Debug.Log("[Level03 Mesh To Terrain] " + report.message);
        }
        catch (Exception exception)
        {
            WriteFailureReport(exception);
            Debug.LogException(exception);
        }
        finally
        {
            if (File.Exists(RequestFilePath))
            {
                File.Delete(RequestFilePath);
            }

            string metaPath = RequestFilePath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            AssetDatabase.Refresh();
        }
    }

    private static ConversionReport Convert()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException(
                "Level03 must be the active scene. Open Assets/Scenes/Level03.unity and run the tool again.");
        }

        GameObject environment = FindSceneObject(scene, EnvironmentName);
        GameObject originalLand = FindSceneObject(scene, LandMeshName);
        MeshRenderer originalRenderer = originalLand.GetComponent<MeshRenderer>();
        MeshCollider originalCollider = originalLand.GetComponent<MeshCollider>();
        MeshFilter originalFilter = originalLand.GetComponent<MeshFilter>();

        if (originalRenderer == null ||
            originalCollider == null ||
            originalFilter == null ||
            originalFilter.sharedMesh == null)
        {
            throw new InvalidOperationException("The generated land Mesh components are incomplete.");
        }

        if (FindSceneObjectOrNull(scene, ConvertedRootName) != null)
        {
            throw new InvalidOperationException(
                $"'{ConvertedRootName}' already exists. Remove or rename it before converting again.");
        }

        if (AssetDatabase.IsValidFolder(OutputFolder))
        {
            throw new InvalidOperationException(
                $"'{OutputFolder}' already exists. Remove or rename it before converting again.");
        }

        TerrainLayer flatLandLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(FlatLandLayerPath);
        TerrainLayer beachLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(BeachLayerPath);
        if (flatLandLayer == null || beachLayer == null)
        {
            throw new InvalidOperationException(
                "The shared flat-land and beach TerrainLayer assets must exist before conversion.");
        }

        bool rendererWasEnabled = originalRenderer.enabled;
        bool colliderWasEnabled = originalCollider.enabled;
        originalRenderer.enabled = true;
        originalCollider.enabled = true;
        Physics.SyncTransforms();

        CreateFolder(OutputFolder);
        GameObject convertedRoot = new GameObject(ConvertedRootName);
        convertedRoot.transform.SetParent(environment.transform, false);
        convertedRoot.layer = originalLand.layer;
        Undo.RegisterCreatedObjectUndo(convertedRoot, "Convert Level03 Mesh To Terrain");

        List<Terrain> terrains = new List<Terrain>(TileCountX * TileCountZ);
        List<string> createdAssetPaths = new List<string>();
        int visibleHoleCells = 0;
        int totalHoleCells = 0;
        float minimumHeight = float.PositiveInfinity;
        float maximumHeight = float.NegativeInfinity;
        float rayStartY = originalRenderer.bounds.max.y + 10f;
        float rayDistance = originalRenderer.bounds.size.y + TerrainHeight + 20f;
        StaticEditorFlags staticFlags = GameObjectUtility.GetStaticEditorFlags(originalLand);

        try
        {
            for (int row = 0; row < TileCountZ; row++)
            {
                for (int column = 0; column < TileCountX; column++)
                {
                    int tileIndex = row * TileCountX + column;
                    EditorUtility.DisplayProgressBar(
                        "Level03 Mesh To Terrain",
                        $"Sampling terrain tile {tileIndex + 1}/{TileCountX * TileCountZ}",
                        (float)tileIndex / (TileCountX * TileCountZ));

                    Vector3 tilePosition = new Vector3(
                        -TileCountX * TileSize * 0.5f + column * TileSize,
                        0f,
                        -TileCountZ * TileSize * 0.5f + row * TileSize);

                    float[,] heights = new float[HeightmapResolution, HeightmapResolution];
                    bool[,] samplesHitLand = new bool[HeightmapResolution, HeightmapResolution];

                    for (int y = 0; y < HeightmapResolution; y++)
                    {
                        float worldZ = tilePosition.z + (float)y / (HeightmapResolution - 1) * TileSize;
                        for (int x = 0; x < HeightmapResolution; x++)
                        {
                            float worldX = tilePosition.x + (float)x / (HeightmapResolution - 1) * TileSize;
                            Ray ray = new Ray(new Vector3(worldX, rayStartY, worldZ), Vector3.down);

                            if (originalCollider.Raycast(ray, out RaycastHit hit, rayDistance))
                            {
                                float sampledHeight = Mathf.Clamp(hit.point.y, 0f, TerrainHeight);
                                heights[y, x] = sampledHeight / TerrainHeight;
                                samplesHitLand[y, x] = true;
                                minimumHeight = Mathf.Min(minimumHeight, sampledHeight);
                                maximumHeight = Mathf.Max(maximumHeight, sampledHeight);
                            }
                            else
                            {
                                heights[y, x] = 0f;
                                samplesHitLand[y, x] = false;
                            }
                        }
                    }

                    bool[,] holes = new bool[HeightmapResolution - 1, HeightmapResolution - 1];
                    for (int y = 0; y < HeightmapResolution - 1; y++)
                    {
                        for (int x = 0; x < HeightmapResolution - 1; x++)
                        {
                            bool visible =
                                samplesHitLand[y, x] ||
                                samplesHitLand[y + 1, x] ||
                                samplesHitLand[y, x + 1] ||
                                samplesHitLand[y + 1, x + 1];
                            holes[y, x] = visible;
                            if (visible)
                            {
                                visibleHoleCells++;
                            }

                            totalHoleCells++;
                        }
                    }

                    TerrainData terrainData = new TerrainData
                    {
                        heightmapResolution = HeightmapResolution,
                        alphamapResolution = AlphamapResolution,
                        baseMapResolution = 1024,
                        size = new Vector3(TileSize, TerrainHeight, TileSize),
                        terrainLayers = new[] { flatLandLayer, beachLayer },
                        name = $"TD_Level03_Converted_{column}_{row}"
                    };

                    terrainData.SetHeights(0, 0, heights);
                    terrainData.SetHoles(0, 0, holes);

                    float[,,] alphamaps = new float[AlphamapResolution, AlphamapResolution, 2];
                    for (int y = 0; y < AlphamapResolution; y++)
                    {
                        for (int x = 0; x < AlphamapResolution; x++)
                        {
                            alphamaps[y, x, 0] = 1f;
                            alphamaps[y, x, 1] = 0f;
                        }
                    }

                    terrainData.SetAlphamaps(0, 0, alphamaps);

                    string assetPath =
                        $"{OutputFolder}/TD_Level03_Converted_{column}_{row}.asset";
                    AssetDatabase.CreateAsset(terrainData, assetPath);
                    createdAssetPaths.Add(assetPath);

                    GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
                    terrainObject.name = $"Terrain_Level03_{column}_{row}";
                    terrainObject.layer = originalLand.layer;
                    terrainObject.transform.SetParent(convertedRoot.transform, true);
                    terrainObject.transform.position = tilePosition;
                    GameObjectUtility.SetStaticEditorFlags(terrainObject, staticFlags);

                    Terrain terrain = terrainObject.GetComponent<Terrain>();
                    terrain.groupingID = 3;
                    terrain.allowAutoConnect = true;
                    terrain.drawInstanced = true;
                    terrain.heightmapPixelError = 3f;
                    terrain.basemapDistance = 2000f;
                    terrain.treeDistance = 5000f;
                    terrains.Add(terrain);
                }
            }

            ConnectTerrainNeighbors(terrains);

            TerrainCollider staleRootCollider = environment.GetComponent<TerrainCollider>();
            if (staleRootCollider != null && environment.GetComponent<Terrain>() == null)
            {
                UnityEngine.Object.DestroyImmediate(staleRootCollider);
            }

            originalRenderer.enabled = false;
            originalCollider.enabled = false;
            EditorUtility.SetDirty(originalRenderer);
            EditorUtility.SetDirty(originalCollider);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new IOException("Unity could not save the converted Level03 scene.");
            }
        }
        catch
        {
            if (convertedRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(convertedRoot);
            }

            foreach (string assetPath in createdAssetPaths)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            AssetDatabase.DeleteAsset(OutputFolder);
            originalRenderer.enabled = rendererWasEnabled;
            originalCollider.enabled = colliderWasEnabled;
            throw;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        return new ConversionReport
        {
            success = true,
            message =
                $"Converted the generated Level03 land Mesh into {terrains.Count} seamless Terrain tiles. " +
                "Height, coastline holes, grass, and the manual beach layer are ready; the original Mesh is disabled as a rollback source.",
            tileCount = terrains.Count,
            heightmapResolutionPerTile = HeightmapResolution,
            visibleHoleCells = visibleHoleCells,
            totalHoleCells = totalHoleCells,
            minimumSampledHeight = float.IsPositiveInfinity(minimumHeight) ? 0f : minimumHeight,
            maximumSampledHeight = float.IsNegativeInfinity(maximumHeight) ? 0f : maximumHeight,
            originalMeshRendererDisabled = !originalRenderer.enabled,
            originalMeshColliderDisabled = !originalCollider.enabled,
            outputFolder = OutputFolder,
            completedAt = DateTime.Now.ToString("O")
        };
    }

    private static void ConnectTerrainNeighbors(IReadOnlyList<Terrain> terrains)
    {
        for (int row = 0; row < TileCountZ; row++)
        {
            for (int column = 0; column < TileCountX; column++)
            {
                Terrain current = terrains[row * TileCountX + column];
                Terrain left = column > 0 ? terrains[row * TileCountX + column - 1] : null;
                Terrain right = column < TileCountX - 1 ? terrains[row * TileCountX + column + 1] : null;
                Terrain top = row < TileCountZ - 1 ? terrains[(row + 1) * TileCountX + column] : null;
                Terrain bottom = row > 0 ? terrains[(row - 1) * TileCountX + column] : null;
                current.SetNeighbors(left, top, right, bottom);
                current.Flush();
            }
        }
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        GameObject result = FindSceneObjectOrNull(scene, objectName);
        if (result == null)
        {
            throw new InvalidOperationException(
                $"Could not find '{objectName}' in the active Level03 scene.");
        }

        return result;
    }

    private static GameObject FindSceneObjectOrNull(Scene scene, string objectName)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(candidate =>
                candidate.scene == scene &&
                candidate.name == objectName);
    }

    private static void CreateFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }

            current = next;
        }
    }

    private static void WriteFailureReport(Exception exception)
    {
        WriteReport(new ConversionReport
        {
            success = false,
            message = exception.GetType().Name + ": " + exception.Message,
            completedAt = DateTime.Now.ToString("O")
        });
    }

    private static void WriteReport(ConversionReport report)
    {
        File.WriteAllText(ReportFilePath, JsonUtility.ToJson(report, true));
    }
}
