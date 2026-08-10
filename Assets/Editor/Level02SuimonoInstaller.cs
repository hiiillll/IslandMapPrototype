using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Suimono.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Level02SuimonoInstaller
{
    private const string ScenePath = "Assets/Scenes/Level02.unity";
    private const string ModulePrefabPath =
        "Assets/SUIMONO - WATER SYSTEM 2/PREFABS/SUIMONO_Module.prefab";
    private const string SurfacePrefabPath =
        "Assets/SUIMONO - WATER SYSTEM 2/PREFABS/SUIMONO_Surface.prefab";
    private const string Level04SkyboxPath =
        "Assets/Level04/Materials/MAT_Level04_CoverSunsetSky.mat";

    private const string ModuleName = "SUIMONO_Module";
    private const string SurfaceName = "ENV_SUIMONO_Ocean";
    private const string SmokeTestSessionKey = "Level02SuimonoInstaller.SmokeTestActive";
    private const string SmokeScreenshotSessionKey = "Level02SuimonoInstaller.ScreenshotTaken";
    private const string SmokeThirdPersonSessionKey = "Level02SuimonoInstaller.ThirdPerson";
    private const string SmokeThirdPersonAppliedSessionKey = "Level02SuimonoInstaller.ThirdPersonApplied";
    private static double smokeTestStartedAt;

    static Level02SuimonoInstaller()
    {
        if (SessionState.GetBool(SmokeTestSessionKey, false))
        {
            EditorApplication.update += UpdateSmokeTest;
        }
    }

    [MenuItem("Tools/Island Map/Apply Level 02 SUIMONO Water")]
    public static void ApplyFromMenu()
    {
        Apply();
    }

    public static void ApplyFromCommandLine()
    {
        try
        {
            Apply();
            Debug.Log("[Level02SuimonoInstaller] Command-line installation completed.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void RunPlayModeSmokeTest()
    {
        BeginPlayModeSmokeTest(false);
    }

    public static void RunThirdPersonSmokeTest()
    {
        BeginPlayModeSmokeTest(true);
    }

    private static void BeginPlayModeSmokeTest(bool thirdPerson)
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(SmokeTestSessionKey, true);
        SessionState.SetBool(SmokeScreenshotSessionKey, false);
        SessionState.SetBool(SmokeThirdPersonSessionKey, thirdPerson);
        SessionState.SetBool(SmokeThirdPersonAppliedSessionKey, false);
        smokeTestStartedAt = EditorApplication.timeSinceStartup;
        EditorApplication.update -= UpdateSmokeTest;
        EditorApplication.update += UpdateSmokeTest;
        EditorApplication.EnterPlaymode();
    }

    private static void Apply()
    {
        EnsureLayer("Suimono_Water");
        EnsureLayer("Suimono_Depth");
        EnsureLayer("Suimono_Screen");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform systems = RequireObject("SYSTEMS").transform;
        Transform environment = RequireObject("ENVIRONMENT").transform;
        Camera mainCamera = RequireObject("SYS_TopDownCamera").GetComponent<Camera>();
        Light mainLight = RequireObject("SYS_DirectionalLight").GetComponent<Light>();
        BoatChaseController player = UnityEngine.Object.FindObjectOfType<BoatChaseController>(true);
        GameObject legacyOcean = RequireObject("ENV_Ocean_4000x4000");

        if (mainCamera == null || mainLight == null || player == null)
        {
            throw new InvalidOperationException(
                "Level02 is missing its main camera, directional light, or player boat.");
        }

        DeleteExistingInstallation();

        GameObject moduleObject = InstantiatePrefab(ModulePrefabPath, systems);
        moduleObject.name = ModuleName;
        ResetLocalTransform(moduleObject.transform);

        SuimonoModule module = moduleObject.GetComponent<SuimonoModule>();
        if (module == null)
        {
            throw new InvalidOperationException("The SUIMONO module prefab has no SuimonoModule component.");
        }

        ConfigureModule(module, mainCamera, player.transform, mainLight);
        ConfigureLevel04Lighting(mainLight);

        GameObject surfaceObject = InstantiatePrefab(SurfacePrefabPath, environment);
        surfaceObject.name = SurfaceName;
        surfaceObject.transform.position = new Vector3(0f, 0.02f, 0f);
        surfaceObject.transform.rotation = Quaternion.identity;
        surfaceObject.transform.localScale = Vector3.one;

        SuimonoObject surface = surfaceObject.GetComponent<SuimonoObject>();
        if (surface == null)
        {
            throw new InvalidOperationException("The SUIMONO surface prefab has no SuimonoObject component.");
        }

        ConfigureSurface(surface, module);
        SetLayerRecursively(surfaceObject, LayerMask.NameToLayer("Suimono_Water"));

        Renderer legacyRenderer = legacyOcean.GetComponent<Renderer>();
        if (legacyRenderer == null || legacyOcean.GetComponent<Collider>() == null)
        {
            throw new InvalidOperationException("The legacy ocean must keep both its renderer and collider components.");
        }

        legacyRenderer.enabled = false;
        EditorUtility.SetDirty(legacyOcean);
        EditorUtility.SetDirty(module);
        EditorUtility.SetDirty(surface);

        int moduleCount = Resources.FindObjectsOfTypeAll<SuimonoModule>()
            .Count(item => item.gameObject.scene == scene);
        int surfaceCount = Resources.FindObjectsOfTypeAll<SuimonoObject>()
            .Count(item => item.gameObject.scene == scene);
        if (moduleCount != 1 || surfaceCount != 1)
        {
            throw new InvalidOperationException(
                $"Expected one SUIMONO module and surface, found {moduleCount} and {surfaceCount}.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[Level02SuimonoInstaller] Installed the native SUIMONO ocean. " +
            "Legacy ocean renderer disabled; collider and boat handling preserved.");
    }

    private static void UpdateSmokeTest()
    {
        if (!SessionState.GetBool(SmokeTestSessionKey, false))
        {
            EditorApplication.update -= UpdateSmokeTest;
            return;
        }

        if (smokeTestStartedAt <= 0d)
        {
            smokeTestStartedAt = EditorApplication.timeSinceStartup;
        }

        double elapsed = EditorApplication.timeSinceStartup - smokeTestStartedAt;
        if (EditorApplication.isPlaying
            && elapsed >= 1d
            && SessionState.GetBool(SmokeThirdPersonSessionKey, false)
            && !SessionState.GetBool(SmokeThirdPersonAppliedSessionKey, false))
        {
            ApplyThirdPersonPreview();
            SessionState.SetBool(SmokeThirdPersonAppliedSessionKey, true);
        }

        if (EditorApplication.isPlaying &&
            elapsed >= 3d &&
            !SessionState.GetBool(SmokeScreenshotSessionKey, false))
        {
            string screenshotPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../Logs/Level02OceanPreview.png"));
            CaptureCameraPreview(screenshotPath);
            SessionState.SetBool(SmokeScreenshotSessionKey, true);
        }

        if (elapsed < 8d)
        {
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
            smokeTestStartedAt = EditorApplication.timeSinceStartup;
            return;
        }

        SessionState.EraseBool(SmokeTestSessionKey);
        SessionState.EraseBool(SmokeScreenshotSessionKey);
        SessionState.EraseBool(SmokeThirdPersonSessionKey);
        SessionState.EraseBool(SmokeThirdPersonAppliedSessionKey);
        EditorApplication.update -= UpdateSmokeTest;
        Debug.Log("[Level02SuimonoInstaller] Play Mode smoke test completed.");
        EditorApplication.Exit(0);
    }

    private static void ApplyThirdPersonPreview()
    {
        BoatChaseTopDownCamera controller = UnityEngine.Object.FindObjectOfType<BoatChaseTopDownCamera>();
        if (controller == null)
        {
            throw new InvalidOperationException("Cannot switch Level02 preview without its camera controller.");
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo viewModeField = typeof(BoatChaseTopDownCamera).GetField("viewMode", flags);
        MethodInfo applySettings = typeof(BoatChaseTopDownCamera).GetMethod("ApplyCameraSettings", flags);
        if (viewModeField == null || applySettings == null)
        {
            throw new MissingMemberException("Level02 camera preview hooks are unavailable.");
        }

        viewModeField.SetValue(controller, Enum.ToObject(viewModeField.FieldType, 1));
        applySettings.Invoke(controller, null);
    }

    private static void CaptureCameraPreview(string path)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            throw new InvalidOperationException("Cannot capture Level02 preview without the main camera.");
        }

        SuimonoObject ocean = UnityEngine.Object.FindObjectOfType<SuimonoObject>();
        if (ocean != null)
        {
            foreach (Renderer renderer in ocean.GetComponentsInChildren<Renderer>(true))
            {
                string shaderName = renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null
                    ? renderer.sharedMaterial.shader.name
                    : "<none>";
                Debug.Log(
                    $"[Level02SuimonoRenderer] {renderer.name}: enabled={renderer.enabled}, " +
                    $"active={renderer.gameObject.activeInHierarchy}, position={renderer.transform.position}, " +
                    $"scale={renderer.transform.lossyScale}, bounds={renderer.bounds.size}, shader={shaderName}");
            }
        }

        const int width = 1280;
        const int height = 720;
        RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;

        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();
            File.WriteAllBytes(path, screenshot.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(screenshot);
        }
    }

    private static void ConfigureModule(
        SuimonoModule module,
        Camera mainCamera,
        Transform player,
        Light mainLight)
    {
        module.autoSetLayers = false;
        module.layerWater = "Suimono_Water";
        module.layerDepth = "Suimono_Depth";
        module.layerScreenFX = "Suimono_Screen";
        module.layerWaterNum = LayerMask.NameToLayer(module.layerWater);
        module.layerDepthNum = LayerMask.NameToLayer(module.layerDepth);
        module.layerScreenFXNum = LayerMask.NameToLayer(module.layerScreenFX);
        module.layersAreSet = true;

        module.cameraTypeIndex = 1;
        module.manualCamera = mainCamera.transform;
        module.mainCamera = mainCamera.transform;
        module.setCamera = mainCamera.transform;
        module.setCameraComponent = mainCamera;
        module.setTrack = player;
        module.setLight = mainLight;
        module.autoSetCameraFX = false;

        module.enableUnderwaterFX = false;
        module.enableInteraction = false;
        module.enableCaustics = false;
        module.enableCausticsBlending = false;
        module.enableTransparency = false;
        module.enableTransition = false;
        module.enableAdvancedEdge = true;
        module.enableAdvancedDistort = true;
        module.enableRefraction = true;
        module.enableReflections = true;
        module.enableDynamicReflections = false;
        module.transResolution = 4;
        module.transRenderDistance = 80f;
        module.playSounds = false;
        module.disableMSAA = false;
    }

    private static void ConfigureSurface(SuimonoObject surface, SuimonoModule module)
    {
        surface.moduleObject = module;
        surface.typeIndex = 0;
        surface.editorIndex = 1;
        surface.editorUseIndex = 1;
        surface.presetIndex = -1;
        surface.presetUseIndex = -1;

        surface.customWaves = true;
        surface.flowDirection = 205f;
        surface.flowSpeed = 0.035f;
        surface.waveScale = 0.9f;
        surface.waveHeight = 0.12f;
        surface.lgWaveHeight = 0.055f;
        surface.lgWaveScale = 0.075f;
        surface.turbulenceFactor = 0.08f;

        surface.refractStrength = 0.08f;
        surface.aberrationScale = 0.015f;
        surface.reflectProjection = 0.35f;
        surface.reflectBlur = 0.2f;
        surface.reflectResolution = 4;
        surface.reflectionDistance = 420f;
        surface.reflectFallback = 3;
        surface.customRefColor = new Color(0.3f, 0.25f, 0.2f, 1f);
        surface.specularPower = 0.42f;
        surface.roughness = 0.62f;
        surface.roughness2 = 0.76f;
        surface.reflectTerm = 0.025f;

        surface.enableReflections = true;
        surface.enableDynamicReflections = false;
        surface.enableTess = false;
        surface.useEnableTess = false;
        surface.enableInteraction = false;
        surface.enableCausticFX = false;
        surface.enableUnderwater = false;
        surface.enableUnderDebris = false;
        surface.enableFoam = false;

        surface.overallBright = 1.05f;
        surface.overallTransparency = 0.96f;
        surface.depthAmt = 0.78f;
        surface.shallowAmt = 0.26f;
        surface.edgeAmt = 0.14f;
        surface.depthColor = new Color(0.13f, 0.17f, 0.22f, 1f);
        surface.shallowColor = new Color(0.23f, 0.27f, 0.3f, 0.72f);
        surface.reflectionColor = new Color(0.42f, 0.3f, 0.2f, 0.18f);
        surface.specularColor = new Color(0.72f, 0.52f, 0.3f, 0.18f);
        surface.sssColor = new Color(0.002f, 0.008f, 0.025f, 1f);
        surface.blendColor = new Color(0.14f, 0.17f, 0.2f, 1f);
        surface.overlayColor = new Color(0.1f, 0.12f, 0.14f, 0.38f);

        surface.foamColor = new Color(0.68f, 0.78f, 0.8f, 0.55f);
        surface.foamScale = 24f;
        surface.foamSpeed = 0.035f;
        surface.edgeFoamAmt = 0.1f;
        surface.shallowFoamAmt = 0f;
        surface.heightFoamAmt = 0.16f;
        surface.hFoamHeight = 0.72f;
        surface.hFoamSpread = 0.28f;

        int waterLayer = LayerMask.NameToLayer("Suimono_Water");
        int depthLayer = LayerMask.NameToLayer("Suimono_Depth");
        int screenLayer = LayerMask.NameToLayer("Suimono_Screen");
        int excludedLayers = (1 << waterLayer) | (1 << depthLayer) | (1 << screenLayer);
        surface.reflectLayer = ~excludedLayers;
        surface.reflectLayerMask = surface.reflectLayer;
    }

    private static void ConfigureLevel04Lighting(Light mainLight)
    {
        Material skybox = AssetDatabase.LoadAssetAtPath<Material>(Level04SkyboxPath);
        if (skybox == null)
        {
            throw new InvalidOperationException("Missing Level04 skybox: " + Level04SkyboxPath);
        }

        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.34f, 0.37f, 0.45f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.4f, 0.38f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.18f, 0.23f, 1f);
        RenderSettings.ambientIntensity = 1.08f;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
        RenderSettings.defaultReflectionResolution = 128;
        RenderSettings.reflectionIntensity = 0.82f;
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.36f, 0.36f, 0.41f, 1f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 680f;
        RenderSettings.fogEndDistance = 2600f;

        mainLight.color = new Color(1f, 0.64f, 0.38f, 1f);
        mainLight.intensity = 1.08f;
        mainLight.shadows = LightShadows.None;
        mainLight.transform.localRotation = new Quaternion(
            0.007630724f,
            0.94025195f,
            -0.021118615f,
            0.3397383f);
        RenderSettings.sun = mainLight;
    }

    private static GameObject InstantiatePrefab(string path, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            throw new InvalidOperationException("Missing prefab: " + path);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        instance.transform.SetParent(parent, false);
        return instance;
    }

    private static void DeleteExistingInstallation()
    {
        GameObject[] installedRoots = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
            .Where(component => component is SuimonoModule || component is SuimonoObject)
            .Select(component => component.gameObject)
            .Where(gameObject => gameObject.scene.IsValid())
            .Select(gameObject => PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject) ?? gameObject)
            .Distinct()
            .ToArray();

        foreach (GameObject gameObject in installedRoots)
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static GameObject RequireObject(string objectName)
    {
        GameObject result = GameObject.Find(objectName);
        if (result == null)
        {
            throw new InvalidOperationException("Level02 is missing required object: " + objectName);
        }

        return result;
    }

    private static void ResetLocalTransform(Transform target)
    {
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static void EnsureLayer(string layerName)
    {
        if (LayerMask.NameToLayer(layerName) >= 0)
        {
            return;
        }

        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int index = 8; index < 32; index++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (!string.IsNullOrEmpty(layer.stringValue))
            {
                continue;
            }

            layer.stringValue = layerName;
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return;
        }

        throw new InvalidOperationException("No free user layer is available for " + layerName + ".");
    }
}
