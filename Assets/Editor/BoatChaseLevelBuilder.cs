using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BoatChaseLevelBuilder
{
    private const string LevelOneScenePath = "Assets/Scenes/IslandMap.unity";
    private const string LevelTwoScenePath = "Assets/Scenes/Level02.unity";
    private const string OceanTexturePath = "Assets/Art/Textures/Ocean_BoatChase_Tile.png";
    private const string OceanMaterialPath = "Assets/Art/Materials/Ocean_BoatChase.mat";
    private const string BoatModelPath = "Assets/Models/Imported/Model_16/485ec29b8ae85aa1f00db80da3760cf6.obj";
    private const string EnemyBoatModelPath = "Assets/Models/Imported/Model_17/25318314cfea921a21d08ea9355017fe.obj";
    private const float OceanSize = 4000f;

    [MenuItem("Tools/Island Map/Setup Level 02 Boat And Camera")]
    public static void SetupBoatAndCamera()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += SetupBoatAndCamera;
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != LevelTwoScenePath)
        {
            Debug.LogError("Open Assets/Scenes/Level02.unity before setting up the speedboat and camera.");
            return;
        }

        GameObject boatModel = AssetDatabase.LoadAssetAtPath<GameObject>(BoatModelPath);
        GameObject enemyBoatModel = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyBoatModelPath);
        if (boatModel == null || enemyBoatModel == null)
        {
            Debug.LogError("Unable to load the Level02 player or enemy boat model.");
            return;
        }

        DestroySceneObject(scene, "PLAYER_Speedboat_Model16");
        DestroySceneObject(scene, "SYS_TopDownCamera");
        DestroySceneObject(scene, "SYS_DirectionalLight");
        DestroySceneObject(scene, "SYS_Level02GameState");
        DestroySceneObject(scene, "SYS_BoatEnemySpawner");

        Transform gameplayRoot = GetOrCreateRoot(scene, "GAMEPLAY");
        Transform systemsRoot = GetOrCreateRoot(scene, "SYSTEMS");
        GameObject playerBoat = CreatePlayerBoat(boatModel, gameplayRoot, scene);
        Camera chaseCamera = CreateCamera(playerBoat.transform, systemsRoot, scene);
        CreateLighting(systemsRoot, scene);
        BoatChaseDifficultyController difficulty = CreateLevelState(systemsRoot, scene);
        CreateEnemySpawner(playerBoat.transform, chaseCamera, enemyBoatModel, difficulty, systemsRoot, scene);

        ConfigureRenderSettings();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = playerBoat;
        Debug.Log("Added Model_16 speedboat and a fixed top-down camera to Level02.");
    }

    [MenuItem("Tools/Island Map/Build Level 02 Boat Chase")]
    public static void Build()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Build;
            return;
        }

        Scene loadedLevelTwo = SceneManager.GetSceneByPath(LevelTwoScenePath);
        if (loadedLevelTwo.IsValid() && loadedLevelTwo.isLoaded)
        {
            EditorSceneManager.CloseScene(loadedLevelTwo, true);
        }

        Material oceanMaterial = CreateOceanMaterial();
        GameObject boatModel = AssetDatabase.LoadAssetAtPath<GameObject>(BoatModelPath);
        GameObject enemyBoatModel = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyBoatModelPath);
        if (boatModel == null || enemyBoatModel == null)
        {
            Debug.LogError("Unable to load the Level02 player or enemy boat model.");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject environmentRoot = CreateRoot("ENVIRONMENT", scene);
        GameObject gameplayRoot = CreateRoot("GAMEPLAY", scene);
        GameObject systemsRoot = CreateRoot("SYSTEMS", scene);

        CreateOcean(oceanMaterial, environmentRoot.transform);
        GameObject playerBoat = CreatePlayerBoat(boatModel, gameplayRoot.transform, scene);
        Camera chaseCamera = CreateCamera(playerBoat.transform, systemsRoot.transform, scene);
        CreateLighting(systemsRoot.transform, scene);
        BoatChaseDifficultyController difficulty = CreateLevelState(systemsRoot.transform, scene);
        CreateEnemySpawner(
            playerBoat.transform,
            chaseCamera,
            enemyBoatModel,
            difficulty,
            systemsRoot.transform,
            scene);

        ConfigureRenderSettings();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, LevelTwoScenePath);
        EditorSceneManager.SetActiveScene(scene);
        ConfigureBuildScenes();
        Selection.activeGameObject = playerBoat;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Built Level02: 4000x4000 ocean, Model_16 player speedboat, top-down camera, wake, water spray, and reset HUD.");
    }

    [MenuItem("Tools/Island Map/Setup Level 02 Enemy Chase")]
    public static void SetupEnemyChase()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += SetupEnemyChase;
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(LevelTwoScenePath, OpenSceneMode.Single);
        GameObject playerBoat = GameObject.Find("PLAYER_Speedboat_Model16");
        Camera chaseCamera = Camera.main;
        Transform systemsRoot = GetOrCreateRoot(scene, "SYSTEMS");
        GameObject stateObject = GameObject.Find("SYS_Level02GameState");
        GameObject enemyBoatModel = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyBoatModelPath);

        if (playerBoat == null || chaseCamera == null || stateObject == null || enemyBoatModel == null)
        {
            Debug.LogError("Level02 is missing its player, camera, game state, or Model_17 enemy boat.");
            return;
        }

        BoatChaseDifficultyController difficulty = stateObject.GetComponent<BoatChaseDifficultyController>();
        if (difficulty == null)
        {
            difficulty = stateObject.AddComponent<BoatChaseDifficultyController>();
        }

        DestroySceneObject(scene, "SYS_BoatEnemySpawner");
        CreateEnemySpawner(
            playerBoat.transform,
            chaseCamera,
            enemyBoatModel,
            difficulty,
            systemsRoot,
            scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Configured Level02 off-screen Model_17 spawning, linear difficulty, and direct pursuit.");
    }

    private static GameObject CreateRoot(string name, Scene scene)
    {
        GameObject root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root;
    }

    private static Transform GetOrCreateRoot(Scene scene, string name)
    {
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == name);
        if (root == null)
        {
            root = CreateRoot(name, scene);
        }
        return root.transform;
    }

    private static void DestroySceneObject(Scene scene, string name)
    {
        Transform existingTransform = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(candidate => candidate.name == name);
        if (existingTransform != null)
        {
            Object.DestroyImmediate(existingTransform.gameObject);
        }
    }

    private static void CreateOcean(Material oceanMaterial, Transform parent)
    {
        GameObject ocean = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ocean.name = "ENV_Ocean_4000x4000";
        ocean.transform.SetParent(parent, false);
        ocean.transform.position = new Vector3(0f, -0.1f, 0f);
        ocean.transform.localScale = new Vector3(OceanSize, 0.2f, OceanSize);
        ocean.GetComponent<MeshRenderer>().sharedMaterial = oceanMaterial;
        ocean.isStatic = true;
    }

    private static GameObject CreatePlayerBoat(GameObject boatModel, Transform parent, Scene scene)
    {
        GameObject playerBoat = new GameObject("PLAYER_Speedboat_Model16");
        playerBoat.tag = "Player";
        SceneManager.MoveGameObjectToScene(playerBoat, scene);
        playerBoat.transform.SetParent(parent, false);

        GameObject visual = PrefabUtility.InstantiatePrefab(boatModel) as GameObject;
        visual.name = "Visual_Model16";
        visual.transform.SetParent(playerBoat.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        visual.transform.localScale = Vector3.one;
        FitVisual(playerBoat, visual, 5f);

        Bounds localBounds = CalculateLocalBounds(playerBoat);
        BoxCollider collider = playerBoat.AddComponent<BoxCollider>();
        collider.center = localBounds.center;
        collider.size = localBounds.size;

        Rigidbody body = playerBoat.AddComponent<Rigidbody>();
        body.mass = 800f;
        BoatChaseController controller = playerBoat.AddComponent<BoatChaseController>();
        controller.ConfigureSteering(300f, 420f, 7f, 1.5f, true);
        playerBoat.AddComponent<BoatWakeTrail>();
        SimplePlayerHealth health = playerBoat.AddComponent<SimplePlayerHealth>();
        health.ResetToFullHealth(3);
        PlayerProgression progression = playerBoat.AddComponent<PlayerProgression>();
        progression.ResetForNewLevel();
        ArcadeGameHud hud = playerBoat.AddComponent<ArcadeGameHud>();
        hud.ConfigureBasicHudOnly();
        playerBoat.transform.position = new Vector3(0f, 0.025f, -72f);
        playerBoat.transform.rotation = Quaternion.identity;
        return playerBoat;
    }

    private static BoatChaseDifficultyController CreateLevelState(Transform parent, Scene scene)
    {
        GameObject stateObject = new GameObject("SYS_Level02GameState");
        SceneManager.MoveGameObjectToScene(stateObject, scene);
        stateObject.transform.SetParent(parent, false);
        SurvivalGameController controller = stateObject.AddComponent<SurvivalGameController>();
        controller.Configure(120f, false, true);
        BoatChaseDifficultyController difficulty = stateObject.AddComponent<BoatChaseDifficultyController>();
        difficulty.Configure(30f, 0.12f, 0f, 0.5f, 2.5f, 0.04f, 0.55f, 50f);
        return difficulty;
    }

    private static Camera CreateCamera(Transform playerBoat, Transform parent, Scene scene)
    {
        GameObject cameraObject = new GameObject("SYS_TopDownCamera");
        cameraObject.tag = "MainCamera";
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.position = playerBoat.position + Vector3.up * 55f;
        cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        Camera cameraComponent = cameraObject.AddComponent<Camera>();
        cameraComponent.clearFlags = CameraClearFlags.SolidColor;
        cameraComponent.backgroundColor = new Color(0.08f, 0.45f, 0.62f);
        BoatChaseTopDownCamera cameraFollow = cameraObject.AddComponent<BoatChaseTopDownCamera>();
        cameraFollow.Configure(playerBoat, 62f, 36f);
        return cameraComponent;
    }

    private static void CreateEnemySpawner(
        Transform playerBoat,
        Camera chaseCamera,
        GameObject enemyBoatModel,
        BoatChaseDifficultyController difficulty,
        Transform parent,
        Scene scene)
    {
        GameObject spawnerObject = new GameObject("SYS_BoatEnemySpawner");
        SceneManager.MoveGameObjectToScene(spawnerObject, scene);
        spawnerObject.transform.SetParent(parent, false);
        BoatEnemySpawner spawner = spawnerObject.AddComponent<BoatEnemySpawner>();
        spawner.Configure(playerBoat, chaseCamera, enemyBoatModel, difficulty);
    }

    private static void CreateLighting(Transform parent, Scene scene)
    {
        GameObject lightObject = new GameObject("SYS_DirectionalLight");
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.85f);
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
    }

    private static Material CreateOceanMaterial()
    {
        Texture2D oceanTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(OceanTexturePath);
        if (oceanTexture == null)
        {
            throw new System.InvalidOperationException($"Ocean texture is missing at {OceanTexturePath}.");
        }

        TextureImporter importer = AssetImporter.GetAtPath(OceanTexturePath) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(OceanMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, OceanMaterialPath);
        }

        material.name = "Ocean_BoatChase";
        material.mainTexture = oceanTexture;
        material.mainTextureScale = new Vector2(8f, 8f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", 0.38f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureRenderSettings()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.5f, 0.72f, 0.85f);
        RenderSettings.ambientEquatorColor = new Color(0.34f, 0.53f, 0.58f);
        RenderSettings.ambientGroundColor = new Color(0.15f, 0.22f, 0.25f);
    }

    private static void ConfigureBuildScenes()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
            .Where(scene => scene.path != LevelOneScenePath && scene.path != LevelTwoScenePath)
            .ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(LevelOneScenePath, true));
        scenes.Insert(1, new EditorBuildSettingsScene(LevelTwoScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void FitVisual(GameObject root, GameObject visual, float targetHorizontalSize)
    {
        Bounds worldBounds = CalculateWorldBounds(visual);
        float horizontalSize = Mathf.Max(worldBounds.size.x, worldBounds.size.z);
        visual.transform.localScale = Vector3.one * (targetHorizontalSize / Mathf.Max(horizontalSize, 0.01f));

        Bounds fittedBounds = CalculateLocalBounds(root);
        visual.transform.localPosition -= new Vector3(fittedBounds.center.x, fittedBounds.min.y, fittedBounds.center.z);
    }

    private static Bounds CalculateWorldBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        for (int rendererIndex = 1; rendererIndex < renderers.Length; rendererIndex++)
        {
            bounds.Encapsulate(renderers[rendererIndex].bounds);
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
}
