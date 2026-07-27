using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds a shared beach TerrainLayer to every Terrain tile in Level03 and blends it
/// with the authored beach mask without changing heights, trees, details, or objects.
/// </summary>
[InitializeOnLoad]
public static class Level03BeachTerrainInstaller
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string BeachTexturePath = "Assets/Art/Textures/Beach.png";
    private const string FlatLandLayerPath = "Assets/Level03/FlatTerrain/TL_Level03_FlatLand.terrainlayer";
    private const string BeachLayerFolder = "Assets/Level03/TerrainLayers";
    private const string BeachLayerPath = BeachLayerFolder + "/TL_Level03_Beach.terrainlayer";
    private const string RequestAssetPath = "Assets/Editor/Level03BeachSetup.request";
    private const string ReportPath = "Library/CodexLevel03BeachSetupReport.json";
    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    private static string RequestFilePath => Path.Combine(ProjectRoot, RequestAssetPath);
    private static string ReportFilePath => Path.Combine(ProjectRoot, ReportPath);

    [Serializable]
    private sealed class SetupReport
    {
        public bool success;
        public string message;
        public int terrainCount;
        public int terrainLayerCount;
        public int changedAlphamapPixels;
        public float beachCoveragePercent;
        public string terrainBounds;
        public string completedAt;
    }

    private sealed class TerrainSnapshot
    {
        public Terrain terrain;
        public TerrainLayer[] layers;
        public float[,,] alphamaps;
    }

    static Level03BeachTerrainInstaller()
    {
        if (File.Exists(RequestFilePath))
        {
            EditorApplication.delayCall += ProcessOneShotRequest;
        }
    }

    [MenuItem("Tools/Island Map/Level03/Add Beach Brush Only")]
    public static void AddBeachBrushOnlyFromMenu()
    {
        try
        {
            SetupReport report = AddBeachBrushOnly();
            WriteReport(report);
            EditorUtility.DisplayDialog(
                "Level03 Beach Brush",
                report.message + "\n\nYou can undo the operation before closing Unity.",
                "OK");
        }
        catch (Exception exception)
        {
            WriteFailureReport(exception);
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Level03 Beach Brush", exception.Message, "OK");
        }
    }

    private static void ProcessOneShotRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ProcessOneShotRequest;
            return;
        }

        try
        {
            SetupReport report = AddBeachBrushOnly();
            WriteReport(report);
            Debug.Log("[Level03 Beach] " + report.message);
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

            string requestMetaPath = RequestFilePath + ".meta";
            if (File.Exists(requestMetaPath))
            {
                File.Delete(requestMetaPath);
            }

            AssetDatabase.Refresh();
        }
    }

    private static SetupReport AddBeachBrushOnly()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.path != ScenePath)
        {
            throw new InvalidOperationException(
                "Level03 must be the active scene. Open Assets/Scenes/Level03.unity and run the tool again.");
        }

        Texture2D beachTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BeachTexturePath);
        if (beachTexture == null)
        {
            throw new FileNotFoundException("Beach texture was not found.", BeachTexturePath);
        }

        Terrain[] terrains = UnityEngine.Object.FindObjectsOfType<Terrain>(true)
            .Where(terrain => terrain.gameObject.scene == activeScene && terrain.terrainData != null)
            .OrderBy(terrain => terrain.transform.position.z)
            .ThenBy(terrain => terrain.transform.position.x)
            .ToArray();

        if (terrains.Length == 0)
        {
            throw new InvalidOperationException("No Terrain components were found in Level03.");
        }

        if (terrains.Select(terrain => terrain.terrainData).Distinct().Count() != terrains.Length)
        {
            throw new InvalidOperationException(
                "Two or more Terrain tiles share the same TerrainData. Make each tile use unique TerrainData first.");
        }

        TerrainLayer flatLandLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(FlatLandLayerPath);
        TerrainLayer beachLayer = GetOrCreateBeachLayer(beachTexture);
        List<TerrainLayer> canonicalLayers = BuildCanonicalLayerList(terrains, flatLandLayer, beachLayer);
        int beachLayerIndex = canonicalLayers.IndexOf(beachLayer);
        int fallbackLayerIndex = canonicalLayers.FindIndex(layer => layer != beachLayer);
        List<TerrainSnapshot> snapshots = CaptureTerrainSnapshots(terrains);

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add Level03 Beach Brush Only");
        int clearedBeachPixels = 0;

        foreach (TerrainSnapshot snapshot in snapshots)
        {
            TerrainData data = snapshot.terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(data, "Add Level03 Beach Brush Only");
            data.terrainLayers = canonicalLayers.ToArray();

            int resolution = data.alphamapResolution;
            float[,,] output = new float[resolution, resolution, canonicalLayers.Count];
            Dictionary<int, int> oldToCanonical = BuildLayerIndexMap(snapshot.layers, canonicalLayers);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float nonBeachWeight = 0f;
                    for (int oldIndex = 0; oldIndex < snapshot.layers.Length; oldIndex++)
                    {
                        int canonicalIndex = oldToCanonical[oldIndex];
                        float weight = snapshot.alphamaps[y, x, oldIndex];
                        if (canonicalIndex == beachLayerIndex)
                        {
                            if (weight > 0.0001f)
                            {
                                clearedBeachPixels++;
                            }

                            continue;
                        }

                        output[y, x, canonicalIndex] += weight;
                        nonBeachWeight += weight;
                    }

                    if (nonBeachWeight > 0.0001f)
                    {
                        float scale = 1f / nonBeachWeight;
                        for (int layerIndex = 0; layerIndex < canonicalLayers.Count; layerIndex++)
                        {
                            if (layerIndex != beachLayerIndex)
                            {
                                output[y, x, layerIndex] *= scale;
                            }
                        }
                    }
                    else
                    {
                        output[y, x, fallbackLayerIndex] = 1f;
                    }

                    output[y, x, beachLayerIndex] = 0f;
                }
            }

            data.SetAlphamaps(0, 0, output);
            EditorUtility.SetDirty(data);
            snapshot.terrain.Flush();
        }

        EditorUtility.SetDirty(beachLayer);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);

        return new SetupReport
        {
            success = true,
            message =
                $"Removed generated beach paint from {terrains.Length} Terrain tiles and kept " +
                "TL_Level03_Beach as a zero-weight manual paint brush.",
            terrainCount = terrains.Length,
            terrainLayerCount = canonicalLayers.Count,
            changedAlphamapPixels = clearedBeachPixels,
            beachCoveragePercent = 0f,
            completedAt = DateTime.Now.ToString("O")
        };
    }

    private static TerrainLayer GetOrCreateBeachLayer(Texture2D beachTexture)
    {
        EnsureFolder(BeachLayerFolder);
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(BeachLayerPath);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, BeachLayerPath);
        }

        layer.name = "TL_Level03_Beach";
        layer.diffuseTexture = beachTexture;
        layer.tileSize = new Vector2(42f, 42f);
        layer.tileOffset = Vector2.zero;
        layer.metallic = 0f;
        layer.smoothness = 0.08f;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static List<TerrainLayer> BuildCanonicalLayerList(
        IEnumerable<Terrain> terrains,
        TerrainLayer flatLandLayer,
        TerrainLayer beachLayer)
    {
        List<TerrainLayer> layers = new List<TerrainLayer>();
        if (flatLandLayer != null)
        {
            layers.Add(flatLandLayer);
        }

        foreach (Terrain terrain in terrains)
        {
            foreach (TerrainLayer layer in terrain.terrainData.terrainLayers)
            {
                if (layer != null && layer != beachLayer && !layers.Contains(layer))
                {
                    layers.Add(layer);
                }
            }
        }

        if (layers.Count == 0)
        {
            throw new InvalidOperationException(
                "No non-beach TerrainLayer is available. Create a base terrain layer before applying the beach.");
        }

        layers.Add(beachLayer);
        return layers;
    }

    private static List<TerrainSnapshot> CaptureTerrainSnapshots(IEnumerable<Terrain> terrains)
    {
        List<TerrainSnapshot> snapshots = new List<TerrainSnapshot>();
        foreach (Terrain terrain in terrains)
        {
            TerrainData data = terrain.terrainData;
            TerrainLayer[] layers = data.terrainLayers;
            snapshots.Add(new TerrainSnapshot
            {
                terrain = terrain,
                layers = layers,
                alphamaps = layers.Length > 0
                    ? data.GetAlphamaps(0, 0, data.alphamapResolution, data.alphamapResolution)
                    : new float[data.alphamapResolution, data.alphamapResolution, 0]
            });
        }

        return snapshots;
    }

    private static Dictionary<int, int> BuildLayerIndexMap(
        IReadOnlyList<TerrainLayer> oldLayers,
        IReadOnlyList<TerrainLayer> canonicalLayers)
    {
        Dictionary<int, int> map = new Dictionary<int, int>();
        for (int index = 0; index < oldLayers.Count; index++)
        {
            int canonicalIndex = -1;
            for (int candidate = 0; candidate < canonicalLayers.Count; candidate++)
            {
                if (canonicalLayers[candidate] == oldLayers[index])
                {
                    canonicalIndex = candidate;
                    break;
                }
            }

            if (canonicalIndex >= 0)
            {
                map[index] = canonicalIndex;
            }
        }

        return map;
    }

    private static void EnsureFolder(string folderPath)
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
        WriteReport(new SetupReport
        {
            success = false,
            message = exception.GetType().Name + ": " + exception.Message,
            completedAt = DateTime.Now.ToString("O")
        });
    }

    private static void WriteReport(SetupReport report)
    {
        File.WriteAllText(ReportFilePath, JsonUtility.ToJson(report, true));
    }
}
