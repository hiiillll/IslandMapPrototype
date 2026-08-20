using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

/// <summary>
/// Automated, UI-free Level 01 render sampling. This component is installed
/// only when the standalone player is launched with -renderAudit.
/// </summary>
public sealed class Level01RenderAuditCapture : MonoBehaviour
{
    private const string AuditArgument = "-renderAudit";
    private const string OutputArgument = "-renderAuditOutput";
    private const string Level01SceneName = "IslandMap";
    private const int CaptureWidth = 1920;
    private const int CaptureHeight = 1080;
    private const int TideSequenceFrameCount = 6;

    private sealed class AuditView
    {
        public readonly string FileName;
        public readonly Vector3 Position;
        public readonly Vector3 Target;
        public readonly float FieldOfView;

        public AuditView(
            string fileName,
            Vector3 position,
            Vector3 target,
            float fieldOfView)
        {
            FileName = fileName;
            Position = position;
            Target = target;
            FieldOfView = fieldOfView;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallWhenRequested()
    {
        if (!HasCommandLineArgument(AuditArgument))
        {
            return;
        }

        GameObject auditObject = new GameObject("SYS_Level01RenderAuditCapture");
        DontDestroyOnLoad(auditObject);
        auditObject.AddComponent<Level01RenderAuditCapture>();
    }

    private IEnumerator Start()
    {
        // The audit player is intentionally launched hidden and unfocused.
        Application.runInBackground = true;
        string outputDirectory = ResolveOutputDirectory();
        Directory.CreateDirectory(outputDirectory);
        Debug.Log(
            $"[Render Audit] Started. Active scene: "
            + $"'{SceneManager.GetActiveScene().name}', output: {outputDirectory}");

        if (SceneManager.GetActiveScene().name != Level01SceneName)
        {
            Debug.Log($"[Render Audit] Loading scene '{Level01SceneName}'.");
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(Level01SceneName);
            while (loadOperation != null && !loadOperation.isDone)
            {
                yield return null;
            }
            Debug.Log($"[Render Audit] Scene load finished: '{SceneManager.GetActiveScene().name}'.");
        }

        // Let scene bootstraps, materials, post processing and LOD guards settle.
        for (int frame = 0; frame < 8; frame++)
        {
            yield return null;
        }

        Camera auditCamera = FindMainCamera();
        if (auditCamera == null)
        {
            WriteFailure(outputDirectory, "The Level 01 MainCamera could not be found.");
            Application.Quit(2);
            yield break;
        }

        if (QualitySettings.names.Length > 0)
        {
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
        }

        Debug.Log("[Render Audit] Writing configuration report.");
        WriteReport(outputDirectory, auditCamera);
        Debug.Log("[Render Audit] Configuration report written.");
        DisableMotionBlurForTeleportedAuditCamera();

        bool previousCameraEnabled = auditCamera.enabled;
        RenderTexture previousTargetTexture = auditCamera.targetTexture;
        Vector3 previousPosition = auditCamera.transform.position;
        Quaternion previousRotation = auditCamera.transform.rotation;
        float previousFieldOfView = auditCamera.fieldOfView;
        float previousAspect = auditCamera.aspect;
        List<Canvas> canvases = FindObjectsOfType<Canvas>(true).ToList();
        bool[] canvasStates = canvases.Select(canvas => canvas.enabled).ToArray();
        List<MonoBehaviour> cameraControllers = auditCamera
            .GetComponents<MonoBehaviour>()
            .Where(component => component != null
                && component.enabled
                && component.GetType().FullName != null
                && !component.GetType().FullName.StartsWith(
                    "UnityEngine.Rendering.PostProcessing.",
                    StringComparison.Ordinal))
            .ToList();

        for (int index = 0; index < canvases.Count; index++)
        {
            canvases[index].enabled = false;
        }

        foreach (MonoBehaviour controller in cameraControllers)
        {
            controller.enabled = false;
        }

        auditCamera.enabled = false;
        yield return MeasureRepresentativePerformance(auditCamera, outputDirectory);
        foreach (AuditView view in CreateAuditViews())
        {
            Debug.Log($"[Render Audit] Capturing {view.FileName}.");
            auditCamera.transform.SetPositionAndRotation(
                view.Position,
                Quaternion.LookRotation(
                    (view.Target - view.Position).normalized,
                    Vector3.up));
            auditCamera.fieldOfView = view.FieldOfView;

            // Two frames allow camera-dependent effects and reflection state to settle.
            yield return null;
            yield return null;
            CaptureCamera(auditCamera, Path.Combine(outputDirectory, view.FileName));
        }

        // Keep one shoreline camera fixed while real game time advances. These
        // frames verify that the shared water/sand tide actually advances and
        // retreats instead of merely changing isolated highlights.
        AuditView tideView = new AuditView(
            "09_shore_tide.png",
            new Vector3(124f, 18f, 24f),
            new Vector3(149f, 0.1f, 40f),
            42f);
        auditCamera.transform.SetPositionAndRotation(
            tideView.Position,
            Quaternion.LookRotation(
                (tideView.Target - tideView.Position).normalized,
                Vector3.up));
        auditCamera.fieldOfView = tideView.FieldOfView;
        float previousTimeScale = Time.timeScale;
        // Level 01 is normally paused behind its menu when this headless audit
        // enters the scene. Shader _Time follows scaled game time, so force it
        // to advance while sampling the tide exactly as it does during play.
        Time.timeScale = 1f;
        for (int index = 0; index < TideSequenceFrameCount; index++)
        {
            yield return new WaitForSecondsRealtime(0.7f);
            string sequenceName = $"09_shore_tide_{index + 1:00}.png";
            Debug.Log($"[Render Audit] Capturing {sequenceName}.");
            CaptureCamera(auditCamera, Path.Combine(outputDirectory, sequenceName));
        }
        Time.timeScale = previousTimeScale;

        auditCamera.targetTexture = previousTargetTexture;
        auditCamera.transform.SetPositionAndRotation(previousPosition, previousRotation);
        auditCamera.fieldOfView = previousFieldOfView;
        auditCamera.aspect = previousAspect;
        auditCamera.enabled = previousCameraEnabled;
        foreach (MonoBehaviour controller in cameraControllers)
        {
            if (controller != null)
            {
                controller.enabled = true;
            }
        }
        for (int index = 0; index < canvases.Count; index++)
        {
            if (canvases[index] != null)
            {
                canvases[index].enabled = canvasStates[index];
            }
        }

        File.WriteAllText(
            Path.Combine(outputDirectory, "CAPTURE_COMPLETE.txt"),
            $"Captured {CreateAuditViews().Count + TideSequenceFrameCount} Level 01 views at "
            + $"{CaptureWidth}x{CaptureHeight}.\n",
            Encoding.UTF8);
        Debug.Log($"[Render Audit] Capture complete: {outputDirectory}");
        Application.Quit(0);
    }

    private static List<AuditView> CreateAuditViews()
    {
        Vector3 player = new Vector3(-66.16f, 0.08f, -98.5f);
        return new List<AuditView>
        {
            new AuditView(
                "01_spawn_third_person.png",
                player + new Vector3(-18f, 7.5f, 0f),
                player + new Vector3(14f, 1.6f, 0f),
                62f),
            new AuditView(
                "02_spawn_elevated.png",
                player + new Vector3(-4f, 30f, -22f),
                player + new Vector3(12f, 0f, 8f),
                55f),
            new AuditView(
                "03_center_boulevard.png",
                new Vector3(0f, 8f, -118f),
                new Vector3(0f, 2.5f, 95f),
                58f),
            new AuditView(
                "04_beach_to_ocean.png",
                new Vector3(12f, 5.5f, 112f),
                new Vector3(20f, 0f, 172f),
                60f),
            new AuditView(
                "05_ocean_to_island.png",
                new Vector3(18f, 11f, 195f),
                new Vector3(5f, 3f, 105f),
                56f),
            new AuditView(
                "06_shoreline_diagonal.png",
                new Vector3(112f, 10f, 118f),
                new Vector3(158f, 0f, 158f),
                58f),
            new AuditView(
                "07_city_reverse_light.png",
                new Vector3(108f, 15f, 15f),
                new Vector3(-70f, 5f, -30f),
                56f),
            new AuditView(
                "08_island_overview.png",
                new Vector3(-215f, 165f, -215f),
                new Vector3(0f, 0f, 0f),
                50f)
        };
    }

    private static void CaptureCamera(Camera camera, string path)
    {
        RenderTexture renderTexture = new RenderTexture(
            CaptureWidth,
            CaptureHeight,
            24,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default);
        renderTexture.name = "RenderAuditTarget";
        renderTexture.antiAliasing = 1;
        renderTexture.Create();

        RenderTexture previousActive = RenderTexture.active;
        camera.targetTexture = renderTexture;
        camera.aspect = (float)CaptureWidth / CaptureHeight;
        camera.Render();

        RenderTexture.active = renderTexture;
        Texture2D image = new Texture2D(
            CaptureWidth,
            CaptureHeight,
            TextureFormat.RGB24,
            false,
            false);
        image.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0);
        image.Apply(false, false);
        File.WriteAllBytes(path, image.EncodeToPNG());

        camera.targetTexture = null;
        RenderTexture.active = previousActive;
        Destroy(image);
        renderTexture.Release();
        Destroy(renderTexture);
    }

    private static void WriteReport(string outputDirectory, Camera camera)
    {
        StringBuilder report = new StringBuilder(8192);
        report.AppendLine("LEVEL 01 RENDER AUDIT");
        report.AppendLine($"Captured UTC: {DateTime.UtcNow:O}");
        report.AppendLine($"Unity: {Application.unityVersion}");
        report.AppendLine($"Scene: {SceneManager.GetActiveScene().name}");
        report.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
        report.AppendLine($"GPU API: {SystemInfo.graphicsDeviceType}");
        report.AppendLine($"VRAM: {SystemInfo.graphicsMemorySize} MB");
        report.AppendLine($"CPU: {SystemInfo.processorType} ({SystemInfo.processorCount} threads)");
        report.AppendLine($"System memory: {SystemInfo.systemMemorySize} MB");
        report.AppendLine();

        report.AppendLine("QUALITY");
        report.AppendLine($"Quality level: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        report.AppendLine($"LOD bias: {QualitySettings.lodBias}");
        report.AppendLine($"Texture mip limit: {QualitySettings.globalTextureMipmapLimit}");
        report.AppendLine($"Anisotropic filtering: {QualitySettings.anisotropicFiltering}");
        report.AppendLine($"Anti-aliasing samples: {QualitySettings.antiAliasing}");
        report.AppendLine($"Shadow distance: {QualitySettings.shadowDistance}");
        report.AppendLine($"Shadow resolution: {QualitySettings.shadowResolution}");
        report.AppendLine($"Shadow cascades: {QualitySettings.shadowCascades}");
        report.AppendLine($"VSync: {QualitySettings.vSyncCount}");
        report.AppendLine();

        report.AppendLine("CAMERA");
        report.AppendLine($"Name: {camera.name}");
        report.AppendLine($"FOV: {camera.fieldOfView}");
        report.AppendLine($"Clip planes: {camera.nearClipPlane} / {camera.farClipPlane}");
        report.AppendLine($"HDR: {camera.allowHDR}");
        report.AppendLine($"MSAA: {camera.allowMSAA}");
        report.AppendLine($"Occlusion culling: {camera.useOcclusionCulling}");
        report.AppendLine($"Depth mode: {camera.depthTextureMode}");
        report.AppendLine("Camera components:");
        foreach (Component component in camera.GetComponents<Component>())
        {
            if (component != null)
            {
                report.AppendLine($"  - {component.GetType().FullName}");
            }
        }
        report.AppendLine();

        report.AppendLine("ENVIRONMENT");
        report.AppendLine($"Fog: {RenderSettings.fog}");
        report.AppendLine($"Fog mode: {RenderSettings.fogMode}");
        report.AppendLine($"Fog color: {RenderSettings.fogColor}");
        report.AppendLine($"Fog linear range: {RenderSettings.fogStartDistance} / {RenderSettings.fogEndDistance}");
        report.AppendLine($"Ambient mode: {RenderSettings.ambientMode}");
        report.AppendLine($"Ambient intensity: {RenderSettings.ambientIntensity}");
        report.AppendLine($"Reflection intensity: {RenderSettings.reflectionIntensity}");
        report.AppendLine($"Reflection bounces: {RenderSettings.reflectionBounces}");
        report.AppendLine($"Skybox: {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "None")}");
        report.AppendLine();

        Light[] lights = FindObjectsOfType<Light>(true);
        report.AppendLine($"LIGHTS ({lights.Length})");
        foreach (Light light in lights.OrderBy(item => item.type).ThenBy(item => item.name))
        {
            report.AppendLine(
                $"- {light.name}: {light.type}, enabled={light.enabled}, "
                + $"intensity={light.intensity}, color={light.color}, "
                + $"shadows={light.shadows}, shadowStrength={light.shadowStrength}, "
                + $"range={light.range}");
        }
        report.AppendLine();

        Renderer[] renderers = FindObjectsOfType<Renderer>(true);
        LODGroup[] lodGroups = FindObjectsOfType<LODGroup>(true);
        report.AppendLine("SCENE COMPLEXITY");
        report.AppendLine($"Renderers: {renderers.Length}");
        report.AppendLine($"Enabled renderers: {renderers.Count(renderer => renderer.enabled)}");
        report.AppendLine($"LOD groups: {lodGroups.Length}");
        report.AppendLine($"Total LOD levels: {lodGroups.Sum(group => group.lodCount)}");

        Dictionary<string, int> shaderUsage = new Dictionary<string, int>();
        HashSet<Material> materials = new HashSet<Material>();
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                {
                    continue;
                }

                materials.Add(material);
                string shaderName = material.shader != null ? material.shader.name : "Missing Shader";
                shaderUsage[shaderName] = shaderUsage.TryGetValue(shaderName, out int count)
                    ? count + 1
                    : 1;
            }
        }

        report.AppendLine($"Unique materials: {materials.Count}");
        report.AppendLine("Shader usage by material slot:");
        foreach (KeyValuePair<string, int> item in shaderUsage.OrderByDescending(item => item.Value))
        {
            report.AppendLine($"  {item.Value,5}  {item.Key}");
        }

        File.WriteAllText(
            Path.Combine(outputDirectory, "RenderAuditReport.txt"),
            report.ToString(),
            Encoding.UTF8);
    }

    private static IEnumerator MeasureRepresentativePerformance(
        Camera camera,
        string outputDirectory)
    {
        int previousTargetFrameRate = Application.targetFrameRate;
        int previousVSync = QualitySettings.vSyncCount;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 100;
        camera.transform.SetPositionAndRotation(
            new Vector3(-84f, 7.5f, -98.5f),
            Quaternion.LookRotation(new Vector3(1f, -0.18f, 0f).normalized, Vector3.up));
        camera.fieldOfView = 62f;
        camera.enabled = true;

        // Warm shader variants, post effects and texture residency before the
        // sample so first-frame compilation does not distort the play result.
        for (int index = 0; index < 40; index++)
        {
            yield return null;
        }

        const float sampleDuration = 4f;
        float elapsed = 0f;
        float worstFrame = 0f;
        int frames = 0;
        while (elapsed < sampleDuration)
        {
            yield return null;
            float delta = Mathf.Max(Time.unscaledDeltaTime, 0.000001f);
            elapsed += delta;
            worstFrame = Mathf.Max(worstFrame, delta);
            frames++;
        }

        camera.enabled = false;
        Application.targetFrameRate = previousTargetFrameRate;
        QualitySettings.vSyncCount = previousVSync;
        float averageFps = frames / Mathf.Max(elapsed, 0.000001f);
        string performance = Environment.NewLine
            + "PERFORMANCE SAMPLE" + Environment.NewLine
            + $"Representative 1080p frames: {frames}" + Environment.NewLine
            + $"Elapsed realtime: {elapsed:F3} s" + Environment.NewLine
            + $"Average FPS: {averageFps:F1}" + Environment.NewLine
            + $"Average frame: {(elapsed / Mathf.Max(frames, 1)) * 1000f:F2} ms" + Environment.NewLine
            + $"Worst sampled frame: {worstFrame * 1000f:F2} ms" + Environment.NewLine;
        File.AppendAllText(
            Path.Combine(outputDirectory, "RenderAuditReport.txt"),
            performance,
            Encoding.UTF8);
        Debug.Log($"[Render Audit] Representative performance: {averageFps:F1} FPS.");
    }

    private static void DisableMotionBlurForTeleportedAuditCamera()
    {
        foreach (PostProcessVolume volume in FindObjectsOfType<PostProcessVolume>(true))
        {
            PostProcessProfile profile = volume.profile;
            if (profile != null && profile.TryGetSettings(out MotionBlur motionBlur))
            {
                motionBlur.enabled.Override(false);
            }
        }

        Debug.Log("[Render Audit] Motion blur disabled for fixed-camera sampling.");
    }

    private static Camera FindMainCamera()
    {
        Camera taggedCamera = Camera.main;
        if (taggedCamera != null)
        {
            return taggedCamera;
        }

        return FindObjectsOfType<Camera>(true)
            .FirstOrDefault(camera => camera.gameObject.scene.name == Level01SceneName);
    }

    private static string ResolveOutputDirectory()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], OutputArgument, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(arguments[index + 1]);
            }
        }

        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "RenderAudit"));
    }

    private static bool HasCommandLineArgument(string targetArgument)
    {
        return Environment.GetCommandLineArgs().Any(
            argument => string.Equals(
                argument,
                targetArgument,
                StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteFailure(string outputDirectory, string message)
    {
        File.WriteAllText(
            Path.Combine(outputDirectory, "CAPTURE_FAILED.txt"),
            message,
            Encoding.UTF8);
        Debug.LogError($"[Render Audit] {message}");
    }
}
