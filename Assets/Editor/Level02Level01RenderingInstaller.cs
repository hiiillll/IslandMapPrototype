using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

public static class Level02Level01RenderingInstaller
{
    private const string ScenePath = "Assets/Scenes/Level02.unity";
    private const string SourceSkyboxPath = "Assets/Level01/Materials/MAT_Level01_CoverSunsetSky.mat";
    private const string Level02SkyboxPath = "Assets/Level02/Materials/MAT_Level02_OceanSunsetSky.mat";
    private const string Level02SkyShaderPath = "Assets/Level02/Shaders/Level02OceanSunsetSkybox.shader";
    private const string SourceProfilePath = "Assets/Art/PostProcessing/PP_Level01_GTA_Daylight.asset";
    private const string Level02ProfilePath = "Assets/Art/PostProcessing/PP_Level02_CinematicOcean.asset";

    [MenuItem("Tools/Island Map/Level02/Match Level 01 Rendering")]
    public static void ApplyFromMenu()
    {
        Apply();
    }

    public static void ApplyFromCommandLine()
    {
        try
        {
            Apply();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Apply()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Scene scene = activeScene.IsValid() && activeScene.path == ScenePath
            ? activeScene
            : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera camera = FindSceneComponent<Camera>(scene, "SYS_TopDownCamera");
        Light sun = FindSceneComponent<Light>(scene, "SYS_DirectionalLight");
        Material sourceSkybox = AssetDatabase.LoadAssetAtPath<Material>(SourceSkyboxPath);
        PostProcessProfile sourceProfile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(SourceProfilePath);

        if (camera == null || sun == null || sourceSkybox == null || sourceProfile == null)
        {
            throw new InvalidOperationException(
                "Level02 rendering sync requires its main camera/light and the Level01 skybox/profile.");
        }

        Material skybox = EnsureLevel02Skybox(sourceSkybox);
        PostProcessProfile profile = EnsureLevel02Profile(sourceProfile);
        ConfigureRenderSettings(skybox, sun);
        ConfigureCamera(camera, profile, scene);
        ConfigureBoatRenderers(scene);
        RemoveLegacyFillLights(scene);

        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Level02 Rendering] Applied the dedicated ocean sky, lighting, reflections, camera, and post processing.");
    }

    private static Material EnsureLevel02Skybox(Material sourceSkybox)
    {
        EnsureAssetFolder("Assets/Level02");
        EnsureAssetFolder("Assets/Level02/Materials");
        Shader level02Shader = AssetDatabase.LoadAssetAtPath<Shader>(Level02SkyShaderPath);
        if (level02Shader == null)
        {
            throw new InvalidOperationException("The Level02 ocean sky shader could not be loaded.");
        }

        Material skybox = AssetDatabase.LoadAssetAtPath<Material>(Level02SkyboxPath);
        if (skybox == null)
        {
            if (!AssetDatabase.CopyAsset(SourceSkyboxPath, Level02SkyboxPath))
            {
                throw new InvalidOperationException("Unable to create the Level02 ocean skybox.");
            }
            AssetDatabase.ImportAsset(Level02SkyboxPath, ImportAssetOptions.ForceUpdate);
            skybox = AssetDatabase.LoadAssetAtPath<Material>(Level02SkyboxPath);
        }
        if (skybox == null)
        {
            throw new InvalidOperationException("The Level02 ocean skybox could not be loaded.");
        }

        skybox.CopyPropertiesFromMaterial(sourceSkybox);
        skybox.shader = level02Shader;
        skybox.name = "MAT_Level02_OceanSunsetSky";
        skybox.SetColor("_ZenithColor", new Color(0.045f, 0.1f, 0.22f, 1f));
        skybox.SetColor("_UpperSkyColor", new Color(0.13f, 0.28f, 0.5f, 1f));
        skybox.SetColor("_HorizonColor", new Color(0.7f, 0.4f, 0.23f, 1f));
        skybox.SetColor("_GroundColor", new Color(0.23f, 0.27f, 0.31f, 1f));
        skybox.SetColor("_CloudShadow", new Color(0.22f, 0.28f, 0.37f, 1f));
        skybox.SetColor("_CloudLight", new Color(0.78f, 0.72f, 0.66f, 1f));
        skybox.SetFloat("_CloudCoverage", 0.56f);
        skybox.SetFloat("_CloudOpacity", 0.66f);
        skybox.SetFloat("_Exposure", 0.98f);
        EditorUtility.SetDirty(skybox);
        return skybox;
    }

    private static void EnsureAssetFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        string folder = path.Substring(separator + 1);
        AssetDatabase.CreateFolder(parent, folder);
    }

    private static void ConfigureRenderSettings(Material skybox, Light sun)
    {
        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.34f, 0.45f, 0.62f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.52f, 0.46f, 0.42f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.24f, 0.28f, 0.32f, 1f);
        RenderSettings.ambientIntensity = 1.14f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.defaultReflectionResolution = 1024;
        RenderSettings.reflectionBounces = 2;
        RenderSettings.reflectionIntensity = 0.9f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.34f, 0.38f, 0.43f, 1f);
        RenderSettings.fogStartDistance = 320f;
        RenderSettings.fogEndDistance = 1500f;

        sun.color = new Color(1f, 0.84f, 0.66f, 1f);
        sun.intensity = 1.16f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.46f;
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
        EditorUtility.SetDirty(sun);
        EditorUtility.SetDirty(sun.transform);
    }

    private static void ConfigureCamera(Camera camera, PostProcessProfile profile, Scene scene)
    {
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.allowHDR = true;
        camera.allowMSAA = true;
        camera.useOcclusionCulling = true;
        camera.farClipPlane = 1600f;
        camera.renderingPath = RenderingPath.Forward;
        camera.depthTextureMode |= DepthTextureMode.DepthNormals | DepthTextureMode.MotionVectors;

        Level02HighQualityRendering highQuality = camera.GetComponent<Level02HighQualityRendering>();
        if (highQuality == null)
        {
            highQuality = camera.gameObject.AddComponent<Level02HighQualityRendering>();
        }

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

        Level01ShadowStability stability = camera.GetComponent<Level01ShadowStability>();
        if (stability == null)
        {
            stability = camera.gameObject.AddComponent<Level01ShadowStability>();
        }

        GameObject volumeObject = FindSceneObject(scene, "ENV_PostProcessing_Level02");
        if (volumeObject == null)
        {
            volumeObject = new GameObject("ENV_PostProcessing_Level02");
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

        EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(highQuality);
        EditorUtility.SetDirty(layer);
        EditorUtility.SetDirty(stability);
        EditorUtility.SetDirty(volume);
    }

    private static PostProcessProfile EnsureLevel02Profile(PostProcessProfile sourceProfile)
    {
        PostProcessProfile profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(Level02ProfilePath);
        if (profile == null)
        {
            if (!AssetDatabase.CopyAsset(SourceProfilePath, Level02ProfilePath))
            {
                throw new InvalidOperationException("Unable to create the Level02 post-process profile.");
            }
            AssetDatabase.ImportAsset(Level02ProfilePath, ImportAssetOptions.ForceUpdate);
            profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(Level02ProfilePath);
        }
        if (profile == null)
        {
            throw new InvalidOperationException("The Level02 post-process profile could not be loaded.");
        }

        Bloom bloom = GetOrAddSetting<Bloom>(profile);
        bloom.active = true;
        bloom.enabled.Override(true);
        bloom.intensity.Override(0.075f);
        bloom.threshold.Override(1.28f);
        bloom.softKnee.Override(0.62f);
        bloom.diffusion.Override(6f);
        bloom.color.Override(new Color(1f, 0.96f, 0.9f, 1f));
        bloom.fastMode.Override(false);

        AmbientOcclusion ambientOcclusion = GetOrAddSetting<AmbientOcclusion>(profile);
        ambientOcclusion.active = true;
        ambientOcclusion.enabled.Override(true);
        ambientOcclusion.mode.Override(AmbientOcclusionMode.MultiScaleVolumetricObscurance);
        ambientOcclusion.intensity.Override(0.22f);
        ambientOcclusion.ambientOnly.Override(false);
        ambientOcclusion.thicknessModifier.Override(1.05f);
        ambientOcclusion.quality.Override(AmbientOcclusionQuality.Ultra);

        ColorGrading colorGrading = GetOrAddSetting<ColorGrading>(profile);
        colorGrading.active = true;
        colorGrading.enabled.Override(true);
        colorGrading.gradingMode.Override(GradingMode.HighDefinitionRange);
        colorGrading.tonemapper.Override(Tonemapper.ACES);
        colorGrading.temperature.Override(1f);
        colorGrading.saturation.Override(2f);
        colorGrading.contrast.Override(7f);
        colorGrading.postExposure.Override(0.08f);

        MotionBlur motionBlur = GetOrAddSetting<MotionBlur>(profile);
        motionBlur.active = true;
        motionBlur.enabled.Override(true);
        motionBlur.shutterAngle.Override(18f);
        motionBlur.sampleCount.Override(12);

        Vignette vignette = GetOrAddSetting<Vignette>(profile);
        vignette.active = true;
        vignette.enabled.Override(true);
        vignette.intensity.Override(0.025f);
        vignette.smoothness.Override(0.38f);

        foreach (PostProcessEffectSettings setting in profile.settings)
        {
            EditorUtility.SetDirty(setting);
        }
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static T GetOrAddSetting<T>(PostProcessProfile profile)
        where T : PostProcessEffectSettings
    {
        if (profile.TryGetSettings(out T setting))
        {
            return setting;
        }
        return profile.AddSettings<T>();
    }

    private static void ConfigureBoatRenderers(Scene scene)
    {
        foreach (Renderer renderer in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<Renderer>(true)))
        {
            if (renderer.GetComponentInParent<BoatChaseController>() == null
                && renderer.GetComponentInParent<BoatEnemyChaser>() == null)
            {
                continue;
            }

            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void RemoveLegacyFillLights(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == "FX_PlayerBoatFillLight"
                    || transform.name == "SYS_Level02EnemyFillLight")
                {
                    UnityEngine.Object.DestroyImmediate(transform.gameObject);
                }
            }
        }
    }

    private static T FindSceneComponent<T>(Scene scene, string objectName) where T : Component
    {
        GameObject gameObject = FindSceneObject(scene, objectName);
        return gameObject != null ? gameObject.GetComponent<T>() : null;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(transform => transform.name == objectName)
            ?.gameObject;
    }
}
