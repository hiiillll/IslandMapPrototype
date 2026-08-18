using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

public static class Level01GlobalRenderingInstaller
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string ProfilePath = "Assets/Art/PostProcessing/PP_Level01_GTA_Daylight.asset";
    private const string OceanMaterialPath = "Assets/Level01/Materials/MAT_Level01_SuimonoOcean.mat";
    private const string ShallowWaterMaterialPath = "Assets/Level01/Materials/MAT_Level01_SuimonoShallowWater.mat";
    private const string SourceSkyboxPath = "Assets/Level04/Materials/MAT_Level04_CoverSunsetSky.mat";
    private const string Level01SkyboxPath = "Assets/Level01/Materials/MAT_Level01_CoverSunsetSky.mat";
    private const string Level01SkyboxShaderName = "Custom/Level01GoldenHourSkybox";
    private const string ArchitectureMaterialFolder = "Assets/Level01/Materials/Architecture";

    private static readonly Vector3[] ProbePositions =
    {
        new Vector3(-90f, 15f, 0f),
        new Vector3(0f, 15f, 0f),
        new Vector3(90f, 15f, 0f)
    };

    private static readonly string[] ProbeNames =
    {
        "ENV_ReflectionProbe_West",
        "ENV_ReflectionProbe_Center",
        "ENV_ReflectionProbe_East"
    };

    [MenuItem("Tools/Island Map/Apply Level 01 Global Rendering")]
    public static void ApplyFromMenu()
    {
        Apply();
    }

    public static void ApplyFromCommandLine()
    {
        Apply();
    }

    private static void Apply()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        ConfigureLighting(scene);
        ConfigureSkybox();
        ConfigurePostProcessing(scene);
        ConfigureReflectionProbes(scene);
        ConfigureArchitectureMaterials(scene);
        ConfigureVehicleRenderers(scene);
        ConfigureOceanMaterial();

        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Level 01 Global Rendering] Applied lighting, atmosphere, post processing, vehicle reflections, ocean color, and performance settings.");
    }

    private static void ConfigureLighting(Scene scene)
    {
        Light sun = FindDirectionalLight(scene);
        if (sun == null)
        {
            throw new InvalidOperationException("No active directional light found in " + ScenePath);
        }

        // Keep the established shadow direction; only rebalance its color and response.
        sun.color = new Color(1f, 0.88f, 0.72f, 1f);
        sun.intensity = 1.08f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.34f;
        sun.shadowBias = 0.045f;
        sun.shadowNormalBias = 0.28f;
        sun.shadowCustomResolution = 4096;
        sun.useViewFrustumForShadowCasterCull = false;
        sun.useColorTemperature = false;
        RenderSettings.sun = sun;
        EditorUtility.SetDirty(sun);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.44f, 0.54f, 0.7f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.64f, 0.55f, 0.47f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.42f, 0.44f, 0.47f, 1f);
        RenderSettings.ambientIntensity = 1.24f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.defaultReflectionResolution = 256;
        RenderSettings.reflectionBounces = 2;
        RenderSettings.reflectionIntensity = 0.94f;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.58f, 0.52f, 0.47f, 1f);
        RenderSettings.fogStartDistance = 300f;
        RenderSettings.fogEndDistance = 1050f;
    }

    private static void ConfigureSkybox()
    {
        Shader skyShader = Shader.Find(Level01SkyboxShaderName);
        if (skyShader == null)
        {
            throw new InvalidOperationException("Level 01 skybox shader was not found: " + Level01SkyboxShaderName);
        }

        Material skybox = AssetDatabase.LoadAssetAtPath<Material>(Level01SkyboxPath);
        if (skybox == null)
        {
            skybox = new Material(skyShader) { name = "MAT_Level01_CoverSunsetSky" };
            AssetDatabase.CreateAsset(skybox, Level01SkyboxPath);
        }
        else
        {
            skybox.shader = skyShader;
        }

        Material sourceSkybox = AssetDatabase.LoadAssetAtPath<Material>(SourceSkyboxPath);
        if (sourceSkybox != null)
        {
            SetTexture(skybox, "_CloudTexA", sourceSkybox.GetTexture("_CloudTexA"));
            SetTexture(skybox, "_CloudTexB", sourceSkybox.GetTexture("_CloudTexB"));
        }

        SetColor(skybox, "_ZenithColor", new Color(0.09f, 0.18f, 0.34f, 1f));
        SetColor(skybox, "_UpperSkyColor", new Color(0.25f, 0.4f, 0.6f, 1f));
        SetColor(skybox, "_HorizonColor", new Color(0.78f, 0.52f, 0.32f, 1f));
        SetColor(skybox, "_GroundColor", new Color(0.34f, 0.35f, 0.38f, 1f));
        SetColor(skybox, "_CloudShadow", new Color(0.34f, 0.36f, 0.41f, 1f));
        SetColor(skybox, "_CloudLight", new Color(0.82f, 0.76f, 0.68f, 1f));
        SetColor(skybox, "_SunColor", new Color(1f, 0.67f, 0.34f, 1f));
        SetVector(skybox, "_SunDirection", new Vector4(-0.64f, 0.055f, 0.77f, 0f));
        SetVector(skybox, "_DriftA", new Vector4(0.00045f, 0.00008f, 0f, 0f));
        SetVector(skybox, "_DriftB", new Vector4(-0.00018f, 0.00012f, 0f, 0f));
        SetFloat(skybox, "_CloudCoverage", 0.62f);
        SetFloat(skybox, "_CloudSoftness", 0.14f);
        SetFloat(skybox, "_CloudOpacity", 0.58f);
        SetFloat(skybox, "_Exposure", 1.02f);
        RenderSettings.skybox = skybox;
        EditorUtility.SetDirty(skybox);
    }

    private static void ConfigurePostProcessing(Scene scene)
    {
        Camera camera = FindSceneObject(scene, "SYS_MainCamera")?.GetComponent<Camera>();
        if (camera == null)
        {
            throw new InvalidOperationException("SYS_MainCamera was not found in " + ScenePath);
        }

        camera.allowHDR = true;
        camera.allowMSAA = true;
        camera.useOcclusionCulling = true;
        camera.depthTextureMode |= DepthTextureMode.DepthNormals | DepthTextureMode.MotionVectors;
        EditorUtility.SetDirty(camera);

        PostProcessLayer layer = camera.GetComponent<PostProcessLayer>();
        if (layer == null)
        {
            layer = camera.gameObject.AddComponent<PostProcessLayer>();
        }

        layer.volumeLayer = ~0;
        layer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
        if (layer.subpixelMorphologicalAntialiasing == null)
        {
            layer.subpixelMorphologicalAntialiasing = new SubpixelMorphologicalAntialiasing();
        }
        layer.subpixelMorphologicalAntialiasing.quality = SubpixelMorphologicalAntialiasing.Quality.High;
        layer.fog.enabled = false;
        EditorUtility.SetDirty(layer);

        PostProcessProfile profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(ProfilePath);
        if (profile == null)
        {
            throw new InvalidOperationException("Post-processing profile was not found at " + ProfilePath);
        }

        ColorGrading grading = GetOrAdd<ColorGrading>(profile);
        grading.enabled.Override(true);
        grading.gradingMode.Override(GradingMode.HighDefinitionRange);
        grading.tonemapper.Override(Tonemapper.ACES);
        grading.postExposure.Override(0.18f);
        grading.contrast.Override(5f);
        grading.saturation.Override(2f);
        grading.temperature.Override(4f);
        grading.tint.Override(0f);
        grading.colorFilter.Override(Color.white);

        AmbientOcclusion ambientOcclusion = GetOrAdd<AmbientOcclusion>(profile);
        ambientOcclusion.enabled.Override(true);
        ambientOcclusion.mode.Override(AmbientOcclusionMode.ScalableAmbientObscurance);
        ambientOcclusion.intensity.Override(0.16f);
        ambientOcclusion.radius.Override(0.8f);
        ambientOcclusion.quality.Override(AmbientOcclusionQuality.High);
        ambientOcclusion.ambientOnly.Override(false);
        ambientOcclusion.directLightingStrength.Override(0.05f);

        Bloom bloom = GetOrAdd<Bloom>(profile);
        bloom.enabled.Override(true);
        bloom.intensity.Override(0.06f);
        bloom.threshold.Override(1.3f);
        bloom.softKnee.Override(0.5f);
        bloom.diffusion.Override(5f);
        bloom.color.Override(new Color(1f, 0.96f, 0.9f, 1f));
        bloom.fastMode.Override(false);

        Vignette vignette = GetOrAdd<Vignette>(profile);
        vignette.enabled.Override(true);
        vignette.mode.Override(VignetteMode.Classic);
        vignette.color.Override(Color.black);
        vignette.center.Override(new Vector2(0.5f, 0.5f));
        vignette.intensity.Override(0.02f);
        vignette.smoothness.Override(0.35f);
        vignette.roundness.Override(1f);
        vignette.rounded.Override(false);

        MotionBlur motionBlur = GetOrAdd<MotionBlur>(profile);
        motionBlur.enabled.Override(true);
        motionBlur.shutterAngle.Override(35f);
        motionBlur.sampleCount.Override(16);

        DisableIfPresent<Grain>(profile);
        DisableIfPresent<ChromaticAberration>(profile);
        DisableIfPresent<DepthOfField>(profile);
        DisableIfPresent<AutoExposure>(profile);

        GameObject volumeObject = FindSceneObject(scene, "ENV_PostProcessing_Level01");
        if (volumeObject == null)
        {
            volumeObject = new GameObject("ENV_PostProcessing_Level01");
            SceneManager.MoveGameObjectToScene(volumeObject, scene);
        }

        PostProcessVolume volume = volumeObject.GetComponent<PostProcessVolume>();
        if (volume == null)
        {
            volume = volumeObject.AddComponent<PostProcessVolume>();
        }
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);
        EditorUtility.SetDirty(profile);
    }

    private static void ConfigureReflectionProbes(Scene scene)
    {
        Transform parent = FindSceneObject(scene, "ENVIRONMENT")?.transform;
        int excludedLayers = LayerMask.GetMask("Water", "UI", "Suimono_Water", "Suimono_Depth", "Suimono_Screen");

        for (int index = 0; index < ProbeNames.Length; index++)
        {
            GameObject probeObject = FindSceneObject(scene, ProbeNames[index]);
            if (probeObject == null)
            {
                probeObject = new GameObject(ProbeNames[index]);
                SceneManager.MoveGameObjectToScene(probeObject, scene);
            }

            probeObject.transform.SetParent(parent, false);
            probeObject.transform.localPosition = ProbePositions[index];
            probeObject.transform.localRotation = Quaternion.identity;
            probeObject.transform.localScale = Vector3.one;

            ReflectionProbe probe = probeObject.GetComponent<ReflectionProbe>();
            if (probe == null)
            {
                probe = probeObject.AddComponent<ReflectionProbe>();
            }

            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.cullingMask = ~excludedLayers;
            probe.resolution = 256;
            probe.hdr = true;
            probe.shadowDistance = 120f;
            probe.intensity = 0.96f;
            probe.importance = 2000;
            probe.boxProjection = true;
            probe.blendDistance = 48f;
            probe.size = new Vector3(120f, 50f, 320f);
            probe.center = Vector3.zero;
            EditorUtility.SetDirty(probeObject);
            EditorUtility.SetDirty(probe);
        }
    }

    private static void ConfigureVehicleRenderers(Scene scene)
    {
        GameObject playerCar = FindSceneObject(scene, "PLAYER_Car");
        if (playerCar == null)
        {
            throw new InvalidOperationException("PLAYER_Car was not found in " + ScenePath);
        }

        foreach (Renderer renderer in playerCar.GetComponentsInChildren<Renderer>(true))
        {
            // Anchor probe interpolation to the vehicle pivot instead of the
            // rotating renderer bounds. This prevents probe/light changes during
            // drift spins and keeps the sky reflection as a stable fallback.
            renderer.probeAnchor = playerCar.transform;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void ConfigureArchitectureMaterials(Scene scene)
    {
        EnsureArchitectureMaterialFolder();
        Dictionary<Material, Material> stableMaterials = new Dictionary<Material, Material>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsArchitectureRenderer(renderer.transform))
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material source = materials[index];
                    if (!IsMatteArchitectureMaterial(source))
                    {
                        continue;
                    }

                    if (!stableMaterials.TryGetValue(source, out Material stable))
                    {
                        stable = CreateStableArchitectureMaterial(source);
                        stableMaterials.Add(source, stable);
                    }

                    materials[index] = stable;
                    changed = true;
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                }
            }
        }
    }

    private static bool IsArchitectureRenderer(Transform transform)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            string objectName = current.name;
            if (objectName.IndexOf("Building", StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Hotel", StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("House", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMatteArchitectureMaterial(Material material)
    {
        if (material == null)
        {
            return false;
        }

        string materialName = material.name;
        string[] reflectiveKeywords =
        {
            "Glass", "Window", "Metal", "Chrome", "Mirror", "Lamp", "Light", "Neon", "Sign"
        };
        foreach (string keyword in reflectiveKeywords)
        {
            if (materialName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }
        }

        return material.HasProperty("_Glossiness") || material.HasProperty("_Smoothness");
    }

    private static Material CreateStableArchitectureMaterial(Material source)
    {
        string sourcePath = AssetDatabase.GetAssetPath(source);
        string guid = AssetDatabase.AssetPathToGUID(sourcePath);
        string suffix = guid.Length >= 8 ? guid.Substring(0, 8) : "instance";
        string materialName = "MAT_Level01_Stable_" + SanitizeAssetName(source.name) + "_" + suffix;
        string materialPath = ArchitectureMaterialFolder + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(source) { name = materialName };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            EditorUtility.CopySerialized(source, material);
            material.name = materialName;
        }

        if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", Mathf.Min(material.GetFloat("_Glossiness"), 0.2f));
        }
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", Mathf.Min(material.GetFloat("_Smoothness"), 0.2f));
        }
        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static string SanitizeAssetName(string value)
    {
        StringBuilder result = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            result.Append(char.IsLetterOrDigit(character) || character == '_' || character == '-'
                ? character
                : '_');
        }
        return result.ToString();
    }

    private static void EnsureArchitectureMaterialFolder()
    {
        if (!AssetDatabase.IsValidFolder(ArchitectureMaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/Level01/Materials", "Architecture");
        }
    }

    private static void ConfigureOceanMaterial()
    {
        Material ocean = AssetDatabase.LoadAssetAtPath<Material>(OceanMaterialPath);
        Material shallowWater = AssetDatabase.LoadAssetAtPath<Material>(ShallowWaterMaterialPath);
        if (ocean == null)
        {
            throw new InvalidOperationException("Level 01 ocean material was not found at " + OceanMaterialPath);
        }

        ConfigureWaterMaterial(ocean, false);
        if (shallowWater != null)
        {
            ConfigureWaterMaterial(shallowWater, true);
        }
    }

    private static void ConfigureWaterMaterial(Material water, bool shallow)
    {
        SetColor(water, "_BlendColor", new Color(0.16f, 0.24f, 0.29f, 1f));
        SetColor(water, "_LowColor", new Color(0.16f, 0.27f, 0.32f, 0.15f));
        SetColor(water, "_ReflectionColor", new Color(0.52f, 0.43f, 0.36f, 0.18f));
        SetColor(water, "_SpecularColor", new Color(0.85f, 0.74f, 0.62f, 0.18f));
        SetColor(water, "_depthColor", shallow
            ? new Color(0.2f, 0.36f, 0.42f, 1f)
            : new Color(0.12f, 0.27f, 0.36f, 1f));
        SetColor(water, "_shallowColor", shallow
            ? new Color(0.38f, 0.54f, 0.56f, 0.72f)
            : new Color(0.3f, 0.46f, 0.52f, 0.72f));
        SetFloat(water, "_ReflectStrength", 0.86f);
        SetFloat(water, "_roughness", shallow ? 0.6f : 0.55f);
        SetFloat(water, "_overallBrightness", shallow ? 1.1f : 1.08f);
        SetFloat(water, "_Level01ColorBlend", 1f);
        SetColor(water, "_Level01ReflectionTint", new Color(1.03f, 1f, 0.96f, 1f));
        EditorUtility.SetDirty(water);
    }

    private static T GetOrAdd<T>(PostProcessProfile profile) where T : PostProcessEffectSettings
    {
        T setting = profile.GetSetting<T>();
        if (setting != null)
        {
            return setting;
        }

        setting = profile.AddSettings<T>();
        AssetDatabase.AddObjectToAsset(setting, profile);
        return setting;
    }

    private static void DisableIfPresent<T>(PostProcessProfile profile) where T : PostProcessEffectSettings
    {
        T setting = profile.GetSetting<T>();
        if (setting != null)
        {
            setting.enabled.Override(false);
            EditorUtility.SetDirty(setting);
        }
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

    private static void SetTexture(Material material, string propertyName, Texture value)
    {
        if (value != null && material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, value);
        }
    }

    private static void SetVector(Material material, string propertyName, Vector4 value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetVector(propertyName, value);
        }
    }

    private static Light FindDirectionalLight(Scene scene)
    {
        foreach (Light light in Resources.FindObjectsOfTypeAll<Light>())
        {
            if (light.gameObject.scene == scene && light.type == LightType.Directional && light.isActiveAndEnabled)
            {
                return light;
            }
        }
        return null;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (gameObject.scene == scene && gameObject.name == objectName)
            {
                return gameObject;
            }
        }
        return null;
    }
}
