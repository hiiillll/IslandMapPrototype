using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Porsche911AssetConfigurator
{
    private const string ModelPath = "Assets/Resources/Vehicles/GarageCar02/Porsche911_GT3RS.fbx";
    private static readonly string[] NormalTexturePaths =
    {
        "Assets/Resources/Vehicles/GarageCar02/Porsche911_Normal.png",
        "Assets/Resources/Vehicles/GarageCar02/Porsche911_WheelNormal.png"
    };
    private static readonly string[] ColorTexturePaths =
    {
        "Assets/Resources/Vehicles/GarageCar02/Porsche911_Albedo.png",
        "Assets/Resources/Vehicles/GarageCar02/Porsche911_WheelAlbedo.png"
    };

    [MenuItem("Tools/Island Map/Configure Porsche 911 Asset")]
    public static void ConfigureImportedAssets()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureModel();
        foreach (string path in ColorTexturePaths)
        {
            ConfigureTexture(path, false);
        }
        foreach (string path in NormalTexturePaths)
        {
            ConfigureTexture(path, true);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ValidateModel();
    }

    private static void ConfigureModel()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("Porsche 911 FBX importer was not found.");
        }

        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.isReadable = false;
        importer.preserveHierarchy = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.SaveAndReimport();
    }

    private static void ConfigureTexture(string path, bool normalMap)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("Texture importer was not found: " + path);
        }

        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normalMap;
        importer.mipmapEnabled = true;
        importer.streamingMipmaps = true;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void ValidateModel()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (source == null)
        {
            throw new InvalidOperationException("Porsche 911 FBX could not be loaded after import.");
        }

        string[] requiredNodes =
        {
            "Body",
            "Wheel_FL_Steer", "Wheel_FL_Spin", "Wheel_FL_Tire",
            "Wheel_FR_Steer", "Wheel_FR_Spin", "Wheel_FR_Tire",
            "Wheel_RL_Spin", "Wheel_RL_Tire",
            "Wheel_RR_Spin", "Wheel_RR_Tire"
        };
        Transform[] transforms = source.GetComponentsInChildren<Transform>(true);
        foreach (string nodeName in requiredNodes)
        {
            if (!transforms.Any(value => value.name == nodeName))
            {
                throw new InvalidOperationException("Porsche 911 hierarchy is missing node: " + nodeName);
            }
        }

        ValidateSteeringAxis(source);

        int triangles = source.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter.sharedMesh != null)
            .Sum(filter => filter.sharedMesh.triangles.Length / 3);
        Debug.Log($"[Porsche911] Import validated: {triangles:N0} triangles and all wheel pivots present.");
    }

    private static void ValidateSteeringAxis(GameObject source)
    {
        GameObject instance = UnityEngine.Object.Instantiate(source);
        try
        {
            Transform steer = instance.GetComponentsInChildren<Transform>(true)
                .First(value => value.name == "Wheel_FL_Steer");
            Quaternion baseRotation = steer.localRotation;
            Vector3 vehicleUp = instance.transform.up;
            Vector3 parentLocalUp = steer.parent.InverseTransformDirection(vehicleUp).normalized;
            steer.localRotation = Quaternion.AngleAxis(28f, parentLocalUp) * baseRotation;

            float axleVerticalComponent = Mathf.Abs(Vector3.Dot(steer.right.normalized, vehicleUp.normalized));
            if (axleVerticalComponent > 0.001f)
            {
                throw new InvalidOperationException(
                    $"Porsche 911 steering tilts the wheel axle vertically: {axleVerticalComponent:F6}");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}
