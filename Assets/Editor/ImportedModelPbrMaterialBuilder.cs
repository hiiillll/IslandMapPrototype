using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ImportedModelPbrMaterialBuilder
{
    private const string ImportedModelRoot = "Assets/Models/Imported";
    private const string AlbedoName = "texture_pbr_20250901.png";
    private const string MetallicName = "texture_pbr_20250901_metallic.png";
    private const string RoughnessName = "texture_pbr_20250901_roughness.png";
    private const string NormalName = "texture_pbr_20250901_normal.png";
    private const string PackedMapName = "texture_pbr_metallic_smoothness.png";
    private const string MaterialName = "PBR_Material.mat";

    private static readonly string[] PendingModelFolders =
    {
        "Model_18",
        "Model_19",
        "Model_20"
    };

    [MenuItem("Tools/Island Map/Configure Pending Imported Model PBR Materials")]
    public static void ConfigurePendingModels()
    {
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (string folderName in PendingModelFolders)
            {
                ConfigureModelFolder($"{ImportedModelRoot}/{folderName}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Configured PBR materials for {PendingModelFolders.Length} imported models.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static void ConfigureModelFolder(string folderPath)
    {
        string albedoPath = $"{folderPath}/{AlbedoName}";
        string metallicPath = $"{folderPath}/{MetallicName}";
        string roughnessPath = $"{folderPath}/{RoughnessName}";
        string normalPath = $"{folderPath}/{NormalName}";
        string packedMapPath = $"{folderPath}/{PackedMapName}";
        string materialPath = $"{folderPath}/{MaterialName}";

        RequireAsset(albedoPath);
        RequireAsset(metallicPath);
        RequireAsset(roughnessPath);
        RequireAsset(normalPath);

        ConfigureTexture(albedoPath, TextureImporterType.Default, true);
        ConfigureTexture(metallicPath, TextureImporterType.Default, false);
        ConfigureTexture(roughnessPath, TextureImporterType.Default, false);
        ConfigureTexture(normalPath, TextureImporterType.NormalMap, false);

        BuildMetallicSmoothnessMap(metallicPath, roughnessPath, packedMapPath);
        AssetDatabase.ImportAsset(
            packedMapPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        ConfigureTexture(packedMapPath, TextureImporterType.Default, false);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("Unity Standard shader is unavailable.");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }

        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        material.SetFloat("_BumpScale", 1f);
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedMapPath));
        material.SetFloat("_Metallic", 1f);
        material.SetFloat("_Glossiness", 1f);
        material.SetFloat("_GlossMapScale", 1f);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICGLOSSMAP");
        EditorUtility.SetDirty(material);

        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });
        if (modelGuids.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one model in {folderPath}, found {modelGuids.Length}.");
        }

        string modelPath = AssetDatabase.GUIDToAssetPath(modelGuids[0]);
        ModelImporter modelImporter = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (modelImporter == null)
        {
            throw new InvalidOperationException($"Could not load model importer for {modelPath}.");
        }

        modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        modelImporter.importNormals = ModelImporterNormals.Calculate;
        modelImporter.importTangents = ModelImporterTangents.CalculateMikk;
        modelImporter.normalSmoothingAngle = 60f;
        modelImporter.AddRemap(
            new AssetImporter.SourceAssetIdentifier(typeof(Material), "Material"),
            material);
        modelImporter.SaveAndReimport();
    }

    private static void ConfigureTexture(
        string assetPath,
        TextureImporterType textureType,
        bool isSrgb)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"Could not load texture importer for {assetPath}.");
        }

        bool changed = importer.textureType != textureType ||
                       importer.sRGBTexture != isSrgb ||
                       importer.maxTextureSize != 2048;
        importer.textureType = textureType;
        importer.sRGBTexture = isSrgb;
        importer.maxTextureSize = 2048;

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static void BuildMetallicSmoothnessMap(
        string metallicAssetPath,
        string roughnessAssetPath,
        string outputAssetPath)
    {
        Texture2D metallic = LoadSourceTexture(metallicAssetPath, false);
        Texture2D roughness = LoadSourceTexture(roughnessAssetPath, false);
        try
        {
            if (metallic.width != roughness.width || metallic.height != roughness.height)
            {
                throw new InvalidOperationException(
                    $"Metallic and roughness dimensions differ in {Path.GetDirectoryName(metallicAssetPath)}.");
            }

            Color32[] metallicPixels = metallic.GetPixels32();
            Color32[] roughnessPixels = roughness.GetPixels32();
            Color32[] packedPixels = new Color32[metallicPixels.Length];
            for (int index = 0; index < packedPixels.Length; index++)
            {
                byte metallicValue = metallicPixels[index].r;
                byte smoothnessValue = (byte)(byte.MaxValue - roughnessPixels[index].r);
                packedPixels[index] = new Color32(
                    metallicValue,
                    metallicValue,
                    metallicValue,
                    smoothnessValue);
            }

            Texture2D packed = new Texture2D(
                metallic.width,
                metallic.height,
                TextureFormat.RGBA32,
                false,
                true);
            try
            {
                packed.SetPixels32(packedPixels);
                packed.Apply(false, false);
                File.WriteAllBytes(outputAssetPath, packed.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(packed);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(metallic);
            UnityEngine.Object.DestroyImmediate(roughness);
        }
    }

    private static Texture2D LoadSourceTexture(string assetPath, bool markNonReadable)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        if (!texture.LoadImage(File.ReadAllBytes(assetPath), markNonReadable))
        {
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidOperationException($"Could not decode {assetPath}.");
        }

        return texture;
    }

    private static void RequireAsset(string assetPath)
    {
        if (!File.Exists(assetPath))
        {
            throw new FileNotFoundException($"Required model asset is missing: {assetPath}");
        }
    }
}
