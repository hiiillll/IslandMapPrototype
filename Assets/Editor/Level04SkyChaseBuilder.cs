using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class Level04SkyChaseBuilder
{
    private const string ScenePath = "Assets/Scenes/Level04.unity";
    private const string LevelFolder = "Assets/Level04";
    private const string MaterialFolder = LevelFolder + "/Materials";
    private const string PrefabFolder = LevelFolder + "/Prefabs";
    private const string PreviewFolder = "Previews";
    private const string PreviewPath = PreviewFolder + "/Level04_ScenePreview.png";
    private const string ThirdPersonPreviewPath = PreviewFolder + "/Level04_ThirdPersonPreview.png";
    private const string CloudTexturePath = LevelFolder + "/CloudSea_V2_Generated.png";
    private const string CloudMaskTexturePath = LevelFolder + "/CloudWispMask_Generated.png";
    private const string CloudHorizonMaskTexturePath = LevelFolder + "/CloudHorizonMask_Generated.png";
    private const string FallbackCloudTexturePath = LevelFolder + "/CloudSea_Fallback.png";
    private const string EnemyPrefabPath = PrefabFolder + "/PF_Level04_EnemyPlane.prefab";
    private const string PlayerModelPath = LevelFolder + "/Models/Player/7e82465a5265349baef858b3f34b69a2.obj";
    private const string PlayerTextureFolder = LevelFolder + "/Models/Player";
    private const string EnemyModelPath = LevelFolder + "/Models/Enemy/0ea76cffe7078d24a61b2cf4e5ed71bd.obj";
    private const string EnemyTextureFolder = LevelFolder + "/Models/Enemy";
    private const string SkyboxPath = "Assets/Art/Sky/MAT_Sky_TropicalNoon.mat";
    private const float WorldSize = 8000f;

    [MenuItem("Tools/Island Map/Level04/Build Sky Chase Preview")]
    public static void BuildFromMenu()
    {
        Build();
        CapturePreview();
    }

    public static void BuildFromCommandLine()
    {
        try
        {
            Build();
            CapturePreview();
            Debug.Log("[Level04] Build and preview capture completed.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Build()
    {
        EnsureFolders();
        DeleteObsoleteAssets();
        ConfigureImportedAircraftAssets();
        Material cloudSeaMaterial = CreateCloudSeaMaterial();
        Material cloudDeckMaterial = CreateCloudSheetMaterial(
            CloudMaskTexturePath,
            MaterialFolder + "/MAT_Level04_CloudSheets.mat",
            new Color(0.69f, 0.78f, 0.88f, 0.38f),
            0.23f,
            0.21f,
            1.95f,
            0.38f,
            new Vector2(0.0015f, 0.00045f),
            new Vector2(-0.0007f, 0.001f),
            0.72f,
            0.08f,
            0.15f,
            0.13f);
        Material cloudHorizonMaterial = CreateCloudSheetMaterial(
            CloudHorizonMaskTexturePath,
            MaterialFolder + "/MAT_Level04_CloudHorizon.mat",
            new Color(0.58f, 0.68f, 0.79f, 0.46f),
            0.17f,
            0.22f,
            1.62f,
            0.22f,
            new Vector2(0.00045f, 0.00008f),
            new Vector2(-0.00018f, 0.0002f),
            0.92f,
            0.16f,
            0.1f,
            0f);
        Material playerMaterial = CreateAircraftMaterial(
            MaterialFolder + "/MAT_Level04_PlayerAircraft.mat",
            PlayerTextureFolder,
            0.12f,
            0.38f);
        Material enemyMaterial = CreateAircraftMaterial(
            MaterialFolder + "/MAT_Level04_EnemyAircraft.mat",
            EnemyTextureFolder,
            0.16f,
            0.42f);
        GameObject playerModel = LoadRequiredModel(PlayerModelPath, "player");
        GameObject enemyModel = LoadRequiredModel(EnemyModelPath, "enemy");
        GameObject enemyPrefab = CreateEnemyPrefab(enemyModel, enemyMaterial);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject environmentRoot = CreateRoot("ENVIRONMENT", scene);
        GameObject gameplayRoot = CreateRoot("GAMEPLAY", scene);
        GameObject systemsRoot = CreateRoot("SYSTEMS", scene);

        CreateSkyFloor(environmentRoot.transform, cloudSeaMaterial);
        GameObject player = CreatePlayerPlane(
            scene,
            gameplayRoot.transform,
            playerModel,
            playerMaterial,
            out Transform playerBankPivot);
        Camera chaseCamera = CreateCamera(scene, systemsRoot.transform, player.transform);
        CreateLighting(scene, systemsRoot.transform);

        GameObject cloudFieldObject = new GameObject("ENV_DynamicCloudField");
        SceneManager.MoveGameObjectToScene(cloudFieldObject, scene);
        cloudFieldObject.transform.SetParent(environmentRoot.transform, false);
        PlaneCloudField cloudField = cloudFieldObject.AddComponent<PlaneCloudField>();
        cloudField.Configure(player.transform, cloudDeckMaterial, cloudHorizonMaterial);

        GameObject stateObject = new GameObject("SYS_Level04GameState");
        SceneManager.MoveGameObjectToScene(stateObject, scene);
        stateObject.transform.SetParent(systemsRoot.transform, false);
        SurvivalGameController survivalController = stateObject.AddComponent<SurvivalGameController>();
        survivalController.Configure(120f, false, false);
        Level04GameController gameController = stateObject.AddComponent<Level04GameController>();

        PlaneChaseController playerController = player.AddComponent<PlaneChaseController>();
        playerController.Configure(gameController, playerBankPivot);
        PlaneAirflowEffect airflowEffect = player.AddComponent<PlaneAirflowEffect>();
        airflowEffect.Configure(playerBankPivot);
        SimplePlayerHealth health = player.AddComponent<SimplePlayerHealth>();
        health.ResetToFullHealth(3);
        PlayerProgression progression = player.AddComponent<PlayerProgression>();
        progression.ResetForNewLevel();
        ArcadeGameHud hud = player.AddComponent<ArcadeGameHud>();
        hud.ConfigureBasicHudOnly();

        GameObject spawnerObject = new GameObject("SYS_PlaneEnemySpawner");
        SceneManager.MoveGameObjectToScene(spawnerObject, scene);
        spawnerObject.transform.SetParent(systemsRoot.transform, false);
        PlaneEnemySpawner spawner = spawnerObject.AddComponent<PlaneEnemySpawner>();
        spawner.Configure(player.transform, chaseCamera, enemyPrefab, gameController);
        gameController.Configure(health, spawner, survivalController);

        ConfigureRenderSettings();
        Level04CoverSkyInstaller.ApplyToLoadedScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            throw new IOException("Unity could not save " + ScenePath);
        }

        ConfigureBuildScenes();
        Selection.activeGameObject = player;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Level04] Built 120-second A/D steering and W/S altitude sky chase scene.");
    }

    private static GameObject CreatePlayerPlane(
        Scene scene,
        Transform parent,
        GameObject modelPrefab,
        Material aircraftMaterial,
        out Transform bankPivot)
    {
        GameObject player = new GameObject("PLAYER_Plane");
        player.tag = "Player";
        SceneManager.MoveGameObjectToScene(player, scene);
        player.transform.SetParent(parent, false);
        player.transform.position = new Vector3(0f, 24f, -42f);
        player.transform.rotation = Quaternion.identity;

        GameObject pivotObject = new GameObject("BankPivot");
        pivotObject.transform.SetParent(player.transform, false);
        bankPivot = pivotObject.transform;

        GameObject visual = CreateImportedPlaneVisual(
            bankPivot,
            "VIS_PlayerPlane",
            modelPrefab,
            aircraftMaterial,
            9f);

        Bounds bounds = CalculateLocalBounds(player);
        BoxCollider collider = player.AddComponent<BoxCollider>();
        collider.center = bounds.center;
        collider.size = Vector3.Scale(bounds.size, new Vector3(0.72f, 0.8f, 0.72f));
        Rigidbody body = player.AddComponent<Rigidbody>();
        body.mass = 900f;
        return player;
    }

    private static GameObject CreateEnemyPrefab(GameObject modelPrefab, Material aircraftMaterial)
    {
        GameObject root = new GameObject("PF_Level04_EnemyPlane");
        GameObject pivot = new GameObject("BankPivot");
        pivot.transform.SetParent(root.transform, false);
        CreateImportedPlaneVisual(
            pivot.transform,
            "VIS_EnemyPlane",
            modelPrefab,
            aircraftMaterial,
            7.65f);

        Bounds bounds = CalculateLocalBounds(root);
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = bounds.center;
        collider.size = Vector3.Scale(bounds.size, new Vector3(0.72f, 0.8f, 0.72f));
        collider.isTrigger = true;
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 760f;
        root.AddComponent<PlaneEnemyChaser>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateImportedPlaneVisual(
        Transform parent,
        string name,
        GameObject modelPrefab,
        Material material,
        float targetHorizontalSize)
    {
        GameObject visual = PrefabUtility.InstantiatePrefab(modelPrefab, parent) as GameObject;
        if (visual == null)
        {
            throw new MissingReferenceException("Could not instantiate imported aircraft model.");
        }

        visual.name = name;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        OverrideMaterials(visual, material);
        FitVisual(parent.gameObject, visual, targetHorizontalSize);
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        return visual;
    }

    private static GameObject CreateLowPolyPlane(
        Transform parent,
        string name,
        Material bodyMaterial,
        Material accentMaterial,
        Material canopyMaterial,
        float scale)
    {
        GameObject visual = new GameObject(name);
        visual.transform.SetParent(parent, false);
        visual.transform.localScale = Vector3.one * scale;

        CreatePrimitive(
            PrimitiveType.Capsule,
            "Fuselage",
            visual.transform,
            new Vector3(0f, 0f, 0.2f),
            new Vector3(1.05f, 2.9f, 1.05f),
            bodyMaterial,
            Quaternion.Euler(90f, 0f, 0f));
        CreatePrimitive(
            PrimitiveType.Sphere,
            "Nose",
            visual.transform,
            new Vector3(0f, 0f, 3.15f),
            new Vector3(1.05f, 0.9f, 1.5f),
            accentMaterial);
        CreatePrimitive(
            PrimitiveType.Cube,
            "MainWing",
            visual.transform,
            new Vector3(0f, -0.05f, 0.15f),
            new Vector3(8.8f, 0.24f, 1.35f),
            bodyMaterial);
        CreatePrimitive(
            PrimitiveType.Cube,
            "MainWingStripe",
            visual.transform,
            new Vector3(0f, 0.09f, 0.25f),
            new Vector3(7.8f, 0.06f, 0.24f),
            accentMaterial);
        CreatePrimitive(
            PrimitiveType.Cube,
            "TailWing",
            visual.transform,
            new Vector3(0f, 0.02f, -3.05f),
            new Vector3(3.65f, 0.2f, 0.82f),
            bodyMaterial);
        CreatePrimitive(
            PrimitiveType.Cube,
            "VerticalTail",
            visual.transform,
            new Vector3(0f, 0.65f, -3.15f),
            new Vector3(0.22f, 1.38f, 1.05f),
            accentMaterial,
            Quaternion.Euler(18f, 0f, 0f));
        CreatePrimitive(
            PrimitiveType.Sphere,
            "Canopy",
            visual.transform,
            new Vector3(0f, 0.67f, 1.25f),
            new Vector3(0.72f, 0.42f, 1.28f),
            canopyMaterial);

        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 2.45f;
            CreatePrimitive(
                PrimitiveType.Capsule,
                side < 0 ? "Engine_Left" : "Engine_Right",
                visual.transform,
                new Vector3(x, 0.02f, 0.45f),
                new Vector3(0.56f, 1.05f, 0.56f),
                accentMaterial,
                Quaternion.Euler(90f, 0f, 0f));
            CreatePrimitive(
                PrimitiveType.Cube,
                side < 0 ? "Propeller_Left" : "Propeller_Right",
                visual.transform,
                new Vector3(x, 0.12f, 1.65f),
                new Vector3(1.85f, 0.08f, 0.14f),
                canopyMaterial,
                Quaternion.Euler(0f, 12f * side, 0f));
        }

        CreatePrimitive(
            PrimitiveType.Sphere,
            "NavigationLight_Left",
            visual.transform,
            new Vector3(-4.45f, 0.08f, 0.15f),
            Vector3.one * 0.22f,
            accentMaterial);
        CreatePrimitive(
            PrimitiveType.Sphere,
            "NavigationLight_Right",
            visual.transform,
            new Vector3(4.45f, 0.08f, 0.15f),
            Vector3.one * 0.22f,
            accentMaterial);
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        return visual;
    }

    private static Camera CreateCamera(Scene scene, Transform parent, Transform player)
    {
        GameObject cameraObject = new GameObject("SYS_Level04Camera");
        cameraObject.tag = "MainCamera";
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.position = player.position + Vector3.up * 55f;
        cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Camera cameraComponent = cameraObject.AddComponent<Camera>();
        cameraComponent.clearFlags = CameraClearFlags.Skybox;
        cameraComponent.backgroundColor = new Color(0.3f, 0.66f, 0.88f);
        cameraComponent.allowHDR = true;
        PlaneChaseTopDownCamera follow = cameraObject.AddComponent<PlaneChaseTopDownCamera>();
        follow.Configure(player, 62f, 36f);
        return cameraComponent;
    }

    private static void CreateLighting(Scene scene, Transform parent)
    {
        GameObject lightObject = new GameObject("SYS_Level04Sun");
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.rotation = Quaternion.Euler(46f, -28f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.78f, 0.86f, 0.96f);
        light.intensity = 0.82f;
        light.shadows = LightShadows.None;
    }

    private static void CreateSkyFloor(Transform parent, Material material)
    {
        GameObject floor = CreatePrimitive(
            PrimitiveType.Cube,
            "ENV_CloudSea",
            parent,
            new Vector3(0f, -28f, 0f),
            new Vector3(WorldSize, 1f, WorldSize),
            material);
        floor.isStatic = true;
    }

    private static void CreateDistantIslands(Transform parent, Material material)
    {
        Vector3[] positions =
        {
            new Vector3(-34f, -16f, -71f),
            new Vector3(-120f, -16f, 180f),
            new Vector3(210f, -16f, -260f),
            new Vector3(-340f, -16f, -120f)
        };
        Vector3[] scales =
        {
            new Vector3(22f, 2.5f, 15f),
            new Vector3(54f, 4f, 32f),
            new Vector3(72f, 5f, 44f),
            new Vector3(46f, 3f, 58f)
        };
        for (int index = 0; index < positions.Length; index++)
        {
            CreatePrimitive(
                PrimitiveType.Sphere,
                $"ENV_DistantIsland_{index + 1:00}",
                parent,
                positions[index],
                scales[index],
                material);
        }
    }

    private static GameObject CreatePrimitive(
        PrimitiveType primitiveType,
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material,
        Quaternion? localRotation = null)
    {
        GameObject gameObject = GameObject.CreatePrimitive(primitiveType);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localRotation = localRotation ?? Quaternion.identity;
        gameObject.transform.localScale = localScale;
        Renderer renderer = gameObject.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Collider collider = gameObject.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }
        return gameObject;
    }

    private static Material CreateCloudSeaMaterial()
    {
        if (!File.Exists(CloudTexturePath))
        {
            GenerateCloudTexture();
        }
        else
        {
            AssetDatabase.ImportAsset(CloudTexturePath, ImportAssetOptions.ForceUpdate);
            ConfigureCloudTextureImporter(CloudTexturePath);
        }
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CloudTexturePath);
        if (texture == null)
        {
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FallbackCloudTexturePath);
        }
        Material material = CreateMaterial(
            MaterialFolder + "/MAT_Level04_CloudSea.mat",
            Color.white,
            0f,
            0.06f);
        material.mainTexture = texture;
        material.mainTextureScale = new Vector2(3.5f, 3.5f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateCloudSheetMaterial(
        string texturePath,
        string materialPath,
        Color tint,
        float cutoff,
        float softness,
        float secondaryScale,
        float detailBlend,
        Vector2 primaryDrift,
        Vector2 secondaryDrift,
        float gradientStrength,
        float verticalShade,
        float edgeFadeX,
        float edgeFadeY)
    {
        if (!File.Exists(texturePath))
        {
            throw new FileNotFoundException("Generated cloud mask is missing.", texturePath);
        }

        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
        ConfigureCloudTextureImporter(texturePath);
        Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        Shader shader = Shader.Find("Custom/Level04CloudSheet");
        if (mask == null || shader == null)
        {
            throw new MissingReferenceException("Level04 cloud sheet texture or shader is missing.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.SetTexture("_MainTex", mask);
        material.SetColor("_Tint", tint);
        material.SetFloat("_Cutoff", cutoff);
        material.SetFloat("_Softness", softness);
        material.SetFloat("_SecondaryScale", secondaryScale);
        material.SetFloat("_DetailBlend", detailBlend);
        material.SetVector("_DriftA", new Vector4(primaryDrift.x, primaryDrift.y, 0f, 0f));
        material.SetVector("_DriftB", new Vector4(secondaryDrift.x, secondaryDrift.y, 0f, 0f));
        material.SetFloat("_GradientStrength", gradientStrength);
        material.SetFloat("_VerticalShade", verticalShade);
        material.SetFloat("_EdgeFadeX", edgeFadeX);
        material.SetFloat("_EdgeFadeY", edgeFadeY);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void GenerateCloudTexture()
    {
        const int size = 512;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, false);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                float v = (float)y / size;
                float broad = SampleTileablePerlin(u, v, 5.5f, 2.1f, 8.7f);
                float detail = SampleTileablePerlin(u, v, 17f, 11.3f, 3.2f);
                float cloud = Mathf.SmoothStep(0.28f, 0.78f, broad * 0.78f + detail * 0.22f);
                Color sky = new Color(0.25f, 0.66f, 0.88f);
                Color cloudColor = new Color(0.91f, 0.96f, 1f);
                pixels[y * size + x] = Color.Lerp(sky, cloudColor, cloud);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(FallbackCloudTexturePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(FallbackCloudTexturePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(FallbackCloudTexturePath) as TextureImporter;
        ConfigureCloudTextureImporter(importer);
    }

    private static void ConfigureCloudTextureImporter(string path)
    {
        ConfigureCloudTextureImporter(AssetImporter.GetAtPath(path) as TextureImporter);
    }

    private static void ConfigureCloudTextureImporter(TextureImporter importer)
    {
        if (importer == null)
        {
            return;
        }

        importer.wrapMode = TextureWrapMode.Mirror;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static float SampleTileablePerlin(
        float u,
        float v,
        float scale,
        float offsetX,
        float offsetY)
    {
        float bottomLeft = Mathf.PerlinNoise(u * scale + offsetX, v * scale + offsetY);
        float bottomRight = Mathf.PerlinNoise((u - 1f) * scale + offsetX, v * scale + offsetY);
        float topLeft = Mathf.PerlinNoise(u * scale + offsetX, (v - 1f) * scale + offsetY);
        float topRight = Mathf.PerlinNoise(
            (u - 1f) * scale + offsetX,
            (v - 1f) * scale + offsetY);
        float bottom = Mathf.Lerp(bottomLeft, bottomRight, u);
        float top = Mathf.Lerp(topLeft, topRight, u);
        return Mathf.Lerp(bottom, top, v);
    }

    private static Material CreateMaterial(
        string path,
        Color color,
        float metallic,
        float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateAircraftMaterial(
        string path,
        string textureFolder,
        float metallic,
        float smoothness)
    {
        Material material = CreateMaterial(path, Color.white, metallic, smoothness);
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
            textureFolder + "/texture_pbr_20250901.png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(
            textureFolder + "/texture_pbr_20250901_normal.png");
        if (albedo == null || normal == null)
        {
            throw new MissingReferenceException("Aircraft PBR textures are missing from " + textureFolder);
        }

        material.SetTexture("_MainTex", albedo);
        material.SetTexture("_BumpMap", normal);
        material.SetFloat("_BumpScale", 1f);
        material.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject LoadRequiredModel(string path, string role)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null)
        {
            throw new MissingReferenceException("Level04 " + role + " aircraft model is missing: " + path);
        }
        return model;
    }

    private static void ConfigureImportedAircraftAssets()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureAircraftModelImporter(PlayerModelPath);
        ConfigureAircraftModelImporter(EnemyModelPath);
        ConfigureAircraftTextureImporters(PlayerTextureFolder);
        ConfigureAircraftTextureImporters(EnemyTextureFolder);
    }

    private static void ConfigureAircraftModelImporter(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            throw new MissingReferenceException("Could not configure aircraft model importer: " + path);
        }

        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.meshCompression = ModelImporterMeshCompression.Medium;
        importer.optimizeMeshPolygons = true;
        importer.optimizeMeshVertices = true;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    private static void ConfigureAircraftTextureImporters(string textureFolder)
    {
        ConfigureAircraftTextureImporter(
            textureFolder + "/texture_pbr_20250901.png",
            TextureImporterType.Default,
            true);
        ConfigureAircraftTextureImporter(
            textureFolder + "/texture_pbr_20250901_metallic.png",
            TextureImporterType.Default,
            false);
        ConfigureAircraftTextureImporter(
            textureFolder + "/texture_pbr_20250901_roughness.png",
            TextureImporterType.Default,
            false);
        ConfigureAircraftTextureImporter(
            textureFolder + "/texture_pbr_20250901_normal.png",
            TextureImporterType.NormalMap,
            false);
    }

    private static void ConfigureAircraftTextureImporter(
        string path,
        TextureImporterType textureType,
        bool useSrgb)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            throw new MissingReferenceException("Could not configure aircraft texture importer: " + path);
        }

        importer.textureType = textureType;
        importer.sRGBTexture = useSrgb;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void OverrideMaterials(GameObject root, Material material)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                materials[index] = material;
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static void FitVisual(GameObject root, GameObject visual, float targetHorizontalSize)
    {
        Bounds worldBounds = CalculateWorldBounds(visual);
        float horizontalSize = Mathf.Max(worldBounds.size.x, worldBounds.size.z);
        visual.transform.localScale = Vector3.one * (targetHorizontalSize / Mathf.Max(0.01f, horizontalSize));
        Bounds localBounds = CalculateLocalBounds(root);
        visual.transform.localPosition -= new Vector3(localBounds.center.x, localBounds.center.y, localBounds.center.z);
    }

    private static Bounds CalculateWorldBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }
        return bounds;
    }

    private static Bounds CalculateLocalBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool initialized = false;
        Bounds bounds = new Bounds();
        foreach (Renderer renderer in renderers)
        {
            Bounds rendererBounds = renderer.bounds;
            Vector3[] corners =
            {
                new Vector3(rendererBounds.min.x, rendererBounds.min.y, rendererBounds.min.z),
                new Vector3(rendererBounds.min.x, rendererBounds.min.y, rendererBounds.max.z),
                new Vector3(rendererBounds.min.x, rendererBounds.max.y, rendererBounds.min.z),
                new Vector3(rendererBounds.min.x, rendererBounds.max.y, rendererBounds.max.z),
                new Vector3(rendererBounds.max.x, rendererBounds.min.y, rendererBounds.min.z),
                new Vector3(rendererBounds.max.x, rendererBounds.min.y, rendererBounds.max.z),
                new Vector3(rendererBounds.max.x, rendererBounds.max.y, rendererBounds.min.z),
                new Vector3(rendererBounds.max.x, rendererBounds.max.y, rendererBounds.max.z)
            };
            foreach (Vector3 corner in corners)
            {
                Vector3 localPoint = root.transform.InverseTransformPoint(corner);
                if (!initialized)
                {
                    bounds = new Bounds(localPoint, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(localPoint);
                }
            }
        }
        return bounds;
    }

    private static GameObject CreateRoot(string name, Scene scene)
    {
        GameObject root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root;
    }

    private static void ConfigureRenderSettings()
    {
        Material skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.2f, 0.34f, 0.48f);
        RenderSettings.ambientEquatorColor = new Color(0.3f, 0.42f, 0.5f);
        RenderSettings.ambientGroundColor = new Color(0.08f, 0.12f, 0.17f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.24f, 0.38f, 0.5f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 130f;
        RenderSettings.fogEndDistance = 360f;
        DynamicGI.UpdateEnvironment();
    }

    private static void ConfigureBuildScenes()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
            .Where(scene => scene.path != ScenePath)
            .ToList();
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void CapturePreview()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera camera = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault();
        if (camera == null)
        {
            throw new MissingReferenceException("Level04 preview camera was not found.");
        }

        GameObject player = GameObject.Find("PLAYER_Plane");
        if (player == null)
        {
            throw new MissingReferenceException("Level04 preview player was not found.");
        }

        PlaneCloudField cloudField = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlaneCloudField>(true))
            .FirstOrDefault();
        cloudField?.BuildPreviewClouds();

        Vector3 originalPosition = camera.transform.position;
        Quaternion originalRotation = camera.transform.rotation;
        bool originalOrthographic = camera.orthographic;
        float originalOrthographicSize = camera.orthographicSize;
        float originalFieldOfView = camera.fieldOfView;
        camera.transform.position = player.transform.position + Vector3.up * 62f;
        camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        camera.orthographic = true;
        camera.orthographicSize = 36f;
        GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        List<GameObject> previewEnemies = CreatePreviewEnemies(player.transform, enemyPrefab);
        RenderPreview(camera, PreviewPath);

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
        RenderPreview(camera, ThirdPersonPreviewPath);

        camera.transform.position = originalPosition;
        camera.transform.rotation = originalRotation;
        camera.orthographic = originalOrthographic;
        camera.orthographicSize = originalOrthographicSize;
        camera.fieldOfView = originalFieldOfView;
        cloudField?.ClearPreviewClouds();
        foreach (GameObject enemy in previewEnemies)
        {
            UnityEngine.Object.DestroyImmediate(enemy);
        }
        AssetDatabase.Refresh();
        Debug.Log("[Level04] Previews saved to " + PreviewPath + " and " + ThirdPersonPreviewPath);
    }

    private static void RenderPreview(Camera camera, string outputPath)
    {
        RenderTexture target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
        image.Apply();
        Directory.CreateDirectory(PreviewFolder);
        File.WriteAllBytes(outputPath, image.EncodeToPNG());
        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        UnityEngine.Object.DestroyImmediate(image);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
    }

    private static List<GameObject> CreatePreviewEnemies(Transform player, GameObject enemyPrefab)
    {
        List<GameObject> enemies = new List<GameObject>();
        if (enemyPrefab == null)
        {
            return enemies;
        }

        Vector3[] offsets =
        {
            new Vector3(-24f, 0f, -23f),
            new Vector3(23f, 0f, -29f),
            new Vector3(34f, 0f, 18f)
        };
        float[] yaws = { 18f, -16f, -74f };
        for (int index = 0; index < offsets.Length; index++)
        {
            GameObject enemy = PrefabUtility.InstantiatePrefab(enemyPrefab) as GameObject;
            enemy.name = $"PREVIEW_Enemy_{index + 1:00}";
            enemy.transform.position = player.position + offsets[index];
            enemy.transform.rotation = Quaternion.Euler(0f, yaws[index], 0f);
            PlaneEnemyChaser chaser = enemy.GetComponent<PlaneEnemyChaser>();
            if (chaser != null)
            {
                chaser.enabled = false;
            }
            enemies.Add(enemy);
        }
        return enemies;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Level04");
        EnsureFolder(LevelFolder, "Materials");
        EnsureFolder(LevelFolder, "Prefabs");
        Directory.CreateDirectory(PreviewFolder);
    }

    private static void DeleteObsoleteAssets()
    {
        string[] obsoletePaths =
        {
            LevelFolder + "/CloudSea.png",
            MaterialFolder + "/MAT_Level04_Cloud.mat",
            MaterialFolder + "/MAT_Level04_DistantCloud.mat",
            MaterialFolder + "/MAT_Level04_DistantIsland.mat",
            MaterialFolder + "/MAT_Level04_CloudVolumeNear.mat",
            MaterialFolder + "/MAT_Level04_CloudVolumeDistant.mat"
        };
        foreach (string path in obsoletePaths)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
