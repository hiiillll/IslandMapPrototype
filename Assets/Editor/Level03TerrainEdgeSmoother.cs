using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Smooths Level03's converted shoreline as one continuous grid so tile seams,
/// mountains, terrain layers, trees, and details remain intact.
/// </summary>
[InitializeOnLoad]
public static class Level03TerrainEdgeSmoother
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string ConvertedRootName = "ENV_Level03_ConvertedTerrain";
    private const string RequestAssetPath = "Assets/Editor/Level03TerrainEdgeSmoother.request";
    private const string ReportPath = "Library/CodexLevel03TerrainEdgeSmoothingReport.json";

    private const int TileCount = 4;
    private const int TileQuads = 1024;
    private const int GlobalHoleResolution = TileCount * TileQuads;
    private const int GlobalHeightResolution = GlobalHoleResolution + 1;
    private const int MaskRadius = 2;
    private const int MaskPasses = 2;
    private const int HeightBandRadius = 12;
    private const int HeightSmoothRadius = 2;
    private const int HeightSmoothPasses = 2;
    private const float TerrainHeight = 400f;
    private const float CoastHeight = 0.35f;

    [Serializable]
    private sealed class SmoothingReport
    {
        public bool success;
        public string message;
        public int terrainCount;
        public int changedHoleCells;
        public int adjustedHeightVertices;
        public float smoothingBandMeters;
        public float maximumHeightBefore;
        public float maximumHeightAfter;
        public string completedAt;
    }

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    private static string RequestFilePath => Path.Combine(ProjectRoot, RequestAssetPath);
    private static string ReportFilePath => Path.Combine(ProjectRoot, ReportPath);

    static Level03TerrainEdgeSmoother()
    {
        if (File.Exists(RequestFilePath))
        {
            EditorApplication.delayCall += SmoothOnce;
        }
    }

    [MenuItem("Tools/Island Map/Level03/Smooth Converted Terrain Coastline")]
    public static void SmoothFromMenu()
    {
        try
        {
            SmoothingReport report = Smooth();
            WriteReport(report);
            EditorUtility.DisplayDialog("Level03 Terrain Edge Smoothing", report.message, "OK");
        }
        catch (Exception exception)
        {
            WriteFailureReport(exception);
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Level03 Terrain Edge Smoothing", exception.Message, "OK");
        }
    }

    private static void SmoothOnce()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += SmoothOnce;
            return;
        }

        try
        {
            SmoothingReport report = Smooth();
            WriteReport(report);
            Debug.Log("[Level03 Terrain Edge Smoothing] " + report.message);
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

    private static SmoothingReport Smooth()
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
        float[,] globalHeights = new float[GlobalHeightResolution, GlobalHeightResolution];
        bool[,] globalHoles = new bool[GlobalHoleResolution, GlobalHoleResolution];
        ReadGlobalTerrainData(terrainGrid, globalHeights, globalHoles);

        float maximumBefore = FindMaximum(globalHeights) * TerrainHeight;
        bool[,] smoothedHoles = globalHoles;
        for (int pass = 0; pass < MaskPasses; pass++)
        {
            smoothedHoles = MajoritySmooth(smoothedHoles, MaskRadius);
        }

        int changedHoleCells = CountDifferences(globalHoles, smoothedHoles);
        bool[,] boundaryBand = BuildBoundaryVertexBand(smoothedHoles, HeightBandRadius);
        float[,] smoothedHeights = globalHeights;

        for (int pass = 0; pass < HeightSmoothPasses; pass++)
        {
            smoothedHeights = SmoothBoundaryHeights(
                smoothedHeights,
                smoothedHoles,
                boundaryBand,
                HeightSmoothRadius);
        }

        EnsureVisibleCoastHasHeight(smoothedHeights, smoothedHoles);
        int adjustedVertices = CountDifferences(globalHeights, smoothedHeights, 0.000001f);
        float maximumAfter = FindMaximum(smoothedHeights) * TerrainHeight;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Smooth Level03 Terrain Coastline");

        for (int row = 0; row < TileCount; row++)
        {
            for (int column = 0; column < TileCount; column++)
            {
                Terrain terrain = terrainGrid[column, row];
                TerrainData data = terrain.terrainData;
                Undo.RegisterCompleteObjectUndo(data, "Smooth Level03 Terrain Coastline");

                float[,] tileHeights = new float[TileQuads + 1, TileQuads + 1];
                bool[,] tileHoles = new bool[TileQuads, TileQuads];
                int heightStartX = column * TileQuads;
                int heightStartY = row * TileQuads;

                for (int y = 0; y <= TileQuads; y++)
                {
                    for (int x = 0; x <= TileQuads; x++)
                    {
                        tileHeights[y, x] = smoothedHeights[heightStartY + y, heightStartX + x];
                    }
                }

                for (int y = 0; y < TileQuads; y++)
                {
                    for (int x = 0; x < TileQuads; x++)
                    {
                        tileHoles[y, x] = smoothedHoles[heightStartY + y, heightStartX + x];
                    }
                }

                data.SetHeightsDelayLOD(0, 0, tileHeights);
                data.SetHoles(0, 0, tileHoles);
                data.SyncHeightmap();
                EditorUtility.SetDirty(data);
                terrain.Flush();
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();

        return new SmoothingReport
        {
            success = true,
            message =
                "Smoothed the coastline outline and its narrow height transition across all 16 Terrain tiles. " +
                "Mountain peaks, Terrain layers, trees, details, and tile seams were preserved.",
            terrainCount = TileCount * TileCount,
            changedHoleCells = changedHoleCells,
            adjustedHeightVertices = adjustedVertices,
            smoothingBandMeters = HeightBandRadius * (1000f / TileQuads),
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
                $"Expected {TileCount * TileCount} converted Terrain tiles, but found {terrains.Length}.");
        }

        Terrain[,] grid = new Terrain[TileCount, TileCount];
        foreach (Terrain terrain in terrains)
        {
            if (terrain.terrainData.heightmapResolution != TileQuads + 1 ||
                terrain.terrainData.holesResolution != TileQuads)
            {
                throw new InvalidOperationException(
                    $"Terrain '{terrain.name}' has an unexpected heightmap or holes resolution.");
            }

            int column = Mathf.RoundToInt((terrain.transform.position.x + 2000f) / 1000f);
            int row = Mathf.RoundToInt((terrain.transform.position.z + 2000f) / 1000f);
            if (column < 0 || column >= TileCount || row < 0 || row >= TileCount)
            {
                throw new InvalidOperationException(
                    $"Terrain '{terrain.name}' is outside the expected 4x4 grid.");
            }

            if (grid[column, row] != null)
            {
                throw new InvalidOperationException(
                    $"Two Terrain tiles occupy grid position ({column}, {row}).");
            }

            grid[column, row] = terrain;
        }

        return grid;
    }

    private static void ReadGlobalTerrainData(
        Terrain[,] grid,
        float[,] heights,
        bool[,] holes)
    {
        for (int row = 0; row < TileCount; row++)
        {
            for (int column = 0; column < TileCount; column++)
            {
                TerrainData data = grid[column, row].terrainData;
                float[,] tileHeights = data.GetHeights(0, 0, TileQuads + 1, TileQuads + 1);
                bool[,] tileHoles = data.GetHoles(0, 0, TileQuads, TileQuads);
                int startX = column * TileQuads;
                int startY = row * TileQuads;

                for (int y = 0; y <= TileQuads; y++)
                {
                    for (int x = 0; x <= TileQuads; x++)
                    {
                        heights[startY + y, startX + x] = tileHeights[y, x];
                    }
                }

                for (int y = 0; y < TileQuads; y++)
                {
                    for (int x = 0; x < TileQuads; x++)
                    {
                        holes[startY + y, startX + x] = tileHoles[y, x];
                    }
                }
            }
        }
    }

    private static bool[,] MajoritySmooth(bool[,] source, int radius)
    {
        int height = source.GetLength(0);
        int width = source.GetLength(1);
        int[,] integral = new int[height + 1, width + 1];

        for (int y = 0; y < height; y++)
        {
            int rowTotal = 0;
            for (int x = 0; x < width; x++)
            {
                rowTotal += source[y, x] ? 1 : 0;
                integral[y + 1, x + 1] = integral[y, x + 1] + rowTotal;
            }
        }

        bool[,] result = new bool[height, width];
        for (int y = 0; y < height; y++)
        {
            int minY = Mathf.Max(0, y - radius);
            int maxY = Mathf.Min(height - 1, y + radius);
            for (int x = 0; x < width; x++)
            {
                int minX = Mathf.Max(0, x - radius);
                int maxX = Mathf.Min(width - 1, x + radius);
                int sum =
                    integral[maxY + 1, maxX + 1] -
                    integral[minY, maxX + 1] -
                    integral[maxY + 1, minX] +
                    integral[minY, minX];
                int area = (maxX - minX + 1) * (maxY - minY + 1);
                result[y, x] = sum * 2 >= area;
            }
        }

        return result;
    }

    private static bool[,] BuildBoundaryVertexBand(bool[,] holes, int radius)
    {
        bool[,] band = new bool[GlobalHeightResolution, GlobalHeightResolution];

        for (int y = 0; y < GlobalHoleResolution; y++)
        {
            for (int x = 0; x < GlobalHoleResolution; x++)
            {
                bool visible = holes[y, x];
                bool boundary =
                    (x > 0 && holes[y, x - 1] != visible) ||
                    (x < GlobalHoleResolution - 1 && holes[y, x + 1] != visible) ||
                    (y > 0 && holes[y - 1, x] != visible) ||
                    (y < GlobalHoleResolution - 1 && holes[y + 1, x] != visible);
                if (!boundary)
                {
                    continue;
                }

                int minY = Mathf.Max(0, y - radius);
                int maxY = Mathf.Min(GlobalHeightResolution - 1, y + 1 + radius);
                int minX = Mathf.Max(0, x - radius);
                int maxX = Mathf.Min(GlobalHeightResolution - 1, x + 1 + radius);
                for (int vertexY = minY; vertexY <= maxY; vertexY++)
                {
                    for (int vertexX = minX; vertexX <= maxX; vertexX++)
                    {
                        band[vertexY, vertexX] = true;
                    }
                }
            }
        }

        return band;
    }

    private static float[,] SmoothBoundaryHeights(
        float[,] source,
        bool[,] holes,
        bool[,] boundaryBand,
        int radius)
    {
        float[,] result = (float[,])source.Clone();
        float coastNormalized = CoastHeight / TerrainHeight;

        for (int y = 0; y < GlobalHeightResolution; y++)
        {
            for (int x = 0; x < GlobalHeightResolution; x++)
            {
                if (!boundaryBand[y, x])
                {
                    continue;
                }

                float total = 0f;
                float totalWeight = 0f;
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    int sampleY = y + offsetY;
                    if (sampleY < 0 || sampleY >= GlobalHeightResolution)
                    {
                        continue;
                    }

                    for (int offsetX = -radius; offsetX <= radius; offsetX++)
                    {
                        int sampleX = x + offsetX;
                        if (sampleX < 0 || sampleX >= GlobalHeightResolution ||
                            !VertexTouchesVisibleCell(holes, sampleX, sampleY))
                        {
                            continue;
                        }

                        float sample = source[sampleY, sampleX];
                        if (sample < coastNormalized * 0.5f)
                        {
                            continue;
                        }

                        float weight = (radius + 1 - Mathf.Abs(offsetX)) *
                                       (radius + 1 - Mathf.Abs(offsetY));
                        total += sample * weight;
                        totalWeight += weight;
                    }
                }

                if (totalWeight > 0f)
                {
                    result[y, x] = Mathf.Max(coastNormalized, total / totalWeight);
                }
                else if (VertexTouchesVisibleCell(holes, x, y))
                {
                    result[y, x] = coastNormalized;
                }
            }
        }

        return result;
    }

    private static void EnsureVisibleCoastHasHeight(float[,] heights, bool[,] holes)
    {
        float coastNormalized = CoastHeight / TerrainHeight;
        for (int y = 0; y < GlobalHeightResolution; y++)
        {
            for (int x = 0; x < GlobalHeightResolution; x++)
            {
                if (VertexTouchesVisibleCell(holes, x, y) && heights[y, x] < coastNormalized)
                {
                    heights[y, x] = coastNormalized;
                }
            }
        }
    }

    private static bool VertexTouchesVisibleCell(bool[,] holes, int vertexX, int vertexY)
    {
        int minX = Mathf.Max(0, vertexX - 1);
        int maxX = Mathf.Min(GlobalHoleResolution - 1, vertexX);
        int minY = Mathf.Max(0, vertexY - 1);
        int maxY = Mathf.Min(GlobalHoleResolution - 1, vertexY);

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

    private static int CountDifferences(bool[,] first, bool[,] second)
    {
        int differences = 0;
        for (int y = 0; y < first.GetLength(0); y++)
        {
            for (int x = 0; x < first.GetLength(1); x++)
            {
                if (first[y, x] != second[y, x])
                {
                    differences++;
                }
            }
        }

        return differences;
    }

    private static int CountDifferences(float[,] first, float[,] second, float epsilon)
    {
        int differences = 0;
        for (int y = 0; y < first.GetLength(0); y++)
        {
            for (int x = 0; x < first.GetLength(1); x++)
            {
                if (Mathf.Abs(first[y, x] - second[y, x]) > epsilon)
                {
                    differences++;
                }
            }
        }

        return differences;
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
        WriteReport(new SmoothingReport
        {
            success = false,
            message = exception.GetType().Name + ": " + exception.Message,
            completedAt = DateTime.Now.ToString("O")
        });
    }

    private static void WriteReport(SmoothingReport report)
    {
        File.WriteAllText(ReportFilePath, JsonUtility.ToJson(report, true));
    }
}
