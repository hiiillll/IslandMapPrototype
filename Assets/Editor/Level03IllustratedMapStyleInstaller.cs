using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class Level03IllustratedMapStyleInstaller
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string TextureFolder = "Assets/Level03/MapStyleTextures";
    private const string LayerFolder = "Assets/Level03/TerrainLayers";
    private const string SandTexturePath = TextureFolder + "/T_Level03_IllustratedSand_Gorilla.png";
    private const string GrassTexturePath = TextureFolder + "/T_Level03_IllustratedGrass_Gorilla.png";
    private const string SoilTexturePath = TextureFolder + "/T_Level03_IllustratedSoil_Gorilla.png";
    private const string RockTexturePath = TextureFolder + "/T_Level03_IllustratedRock_Gorilla.png";
    private const string OceanTexturePath = TextureFolder + "/T_Level03_IllustratedOcean_Gorilla.png";
    private const string ShoreMaskPath = TextureFolder + "/T_Level03_IllustratedShoreMask.png";
    private const string BeachLayerPath = LayerFolder + "/TL_Level03_Beach.terrainlayer";
    private const string GrassLayerPath = LayerFolder + "/TL_Level03_MountainGrass.terrainlayer";
    private const string SoilLayerPath = LayerFolder + "/TL_Level03_MountainSoil.terrainlayer";
    private const string RockLayerPath = LayerFolder + "/TL_Level03_MountainRock.terrainlayer";
    private const string OceanMaterialPath = "Assets/Level03/GeneratedTerrainRoad/MAT_Level03_Ocean.mat";
    private const string Level02OceanMaterialPath = "Assets/Art/Materials/Ocean_BoatChase.mat";
    private const string BeachMaterialPath = "Assets/Level03/GeneratedTerrainRoad/MAT_Level03_Beach.mat";
    private const string RoadMaterialPath = "Assets/Level03/GeneratedTerrainRoad/MAT_Level03_Road.mat";
    private const string OceanShaderName = "IslandMap/IllustratedOcean";
    private const string FlatGrassName = "ENV_Level03_FlatGrass_FirstLevel";
    private const string GorillaPropsName = "DECOR_Level03_GorillaMountainProps";
    private const int ShoreMaskResolution = 1024;
    private const float WorldMinimum = -2000f;
    private const float WorldSize = 4000f;

    [Serializable]
    private sealed class StyleReport
    {
        public bool success;
        public int terrainCount;
        public long sandPixels;
        public long grassPixels;
        public long soilPixels;
        public long rockPixels;
        public bool flatGrassOverlayDisabled;
        public bool shoreMaskCreated;
        public bool illustratedOceanApplied;
        public string completedAt;
    }

    private sealed class TerrainHoleMap
    {
        public Terrain terrain;
        public bool[,] surface;
    }

    public static void ApplyFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Apply();
        Level03ActivePlanSplineRoadRebuilder.RenderVerificationPreview();
    }

    public static void ApplyUniformOceanOnlyFromCommandLine()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Texture2D shoreMask = LoadRequired<Texture2D>(ShoreMaskPath);
        ConfigureUniformOcean(shoreMask);
        RemoveGorillaProps(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Level03 Ocean] Applied one uniform cyan-blue color without modifying Terrain data.");
    }

    public static void ApplyLevel02OceanOnlyFromCommandLine()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject ocean = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(item => item.scene == scene && item.name == "ENV_Level03_Ocean_4000x4000");
        MeshRenderer renderer = ocean != null ? ocean.GetComponent<MeshRenderer>() : null;
        if (renderer == null)
        {
            throw new InvalidOperationException("Level03 ocean renderer was not found.");
        }

        renderer.sharedMaterial = LoadRequired<Material>(Level02OceanMaterialPath);
        EditorUtility.SetDirty(renderer);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Level03 Ocean] Assigned the exact Ocean_BoatChase material used by Level02.");
    }

    [MenuItem("Tools/Island Map/Level03/Apply Illustrated Whole Map Style")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException("Level03 must be the active scene.");
        }

        EnsureRequiredTexture(SandTexturePath);
        EnsureRequiredTexture(GrassTexturePath);
        EnsureRequiredTexture(SoilTexturePath);
        EnsureRequiredTexture(RockTexturePath);
        EnsureRequiredTexture(OceanTexturePath);

        TerrainLayer sandLayer = ConfigureTerrainLayer(
            BeachLayerPath,
            SandTexturePath,
            new Vector2(62f, 62f),
            new Color(0.78f, 0.78f, 0.70f, 1f));
        TerrainLayer grassLayer = ConfigureTerrainLayer(
            GrassLayerPath,
            GrassTexturePath,
            new Vector2(78f, 78f),
            new Color(0.48f, 0.72f, 0.38f, 1f));
        TerrainLayer soilLayer = ConfigureTerrainLayer(
            SoilLayerPath,
            SoilTexturePath,
            new Vector2(58f, 58f),
            new Color(0.70f, 0.61f, 0.47f, 1f));
        TerrainLayer rockLayer = ConfigureTerrainLayer(
            RockLayerPath,
            RockTexturePath,
            new Vector2(70f, 70f),
            new Color(0.90f, 0.89f, 0.82f, 1f));
        TerrainLayer[] layers = { sandLayer, grassLayer, soilLayer, rockLayer };

        Terrain[] terrains = UnityEngine.Object.FindObjectsOfType<Terrain>(true)
            .Where(item => item.gameObject.scene == scene && item.terrainData != null)
            .OrderBy(item => item.transform.position.z)
            .ThenBy(item => item.transform.position.x)
            .ToArray();
        if (terrains.Length != 16)
        {
            throw new InvalidOperationException($"Expected 16 Level03 Terrain tiles, found {terrains.Length}.");
        }

        long sandPixels = 0;
        long grassPixels = 0;
        long soilPixels = 0;
        long rockPixels = 0;
        foreach (Terrain terrain in terrains)
        {
            PaintTerrain(
                terrain,
                layers,
                ref sandPixels,
                ref grassPixels,
                ref soilPixels,
                ref rockPixels);
        }

        bool overlayDisabled = DisableFlatGrassOverlay(scene);
        RemoveGorillaProps(scene);
        Texture2D shoreMask = BuildShoreMask(terrains);
        ConfigureSceneMaterials(shoreMask);
        ConfigureLighting(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        StyleReport report = new StyleReport
        {
            success = true,
            terrainCount = terrains.Length,
            sandPixels = sandPixels,
            grassPixels = grassPixels,
            soilPixels = soilPixels,
            rockPixels = rockPixels,
            flatGrassOverlayDisabled = overlayDisabled,
            shoreMaskCreated = shoreMask != null,
            illustratedOceanApplied = AssetDatabase.LoadAssetAtPath<Material>(OceanMaterialPath)?.shader?.name ==
                                     OceanShaderName,
            completedAt = DateTime.Now.ToString("O")
        };
        File.WriteAllText(
            Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Library/CodexLevel03IllustratedMapStyle.json"),
            JsonUtility.ToJson(report, true));
        Debug.Log(
            $"[Level03 Illustrated Map Style] Painted {terrains.Length} tiles; " +
            $"sand={sandPixels:N0}, grass={grassPixels:N0}, soil={soilPixels:N0}, " +
            $"rock={rockPixels:N0}; shore mask and illustrated ocean applied.");
    }

    private static void PaintTerrain(
        Terrain terrain,
        TerrainLayer[] layers,
        ref long sandPixels,
        ref long grassPixels,
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
                float macro = Mathf.PerlinNoise(worldX * 0.0022f + 31.7f, worldZ * 0.0022f + 83.2f);
                float detail = Mathf.PerlinNoise(worldX * 0.0068f + 12.1f, worldZ * 0.0068f + 54.4f);
                float patch = Mathf.Clamp01(macro * 0.74f + detail * 0.26f);

                float mountain = SmoothRange(9f, 38f, height);
                float lowland = 1f - mountain;
                float lowGrass = lowland * SmoothRange(0.43f, 0.68f, patch) * 0.84f;
                lowGrass *= Mathf.Lerp(0.72f, 1f, SmoothRange(0.32f, 8f, height));
                float sand = Mathf.Max(0f, lowland - lowGrass);

                float noisySlope = slope + (macro - 0.5f) * 7f;
                float slopeRock = SmoothRange(24f, 43f, noisySlope);
                float summitRock = SmoothRange(220f, 330f, height) * 0.88f;
                float rockHeight = Mathf.Lerp(0.05f, 1f, SmoothRange(72f, 235f, height));
                float rock = mountain * Mathf.Max(slopeRock * rockHeight, summitRock);

                float soilBand = SmoothRange(10f, 23f, noisySlope) *
                                 (1f - SmoothRange(34f, 46f, noisySlope));
                float dryPatch = SmoothRange(0.50f, 0.78f, 1f - patch);
                float soil = mountain * Mathf.Clamp01(soilBand * 0.58f + dryPatch * 0.22f) *
                             (1f - rock);
                float grass = lowGrass + mountain * Mathf.Max(0f, 1f - rock - soil);

                float total = Mathf.Max(0.0001f, sand + grass + soil + rock);
                sand /= total;
                grass /= total;
                soil /= total;
                rock /= total;
                weights[y, x, 0] = sand;
                weights[y, x, 1] = grass;
                weights[y, x, 2] = soil;
                weights[y, x, 3] = rock;

                if (sand > 0.15f) sandPixels++;
                if (grass > 0.15f) grassPixels++;
                if (soil > 0.15f) soilPixels++;
                if (rock > 0.15f) rockPixels++;
            }
        }

        data.SetAlphamaps(0, 0, weights);
        EditorUtility.SetDirty(data);
        terrain.Flush();
    }

    private static Texture2D BuildShoreMask(IReadOnlyList<Terrain> terrains)
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
        int pixelCount = ShoreMaskResolution * ShoreMaskResolution;
        bool[] land = new bool[pixelCount];
        float[] distance = new float[pixelCount];
        const float infinite = 100000f;

        for (int y = 0; y < ShoreMaskResolution; y++)
        {
            float worldZ = WorldMinimum + (y + 0.5f) / ShoreMaskResolution * WorldSize;
            for (int x = 0; x < ShoreMaskResolution; x++)
            {
                float worldX = WorldMinimum + (x + 0.5f) / ShoreMaskResolution * WorldSize;
                bool isLand = SampleTerrainSurface(maps, worldX, worldZ);
                int index = y * ShoreMaskResolution + x;
                land[index] = isLand;
                distance[index] = isLand ? 0f : infinite;
            }
        }

        ChamferDistancePass(distance, true);
        ChamferDistancePass(distance, false);
        float worldUnitsPerPixel = WorldSize / ShoreMaskResolution;
        Color[] pixels = new Color[pixelCount];
        for (int index = 0; index < pixelCount; index++)
        {
            if (land[index])
            {
                pixels[index] = new Color(1f, 0f, 0f, 1f);
                continue;
            }

            float worldDistance = distance[index] * worldUnitsPerPixel;
            float shallow = 1f - SmoothRange(3f, 90f, worldDistance);
            float foam = 1f - SmoothRange(2f, 15f, worldDistance);
            pixels[index] = new Color(shallow, foam, 0f, 1f);
        }

        Texture2D texture = new Texture2D(
            ShoreMaskResolution,
            ShoreMaskResolution,
            TextureFormat.RGBA32,
            false,
            true);
        texture.name = "T_Level03_IllustratedShoreMask";
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, ShoreMaskPath),
            texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(ShoreMaskPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(ShoreMaskPath) as TextureImporter;
        if (importer != null)
        {
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ShoreMaskPath);
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

    private static void ChamferDistancePass(float[] distance, bool forward)
    {
        int start = forward ? 0 : ShoreMaskResolution - 1;
        int end = forward ? ShoreMaskResolution : -1;
        int step = forward ? 1 : -1;
        for (int y = start; y != end; y += step)
        {
            for (int x = start; x != end; x += step)
            {
                int index = y * ShoreMaskResolution + x;
                float best = distance[index];
                int previousX = x - step;
                int previousY = y - step;
                if (previousX >= 0 && previousX < ShoreMaskResolution)
                {
                    best = Mathf.Min(best, distance[y * ShoreMaskResolution + previousX] + 1f);
                }
                if (previousY >= 0 && previousY < ShoreMaskResolution)
                {
                    best = Mathf.Min(best, distance[previousY * ShoreMaskResolution + x] + 1f);
                    if (previousX >= 0 && previousX < ShoreMaskResolution)
                    {
                        best = Mathf.Min(
                            best,
                            distance[previousY * ShoreMaskResolution + previousX] + 1.41421356f);
                    }
                    int oppositeX = x + step;
                    if (oppositeX >= 0 && oppositeX < ShoreMaskResolution)
                    {
                        best = Mathf.Min(
                            best,
                            distance[previousY * ShoreMaskResolution + oppositeX] + 1.41421356f);
                    }
                }
                distance[index] = best;
            }
        }
    }

    private static void ConfigureSceneMaterials(Texture2D shoreMask)
    {
        ConfigureUniformOcean(shoreMask);

        Material beach = LoadRequired<Material>(BeachMaterialPath);
        SetMainTexture(beach, LoadRequired<Texture2D>(SandTexturePath), new Vector2(1.25f, 1.25f));
        SetMaterialColor(beach, new Color(0.83f, 0.79f, 0.68f, 1f));
        SetMaterialFloatIfPresent(beach, "_Glossiness", 0.02f);
        EditorUtility.SetDirty(beach);

        Material road = LoadRequired<Material>(RoadMaterialPath);
        SetMaterialColor(road, new Color(0.54f, 0.53f, 0.49f, 1f));
        SetMaterialFloatIfPresent(road, "_Glossiness", 0.05f);
        SetMaterialFloatIfPresent(road, "_Metallic", 0f);
        EditorUtility.SetDirty(road);
    }

    private static void ConfigureUniformOcean(Texture2D shoreMask)
    {
        Material ocean = LoadRequired<Material>(OceanMaterialPath);
        Shader oceanShader = Shader.Find(OceanShaderName);
        if (oceanShader == null)
        {
            throw new InvalidOperationException($"Ocean shader was not found: {OceanShaderName}");
        }
        ocean.shader = oceanShader;
        ocean.SetTexture("_MainTex", LoadRequired<Texture2D>(OceanTexturePath));
        ocean.SetTextureScale("_MainTex", new Vector2(5f, 5f));
        ocean.SetTexture("_ShoreMask", shoreMask);
        Color uniformOcean = new Color(0.035f, 0.50f, 0.60f, 1f);
        ocean.SetColor("_DeepColor", uniformOcean);
        ocean.SetColor("_MidColor", uniformOcean);
        ocean.SetColor("_ShallowColor", uniformOcean);
        ocean.SetColor("_FoamColor", uniformOcean);
        ocean.SetFloat("_TextureStrength", 0f);
        ocean.SetFloat("_FoamStrength", 0f);
        EditorUtility.SetDirty(ocean);
    }

    private static void ConfigureLighting(Scene scene)
    {
        Light sun = Resources.FindObjectsOfTypeAll<Light>()
            .FirstOrDefault(item => item.gameObject.scene == scene && item.type == LightType.Directional);
        if (sun != null)
        {
            sun.color = new Color(1f, 0.96f, 0.84f, 1f);
            sun.intensity = 0.88f;
            sun.shadows = LightShadows.Soft;
            EditorUtility.SetDirty(sun);
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.76f, 0.75f);
        RenderSettings.ambientEquatorColor = new Color(0.61f, 0.62f, 0.54f);
        RenderSettings.ambientGroundColor = new Color(0.43f, 0.44f, 0.38f);
    }

    private static TerrainLayer ConfigureTerrainLayer(
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

    private static bool DisableFlatGrassOverlay(Scene scene)
    {
        GameObject overlay = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(item => item.scene == scene && item.name == FlatGrassName);
        MeshRenderer renderer = overlay != null ? overlay.GetComponent<MeshRenderer>() : null;
        if (renderer == null)
        {
            throw new InvalidOperationException($"Flat grass overlay was not found: {FlatGrassName}");
        }
        renderer.enabled = false;
        EditorUtility.SetDirty(renderer);
        return !renderer.enabled;
    }

    private static void RemoveGorillaProps(Scene scene)
    {
        GameObject props = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(item => item.scene == scene && item.name == GorillaPropsName);
        if (props != null)
        {
            UnityEngine.Object.DestroyImmediate(props);
        }
    }

    private static void EnsureRequiredTexture(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            throw new FileNotFoundException("Illustrated map texture was not found.", assetPath);
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

    private static void SetMainTexture(Material material, Texture texture, Vector2 scale)
    {
        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", scale);
        }
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", scale);
        }
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
    }

    private static void SetMaterialFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
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
