using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class IslandMapPrototypeBuilder
{
    private const float MapScale = 3f;
    private const float OceanScale = 5f;
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string MaterialFolder = "Assets/Art/Materials";
    private const string TextureFolder = "Assets/Art/Textures";

    public static void Build()
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder(MaterialFolder);
        ConfigureTextureImporters();

        Material oceanMaterial = CreateMaterial("Ocean", "Ocean.png", new Vector2(12f, 12f), 0.32f);
        Material beachMaterial = CreateMaterial("Beach", "Beach.png", new Vector2(10f, 10f), 0.08f);
        Material grassMaterial = CreateMaterial("Grass", "Grass.png", new Vector2(9f, 9f), 0.06f);
        Material roadMaterial = CreateMaterial("Road", "Road.png", new Vector2(8f, 8f), 0.14f);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject environment = new GameObject("Environment");

        GameObject ocean = CreateTile("Ocean", new Vector3(0f, -0.30f, 0f), new Vector3(200f * OceanScale, 0.10f, 200f * OceanScale), oceanMaterial, environment.transform);
        GameObject beach = CreateTile("Beach", new Vector3(0f, -0.20f, 0f), new Vector3(170f * MapScale, 0.10f, 170f * MapScale), beachMaterial, environment.transform);
        GameObject grass = CreateTile("City Grass", new Vector3(0f, -0.10f, 0f), new Vector3(130f * MapScale, 0.10f, 130f * MapScale), grassMaterial, environment.transform);

        ocean.isStatic = true;
        beach.isStatic = true;
        grass.isStatic = true;

        GameObject roads = new GameObject("Tian Road Layout");
        roads.transform.SetParent(environment.transform);
        CreateRoad("North Road", new Vector3(0f, 0f, 59f * MapScale), new Vector3(130f * MapScale, 0.10f, 12f * MapScale), roadMaterial, roads.transform);
        CreateRoad("South Road", new Vector3(0f, 0f, -59f * MapScale), new Vector3(130f * MapScale, 0.10f, 12f * MapScale), roadMaterial, roads.transform);
        CreateRoad("West Road", new Vector3(-59f * MapScale, 0f, 0f), new Vector3(12f * MapScale, 0.10f, 106f * MapScale), roadMaterial, roads.transform);
        CreateRoad("East Road", new Vector3(59f * MapScale, 0f, 0f), new Vector3(12f * MapScale, 0.10f, 106f * MapScale), roadMaterial, roads.transform);
        CreateRoad("Center West", new Vector3(-29.5f * MapScale, 0f, 0f), new Vector3(47f * MapScale, 0.10f, 12f * MapScale), roadMaterial, roads.transform);
        CreateRoad("Center East", new Vector3(29.5f * MapScale, 0f, 0f), new Vector3(47f * MapScale, 0.10f, 12f * MapScale), roadMaterial, roads.transform);
        CreateRoad("Center North", new Vector3(0f, 0f, 29.5f * MapScale), new Vector3(12f * MapScale, 0.10f, 47f * MapScale), roadMaterial, roads.transform);
        CreateRoad("Center South", new Vector3(0f, 0f, -29.5f * MapScale), new Vector3(12f * MapScale, 0.10f, 47f * MapScale), roadMaterial, roads.transform);
        CreateRoad("Center Intersection", Vector3.zero, new Vector3(12f * MapScale, 0.10f, 12f * MapScale), roadMaterial, roads.transform);

        Camera camera = CreateCamera();
        CreateLighting();

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.82f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.50f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.25f, 0.28f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        SavePreview(camera);
        Selection.activeGameObject = roads;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("IslandMapPrototype: generated square ocean, beach, grass city and Tian-shaped roads.");
    }

    private static GameObject CreateTile(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tile.name = name;
        tile.transform.SetParent(parent);
        tile.transform.position = position;
        tile.transform.localScale = scale;
        tile.GetComponent<MeshRenderer>().sharedMaterial = material;
        return tile;
    }

    private static void CreateRoad(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        GameObject road = CreateTile(name, position, scale, material, parent);
        road.isStatic = true;
    }

    private static Camera CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 650f, 0f);
        cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        camera.orthographic = false;
        camera.fieldOfView = 62f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 300f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.18f, 0.52f, 0.68f);
        return camera;
    }

    private static void CreateLighting()
    {
        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
    }

    private static void SavePreview(Camera camera)
    {
        const string previewFolder = "Assets/Preview";
        const string previewPath = previewFolder + "/IslandMapPreview.png";
        EnsureFolder(previewFolder);

        RenderTexture renderTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
        Texture2D preview = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        camera.targetTexture = renderTexture;
        camera.Render();
        RenderTexture.active = renderTexture;
        preview.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
        preview.Apply();
        File.WriteAllBytes(previewPath, preview.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(preview);
        Object.DestroyImmediate(renderTexture);
        AssetDatabase.ImportAsset(previewPath, ImportAssetOptions.ForceUpdate);
    }

    private static Material CreateMaterial(string name, string textureName, Vector2 tiling, float smoothness)
    {
        string materialPath = $"{MaterialFolder}/{name}.mat";
        AssetDatabase.DeleteAsset(materialPath);

        Shader shader = Shader.Find("Standard");
        Material material = new Material(shader)
        {
            name = name
        };
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{textureName}");
        material.mainTexture = texture;
        material.mainTextureScale = tiling;
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", smoothness);
        AssetDatabase.CreateAsset(material, materialPath);
        return material;
    }

    private static void ConfigureTextureImporters()
    {
        string[] textureNames = { "Ocean.png", "Beach.png", "Grass.png", "Road.png" };
        foreach (string textureName in textureNames)
        {
            string assetPath = $"{TextureFolder}/{textureName}";
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                continue;
            }

            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
    }
}
