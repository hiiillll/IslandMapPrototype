using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Reusable post-conversion inspection utility.
[InitializeOnLoad]
public static class Level03MeshTerrainInspector
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string LandName = "ENV_Level03_FlatIslands_And_MainMountain";
    private const string RequestAssetPath = "Assets/Editor/Level03MeshTerrainInspector.request";
    private const string ReportPath = "Library/CodexLevel03MeshTerrainInspection.json";

    [Serializable]
    private sealed class TerrainInfo
    {
        public string name;
        public string assetPath;
        public Vector3 position;
        public Vector3 size;
        public int heightmapResolution;
        public int alphamapResolution;
        public int holesResolution;
        public int terrainLayerCount;
        public string terrainLayers;
        public int treeCount;
        public float minimumWorldHeight;
        public float maximumWorldHeight;
    }

    [Serializable]
    private sealed class InspectionReport
    {
        public bool success;
        public string message;
        public string meshAssetPath;
        public int meshVertexCount;
        public int meshTriangleCount;
        public Bounds meshWorldBounds;
        public bool meshRendererEnabled;
        public bool meshColliderEnabled;
        public int terrainCount;
        public TerrainInfo[] terrains;
        public TerrainInfo[] terrainAssets;
        public float maximumHeightSeamDelta;
        public int connectedNeighborPairs;
        public int expectedNeighborPairs;
        public string completedAt;
    }

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    private static string RequestFilePath => Path.Combine(ProjectRoot, RequestAssetPath);
    private static string ReportFilePath => Path.Combine(ProjectRoot, ReportPath);

    static Level03MeshTerrainInspector()
    {
        if (File.Exists(RequestFilePath))
        {
            EditorApplication.delayCall += InspectOnce;
        }
    }

    private static void InspectOnce()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += InspectOnce;
            return;
        }

        try
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                throw new InvalidOperationException("Level03 must be the active scene.");
            }

            GameObject land = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(candidate => candidate.scene == scene && candidate.name == LandName);
            if (land == null)
            {
                throw new InvalidOperationException("The generated Level03 land Mesh was not found.");
            }

            MeshFilter filter = land.GetComponent<MeshFilter>();
            MeshRenderer renderer = land.GetComponent<MeshRenderer>();
            MeshCollider collider = land.GetComponent<MeshCollider>();
            if (filter == null || filter.sharedMesh == null || renderer == null || collider == null)
            {
                throw new InvalidOperationException("The generated land Mesh components are incomplete.");
            }

            Mesh mesh = filter.sharedMesh;
            Terrain[] terrains = UnityEngine.Object.FindObjectsOfType<Terrain>(true)
                .Where(terrain => terrain.gameObject.scene == scene && terrain.terrainData != null)
                .OrderBy(terrain => terrain.transform.position.z)
                .ThenBy(terrain => terrain.transform.position.x)
                .ToArray();

            TerrainInfo[] terrainInfos = terrains.Select(BuildTerrainInfo).ToArray();
            float maximumHeightSeamDelta = CalculateMaximumHeightSeamDelta(
                terrains,
                out int connectedNeighborPairs,
                out int expectedNeighborPairs);
            TerrainInfo[] terrainAssets = AssetDatabase.FindAssets("t:TerrainData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<TerrainData>(path))
                .Where(data => data != null)
                .Select(BuildTerrainAssetInfo)
                .OrderBy(info => info.assetPath)
                .ToArray();
            InspectionReport report = new InspectionReport
            {
                success = true,
                message = "Level03 Mesh and Terrain inspection completed.",
                meshAssetPath = AssetDatabase.GetAssetPath(mesh),
                meshVertexCount = mesh.vertexCount,
                meshTriangleCount = mesh.triangles.Length / 3,
                meshWorldBounds = renderer.bounds,
                meshRendererEnabled = renderer.enabled,
                meshColliderEnabled = collider.enabled,
                terrainCount = terrains.Length,
                terrains = terrainInfos,
                terrainAssets = terrainAssets,
                maximumHeightSeamDelta = maximumHeightSeamDelta,
                connectedNeighborPairs = connectedNeighborPairs,
                expectedNeighborPairs = expectedNeighborPairs,
                completedAt = DateTime.Now.ToString("O")
            };

            File.WriteAllText(ReportFilePath, JsonUtility.ToJson(report, true));
            Debug.Log("[Level03 Mesh Terrain Inspection] " + report.message);
        }
        catch (Exception exception)
        {
            File.WriteAllText(
                ReportFilePath,
                JsonUtility.ToJson(
                    new InspectionReport
                    {
                        success = false,
                        message = exception.GetType().Name + ": " + exception.Message,
                        completedAt = DateTime.Now.ToString("O")
                    },
                    true));
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

    private static TerrainInfo BuildTerrainInfo(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        int resolution = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, resolution, resolution);
        float minimum = 1f;
        float maximum = 0f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float height = heights[y, x];
                minimum = Mathf.Min(minimum, height);
                maximum = Mathf.Max(maximum, height);
            }
        }

        return new TerrainInfo
        {
            name = terrain.name,
            assetPath = AssetDatabase.GetAssetPath(data),
            position = terrain.transform.position,
            size = data.size,
            heightmapResolution = resolution,
            alphamapResolution = data.alphamapResolution,
            holesResolution = data.holesResolution,
            terrainLayerCount = data.terrainLayers.Length,
            terrainLayers = string.Join(
                ", ",
                data.terrainLayers.Select(layer => layer != null ? layer.name : "<null>")),
            treeCount = data.treeInstanceCount,
            minimumWorldHeight = terrain.transform.position.y + minimum * data.size.y,
            maximumWorldHeight = terrain.transform.position.y + maximum * data.size.y
        };
    }

    private static TerrainInfo BuildTerrainAssetInfo(TerrainData data)
    {
        int resolution = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, resolution, resolution);
        float minimum = 1f;
        float maximum = 0f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float height = heights[y, x];
                minimum = Mathf.Min(minimum, height);
                maximum = Mathf.Max(maximum, height);
            }
        }

        return new TerrainInfo
        {
            name = data.name,
            assetPath = AssetDatabase.GetAssetPath(data),
            size = data.size,
            heightmapResolution = resolution,
            alphamapResolution = data.alphamapResolution,
            holesResolution = data.holesResolution,
            terrainLayerCount = data.terrainLayers.Length,
            terrainLayers = string.Join(
                ", ",
                data.terrainLayers.Select(layer => layer != null ? layer.name : "<null>")),
            treeCount = data.treeInstanceCount,
            minimumWorldHeight = minimum * data.size.y,
            maximumWorldHeight = maximum * data.size.y
        };
    }

    private static float CalculateMaximumHeightSeamDelta(
        Terrain[] terrains,
        out int connectedNeighborPairs,
        out int expectedNeighborPairs)
    {
        connectedNeighborPairs = 0;
        expectedNeighborPairs = 0;
        float maximumDelta = 0f;
        Terrain[] converted = terrains
            .Where(terrain => terrain.name.StartsWith("Terrain_Level03_", StringComparison.Ordinal))
            .ToArray();

        foreach (Terrain terrain in converted)
        {
            Terrain right = converted.FirstOrDefault(candidate =>
                Mathf.Abs(candidate.transform.position.x - terrain.transform.position.x - 1000f) < 0.01f &&
                Mathf.Abs(candidate.transform.position.z - terrain.transform.position.z) < 0.01f);
            if (right != null)
            {
                expectedNeighborPairs++;
                if (terrain.rightNeighbor == right && right.leftNeighbor == terrain)
                {
                    connectedNeighborPairs++;
                }

                int resolution = terrain.terrainData.heightmapResolution;
                float[,] first = terrain.terrainData.GetHeights(
                    resolution - 1,
                    0,
                    1,
                    resolution);
                float[,] second = right.terrainData.GetHeights(0, 0, 1, resolution);
                for (int index = 0; index < resolution; index++)
                {
                    float firstHeight =
                        terrain.transform.position.y +
                        first[index, 0] * terrain.terrainData.size.y;
                    float secondHeight =
                        right.transform.position.y +
                        second[index, 0] * right.terrainData.size.y;
                    maximumDelta = Mathf.Max(
                        maximumDelta,
                        Mathf.Abs(firstHeight - secondHeight));
                }
            }

            Terrain top = converted.FirstOrDefault(candidate =>
                Mathf.Abs(candidate.transform.position.x - terrain.transform.position.x) < 0.01f &&
                Mathf.Abs(candidate.transform.position.z - terrain.transform.position.z - 1000f) < 0.01f);
            if (top != null)
            {
                expectedNeighborPairs++;
                if (terrain.topNeighbor == top && top.bottomNeighbor == terrain)
                {
                    connectedNeighborPairs++;
                }

                int resolution = terrain.terrainData.heightmapResolution;
                float[,] first = terrain.terrainData.GetHeights(
                    0,
                    resolution - 1,
                    resolution,
                    1);
                float[,] second = top.terrainData.GetHeights(0, 0, resolution, 1);
                for (int index = 0; index < resolution; index++)
                {
                    float firstHeight =
                        terrain.transform.position.y +
                        first[0, index] * terrain.terrainData.size.y;
                    float secondHeight =
                        top.transform.position.y +
                        second[0, index] * top.terrainData.size.y;
                    maximumDelta = Mathf.Max(
                        maximumDelta,
                        Mathf.Abs(firstHeight - secondHeight));
                }
            }
        }

        return maximumDelta;
    }
}
