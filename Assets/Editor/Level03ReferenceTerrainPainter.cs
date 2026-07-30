using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Level03ReferenceTerrainPainter
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string LayerFolder = "Assets/Level03/TerrainLayers";
    private const string MapTextureFolder = "Assets/Level03/MapStyleTextures";
    private const string TerrainTextureFolder = "Assets/Level03/TerrainTextures";
    private const string SandLayerPath = LayerFolder + "/TL_Level03_Beach.terrainlayer";
    private const string LowlandLayerPath = LayerFolder + "/TL_Level03_PaintableGreen.terrainlayer";
    private const string JungleLayerPath = LayerFolder + "/TL_Level03_MountainGrass.terrainlayer";
    private const string SoilLayerPath = LayerFolder + "/TL_Level03_MountainSoil.terrainlayer";
    private const string RockLayerPath = LayerFolder + "/TL_Level03_MountainRock.terrainlayer";
    private const string SandTexturePath = MapTextureFolder + "/T_Level03_IllustratedSand_Gorilla.png";
    private const string LowlandTexturePath = MapTextureFolder + "/T_Level03_PaintableGreen_Gorilla.png";
    private const string JungleTexturePath = TerrainTextureFolder + "/T_Level03_MountainGrass_Gorilla.png";
    private const string SoilTexturePath = TerrainTextureFolder + "/T_Level03_MountainSoil_Gorilla.png";
    private const string RockTexturePath = TerrainTextureFolder + "/T_Level03_MountainRock_Gorilla.png";
    private const string ReportPath = "Library/CodexLevel03ReferenceTerrainPaint.json";
    private const int CoastMaskResolution = 1024;

    [Serializable]
    private sealed class PaintReport
    {
        public bool success;
        public int terrainCount;
        public int alphamapResolution;
        public float worldWidth;
        public float worldDepth;
        public float maximumHeight;
        public long sandPixels;
        public long lowlandPixels;
        public long junglePixels;
        public long soilPixels;
        public long rockPixels;
        public string referenceImage;
        public string completedAt;
    }

    private sealed class TerrainHoleMap
    {
        public Terrain terrain;
        public bool[,] surface;
    }

    private sealed class CoastDistanceMap
    {
        public float minimumX;
        public float minimumZ;
        public float width;
        public float depth;
        public int resolution;
        public float[] landDistance;
        public bool[] surface;

        public float Sample(float worldX, float worldZ)
        {
            float normalizedX = Mathf.InverseLerp(minimumX, minimumX + width, worldX);
            float normalizedZ = Mathf.InverseLerp(minimumZ, minimumZ + depth, worldZ);
            int x = Mathf.Clamp(Mathf.FloorToInt(normalizedX * resolution), 0, resolution - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(normalizedZ * resolution), 0, resolution - 1);
            return landDistance[y * resolution + x];
        }

        public bool IsLand(float worldX, float worldZ)
        {
            float normalizedX = Mathf.InverseLerp(minimumX, minimumX + width, worldX);
            float normalizedZ = Mathf.InverseLerp(minimumZ, minimumZ + depth, worldZ);
            int x = Mathf.Clamp(Mathf.FloorToInt(normalizedX * resolution), 0, resolution - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(normalizedZ * resolution), 0, resolution - 1);
            return surface[y * resolution + x];
        }
    }

    public static void ApplyFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Apply();
        Level03ActivePlanSplineRoadRebuilder.RenderVerificationPreview();
    }

    [MenuItem("Tools/Island Map/Level03/Paint Terrain From Tropical Reference")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException("Level03 must be the active scene.");
        }

        Terrain[] terrains = UnityEngine.Object.FindObjectsOfType<Terrain>(true)
            .Where(item => item.gameObject.scene == scene && item.terrainData != null)
            .OrderBy(item => item.transform.position.z)
            .ThenBy(item => item.transform.position.x)
            .ToArray();
        if (terrains.Length != 16)
        {
            throw new InvalidOperationException($"Expected 16 Level03 Terrain tiles, found {terrains.Length}.");
        }

        EnsureTextureImport(SandTexturePath);
        EnsureTextureImport(LowlandTexturePath);
        EnsureTextureImport(JungleTexturePath);
        EnsureTextureImport(SoilTexturePath);
        EnsureTextureImport(RockTexturePath);

        TerrainLayer sand = ConfigureLayer(
            SandLayerPath,
            SandTexturePath,
            new Vector2(42f, 42f),
            new Color(0.92f, 0.82f, 0.58f, 1f));
        TerrainLayer lowland = ConfigureLayer(
            LowlandLayerPath,
            LowlandTexturePath,
            new Vector2(46f, 46f),
            new Color(0.68f, 0.96f, 0.50f, 1f));
        TerrainLayer jungle = ConfigureLayer(
            JungleLayerPath,
            JungleTexturePath,
            new Vector2(34f, 34f),
            new Color(0.72f, 0.82f, 0.58f, 1f));
        TerrainLayer soil = ConfigureLayer(
            SoilLayerPath,
            SoilTexturePath,
            new Vector2(30f, 30f),
            new Color(0.62f, 0.50f, 0.38f, 1f));
        TerrainLayer rock = ConfigureLayer(
            RockLayerPath,
            RockTexturePath,
            new Vector2(38f, 38f),
            new Color(0.78f, 0.78f, 0.72f, 1f));
        TerrainLayer[] layers = { sand, lowland, jungle, soil, rock };

        CoastDistanceMap coastDistance = BuildCoastDistanceMap(terrains);
        float maximumHeight = FindMaximumHeight(terrains);
        long sandPixels = 0;
        long lowlandPixels = 0;
        long junglePixels = 0;
        long soilPixels = 0;
        long rockPixels = 0;
        foreach (Terrain terrain in terrains)
        {
            PaintTerrain(
                terrain,
                layers,
                coastDistance,
                maximumHeight,
                ref sandPixels,
                ref lowlandPixels,
                ref junglePixels,
                ref soilPixels,
                ref rockPixels);
        }

        AssetDatabase.SaveAssets();
        PaintReport report = new PaintReport
        {
            success = true,
            terrainCount = terrains.Length,
            alphamapResolution = terrains[0].terrainData.alphamapResolution,
            worldWidth = coastDistance.width,
            worldDepth = coastDistance.depth,
            maximumHeight = maximumHeight,
            sandPixels = sandPixels,
            lowlandPixels = lowlandPixels,
            junglePixels = junglePixels,
            soilPixels = soilPixels,
            rockPixels = rockPixels,
            referenceImage = "C:/Users/Administrator/Desktop/5589ebaa2954a02933cd504a04a07312.jpg",
            completedAt = DateTime.Now.ToString("O")
        };
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        File.WriteAllText(
            Path.Combine(projectRoot, ReportPath),
            JsonUtility.ToJson(report, true));
        Debug.Log(
            $"[Level03 Reference Terrain] Painted {terrains.Length} tiles from the tropical reference; " +
            $"sand={sandPixels:N0}, lowland={lowlandPixels:N0}, jungle={junglePixels:N0}, " +
            $"soil={soilPixels:N0}, rock={rockPixels:N0}.");
    }

    private static void PaintTerrain(
        Terrain terrain,
        TerrainLayer[] layers,
        CoastDistanceMap coastDistance,
        float maximumHeight,
        ref long sandPixels,
        ref long lowlandPixels,
        ref long junglePixels,
        ref long soilPixels,
        ref long rockPixels)
    {
        TerrainData data = terrain.terrainData;
        data.terrainLayers = layers;
        int resolution = data.alphamapResolution;
        float[,,] weights = new float[resolution, resolution, layers.Length];

        for (int y = 0; y < resolution; y++)
        {
            float normalizedY = (y + 0.5f) / resolution;
            float worldZ = terrain.transform.position.z + normalizedY * data.size.z;
            for (int x = 0; x < resolution; x++)
            {
                float normalizedX = (x + 0.5f) / resolution;
                float worldX = terrain.transform.position.x + normalizedX * data.size.x;
                float height = data.GetInterpolatedHeight(normalizedX, normalizedY) +
                               terrain.transform.position.y;
                float slope = data.GetSteepness(normalizedX, normalizedY);
                float macro = Mathf.PerlinNoise(worldX * 0.0035f + 18.7f, worldZ * 0.0035f + 61.3f);
                float detail = Mathf.PerlinNoise(worldX * 0.0105f + 73.1f, worldZ * 0.0105f + 29.4f);
                float patch = Mathf.Clamp01(macro * 0.72f + detail * 0.28f);

                float shoreVariation = (macro - 0.5f) * 7f;
                float coast = coastDistance.Sample(worldX, worldZ);
                float sandWeight = 1f - SmoothRange(
                    12f + shoreVariation,
                    48f + shoreVariation,
                    coast);
                sandWeight = Mathf.Clamp01(sandWeight);

                float interior = 1f - sandWeight;
                float height01 = Mathf.InverseLerp(4f, maximumHeight * 0.94f, height);
                float mountain = SmoothRange(0.08f, 0.34f, height01);
                float noisySlope = slope + (macro - 0.5f) * 8f;
                float steepRock = SmoothRange(29f, 48f, noisySlope);
                float summitRock = SmoothRange(0.76f, 0.98f, height01);
                float rockShare = mountain * Mathf.Clamp01(
                    steepRock * Mathf.Lerp(0.32f, 0.62f, height01) + summitRock * 0.42f);
                rockShare = Mathf.Min(rockShare, 0.64f);

                float moderateSlope = SmoothRange(12f, 24f, noisySlope) *
                                      (1f - SmoothRange(34f, 45f, noisySlope));
                float dryPatch = SmoothRange(0.60f, 0.88f, 1f - patch);
                float soilShare = mountain * Mathf.Clamp01(
                    moderateSlope * 0.18f + dryPatch * 0.10f);

                float rockWeight = interior * rockShare;
                float soilWeight = (interior - rockWeight) * soilShare;
                float greenWeight = Mathf.Max(0f, interior - rockWeight - soilWeight);
                float jungleRatio = Mathf.Clamp01(
                    0.32f + mountain * 0.58f + (macro - 0.5f) * 0.22f);
                float jungleWeight = greenWeight * jungleRatio;
                float lowlandWeight = greenWeight - jungleWeight;

                float total = Mathf.Max(
                    0.0001f,
                    sandWeight + lowlandWeight + jungleWeight + soilWeight + rockWeight);
                weights[y, x, 0] = sandWeight / total;
                weights[y, x, 1] = lowlandWeight / total;
                weights[y, x, 2] = jungleWeight / total;
                weights[y, x, 3] = soilWeight / total;
                weights[y, x, 4] = rockWeight / total;

                if (coastDistance.IsLand(worldX, worldZ))
                {
                    if (weights[y, x, 0] > 0.35f) sandPixels++;
                    if (weights[y, x, 1] > 0.35f) lowlandPixels++;
                    if (weights[y, x, 2] > 0.35f) junglePixels++;
                    if (weights[y, x, 3] > 0.10f) soilPixels++;
                    if (weights[y, x, 4] > 0.35f) rockPixels++;
                }
            }
        }

        data.SetAlphamaps(0, 0, weights);
        EditorUtility.SetDirty(data);
        terrain.Flush();
    }

    private static CoastDistanceMap BuildCoastDistanceMap(IReadOnlyList<Terrain> terrains)
    {
        TerrainHoleMap[] maps = terrains.Select(terrain => new TerrainHoleMap
        {
            terrain = terrain,
            surface = terrain.terrainData.GetHoles(
                0,
                0,
                terrain.terrainData.holesResolution,
                terrain.terrainData.holesResolution)
        }).ToArray();
        float minimumX = terrains.Min(item => item.transform.position.x);
        float minimumZ = terrains.Min(item => item.transform.position.z);
        float maximumX = terrains.Max(item => item.transform.position.x + item.terrainData.size.x);
        float maximumZ = terrains.Max(item => item.transform.position.z + item.terrainData.size.z);
        float width = maximumX - minimumX;
        float depth = maximumZ - minimumZ;
        float[] distance = new float[CoastMaskResolution * CoastMaskResolution];
        bool[] surface = new bool[distance.Length];
        const float infinite = 100000f;

        for (int y = 0; y < CoastMaskResolution; y++)
        {
            float worldZ = minimumZ + (y + 0.5f) / CoastMaskResolution * depth;
            for (int x = 0; x < CoastMaskResolution; x++)
            {
                float worldX = minimumX + (x + 0.5f) / CoastMaskResolution * width;
                bool land = SampleTerrainSurface(maps, worldX, worldZ);
                int index = y * CoastMaskResolution + x;
                surface[index] = land;
                distance[index] = land ? infinite : 0f;
            }
        }

        ChamferDistancePass(distance, CoastMaskResolution, true);
        ChamferDistancePass(distance, CoastMaskResolution, false);
        float worldUnitsPerPixel = (width + depth) * 0.5f / CoastMaskResolution;
        for (int index = 0; index < distance.Length; index++)
        {
            distance[index] *= worldUnitsPerPixel;
        }

        return new CoastDistanceMap
        {
            minimumX = minimumX,
            minimumZ = minimumZ,
            width = width,
            depth = depth,
            resolution = CoastMaskResolution,
            landDistance = distance,
            surface = surface
        };
    }

    private static bool SampleTerrainSurface(
        IEnumerable<TerrainHoleMap> maps,
        float worldX,
        float worldZ)
    {
        foreach (TerrainHoleMap map in maps)
        {
            Vector3 origin = map.terrain.transform.position;
            Vector3 size = map.terrain.terrainData.size;
            if (worldX < origin.x || worldX >= origin.x + size.x ||
                worldZ < origin.z || worldZ >= origin.z + size.z)
            {
                continue;
            }

            float normalizedX = (worldX - origin.x) / size.x;
            float normalizedZ = (worldZ - origin.z) / size.z;
            int width = map.surface.GetLength(1);
            int height = map.surface.GetLength(0);
            int x = Mathf.Clamp(Mathf.FloorToInt(normalizedX * width), 0, width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(normalizedZ * height), 0, height - 1);
            return map.surface[y, x];
        }
        return false;
    }

    private static void ChamferDistancePass(float[] distance, int resolution, bool forward)
    {
        int start = forward ? 0 : resolution - 1;
        int end = forward ? resolution : -1;
        int step = forward ? 1 : -1;
        for (int y = start; y != end; y += step)
        {
            for (int x = start; x != end; x += step)
            {
                int index = y * resolution + x;
                float best = distance[index];
                int previousX = x - step;
                int previousY = y - step;
                if (previousX >= 0 && previousX < resolution)
                {
                    best = Mathf.Min(best, distance[y * resolution + previousX] + 1f);
                }
                if (previousY >= 0 && previousY < resolution)
                {
                    best = Mathf.Min(best, distance[previousY * resolution + x] + 1f);
                    if (previousX >= 0 && previousX < resolution)
                    {
                        best = Mathf.Min(
                            best,
                            distance[previousY * resolution + previousX] + 1.41421356f);
                    }
                    int oppositeX = x + step;
                    if (oppositeX >= 0 && oppositeX < resolution)
                    {
                        best = Mathf.Min(
                            best,
                            distance[previousY * resolution + oppositeX] + 1.41421356f);
                    }
                }
                distance[index] = best;
            }
        }
    }

    private static float FindMaximumHeight(IEnumerable<Terrain> terrains)
    {
        float maximum = 0f;
        foreach (Terrain terrain in terrains)
        {
            const int samples = 64;
            for (int y = 0; y <= samples; y++)
            {
                float normalizedY = y / (float)samples;
                for (int x = 0; x <= samples; x++)
                {
                    float normalizedX = x / (float)samples;
                    float height = terrain.terrainData.GetInterpolatedHeight(normalizedX, normalizedY) +
                                   terrain.transform.position.y;
                    maximum = Mathf.Max(maximum, height);
                }
            }
        }
        return maximum;
    }

    private static TerrainLayer ConfigureLayer(
        string layerPath,
        string texturePath,
        Vector2 tileSize,
        Color colorMultiplier)
    {
        TerrainLayer layer = LoadRequired<TerrainLayer>(layerPath);
        layer.diffuseTexture = LoadRequired<Texture2D>(texturePath);
        layer.tileSize = tileSize;
        layer.tileOffset = Vector2.zero;
        layer.metallic = 0f;
        layer.smoothness = 0f;
        layer.normalScale = 1f;
        layer.diffuseRemapMin = Vector4.zero;
        layer.diffuseRemapMax = colorMultiplier;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static void EnsureTextureImport(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            throw new FileNotFoundException($"Terrain texture was not found: {assetPath}");
        }

        bool changed = importer.textureType != TextureImporterType.Default ||
                       !importer.sRGBTexture ||
                       importer.wrapMode != TextureWrapMode.Repeat ||
                       !importer.mipmapEnabled ||
                       importer.anisoLevel != 8 ||
                       importer.maxTextureSize != 2048;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 8;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static T LoadRequired<T>(string assetPath) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset == null)
        {
            throw new FileNotFoundException($"Required asset was not found: {assetPath}");
        }
        return asset;
    }

    private static float SmoothRange(float minimum, float maximum, float value)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minimum, maximum, value));
    }
}
