using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public sealed class RuntimeLightingBootstrap : MonoBehaviour
{
    private const float ReflectionVolumeSize = 10000f;
    private const int ReflectionResolution = 128;
    private const float IslandMapReflectionIntensity = 0.82f;
    private const float Level03ReflectionIntensity = 0.65f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLightingRefresh()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        GameObject bootstrapObject = new GameObject("SYS_RuntimeLightingBootstrap");
        bootstrapObject.hideFlags = HideFlags.HideAndDontSave;
        bootstrapObject.AddComponent<RuntimeLightingBootstrap>();
    }

    private IEnumerator Start()
    {
        QualitySettings.realtimeReflectionProbes = true;
        ApplyRuntimeLighting();
        yield return null;
        ApplyRuntimeLighting();

        ReflectionProbe skyReflection = CreateSkyReflectionProbe();
        int renderId = skyReflection.RenderProbe();
        while (renderId >= 0 && !skyReflection.IsFinishedRendering(renderId))
        {
            yield return null;
        }

        ApplyRuntimeLighting();
    }

    private ReflectionProbe CreateSkyReflectionProbe()
    {
        GameObject probeObject = new GameObject("ENV_RuntimeSkyReflection");
        probeObject.hideFlags = HideFlags.HideAndDontSave;
        probeObject.transform.SetParent(transform, false);

        ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.clearFlags = ReflectionProbeClearFlags.Skybox;
        probe.cullingMask = 0;
        probe.resolution = ReflectionResolution;
        probe.hdr = true;
        probe.intensity = GetSceneReflectionIntensity();
        probe.importance = 1000;
        probe.boxProjection = false;
        probe.blendDistance = 0f;
        probe.size = Vector3.one * ReflectionVolumeSize;
        return probe;
    }

    private static void ApplyRuntimeLighting()
    {
        if (RenderSettings.sun == null)
        {
            RenderSettings.sun = FindMainDirectionalLight();
        }

        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.defaultReflectionResolution = ReflectionResolution;
        RenderSettings.reflectionBounces = 1;
        RenderSettings.reflectionIntensity = GetSceneReflectionIntensity();
        DynamicGI.UpdateEnvironment();
    }

    private static float GetSceneReflectionIntensity()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "IslandMap")
        {
            return IslandMapReflectionIntensity;
        }
        if (sceneName == "Level03")
        {
            return Level03ReflectionIntensity;
        }

        return 1f;
    }

    private static Light FindMainDirectionalLight()
    {
        Light brightestDirectionalLight = null;
        foreach (Light sceneLight in FindObjectsOfType<Light>())
        {
            if (!sceneLight.enabled
                || !sceneLight.gameObject.activeInHierarchy
                || sceneLight.type != LightType.Directional)
            {
                continue;
            }

            if (brightestDirectionalLight == null
                || sceneLight.intensity > brightestDirectionalLight.intensity)
            {
                brightestDirectionalLight = sceneLight;
            }
        }

        return brightestDirectionalLight;
    }
}
