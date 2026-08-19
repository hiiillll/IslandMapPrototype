using Suimono.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(10000)]
public sealed class Level02SuimonoRuntimeGuard : MonoBehaviour
{
    private const string SceneName = "Level02";
    private const float StableOceanScale = 128f;
    private SuimonoModule module;
    private SuimonoObject ocean;
    private Renderer nearRenderer;
    private Renderer farRenderer;
    private cameraTools[] helperTools;
    private Camera[] helperCameras;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneName)
        {
            return;
        }

        GameObject guardObject = new GameObject("SYS_Level02SuimonoRuntimeGuard");
        SceneManager.MoveGameObjectToScene(guardObject, scene);
        guardObject.AddComponent<Level02SuimonoRuntimeGuard>();
    }

    private void Awake()
    {
        module = FindObjectOfType<SuimonoModule>(true);
        ocean = FindObjectOfType<SuimonoObject>(true);

        if (module == null || ocean == null)
        {
            Debug.LogError("[Level02 SUIMONO] Native module or ocean surface is missing.");
            enabled = false;
            return;
        }

        module.gameObject.name = "SUIMONO_Module";
        module.gameObject.SetActive(true);
        ocean.gameObject.SetActive(true);
        BindSceneReferences();
        ConfigureLevel01Rendering();
        RemoveLegacyFillLights();
        ConfigureOcean();
        CacheLegacyHelperCameras();
        DisableLegacyHelperCameras();

        Transform nearSurface = ocean.transform.Find("Suimono_Object");
        Transform farSurface = ocean.transform.Find("Suimono_ObjectScale");
        nearRenderer = nearSurface != null ? nearSurface.GetComponent<Renderer>() : null;
        farRenderer = farSurface != null ? farSurface.GetComponent<Renderer>() : null;

        Renderer legacyOcean = GameObject.Find("ENV_Ocean_4000x4000")?.GetComponent<Renderer>();
        if (legacyOcean != null)
        {
            legacyOcean.enabled = false;
        }

        Debug.Log(
            $"[Level02 SUIMONO] Runtime guard installed. " +
            $"near={nearRenderer != null}, far={farRenderer != null}, camera={module.manualCamera != null}.");
    }

    private void LateUpdate()
    {
        if (ocean == null)
        {
            enabled = false;
            return;
        }

        // SUIMONO creates its runtime materials during Start. Finalize them once
        // after Start/Update, then stop this guard from doing per-frame work.
        ConfigureOcean();
        DisableLegacyHelperCameras();

        ConfigureStableSurface();

        // The compatibility shader animates in world space. SUIMONO's legacy
        // camera-snapping surface and editor layer scans are no longer needed.
        ocean.enabled = false;
        module.enabled = false;
        Debug.Log("[Level02 SUIMONO] Stable world-space ocean initialized.");
        enabled = false;
    }

    private void BindSceneReferences()
    {
        Camera mainCamera = Camera.main;
        BoatChaseController player = FindObjectOfType<BoatChaseController>(true);
        Light mainLight = GameObject.Find("SYS_DirectionalLight")?.GetComponent<Light>();

        module.cameraTypeIndex = 1;
        module.manualCamera = mainCamera != null ? mainCamera.transform : null;
        module.mainCamera = module.manualCamera;
        module.setCamera = module.manualCamera;
        module.setCameraComponent = mainCamera;
        module.setTrack = player != null ? player.transform : module.manualCamera;
        module.setLight = mainLight;
        ocean.moduleObject = module;

        int waterLayer = LayerMask.NameToLayer("Suimono_Water");
        if (mainCamera != null && waterLayer >= 0)
        {
            mainCamera.cullingMask |= 1 << waterLayer;
        }
    }

    private void CacheLegacyHelperCameras()
    {
        helperTools = FindObjectsOfType<cameraTools>(true);
        helperCameras = module.GetComponentsInChildren<Camera>(true);
    }

    private void DisableLegacyHelperCameras()
    {
        int disabledCount = 0;
        foreach (cameraTools helperTool in helperTools)
        {
            if (helperTool == null || helperTool.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            Camera helperCamera = helperTool.GetComponent<Camera>();
            helperTool.enabled = false;
            if (helperCamera != null && helperCamera != module.setCameraComponent)
            {
                helperCamera.targetTexture = null;
                helperCamera.enabled = false;
                disabledCount++;
            }
        }

        foreach (Camera helperCamera in helperCameras)
        {
            if (helperCamera == null || helperCamera == module.setCameraComponent)
            {
                continue;
            }

            helperCamera.targetTexture = null;
            helperCamera.enabled = false;
        }

        if (disabledCount > 0)
        {
            Debug.Log(
                $"[Level02 SUIMONO] Disabled {disabledCount} legacy helper cameras; " +
                "the main camera now owns the final frame.");
        }
    }

    private void ConfigureOcean()
    {
        module.enableDynamicReflections = false;
        module.enableTransparency = false;
        module.enableUnderwaterFX = false;
        module.enableInteraction = false;
        module.enableCaustics = false;

        ocean.typeIndex = 0;
        ocean.customWaves = true;
        ocean.flowDirection = 205f;
        ocean.flowSpeed = 0.035f;
        ocean.waveScale = 0.9f;
        ocean.waveHeight = 0.1f;
        ocean.lgWaveHeight = 0.045f;
        ocean.lgWaveScale = 0.075f;
        ocean.turbulenceFactor = 0.12f;
        ocean.enableReflections = true;
        ocean.enableDynamicReflections = false;
        ocean.enableTess = false;
        ocean.enableInteraction = false;
        ocean.enableCausticFX = false;
        ocean.enableUnderwater = false;
        ocean.enableFoam = false;
        ocean.reflectFallback = 3;
        ocean.customRefColor = new Color(0.6f, 0.64f, 0.68f, 1f);
        ocean.reflectProjection = 0.35f;
        ocean.specularPower = 0.3f;
        ocean.roughness = 0.46f;
        ocean.roughness2 = 0.68f;
        ocean.reflectTerm = 0.025f;
        ocean.overallBright = 1.03f;
        ocean.depthColor = new Color(0.13f, 0.31f, 0.41f, 1f);
        ocean.shallowColor = new Color(0.3f, 0.49f, 0.55f, 0.72f);
        ocean.reflectionColor = new Color(0.6f, 0.64f, 0.68f, 0.24f);
        ocean.specularColor = new Color(0.95f, 0.82f, 0.67f, 0.2f);
        ocean.sssColor = new Color(0.1f, 0.23f, 0.28f, 1f);
        ocean.blendColor = new Color(0.18f, 0.31f, 0.38f, 1f);
        ocean.overlayColor = new Color(0.08f, 0.13f, 0.16f, 0.22f);
    }

    private void ConfigureStableSurface()
    {
        if (nearRenderer == null)
        {
            Debug.LogError("[Level02 SUIMONO] The primary ocean renderer is missing.");
            return;
        }

        Transform surface = nearRenderer.transform;
        surface.position = new Vector3(0f, 0.02f, 0f);
        surface.localScale = new Vector3(StableOceanScale, 1f, StableOceanScale);
        nearRenderer.enabled = true;
        ApplyOceanMaterial(nearRenderer.sharedMaterial);

        // One 5120-unit surface covers the complete 4000-unit playable area and
        // camera offset. Removing the second mesh also removes the visible LOD seam.
        if (farRenderer != null)
        {
            farRenderer.enabled = false;
        }

        foreach (Collider surfaceCollider in ocean.GetComponentsInChildren<Collider>(true))
        {
            surfaceCollider.enabled = false;
        }
    }

    private static void ConfigureLevel01Rendering()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.44f, 0.54f, 0.7f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.64f, 0.55f, 0.47f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.42f, 0.44f, 0.47f, 1f);
        RenderSettings.ambientIntensity = 1.24f;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
        RenderSettings.defaultReflectionResolution = 1024;
        RenderSettings.reflectionBounces = 2;
        RenderSettings.reflectionIntensity = 0.94f;
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.48f, 0.46f, 0.44f, 1f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 220f;
        RenderSettings.fogEndDistance = 1100f;

        Light sun = GameObject.Find("SYS_DirectionalLight")?.GetComponent<Light>();
        if (sun == null)
        {
            return;
        }

        sun.color = new Color(1f, 0.88f, 0.72f, 1f);
        sun.intensity = 1.08f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.34f;
        sun.shadowBias = 0.045f;
        sun.shadowNormalBias = 0.28f;
        sun.shadowCustomResolution = 4096;
        sun.useViewFrustumForShadowCasterCull = false;
        sun.useColorTemperature = false;
        sun.transform.localRotation = new Quaternion(
            0.08445507f,
            0.9109815f,
            -0.23373589f,
            0.32916212f);
        RenderSettings.sun = sun;
        DynamicGI.UpdateEnvironment();
    }

    private static void RemoveLegacyFillLights()
    {
        BoatChaseController player = FindObjectOfType<BoatChaseController>(true);
        Transform playerFill = player != null ? player.transform.Find("FX_PlayerBoatFillLight") : null;
        if (playerFill != null)
        {
            playerFill.gameObject.SetActive(false);
            Destroy(playerFill.gameObject);
        }

        GameObject enemyFill = GameObject.Find("SYS_Level02EnemyFillLight");
        if (enemyFill != null)
        {
            enemyFill.SetActive(false);
            Destroy(enemyFill);
        }
    }

    private static void ApplyOceanMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        material.SetFloat("_overallBrightness", 1.03f);
        material.SetFloat("_specularPower", 0.3f);
        material.SetFloat("_roughness", 0.46f);
        material.SetFloat("_roughness2", 0.68f);
        material.SetFloat("_reflecTerm", 0.025f);
        material.SetFloat("_NormalStrength", 0.58f);
        material.SetFloat("_heightScale", 0.1f);
        material.SetFloat("_lgWaveHeight", 0.045f);
        if (material.HasProperty("_CompatWaveAmplitude"))
        {
            material.SetFloat("_CompatWaveAmplitude", 0.22f);
        }
        material.SetFloat("_lgWaveScale", 0.075f);
        material.SetFloat("_turbulenceFactor", 0.12f);
        material.SetFloat("_enableFoam", 0f);
        material.SetColor("_depthColor", new Color(0.13f, 0.31f, 0.41f, 1f));
        material.SetColor("_shallowColor", new Color(0.3f, 0.49f, 0.55f, 0.72f));
        material.SetColor("_ReflectionColor", new Color(0.6f, 0.64f, 0.68f, 0.24f));
        material.SetColor("_SpecularColor", new Color(0.95f, 0.82f, 0.67f, 0.2f));
        material.SetColor("_SSSColor", new Color(0.1f, 0.23f, 0.28f, 1f));
        material.SetColor("_BlendColor", new Color(0.18f, 0.31f, 0.38f, 1f));
        if (material.HasProperty("_Level01ColorBlend"))
        {
            material.SetFloat("_Level01ColorBlend", 1f);
        }
        if (material.HasProperty("_Level01ReflectionTint"))
        {
            material.SetColor("_Level01ReflectionTint", new Color(1.01f, 1f, 0.98f, 1f));
        }
        if (material.HasProperty("_CinematicOcean"))
        {
            material.SetFloat("_CinematicOcean", 1f);
            material.SetFloat("_CinematicReflection", 0.86f);
            material.SetFloat("_CinematicSunGlint", 0.34f);
            material.SetFloat("_CinematicHorizonBlend", 1f);
            if (material.HasProperty("_CinematicHorizonColor"))
            {
                Color horizonColor = new Color(0.48f, 0.46f, 0.44f, 1f);
                Material skybox = RenderSettings.skybox;
                if (skybox != null && skybox.HasProperty("_SeaHorizonHazeColor"))
                {
                    horizonColor = skybox.GetColor("_SeaHorizonHazeColor");
                }
                material.SetColor("_CinematicHorizonColor", horizonColor);
            }
            material.SetFloat("_CinematicMicroRipple", 0.62f);
        }
        material.SetColor("_OverlayColor", new Color(0.08f, 0.13f, 0.16f, 0.22f));
    }
}
