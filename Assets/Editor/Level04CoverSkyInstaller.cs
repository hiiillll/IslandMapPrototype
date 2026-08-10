using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Level04CoverSkyInstaller
{
    private const string ScenePath = "Assets/Scenes/Level04.unity";
    private const string AutoInstallMarker = "Temp/ApplyLevel04CoverSky.once";
    private const string FastSkyDetailTextureAPath =
        "Assets/#NVJOB Dynamic Sky/Examples Sky/Textures/Tx1.png";
    private const string FastSkyDetailTextureBPath =
        "Assets/#NVJOB Dynamic Sky/Examples Sky/Textures/Tx2.png";
    private const string LevelMaterialFolder = "Assets/Level04/Materials";
    private const string CoverSkyboxPath = LevelMaterialFolder + "/MAT_Level04_CoverSunsetSky.mat";
    private const string CloudSeaMeshFolder = "Assets/Level04/Meshes";
    private const string CloudSeaMeshPath = CloudSeaMeshFolder + "/MESH_Level04_CloudSea.asset";
    private const string PreviewPath = "Previews/Level04_CoverSkyPreview.png";
    private const string SkyObjectName = "ENV_FastDynamicSky_Cover";

    static Level04CoverSkyInstaller()
    {
        EditorApplication.delayCall += TryAutoInstall;
    }

    [MenuItem("Tools/Island Map/Level04/Apply Cover Dynamic Sky")]
    public static void ApplyFromMenu()
    {
        ApplyToSceneAsset();
    }

    public static void ApplyFromCommandLine()
    {
        try
        {
            ApplyToSceneAsset();
            CapturePreviewToFile();
            Debug.Log("[Level04] Cover-matched Fast Dynamic Sky applied.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void ApplyToLoadedScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            throw new InvalidOperationException("Level04 scene must be loaded before applying the sky.");
        }

        GameObject environmentRoot = FindRoot(scene, "ENVIRONMENT");
        GameObject player = FindInScene(scene, "PLAYER_Plane");
        GameObject existingSky = FindInScene(scene, SkyObjectName);
        if (existingSky != null)
        {
            UnityEngine.Object.DestroyImmediate(existingSky);
        }

        Material skyboxMaterial = CreateCoverSkyboxMaterial();

        ConfigureExistingCloudSea(scene);
        ConfigureCloudField(scene);
        ConfigureLighting(scene);
        ConfigureRenderSettings(skyboxMaterial);
        ConfigureCameras(scene);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void TryAutoInstall()
    {
        if (!File.Exists(AutoInstallMarker))
        {
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoInstall;
            return;
        }

        try
        {
            File.Delete(AutoInstallMarker);
            ApplyToSceneAsset();
            Debug.Log("[Level04] Fast Dynamic Sky auto-install completed.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void ApplyToSceneAsset()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene levelScene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForInstall = !levelScene.IsValid() || !levelScene.isLoaded;
        if (openedForInstall)
        {
            levelScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        try
        {
            SceneManager.SetActiveScene(levelScene);
            ApplyToLoadedScene(levelScene);
            if (!EditorSceneManager.SaveScene(levelScene, ScenePath))
            {
                throw new IOException("Unity could not save " + ScenePath);
            }
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }
            if (openedForInstall && levelScene.IsValid() && levelScene.isLoaded)
            {
                EditorSceneManager.CloseScene(levelScene, true);
            }
        }
    }

    private static Material CreateCoverSkyboxMaterial()
    {
        Shader skyShader = Shader.Find("Custom/Level04CinematicSkybox");
        Texture2D detailA = AssetDatabase.LoadAssetAtPath<Texture2D>(FastSkyDetailTextureAPath);
        Texture2D detailB = AssetDatabase.LoadAssetAtPath<Texture2D>(FastSkyDetailTextureBPath);
        if (skyShader == null || detailA == null || detailB == null)
        {
            throw new MissingReferenceException("Level04 cinematic sky shader or Fast Dynamic Sky textures are missing.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(CoverSkyboxPath);
        if (material == null)
        {
            material = new Material(skyShader);
            AssetDatabase.CreateAsset(material, CoverSkyboxPath);
        }
        else
        {
            material.shader = skyShader;
        }
        material.SetTexture("_CloudTexA", detailA);
        material.SetTexture("_CloudTexB", detailB);
        SetColor(material, "_ZenithColor", new Color(0.1f, 0.14f, 0.22f, 1f));
        SetColor(material, "_HorizonColor", new Color(0.34f, 0.36f, 0.42f, 1f));
        SetColor(material, "_CloudShadow", new Color(0.12f, 0.15f, 0.21f, 1f));
        SetColor(material, "_CloudLight", new Color(0.58f, 0.57f, 0.59f, 1f));
        SetColor(material, "_SunColor", new Color(1f, 0.48f, 0.2f, 1f));
        material.SetVector("_SunDirection", new Vector4(-0.64f, 0.045f, 0.77f, 0f));
        SetFloat(material, "_CloudCoverage", 0.46f);
        SetFloat(material, "_CloudContrast", 0.095f);
        SetFloat(material, "_CloudBrightness", 1.02f);
        SetFloat(material, "_Exposure", 1.02f);
        material.SetVector("_DriftA", new Vector4(0.0007f, 0.00015f, 0f, 0f));
        material.SetVector("_DriftB", new Vector4(-0.0003f, 0.00025f, 0f, 0f));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureExistingCloudSea(Scene scene)
    {
        Material sea = AssetDatabase.LoadAssetAtPath<Material>(
            LevelMaterialFolder + "/MAT_Level04_CloudSea.mat");
        if (sea != null)
        {
            Shader cloudSeaShader = Shader.Find("Custom/Level04CloudSeaSunset");
            Texture2D detailA = AssetDatabase.LoadAssetAtPath<Texture2D>(FastSkyDetailTextureAPath);
            Texture2D detailB = AssetDatabase.LoadAssetAtPath<Texture2D>(FastSkyDetailTextureBPath);
            if (cloudSeaShader == null || detailA == null || detailB == null)
            {
                throw new MissingReferenceException("Level04 cloud sea shader or Fast Dynamic Sky textures are missing.");
            }
            sea.shader = cloudSeaShader;
            sea.SetColor("_Tint", new Color(0.94f, 0.91f, 0.88f, 1f));
            sea.SetColor("_ShadowTint", new Color(0.23f, 0.28f, 0.38f, 1f));
            sea.SetColor("_WarmTint", new Color(1f, 0.68f, 0.42f, 1f));
            sea.SetTexture("_DetailTexA", detailA);
            sea.SetTexture("_DetailTexB", detailB);
            sea.SetFloat("_DetailStrength", 0.12f);
            sea.SetFloat("_WarmStrength", 0.18f);
            sea.SetVector("_SunDirection", new Vector4(-0.64f, 0.045f, 0.77f, 0f));
            sea.SetVector("_DriftA", new Vector4(0.0005f, 0.00015f, 0f, 0f));
            sea.SetVector("_DriftB", new Vector4(-0.0002f, 0.00035f, 0f, 0f));
            sea.mainTextureScale = new Vector2(3.5f, 3.5f);
            EditorUtility.SetDirty(sea);
        }

        GameObject cloudSea = FindInScene(scene, "ENV_CloudSea");
        if (cloudSea != null)
        {
            MeshFilter meshFilter = cloudSea.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilter.sharedMesh = CreateCloudSeaMesh();
            }
            cloudSea.transform.localPosition = new Vector3(0f, -36f, 0f);
            cloudSea.transform.localRotation = Quaternion.identity;
            cloudSea.transform.localScale = Vector3.one;
            EditorUtility.SetDirty(cloudSea);
        }

        Material sheets = AssetDatabase.LoadAssetAtPath<Material>(
            LevelMaterialFolder + "/MAT_Level04_CloudSheets.mat");
        if (sheets != null)
        {
            SetColor(sheets, "_Tint", new Color(0.73f, 0.71f, 0.73f, 0.42f));
            SetFloat(sheets, "_GradientStrength", 1.08f);
            SetFloat(sheets, "_VerticalShade", 0.22f);
            EditorUtility.SetDirty(sheets);
        }

        Material horizon = AssetDatabase.LoadAssetAtPath<Material>(
            LevelMaterialFolder + "/MAT_Level04_CloudHorizon.mat");
        if (horizon != null)
        {
            SetColor(horizon, "_Tint", new Color(0.82f, 0.75f, 0.72f, 0.7f));
            SetFloat(horizon, "_Cutoff", 0.28f);
            SetFloat(horizon, "_Softness", 0.12f);
            SetFloat(horizon, "_GradientStrength", 1.28f);
            SetFloat(horizon, "_VerticalShade", 0.3f);
            EditorUtility.SetDirty(horizon);
        }
    }

    private static Mesh CreateCloudSeaMesh()
    {
        if (!AssetDatabase.IsValidFolder(CloudSeaMeshFolder))
        {
            AssetDatabase.CreateFolder("Assets/Level04", "Meshes");
        }

        const int segments = 128;
        const float worldSize = 8000f;
        int verticesPerSide = segments + 1;
        Vector3[] vertices = new Vector3[verticesPerSide * verticesPerSide];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * segments * 6];

        for (int z = 0; z <= segments; z++)
        {
            float v = (float)z / segments;
            for (int x = 0; x <= segments; x++)
            {
                float u = (float)x / segments;
                float broad = Mathf.PerlinNoise(u * 8f + 1.37f, v * 8f + 2.81f);
                float medium = Mathf.PerlinNoise(u * 23f + 5.13f, v * 23f + 7.29f);
                float density = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(
                    0.32f,
                    0.82f,
                    broad * 0.76f + medium * 0.24f));
                float height = density * 18f + (medium - 0.5f) * 2.4f;
                int index = z * verticesPerSide + x;
                vertices[index] = new Vector3(
                    (u - 0.5f) * worldSize,
                    height,
                    (v - 0.5f) * worldSize);
                uvs[index] = new Vector2(u, v);
            }
        }

        int triangleIndex = 0;
        for (int z = 0; z < segments; z++)
        {
            for (int x = 0; x < segments; x++)
            {
                int bottomLeft = z * verticesPerSide + x;
                int topLeft = bottomLeft + verticesPerSide;
                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = bottomLeft + 1;
                triangles[triangleIndex++] = bottomLeft + 1;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topLeft + 1;
            }
        }

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CloudSeaMeshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "MESH_Level04_CloudSea" };
            AssetDatabase.CreateAsset(mesh, CloudSeaMeshPath);
        }
        else
        {
            mesh.Clear();
        }
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static void ConfigureCloudField(Scene scene)
    {
        PlaneCloudField cloudField = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlaneCloudField>(true))
            .FirstOrDefault();
        if (cloudField == null)
        {
            return;
        }

        SerializedObject serializedCloudField = new SerializedObject(cloudField);
        serializedCloudField.FindProperty("deckCardCount").intValue = 30;
        serializedCloudField.FindProperty("horizonCardCount").intValue = 0;
        serializedCloudField.FindProperty("fieldRadius").floatValue = 360f;
        serializedCloudField.FindProperty("horizonRadius").floatValue = 340f;
        serializedCloudField.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cloudField);
    }

    private static void ConfigureLighting(Scene scene)
    {
        GameObject lightObject = FindInScene(scene, "SYS_Level04Sun");
        if (lightObject == null)
        {
            lightObject = new GameObject("SYS_Level04Sun");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            GameObject systemsRoot = FindRoot(scene, "SYSTEMS");
            lightObject.transform.SetParent(systemsRoot.transform, false);
        }

        Light sun = lightObject.GetComponent<Light>();
        if (sun == null)
        {
            sun = lightObject.AddComponent<Light>();
        }
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.64f, 0.38f);
        sun.intensity = 1.08f;
        sun.shadows = LightShadows.None;
        lightObject.transform.rotation = Quaternion.LookRotation(
            new Vector3(0.64f, -0.045f, -0.77f).normalized,
            Vector3.up);
        RenderSettings.sun = sun;
    }

    private static void ConfigureRenderSettings(Material skyboxMaterial)
    {
        RenderSettings.skybox = skyboxMaterial;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.34f, 0.37f, 0.45f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.4f, 0.38f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.18f, 0.23f);
        RenderSettings.ambientIntensity = 1.08f;
        RenderSettings.reflectionIntensity = 0.82f;
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.36f, 0.36f, 0.41f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 680f;
        RenderSettings.fogEndDistance = 2600f;
        DynamicGI.UpdateEnvironment();
    }

    private static void ConfigureCameras(Scene scene)
    {
        foreach (Camera camera in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<Camera>(true)))
        {
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.12f, 0.16f, 0.23f);
            camera.allowHDR = true;
            camera.farClipPlane = 6000f;
        }
    }

    private static void CapturePreviewToFile()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera camera = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault();
        GameObject player = FindInScene(scene, "PLAYER_Plane");
        if (camera == null || player == null)
        {
            throw new MissingReferenceException("Level04 preview camera or player was not found.");
        }

        PlaneCloudField cloudField = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlaneCloudField>(true))
            .FirstOrDefault();
        cloudField?.BuildPreviewClouds();

        Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }
        camera.transform.position = player.transform.position + Vector3.up * 14f - forward * 32f;
        Vector3 lookTarget = player.transform.position + Vector3.up * 2f - forward * 4f;
        camera.transform.rotation = Quaternion.LookRotation(lookTarget - camera.transform.position, Vector3.up);
        camera.orthographic = false;
        camera.fieldOfView = 70f;
        camera.farClipPlane = 6000f;

        RenderTexture target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousActive = RenderTexture.active;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
        image.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath));
        File.WriteAllBytes(PreviewPath, image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = previousActive;
        cloudField?.ClearPreviewClouds();
        UnityEngine.Object.DestroyImmediate(image);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        AssetDatabase.Refresh();
    }

    private static GameObject FindRoot(Scene scene, string objectName)
    {
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == objectName);
        if (root != null)
        {
            return root;
        }
        root = new GameObject(objectName);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root;
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == objectName);
            if (match != null)
            {
                return match.gameObject;
            }
        }
        return null;
    }

    private static void SetColor(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static void SetFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }
}
