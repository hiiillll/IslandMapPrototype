using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

public static class Level01LightingStabilityDiagnostic
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string OutputPath = "Library/Level01LightingDiagnostic/level01_lighting_stability.csv";
    private const string SceneViewOutputFolder = "Library/Level01LightingDiagnostic/SceneViewReproduction";
    private const int Width = 320;
    private const int Height = 180;
    private const int ViewCount = 72;

    private enum TestMode
    {
        Baseline,
        NoShadows,
        NoReflections,
        NoPostProcessing,
        ForceLod0,
        SingleStableCascade
    }

    [MenuItem("Tools/Island Map/Diagnose Level 01 Lighting Stability")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void RunFromCommandLine()
    {
        try
        {
            Run();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void RunSceneViewReproductionFromCommandLine()
    {
        try
        {
            RunSceneViewReproduction();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void RunExactRuntimeCameraComparisonFromCommandLine()
    {
        try
        {
            RunExactRuntimeCameraComparison();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void AuditSceneLightingFromCommandLine()
    {
        try
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (Light light in Resources.FindObjectsOfTypeAll<Light>()
                         .Where(item => item.gameObject.scene == scene))
            {
                Debug.Log(
                    $"[Level01 Lighting Audit] Light name={light.name}, type={light.type}, "
                    + $"enabled={light.enabled}, active={light.gameObject.activeInHierarchy}, "
                    + $"intensity={light.intensity:F3}, shadows={light.shadows}, "
                    + $"renderMode={light.renderMode}, position={light.transform.position}, "
                    + $"rotation={light.transform.eulerAngles}");
            }

            foreach (ReflectionProbe probe in Resources.FindObjectsOfTypeAll<ReflectionProbe>()
                         .Where(item => item.gameObject.scene == scene))
            {
                Debug.Log(
                    $"[Level01 Lighting Audit] Probe name={probe.name}, enabled={probe.enabled}, "
                    + $"active={probe.gameObject.activeInHierarchy}, mode={probe.mode}, "
                    + $"refresh={probe.refreshMode}, intensity={probe.intensity:F3}, "
                    + $"importance={probe.importance}, bounds={probe.bounds}");
            }

            foreach (PostProcessVolume volume in Resources.FindObjectsOfTypeAll<PostProcessVolume>()
                         .Where(item => item.gameObject.scene == scene))
            {
                Debug.Log(
                    $"[Level01 Lighting Audit] PostVolume name={volume.name}, enabled={volume.enabled}, "
                    + $"active={volume.gameObject.activeInHierarchy}, global={volume.isGlobal}, "
                    + $"weight={volume.weight:F3}, priority={volume.priority:F3}, "
                    + $"profile={(volume.sharedProfile != null ? volume.sharedProfile.name : "null")}");
            }

            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void RunSceneViewReproduction()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera camera = FindSceneObject(scene, "SYS_MainCamera")?.GetComponent<Camera>();
        Light sun = FindDirectionalLight(scene);
        if (camera == null || sun == null)
        {
            throw new InvalidOperationException("Level01 diagnostic camera or sun was not found.");
        }

        Renderer[] architecture = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
            .Where(renderer => IsArchitecture(renderer.transform))
            .ToArray();
        ReflectionProbe[] probes = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ReflectionProbe>(true))
            .ToArray();
        LODGroup[] lodGroups = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<LODGroup>(true))
            .Where(group => group.gameObject.activeInHierarchy)
            .ToArray();
        PostProcessLayer postLayer = camera.GetComponent<PostProcessLayer>();
        bool postEnabled = postLayer != null && postLayer.enabled;

        Vector3 originalPosition = camera.transform.position;
        Quaternion originalRotation = camera.transform.rotation;
        float originalFieldOfView = camera.fieldOfView;
        bool originalOrthographic = camera.orthographic;
        CameraClearFlags originalClearFlags = camera.clearFlags;
        RenderTexture originalTarget = camera.targetTexture;
        LightShadows originalShadows = sun.shadows;
        int originalShadowCascades = QualitySettings.shadowCascades;
        ShadowProjection originalShadowProjection = QualitySettings.shadowProjection;
        float originalShadowDistance = QualitySettings.shadowDistance;
        bool[] probeStates = probes.Select(probe => probe.enabled).ToArray();
        ReflectionProbeUsage[] usages = architecture.Select(renderer => renderer.reflectionProbeUsage).ToArray();

        const int sceneViewWidth = 900;
        const int sceneViewHeight = 320;
        Vector3 pivot = new Vector3(44.478992f, 25.16804f, -12.407993f);
        Quaternion baseRotation = new Quaternion(0.12634124f, -0.35185105f, 0.047370046f, 0.9265093f).normalized;
        float sceneViewSize = 2.0380118f;
        float fieldOfView = 60f;
        float cameraDistance = sceneViewSize / Mathf.Sin(fieldOfView * 0.5f * Mathf.Deg2Rad);
        float[] pitchOffsets = { -2f, -1f, -0.5f, 0f, 0.5f, 1f, 2f };
        TestMode[] modes = { TestMode.Baseline, TestMode.NoShadows, TestMode.NoReflections, TestMode.ForceLod0 };

        string outputFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "../", SceneViewOutputFolder));
        Directory.CreateDirectory(outputFolder);
        RenderTexture target = new RenderTexture(sceneViewWidth, sceneViewHeight, 24, RenderTextureFormat.ARGB32);
        Texture2D readback = new Texture2D(sceneViewWidth, sceneViewHeight, TextureFormat.RGB24, false, false);

        try
        {
            camera.targetTexture = target;
            camera.orthographic = false;
            camera.fieldOfView = fieldOfView;
            camera.clearFlags = CameraClearFlags.Skybox;

            foreach (TestMode mode in modes)
            {
                ApplyMode(mode, sun, probes, architecture, postLayer, postEnabled, lodGroups);
                foreach (float pitchOffset in pitchOffsets)
                {
                    Quaternion rotation = baseRotation * Quaternion.AngleAxis(pitchOffset, Vector3.right);
                    camera.transform.rotation = rotation;
                    camera.transform.position = pivot - rotation * Vector3.forward * cameraDistance;
                    camera.Render();

                    RenderTexture previousActive = RenderTexture.active;
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0f, 0f, sceneViewWidth, sceneViewHeight), 0, 0, false);
                    readback.Apply(false, false);
                    RenderTexture.active = previousActive;

                    string suffix = pitchOffset.ToString("+0.0;-0.0;0.0").Replace('+', 'p').Replace('-', 'm').Replace('.', '_');
                    string path = Path.Combine(outputFolder, mode + "_pitch_" + suffix + ".png");
                    File.WriteAllBytes(path, readback.EncodeToPNG());
                }
            }
        }
        finally
        {
            camera.transform.position = originalPosition;
            camera.transform.rotation = originalRotation;
            camera.orthographic = originalOrthographic;
            camera.fieldOfView = originalFieldOfView;
            camera.clearFlags = originalClearFlags;
            camera.targetTexture = originalTarget;
            sun.shadows = originalShadows;
            QualitySettings.shadowCascades = originalShadowCascades;
            QualitySettings.shadowProjection = originalShadowProjection;
            QualitySettings.shadowDistance = originalShadowDistance;
            if (postLayer != null)
            {
                postLayer.enabled = postEnabled;
            }
            for (int index = 0; index < probes.Length; index++)
            {
                probes[index].enabled = probeStates[index];
            }
            for (int index = 0; index < architecture.Length; index++)
            {
                architecture[index].reflectionProbeUsage = usages[index];
            }
            foreach (LODGroup lodGroup in lodGroups)
            {
                lodGroup.ForceLOD(-1);
            }
            UnityEngine.Object.DestroyImmediate(readback);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }

        Debug.Log("[Level01 Lighting Diagnostic] Scene View reproduction written to " + outputFolder);
    }

    private static void RunExactRuntimeCameraComparison()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera camera = FindSceneObject(scene, "SYS_MainCamera")?.GetComponent<Camera>();
        Light sun = FindDirectionalLight(scene);
        if (camera == null || sun == null)
        {
            throw new InvalidOperationException("Level01 diagnostic camera or sun was not found.");
        }

        Renderer[] architecture = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
            .Where(renderer => IsArchitecture(renderer.transform))
            .ToArray();
        ReflectionProbe[] probes = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ReflectionProbe>(true))
            .ToArray();
        LODGroup[] lodGroups = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<LODGroup>(true))
            .Where(group => group.gameObject.activeInHierarchy)
            .ToArray();
        PostProcessLayer postLayer = camera.GetComponent<PostProcessLayer>();

        Vector3 originalPosition = camera.transform.position;
        Quaternion originalRotation = camera.transform.rotation;
        float originalFieldOfView = camera.fieldOfView;
        float originalNear = camera.nearClipPlane;
        float originalFar = camera.farClipPlane;
        bool originalOrthographic = camera.orthographic;
        CameraClearFlags originalClearFlags = camera.clearFlags;
        Color originalBackground = camera.backgroundColor;
        RenderTexture originalTarget = camera.targetTexture;
        LightShadows originalShadows = sun.shadows;
        int originalShadowCascades = QualitySettings.shadowCascades;
        ShadowProjection originalShadowProjection = QualitySettings.shadowProjection;
        float originalShadowDistance = QualitySettings.shadowDistance;
        bool originalPostEnabled = postLayer != null && postLayer.enabled;
        bool[] probeStates = probes.Select(probe => probe.enabled).ToArray();
        ReflectionProbeUsage[] usages = architecture.Select(renderer => renderer.reflectionProbeUsage).ToArray();

        Vector3 position = new Vector3(16.11f, 7.949057f, -78.91f);
        Quaternion rotationA = Quaternion.Euler(21.896f, 38.36f, 3.142f);
        Quaternion rotationB = Quaternion.Euler(21.938f, 37.483f, 2.815f);
        const int comparisonWidth = 960;
        const int comparisonHeight = 540;
        TestMode[] modes = { TestMode.Baseline, TestMode.NoShadows, TestMode.NoReflections, TestMode.NoPostProcessing, TestMode.ForceLod0, TestMode.SingleStableCascade };
        string outputFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "../", "Library/Level01LightingDiagnostic/ExactRuntime"));
        Directory.CreateDirectory(outputFolder);
        RenderTexture target = new RenderTexture(comparisonWidth, comparisonHeight, 24, RenderTextureFormat.ARGB32);
        Texture2D readback = new Texture2D(comparisonWidth, comparisonHeight, TextureFormat.RGB24, false, false);
        List<string> rows = new List<string>
        {
            "mode,whole_a,whole_b,whole_delta,top_a,top_b,top_delta,bottom_a,bottom_b,bottom_delta,center_a,center_b,center_delta,mean_abs_pixel_delta"
        };

        try
        {
            camera.targetTexture = target;
            camera.orthographic = false;
            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.Skybox;

            foreach (TestMode mode in modes)
            {
                ApplyMode(mode, sun, probes, architecture, postLayer, originalPostEnabled, lodGroups);
                Color32[] pixelsA = RenderExactPose(camera, target, readback, position, rotationA);
                SaveReadback(readback, Path.Combine(outputFolder, mode + "_A.png"));
                Color32[] pixelsB = RenderExactPose(camera, target, readback, position, rotationB);
                SaveReadback(readback, Path.Combine(outputFolder, mode + "_B.png"));

                MeasureRegion(pixelsA, comparisonWidth, comparisonHeight, 0f, 0f, 1f, 1f, out float wholeA);
                MeasureRegion(pixelsB, comparisonWidth, comparisonHeight, 0f, 0f, 1f, 1f, out float wholeB);
                MeasureRegion(pixelsA, comparisonWidth, comparisonHeight, 0f, 0.5f, 1f, 1f, out float topA);
                MeasureRegion(pixelsB, comparisonWidth, comparisonHeight, 0f, 0.5f, 1f, 1f, out float topB);
                MeasureRegion(pixelsA, comparisonWidth, comparisonHeight, 0f, 0f, 1f, 0.5f, out float bottomA);
                MeasureRegion(pixelsB, comparisonWidth, comparisonHeight, 0f, 0f, 1f, 0.5f, out float bottomB);
                MeasureRegion(pixelsA, comparisonWidth, comparisonHeight, 0.25f, 0.25f, 0.75f, 0.75f, out float centerA);
                MeasureRegion(pixelsB, comparisonWidth, comparisonHeight, 0.25f, 0.25f, 0.75f, 0.75f, out float centerB);
                float meanAbsDelta = MeanAbsolutePixelDelta(pixelsA, pixelsB);
                rows.Add(string.Join(",", new[]
                {
                    mode.ToString(),
                    wholeA.ToString("F6"), wholeB.ToString("F6"), Mathf.Abs(wholeA - wholeB).ToString("F6"),
                    topA.ToString("F6"), topB.ToString("F6"), Mathf.Abs(topA - topB).ToString("F6"),
                    bottomA.ToString("F6"), bottomB.ToString("F6"), Mathf.Abs(bottomA - bottomB).ToString("F6"),
                    centerA.ToString("F6"), centerB.ToString("F6"), Mathf.Abs(centerA - centerB).ToString("F6"),
                    meanAbsDelta.ToString("F6")
                }));
                Debug.Log($"[Level01 Lighting Diagnostic] Exact {mode}: whole A/B={wholeA:F4}/{wholeB:F4}, "
                    + $"bottom delta={Mathf.Abs(bottomA - bottomB):F4}, center delta={Mathf.Abs(centerA - centerB):F4}, "
                    + $"mean pixel delta={meanAbsDelta:F4}");
            }
        }
        finally
        {
            camera.transform.position = originalPosition;
            camera.transform.rotation = originalRotation;
            camera.fieldOfView = originalFieldOfView;
            camera.nearClipPlane = originalNear;
            camera.farClipPlane = originalFar;
            camera.orthographic = originalOrthographic;
            camera.clearFlags = originalClearFlags;
            camera.backgroundColor = originalBackground;
            camera.targetTexture = originalTarget;
            sun.shadows = originalShadows;
            QualitySettings.shadowCascades = originalShadowCascades;
            QualitySettings.shadowProjection = originalShadowProjection;
            QualitySettings.shadowDistance = originalShadowDistance;
            if (postLayer != null)
            {
                postLayer.enabled = originalPostEnabled;
            }
            for (int index = 0; index < probes.Length; index++)
            {
                probes[index].enabled = probeStates[index];
            }
            for (int index = 0; index < architecture.Length; index++)
            {
                architecture[index].reflectionProbeUsage = usages[index];
            }
            foreach (LODGroup lodGroup in lodGroups)
            {
                lodGroup.ForceLOD(-1);
            }
            target.Release();
            UnityEngine.Object.DestroyImmediate(readback);
            UnityEngine.Object.DestroyImmediate(target);
        }

        string csvPath = Path.Combine(outputFolder, "comparison.csv");
        File.WriteAllLines(csvPath, rows);
        Debug.Log("[Level01 Lighting Diagnostic] Exact runtime comparison written to " + outputFolder);
    }

    private static Color32[] RenderExactPose(Camera camera, RenderTexture target, Texture2D readback, Vector3 position, Quaternion rotation)
    {
        camera.transform.position = position;
        camera.transform.rotation = rotation;
        camera.Render();
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = target;
        readback.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0, false);
        readback.Apply(false, false);
        RenderTexture.active = previousActive;
        return readback.GetPixels32();
    }

    private static void SaveReadback(Texture2D readback, string path)
    {
        File.WriteAllBytes(path, readback.EncodeToPNG());
    }

    private static void MeasureRegion(Color32[] pixels, int width, int height, float xMin, float yMin, float xMax, float yMax, out float meanLuminance)
    {
        int startX = Mathf.Clamp(Mathf.FloorToInt(width * xMin), 0, width - 1);
        int endX = Mathf.Clamp(Mathf.CeilToInt(width * xMax), startX + 1, width);
        int startY = Mathf.Clamp(Mathf.FloorToInt(height * yMin), 0, height - 1);
        int endY = Mathf.Clamp(Mathf.CeilToInt(height * yMax), startY + 1, height);
        double total = 0d;
        int count = 0;
        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                Color32 pixel = pixels[y * width + x];
                total += (0.2126f * pixel.r + 0.7152f * pixel.g + 0.0722f * pixel.b) / 255f;
                count++;
            }
        }
        meanLuminance = count > 0 ? (float)(total / count) : 0f;
    }

    private static float MeanAbsolutePixelDelta(Color32[] first, Color32[] second)
    {
        double total = 0d;
        int count = Mathf.Min(first.Length, second.Length);
        for (int index = 0; index < count; index++)
        {
            total += (Mathf.Abs(first[index].r - second[index].r)
                + Mathf.Abs(first[index].g - second[index].g)
                + Mathf.Abs(first[index].b - second[index].b)) / (3f * 255f);
        }
        return count > 0 ? (float)(total / count) : 0f;
    }

    private static void Run()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera camera = FindSceneObject(scene, "SYS_MainCamera")?.GetComponent<Camera>();
        Light sun = FindDirectionalLight(scene);
        if (camera == null || sun == null)
        {
            throw new InvalidOperationException("Level01 diagnostic camera or sun was not found.");
        }

        Renderer[] architecture = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
            .Where(renderer => IsArchitecture(renderer.transform))
            .ToArray();
        if (architecture.Length == 0)
        {
            throw new InvalidOperationException("No Level01 architecture renderers were found.");
        }

        Bounds bounds = architecture[0].bounds;
        foreach (Renderer renderer in architecture.Skip(1))
        {
            bounds.Encapsulate(renderer.bounds);
        }

        Vector3 originalPosition = camera.transform.position;
        Quaternion originalRotation = camera.transform.rotation;
        float originalFieldOfView = camera.fieldOfView;
        CameraClearFlags originalClearFlags = camera.clearFlags;
        Color originalBackground = camera.backgroundColor;
        RenderTexture originalTarget = camera.targetTexture;
        LightShadows originalShadows = sun.shadows;
        int originalShadowCascades = QualitySettings.shadowCascades;
        ShadowProjection originalShadowProjection = QualitySettings.shadowProjection;
        float originalShadowDistance = QualitySettings.shadowDistance;
        PostProcessLayer postLayer = camera.GetComponent<PostProcessLayer>();
        bool originalPostEnabled = postLayer != null && postLayer.enabled;
        ReflectionProbe[] probes = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ReflectionProbe>(true))
            .ToArray();
        LODGroup[] lodGroups = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<LODGroup>(true))
            .Where(group => group.gameObject.activeInHierarchy)
            .ToArray();
        bool[] probeStates = probes.Select(probe => probe.enabled).ToArray();
        ReflectionProbeUsage[] usages = architecture
            .Select(renderer => renderer.reflectionProbeUsage)
            .ToArray();

        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        Texture2D readback = new Texture2D(Width, Height, TextureFormat.RGB24, false, true);
        List<string> rows = new List<string> { "mode,angle,mean_luminance,highlight_ratio" };

        try
        {
            camera.targetTexture = target;
            camera.fieldOfView = 58f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);

            foreach (TestMode mode in Enum.GetValues(typeof(TestMode)))
            {
                ApplyMode(mode, sun, probes, architecture, postLayer, originalPostEnabled, lodGroups);
                float maxJump = 0f;
                float previousLuminance = -1f;
                float maxJumpAngle = 0f;
                float radius = Mathf.Max(150f, Mathf.Max(bounds.size.x, bounds.size.z) * 0.58f);
                Vector3 focus = bounds.center + Vector3.up * Mathf.Min(18f, bounds.extents.y * 0.4f);

                for (int index = 0; index < ViewCount; index++)
                {
                    float angle = index * (360f / ViewCount);
                    float radians = angle * Mathf.Deg2Rad;
                    camera.transform.position = focus + new Vector3(
                        Mathf.Sin(radians) * radius,
                        Mathf.Max(65f, bounds.size.y * 1.3f),
                        Mathf.Cos(radians) * radius);
                    camera.transform.rotation = Quaternion.LookRotation(
                        focus - camera.transform.position,
                        Vector3.up);

                    camera.Render();
                    RenderTexture previousActive = RenderTexture.active;
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0, false);
                    readback.Apply(false, false);
                    RenderTexture.active = previousActive;

                    Measure(readback, out float luminance, out float highlights);
                    rows.Add($"{mode},{angle:F1},{luminance:F6},{highlights:F6}");
                    if (previousLuminance >= 0f)
                    {
                        float jump = Mathf.Abs(luminance - previousLuminance);
                        if (jump > maxJump)
                        {
                            maxJump = jump;
                            maxJumpAngle = angle;
                        }
                    }
                    previousLuminance = luminance;
                }

                Debug.Log(
                    $"[Level01 Lighting Diagnostic] {mode}: max adjacent mean-luminance jump="
                    + $"{maxJump:F6} near {maxJumpAngle:F1} degrees.");
            }
        }
        finally
        {
            camera.transform.position = originalPosition;
            camera.transform.rotation = originalRotation;
            camera.fieldOfView = originalFieldOfView;
            camera.clearFlags = originalClearFlags;
            camera.backgroundColor = originalBackground;
            camera.targetTexture = originalTarget;
            sun.shadows = originalShadows;
            QualitySettings.shadowCascades = originalShadowCascades;
            QualitySettings.shadowProjection = originalShadowProjection;
            QualitySettings.shadowDistance = originalShadowDistance;
            if (postLayer != null)
            {
                postLayer.enabled = originalPostEnabled;
            }
            for (int index = 0; index < probes.Length; index++)
            {
                probes[index].enabled = probeStates[index];
            }
            for (int index = 0; index < architecture.Length; index++)
            {
                architecture[index].reflectionProbeUsage = usages[index];
            }
            foreach (LODGroup lodGroup in lodGroups)
            {
                lodGroup.ForceLOD(-1);
            }
            UnityEngine.Object.DestroyImmediate(readback);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }

        string absoluteOutputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../", OutputPath));
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath));
        File.WriteAllLines(absoluteOutputPath, rows);
        Debug.Log("[Level01 Lighting Diagnostic] Results written to " + absoluteOutputPath);
    }

    private static void ApplyMode(
        TestMode mode,
        Light sun,
        ReflectionProbe[] probes,
        Renderer[] architecture,
        PostProcessLayer postLayer,
        bool postEnabled,
        LODGroup[] lodGroups)
    {
        foreach (LODGroup lodGroup in lodGroups)
        {
            lodGroup.ForceLOD(mode == TestMode.ForceLod0 ? 0 : -1);
        }
        QualitySettings.shadowCascades = mode == TestMode.SingleStableCascade ? 0 : QualitySettings.shadowCascades;
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        if (mode == TestMode.SingleStableCascade)
        {
            QualitySettings.shadowDistance = 150f;
        }
        sun.shadows = mode == TestMode.NoShadows ? LightShadows.None : LightShadows.Soft;
        bool reflectionsEnabled = mode != TestMode.NoReflections;
        foreach (ReflectionProbe probe in probes)
        {
            probe.enabled = reflectionsEnabled;
        }
        foreach (Renderer renderer in architecture)
        {
            renderer.reflectionProbeUsage = reflectionsEnabled
                ? ReflectionProbeUsage.BlendProbes
                : ReflectionProbeUsage.Off;
        }
        if (postLayer != null)
        {
            postLayer.enabled = postEnabled && mode != TestMode.NoPostProcessing;
        }
    }

    private static void Measure(Texture2D texture, out float meanLuminance, out float highlightRatio)
    {
        Color32[] pixels = texture.GetPixels32();
        double luminanceTotal = 0d;
        int highlights = 0;
        int samples = 0;
        for (int index = 0; index < pixels.Length; index += 4)
        {
            Color32 pixel = pixels[index];
            float luminance = (0.2126f * pixel.r + 0.7152f * pixel.g + 0.0722f * pixel.b) / 255f;
            luminanceTotal += luminance;
            if (luminance > 0.92f)
            {
                highlights++;
            }
            samples++;
        }
        meanLuminance = samples > 0 ? (float)(luminanceTotal / samples) : 0f;
        highlightRatio = samples > 0 ? (float)highlights / samples : 0f;
    }

    private static bool IsArchitecture(Transform transform)
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

    private static Light FindDirectionalLight(Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<Light>()
            .FirstOrDefault(light => light.gameObject.scene == scene
                && light.type == LightType.Directional
                && light.isActiveAndEnabled);
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(gameObject => gameObject.scene == scene && gameObject.name == objectName);
    }
}
