using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

public static class FirstRoundLightingInstaller
{
    private const string IslandScenePath = "Assets/Scenes/IslandMap.unity";
    private const string Level03ScenePath = "Assets/Scenes/Level03.unity";
    private const string ProfileFolder = "Assets/Art/PostProcessing";
    private const string IslandProfilePath = ProfileFolder + "/PP_Level01_GTA_Daylight.asset";
    private const string Level03ProfilePath = ProfileFolder + "/PP_Level03_GTA_HazyDaylight.asset";

    [MenuItem("Tools/Island Map/Apply First Round Lighting")]
    public static void ApplyFromMenu()
    {
        ApplyAll();
    }

    public static void ApplyFromCommandLine()
    {
        ApplyAll();
    }

    private static void ApplyAll()
    {
        EnsureFolder("Assets/Art");
        EnsureFolder(ProfileFolder);

        ApplyScene(IslandScenePath, false);
        ApplyScene(Level03ScenePath, true);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[First Round Lighting] Applied scene profiles and GTA-style tone mapping to Level01 and Level03.");
    }

    private static void ApplyScene(string scenePath, bool level03)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        ConfigureLighting(scene, level03);
        ConfigurePostProcessing(scene, level03);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
    }

    private static void ConfigureLighting(Scene scene, bool level03)
    {
        Light sun = FindSceneDirectionalLight(scene);
        if (sun == null)
        {
            throw new InvalidOperationException("No directional light found in " + scene.path);
        }

        sun.color = level03
            ? new Color(1f, 0.97f, 0.9f, 1f)
            : new Color(1f, 0.965f, 0.89f, 1f);
        sun.intensity = level03 ? 1.2f : 1.18f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = level03 ? 0.7f : 0.72f;
        sun.shadowBias = 0.045f;
        sun.shadowNormalBias = 0.32f;
        sun.useColorTemperature = true;
        sun.colorTemperature = level03 ? 5600f : 5600f;
        RenderSettings.sun = sun;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientIntensity = level03 ? 0.76f : 0.74f;
        RenderSettings.ambientSkyColor = level03
            ? new Color(0.58f, 0.7f, 0.84f, 1f)
            : new Color(0.51f, 0.66f, 0.8f, 1f);
        RenderSettings.ambientEquatorColor = level03
            ? new Color(0.64f, 0.69f, 0.7f, 1f)
            : new Color(0.63f, 0.68f, 0.68f, 1f);
        RenderSettings.ambientGroundColor = level03
            ? new Color(0.36f, 0.39f, 0.37f, 1f)
            : new Color(0.33f, 0.37f, 0.33f, 1f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = level03
            ? new Color(0.58f, 0.68f, 0.76f, 1f)
            : new Color(0.6f, 0.7f, 0.76f, 1f);
        RenderSettings.fogStartDistance = level03 ? 180f : 250f;
        RenderSettings.fogEndDistance = level03 ? 700f : 900f;
        RenderSettings.fogDensity = level03 ? 0.006f : 0.003f;
        DynamicGI.UpdateEnvironment();
    }

    private static void ConfigurePostProcessing(Scene scene, bool level03)
    {
        Camera camera = FindSceneCamera(scene);
        if (camera == null)
        {
            throw new InvalidOperationException("SYS_MainCamera was not found in " + scene.path);
        }

        camera.allowHDR = true;
        camera.depthTextureMode |= DepthTextureMode.DepthNormals;

        PostProcessLayer layer = camera.GetComponent<PostProcessLayer>();
        if (layer == null)
        {
            layer = camera.gameObject.AddComponent<PostProcessLayer>();
        }
        layer.volumeLayer = ~0;
        layer.antialiasingMode = PostProcessLayer.Antialiasing.None;
        layer.fog.enabled = false;

        string profilePath = level03 ? Level03ProfilePath : IslandProfilePath;
        PostProcessProfile profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            profile.name = level03 ? "PP_Level03_GTA_HazyDaylight" : "PP_Level01_GTA_Daylight";
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        ConfigureProfile(profile, level03);

        string volumeName = level03 ? "ENV_PostProcessing_Level03" : "ENV_PostProcessing_Level01";
        GameObject volumeObject = FindSceneObject(scene, volumeName);
        if (volumeObject == null)
        {
            volumeObject = new GameObject(volumeName);
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
    }

    private static void ConfigureProfile(PostProcessProfile profile, bool level03)
    {
        ColorGrading grading = GetOrAdd<ColorGrading>(profile);
        grading.enabled.value = true;
        grading.gradingMode.Override(GradingMode.HighDefinitionRange);
        grading.tonemapper.Override(Tonemapper.Neutral);
        grading.postExposure.Override(level03 ? 0.2f : 0.18f);
        grading.contrast.Override(10f);
        grading.saturation.Override(12f);
        grading.temperature.Override(level03 ? -3f : -2f);
        grading.tint.Override(0f);
        grading.colorFilter.Override(level03
            ? new Color(0.96f, 0.99f, 1f, 1f)
            : new Color(0.98f, 1f, 1f, 1f));

        AmbientOcclusion ambientOcclusion = GetOrAdd<AmbientOcclusion>(profile);
        ambientOcclusion.enabled.value = true;
        ambientOcclusion.mode.Override(AmbientOcclusionMode.ScalableAmbientObscurance);
        ambientOcclusion.intensity.Override(level03 ? 0.12f : 0.12f);
        ambientOcclusion.radius.Override(level03 ? 1.8f : 1.7f);
        ambientOcclusion.quality.Override(AmbientOcclusionQuality.Medium);
        ambientOcclusion.ambientOnly.Override(false);

        Bloom bloom = GetOrAdd<Bloom>(profile);
        bloom.enabled.value = true;
        bloom.intensity.Override(level03 ? 0.2f : 0.18f);
        bloom.threshold.Override(1.1f);
        bloom.softKnee.Override(0.55f);
        bloom.diffusion.Override(4f);
        bloom.color.Override(new Color(0.82f, 0.94f, 1f, 1f));
        bloom.fastMode.Override(true);

        EditorUtility.SetDirty(profile);
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

    private static Light FindSceneDirectionalLight(Scene scene)
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

    private static Camera FindSceneCamera(Scene scene)
    {
        foreach (Camera camera in Resources.FindObjectsOfTypeAll<Camera>())
        {
            if (camera.gameObject.scene == scene && camera.gameObject.name == "SYS_MainCamera" && camera.isActiveAndEnabled)
            {
                return camera;
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

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }
        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, name);
    }
}
