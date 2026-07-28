using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Level03MountainSurfaceInstaller
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string FlatLayerPath =
        "Assets/Level03/FlatTerrain/TL_Level03_FlatLand.terrainlayer";
    private const string BeachLayerPath =
        "Assets/Level03/TerrainLayers/TL_Level03_Beach.terrainlayer";
    private const string LayerFolder = "Assets/Level03/TerrainLayers";
    private const string TextureFolder = "Assets/Level03/TerrainTextures";
    private const string MountainGrassTexturePath =
        TextureFolder + "/T_Level03_MountainGrass_Gorilla.png";
    private const string MountainSoilTexturePath =
        TextureFolder + "/T_Level03_MountainSoil_Gorilla.png";
    private const string MountainRockTexturePath =
        TextureFolder + "/T_Level03_MountainRock_Gorilla.png";
    private const string MountainGrassLayerPath =
        LayerFolder + "/TL_Level03_MountainGrass.terrainlayer";
    private const string MountainSoilLayerPath =
        LayerFolder + "/TL_Level03_MountainSoil.terrainlayer";
    private const string MountainRockLayerPath =
        LayerFolder + "/TL_Level03_MountainRock.terrainlayer";

    public static void ApplyFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Apply();
        Level03ActivePlanSplineRoadRebuilder.RenderVerificationPreview();
    }

    [MenuItem("Tools/Island Map/Level03/Apply Gorilla Mountain Surface")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException("Level03 must be the active scene.");
        }

        ConfigureTextureImport(MountainGrassTexturePath);
        ConfigureTextureImport(MountainSoilTexturePath);
        ConfigureTextureImport(MountainRockTexturePath);
        AssetDatabase.Refresh();

        TerrainLayer flatLayer = LoadRequired<TerrainLayer>(FlatLayerPath);
        TerrainLayer beachLayer = LoadRequired<TerrainLayer>(BeachLayerPath);
        TerrainLayer mountainGrassLayer = GetOrCreateLayer(
            MountainGrassLayerPath,
            MountainGrassTexturePath,
            new Vector2(34f, 34f),
            new Color(0.62f, 0.72f, 0.56f, 1f));
        TerrainLayer mountainSoilLayer = GetOrCreateLayer(
            MountainSoilLayerPath,
            MountainSoilTexturePath,
            new Vector2(30f, 30f),
            new Color(0.66f, 0.57f, 0.46f, 1f));
        TerrainLayer mountainRockLayer = GetOrCreateLayer(
            MountainRockLayerPath,
            MountainRockTexturePath,
            new Vector2(46f, 46f),
            new Color(0.78f, 0.80f, 0.80f, 1f));
        TerrainLayer[] canonicalLayers =
        {
            flatLayer,
            beachLayer,
            mountainGrassLayer,
            mountainSoilLayer,
            mountainRockLayer
        };

        Terrain[] terrains = UnityEngine.Object.FindObjectsOfType<Terrain>(true)
            .Where(item => item.gameObject.scene == scene && item.terrainData != null)
            .ToArray();
        if (terrains.Length != 16)
        {
            throw new InvalidOperationException(
                $"Expected 16 Level03 Terrain tiles, found {terrains.Length}.");
        }

        long grassPixels = 0;
        long soilPixels = 0;
        long rockPixels = 0;
        foreach (Terrain terrain in terrains)
        {
            TerrainData data = terrain.terrainData;
            TerrainLayer[] oldLayers = data.terrainLayers;
            int beachIndex = Array.IndexOf(oldLayers, beachLayer);
            float[,,] oldWeights = oldLayers.Length > 0
                ? data.GetAlphamaps(0, 0, data.alphamapResolution, data.alphamapResolution)
                : null;

            data.terrainLayers = canonicalLayers;
            int resolution = data.alphamapResolution;
            float[,,] output = new float[resolution, resolution, canonicalLayers.Length];
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
                    float macroNoise = Mathf.PerlinNoise(
                        worldX * 0.0037f + 41.3f,
                        worldZ * 0.0037f + 17.9f);

                    float beachWeight = oldWeights != null && beachIndex >= 0
                        ? oldWeights[y, x, beachIndex]
                        : 0f;
                    beachWeight = Mathf.Clamp01(beachWeight);
                    float mountain = SmoothRange(8f, 42f, height) * (1f - beachWeight);
                    float noisySlope = slope + (macroNoise - 0.5f) * 7f;
                    float slopeRock = SmoothRange(27f, 45f, noisySlope);
                    float summitRock = SmoothRange(255f, 340f, height) * 0.28f;
                    float rock = mountain * Mathf.Max(slopeRock, summitRock);

                    float soilBand = SmoothRange(12f, 25f, noisySlope) *
                                     (1f - SmoothRange(36f, 48f, noisySlope));
                    float highSoil = SmoothRange(105f, 250f, height) * 0.16f;
                    float soil = mountain * Mathf.Clamp01(
                        soilBand * (0.42f + macroNoise * 0.18f) + highSoil) *
                                 (1f - rock);
                    float mountainGrass = mountain * Mathf.Max(0f, 1f - rock - soil);
                    float flat = Mathf.Max(0f, 1f - beachWeight - mountain);

                    output[y, x, 0] = flat;
                    output[y, x, 1] = beachWeight;
                    output[y, x, 2] = mountainGrass;
                    output[y, x, 3] = soil;
                    output[y, x, 4] = rock;

                    if (mountainGrass > 0.15f)
                    {
                        grassPixels++;
                    }
                    if (soil > 0.15f)
                    {
                        soilPixels++;
                    }
                    if (rock > 0.15f)
                    {
                        rockPixels++;
                    }
                }
            }

            data.SetAlphamaps(0, 0, output);
            EditorUtility.SetDirty(data);
            terrain.Flush();
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            $"[Level03 Mountain Surface] Painted {terrains.Length} tiles with Gorilla " +
            $"textures; grass={grassPixels:N0}, soil={soilPixels:N0}, rock={rockPixels:N0} pixels.");
    }

    private static TerrainLayer GetOrCreateLayer(
        string layerPath,
        string texturePath,
        Vector2 tileSize,
        Color colorMultiplier)
    {
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, layerPath);
        }

        layer.name = Path.GetFileNameWithoutExtension(layerPath);
        layer.diffuseTexture = LoadRequired<Texture2D>(texturePath);
        layer.tileSize = tileSize;
        layer.tileOffset = Vector2.zero;
        layer.metallic = 0f;
        layer.smoothness = 0f;
        layer.normalScale = 1f;
        layer.diffuseRemapMin = Vector4.zero;
        layer.diffuseRemapMax = new Vector4(
            colorMultiplier.r,
            colorMultiplier.g,
            colorMultiplier.b,
            colorMultiplier.a);
        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static void ConfigureTextureImport(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            throw new FileNotFoundException("Terrain texture was not found.", assetPath);
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 8;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
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
