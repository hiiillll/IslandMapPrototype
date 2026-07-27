using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Upgrades the converted Level03 Terrain grid from 513/512 to 1025/1024
/// height/hole resolution and reconstructs the coastline from a signed-distance
/// field. This halves visible Terrain-hole stair steps without changing layers,
/// trees, details, or the mountain height range.
/// </summary>
[InitializeOnLoad]
public static class Level03TerrainEdgeHighResolutionOptimizer
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string ConvertedRootName = "ENV_Level03_ConvertedTerrain";
    private const string RequestAssetPath =
        "Assets/Editor/Level03TerrainEdgeHighResolutionOptimizer.request";
    private const string ReportPath =
        "Library/CodexLevel03TerrainEdgeHighResolutionReport.json";

    private const int TileCount = 4;
    private const int SourceTileQuads = 512;
    private const int TargetTileQuads = 1024;
    private const int SourceGlobalHoles = TileCount * SourceTileQuads;
    private const int SourceGlobalHeights = SourceGlobalHoles + 1;
    private const int TargetGlobalHoles = TileCount * TargetTileQuads;
    private const int TargetGlobalHeights = TargetGlobalHoles + 1;
    private const float TerrainHeight = 400f;
    private const float CoastHeight = 0.35f;

    [Serializable]
    private sealed class OptimizationReport
    {
        public bool success;
        public string message;
        public int terrainCount;
        public int heightmapResolutionPerTile;
        public int holesResolutionPerTile;
        public float holeCellSizeMeters;
        public int visibleHoleCells;
        public int totalHoleCells;
        public float landAreaChangePercent;
        public float maximumHeightBefore;
        public float maximumHeightAfter;
        public string completedAt;
    }

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    private static string RequestFilePath => Path.Combine(ProjectRoot, RequestAssetPath);
    private static string ReportFilePath => Path.Combine(ProjectRoot, ReportPath);

    static Level03TerrainEdgeHighResolutionOptimizer()
    {
        if (File.Exists(RequestFilePath))
        {
            EditorApplication.delayCall += OptimizeOnce;
        }
    }

    [MenuItem("Tools/Island Map/Level03/Optimize Coastline To High Resolution")]
    public static void OptimizeFromMenu()
    {
        try
        {
            OptimizationReport report = Optimize();
            WriteReport(report);
            EditorUtility.DisplayDialog("Level03 High Resolution Coastline", report.message, "OK");
        }
        catch (Exception exception)
        {
            WriteFailureReport(exception);
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Level03 High Resolution Coastline",
                exception.Message,
                "OK");
        }
    }

    private static void OptimizeOnce()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += OptimizeOnce;
            return;
        }

        try
        {
            OptimizationReport report = Optimize();
            WriteReport(report);
            Debug.Log("[Level03 High Resolution Coastline] " + report.message);
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

    private static OptimizationReport Optimize()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException("Level03 must be the active scene.");
        }

        GameObject root = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(candidate =>
                candidate.scene == scene &&
                candidate.name == ConvertedRootName);
        if (root == null)
        {
            throw new InvalidOperationException(
                $"Could not find the converted Terrain root '{ConvertedRootName}'.");
        }

        Terrain[,] terrainGrid = BuildTerrainGrid(root);
        float[,] sourceHeights = new float[SourceGlobalHeights, SourceGlobalHeights];
        bool[,] sourceHoles = new bool[SourceGlobalHoles, SourceGlobalHoles];
        ReadSourceGrid(terrainGrid, sourceHeights, sourceHoles);

        float maximumBefore = FindMaximum(sourceHeights) * TerrainHeight;
        int sourceVisibleCells = CountVisible(sourceHoles);

        EditorUtility.DisplayProgressBar(
            "Level03 High Resolution Coastline",
            "Building signed-distance coastline field",
            0.1f);

        float[,] distanceToWater = BuildDistanceToTarget(sourceHoles, false);
        float[,] distanceToLand = BuildDistanceToTarget(sourceHoles, true);
        float[,] signedDistance = new float[SourceGlobalHoles, SourceGlobalHoles];
        for (int y = 0; y < SourceGlobalHoles; y++)
        {
            for (int x = 0; x < SourceGlobalHoles; x++)
            {
                signedDistance[y, x] = sourceHoles[y, x]
                    ? distanceToWater[y, x]
                    : -distanceToLand[y, x];
            }
        }

        distanceToWater = null;
        distanceToLand = null;

        EditorUtility.DisplayProgressBar(
            "Level03 High Resolution Coastline",
            "Upsampling heights and reconstructing smooth coastline",
            0.35f);

        float[,] targetHeights = UpsampleHeights(sourceHeights);
        bool[,] targetHoles = ReconstructHighResolutionHoles(signedDistance);
        signedDistance = null;
        EnsureVisibleTerrainHasCoastHeight(targetHeights, targetHoles);

        int targetVisibleCells = CountVisible(targetHoles);
        float maximumAfter = FindMaximum(targetHeights) * TerrainHeight;
        float sourceLandRatio =
            (float)sourceVisibleCells / (SourceGlobalHoles * SourceGlobalHoles);
        float targetLandRatio =
            (float)targetVisibleCells / (TargetGlobalHoles * TargetGlobalHoles);
        float areaChangePercent = sourceLandRatio > 0f
            ? (targetLandRatio - sourceLandRatio) / sourceLandRatio * 100f
            : 0f;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Optimize Level03 Coastline To High Resolution");

        for (int row = 0; row < TileCount; row++)
        {
            for (int column = 0; column < TileCount; column++)
            {
                int tileIndex = row * TileCount + column;
                EditorUtility.DisplayProgressBar(
                    "Level03 High Resolution Coastline",
                    $"Writing Terrain tile {tileIndex + 1}/{TileCount * TileCount}",
                    0.55f + 0.4f * tileIndex / (TileCount * TileCount));

                Terrain terrain = terrainGrid[column, row];
                TerrainData data = terrain.terrainData;
                Undo.RegisterCompleteObjectUndo(
                    data,
                    "Optimize Level03 Coastline To High Resolution");

                data.heightmapResolution = TargetTileQuads + 1;
                data.size = new Vector3(1000f, TerrainHeight, 1000f);

                float[,] tileHeights =
                    new float[TargetTileQuads + 1, TargetTileQuads + 1];
                bool[,] tileHoles = new bool[TargetTileQuads, TargetTileQuads];
                int startX = column * TargetTileQuads;
                int startY = row * TargetTileQuads;

                for (int y = 0; y <= TargetTileQuads; y++)
                {
                    for (int x = 0; x <= TargetTileQuads; x++)
                    {
                        tileHeights[y, x] = targetHeights[startY + y, startX + x];
                    }
                }

                for (int y = 0; y < TargetTileQuads; y++)
                {
                    for (int x = 0; x < TargetTileQuads; x++)
                    {
                        tileHoles[y, x] = targetHoles[startY + y, startX + x];
                    }
                }

                data.SetHeightsDelayLOD(0, 0, tileHeights);
                data.SetHoles(0, 0, tileHoles);
                data.SyncHeightmap();
                EditorUtility.SetDirty(data);
                terrain.heightmapPixelError = 2f;
                terrain.Flush();
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        EditorUtility.ClearProgressBar();

        return new OptimizationReport
        {
            success = true,
            message =
                "Upgraded all 16 Terrain tiles to 1025/1024 height/hole resolution and " +
                "rebuilt the coastline from a signed-distance field. Terrain-hole stepping is now under one meter.",
            terrainCount = TileCount * TileCount,
            heightmapResolutionPerTile = TargetTileQuads + 1,
            holesResolutionPerTile = TargetTileQuads,
            holeCellSizeMeters = 1000f / TargetTileQuads,
            visibleHoleCells = targetVisibleCells,
            totalHoleCells = TargetGlobalHoles * TargetGlobalHoles,
            landAreaChangePercent = areaChangePercent,
            maximumHeightBefore = maximumBefore,
            maximumHeightAfter = maximumAfter,
            completedAt = DateTime.Now.ToString("O")
        };
    }

    private static Terrain[,] BuildTerrainGrid(GameObject root)
    {
        Terrain[] terrains = root.GetComponentsInChildren<Terrain>(true);
        if (terrains.Length != TileCount * TileCount)
        {
            throw new InvalidOperationException(
                $"Expected {TileCount * TileCount} Terrain tiles, but found {terrains.Length}.");
        }

        Terrain[,] grid = new Terrain[TileCount, TileCount];
        foreach (Terrain terrain in terrains)
        {
            TerrainData data = terrain.terrainData;
            if (data.heightmapResolution != SourceTileQuads + 1 ||
                data.holesResolution != SourceTileQuads)
            {
                throw new InvalidOperationException(
                    $"Terrain '{terrain.name}' is not at the expected 513/512 source resolution.");
            }

            int column = Mathf.RoundToInt((terrain.transform.position.x + 2000f) / 1000f);
            int row = Mathf.RoundToInt((terrain.transform.position.z + 2000f) / 1000f);
            if (column < 0 || column >= TileCount || row < 0 || row >= TileCount)
            {
                throw new InvalidOperationException(
                    $"Terrain '{terrain.name}' is outside the expected 4x4 grid.");
            }

            grid[column, row] = terrain;
        }

        return grid;
    }

    private static void ReadSourceGrid(
        Terrain[,] grid,
        float[,] heights,
        bool[,] holes)
    {
        for (int row = 0; row < TileCount; row++)
        {
            for (int column = 0; column < TileCount; column++)
            {
                TerrainData data = grid[column, row].terrainData;
                float[,] tileHeights =
                    data.GetHeights(0, 0, SourceTileQuads + 1, SourceTileQuads + 1);
                bool[,] tileHoles =
                    data.GetHoles(0, 0, SourceTileQuads, SourceTileQuads);
                int startX = column * SourceTileQuads;
                int startY = row * SourceTileQuads;

                for (int y = 0; y <= SourceTileQuads; y++)
                {
                    for (int x = 0; x <= SourceTileQuads; x++)
                    {
                        heights[startY + y, startX + x] = tileHeights[y, x];
                    }
                }

                for (int y = 0; y < SourceTileQuads; y++)
                {
                    for (int x = 0; x < SourceTileQuads; x++)
                    {
                        holes[startY + y, startX + x] = tileHoles[y, x];
                    }
                }
            }
        }
    }

    private static float[,] BuildDistanceToTarget(bool[,] source, bool targetValue)
    {
        int height = source.GetLength(0);
        int width = source.GetLength(1);
        const float diagonal = 1.41421356f;
        const float infinity = 100000f;
        float[,] distance = new float[height, width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                distance[y, x] = source[y, x] == targetValue ? 0f : infinity;
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float value = distance[y, x];
                if (x > 0)
                {
                    value = Mathf.Min(value, distance[y, x - 1] + 1f);
                }

                if (y > 0)
                {
                    value = Mathf.Min(value, distance[y - 1, x] + 1f);
                    if (x > 0)
                    {
                        value = Mathf.Min(value, distance[y - 1, x - 1] + diagonal);
                    }

                    if (x < width - 1)
                    {
                        value = Mathf.Min(value, distance[y - 1, x + 1] + diagonal);
                    }
                }

                distance[y, x] = value;
            }
        }

        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = width - 1; x >= 0; x--)
            {
                float value = distance[y, x];
                if (x < width - 1)
                {
                    value = Mathf.Min(value, distance[y, x + 1] + 1f);
                }

                if (y < height - 1)
                {
                    value = Mathf.Min(value, distance[y + 1, x] + 1f);
                    if (x > 0)
                    {
                        value = Mathf.Min(value, distance[y + 1, x - 1] + diagonal);
                    }

                    if (x < width - 1)
                    {
                        value = Mathf.Min(value, distance[y + 1, x + 1] + diagonal);
                    }
                }

                distance[y, x] = value;
            }
        }

        return distance;
    }

    private static float[,] UpsampleHeights(float[,] source)
    {
        float[,] result = new float[TargetGlobalHeights, TargetGlobalHeights];
        for (int y = 0; y < TargetGlobalHeights; y++)
        {
            float sourceY = y * 0.5f;
            int y0 = Mathf.Min(SourceGlobalHeights - 1, Mathf.FloorToInt(sourceY));
            int y1 = Mathf.Min(SourceGlobalHeights - 1, y0 + 1);
            float ty = sourceY - y0;

            for (int x = 0; x < TargetGlobalHeights; x++)
            {
                float sourceX = x * 0.5f;
                int x0 = Mathf.Min(SourceGlobalHeights - 1, Mathf.FloorToInt(sourceX));
                int x1 = Mathf.Min(SourceGlobalHeights - 1, x0 + 1);
                float tx = sourceX - x0;
                float bottom = Mathf.Lerp(source[y0, x0], source[y0, x1], tx);
                float top = Mathf.Lerp(source[y1, x0], source[y1, x1], tx);
                result[y, x] = Mathf.Lerp(bottom, top, ty);
            }
        }

        return result;
    }

    private static bool[,] ReconstructHighResolutionHoles(float[,] signedDistance)
    {
        bool[,] result = new bool[TargetGlobalHoles, TargetGlobalHoles];
        for (int y = 0; y < TargetGlobalHoles; y++)
        {
            float sourceY = (y + 0.5f) * 0.5f - 0.5f;
            for (int x = 0; x < TargetGlobalHoles; x++)
            {
                float sourceX = (x + 0.5f) * 0.5f - 0.5f;
                result[y, x] = SampleBilinear(signedDistance, sourceX, sourceY) >= 0f;
            }
        }

        return result;
    }

    private static float SampleBilinear(float[,] values, float x, float y)
    {
        int height = values.GetLength(0);
        int width = values.GetLength(1);
        float clampedX = Mathf.Clamp(x, 0f, width - 1);
        float clampedY = Mathf.Clamp(y, 0f, height - 1);
        int x0 = Mathf.FloorToInt(clampedX);
        int y0 = Mathf.FloorToInt(clampedY);
        int x1 = Mathf.Min(width - 1, x0 + 1);
        int y1 = Mathf.Min(height - 1, y0 + 1);
        float tx = clampedX - x0;
        float ty = clampedY - y0;
        float bottom = Mathf.Lerp(values[y0, x0], values[y0, x1], tx);
        float top = Mathf.Lerp(values[y1, x0], values[y1, x1], tx);
        return Mathf.Lerp(bottom, top, ty);
    }

    private static void EnsureVisibleTerrainHasCoastHeight(
        float[,] heights,
        bool[,] holes)
    {
        float minimum = CoastHeight / TerrainHeight;
        for (int y = 0; y < TargetGlobalHeights; y++)
        {
            for (int x = 0; x < TargetGlobalHeights; x++)
            {
                if (VertexTouchesVisibleCell(holes, x, y) && heights[y, x] < minimum)
                {
                    heights[y, x] = minimum;
                }
            }
        }
    }

    private static bool VertexTouchesVisibleCell(bool[,] holes, int vertexX, int vertexY)
    {
        int minX = Mathf.Max(0, vertexX - 1);
        int maxX = Mathf.Min(TargetGlobalHoles - 1, vertexX);
        int minY = Mathf.Max(0, vertexY - 1);
        int maxY = Mathf.Min(TargetGlobalHoles - 1, vertexY);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (holes[y, x])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int CountVisible(bool[,] holes)
    {
        int count = 0;
        for (int y = 0; y < holes.GetLength(0); y++)
        {
            for (int x = 0; x < holes.GetLength(1); x++)
            {
                if (holes[y, x])
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static float FindMaximum(float[,] values)
    {
        float maximum = 0f;
        for (int y = 0; y < values.GetLength(0); y++)
        {
            for (int x = 0; x < values.GetLength(1); x++)
            {
                maximum = Mathf.Max(maximum, values[y, x]);
            }
        }

        return maximum;
    }

    private static void WriteFailureReport(Exception exception)
    {
        EditorUtility.ClearProgressBar();
        WriteReport(new OptimizationReport
        {
            success = false,
            message = exception.GetType().Name + ": " + exception.Message,
            completedAt = DateTime.Now.ToString("O")
        });
    }

    private static void WriteReport(OptimizationReport report)
    {
        File.WriteAllText(ReportFilePath, JsonUtility.ToJson(report, true));
    }
}
