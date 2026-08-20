using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Standalone Level 02 visual and jump verification. Installed only when the
/// player is launched with -level02RenderAudit.
/// </summary>
public sealed class Level02RenderAuditCapture : MonoBehaviour
{
    private const string AuditArgument = "-level02RenderAudit";
    private const string OutputArgument = "-level02RenderAuditOutput";
    private const string SceneName = "Level02";
    private const int CaptureWidth = 1920;
    private const int CaptureHeight = 1080;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallWhenRequested()
    {
        if (!Environment.GetCommandLineArgs().Contains(AuditArgument))
        {
            return;
        }

        GameObject auditObject = new GameObject("SYS_Level02RenderAuditCapture");
        DontDestroyOnLoad(auditObject);
        auditObject.AddComponent<Level02RenderAuditCapture>();
    }

    private IEnumerator Start()
    {
        Application.runInBackground = true;
        Time.timeScale = 1f;
        string outputDirectory = ResolveOutputDirectory();
        Directory.CreateDirectory(outputDirectory);

        if (SceneManager.GetActiveScene().name != SceneName)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName);
            while (load != null && !load.isDone)
            {
                yield return null;
            }
        }

        for (int frame = 0; frame < 12; frame++)
        {
            yield return null;
        }

        BoatChaseController player = FindObjectOfType<BoatChaseController>();
        Camera camera = Camera.main;
        if (player == null || camera == null)
        {
            File.WriteAllText(
                Path.Combine(outputDirectory, "AUDIT_FAILED.txt"),
                "Level 02 player boat or MainCamera was not found.",
                Encoding.UTF8);
            Application.Quit(2);
            yield break;
        }

        foreach (Canvas canvas in FindObjectsOfType<Canvas>(true))
        {
            canvas.enabled = false;
        }

        BoatChaseTopDownCamera cameraController = camera.GetComponent<BoatChaseTopDownCamera>();
        if (cameraController != null)
        {
            cameraController.enabled = false;
        }

        if (QualitySettings.names.Length > 0)
        {
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
        }
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 100;

        Rigidbody body = player.GetComponent<Rigidbody>();
        float waterlineY = body.position.y;
        SetChaseView(camera, player.transform);
        for (int frame = 0; frame < 50; frame++)
        {
            SetChaseView(camera, player.transform);
            yield return null;
        }

        CaptureCamera(camera, Path.Combine(outputDirectory, "01_ocean_chase.png"));

        PerformanceSample performance = new PerformanceSample();
        // The PNG readback above stalls one frame. Do not attribute that
        // diagnostic-only stall to normal gameplay rendering performance.
        for (int frame = 0; frame < 12; frame++)
        {
            SetChaseView(camera, player.transform);
            yield return null;
        }
        float performanceStartedAt = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - performanceStartedAt < 3f)
        {
            SetChaseView(camera, player.transform);
            performance.Add(Time.unscaledDeltaTime);
            yield return null;
        }

        player.QueueJump();
        bool capturedAscent = false;
        bool capturedApex = false;
        float maxY = body.position.y;
        float jumpStartedAt = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - jumpStartedAt < 3f)
        {
            SetChaseView(camera, player.transform);
            maxY = Mathf.Max(maxY, body.position.y);

            if (player.IsJumping && !capturedAscent && body.position.y > waterlineY + 0.55f)
            {
                CaptureCamera(camera, Path.Combine(outputDirectory, "02_jump_ascent.png"));
                capturedAscent = true;
            }

            if (player.IsJumping && !capturedApex && body.velocity.y <= 0f)
            {
                CaptureCamera(camera, Path.Combine(outputDirectory, "03_jump_apex.png"));
                capturedApex = true;
            }

            if (capturedAscent && !player.IsJumping)
            {
                break;
            }
            yield return null;
        }

        for (int frame = 0; frame < 8; frame++)
        {
            SetChaseView(camera, player.transform);
            yield return null;
        }
        CaptureCamera(camera, Path.Combine(outputDirectory, "04_jump_landing.png"));

        SetElevatedView(camera, player.transform);
        yield return null;
        CaptureCamera(camera, Path.Combine(outputDirectory, "05_ocean_elevated.png"));

        float landedY = body.position.y;
        bool jumpPassed = capturedAscent
            && capturedApex
            && !player.IsJumping
            && maxY >= waterlineY + 1.5f
            && Mathf.Abs(landedY - waterlineY) <= 0.04f;

        StringBuilder report = new StringBuilder();
        report.AppendLine("LEVEL 02 RENDER AND JUMP AUDIT");
        report.AppendLine($"Captured UTC: {DateTime.UtcNow:O}");
        report.AppendLine($"Unity: {Application.unityVersion}");
        report.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
        report.AppendLine($"Resolution: {CaptureWidth}x{CaptureHeight}");
        report.AppendLine($"Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        report.AppendLine($"Camera: {camera.name}");
        report.AppendLine($"Camera clip planes: {camera.nearClipPlane:F2} / {camera.farClipPlane:F0}");
        report.AppendLine($"Fog range: {RenderSettings.fogStartDistance:F0} / {RenderSettings.fogEndDistance:F0}");
        report.AppendLine();
        report.AppendLine("100 FPS TARGET SAMPLE");
        report.AppendLine($"Frames: {performance.FrameCount}");
        report.AppendLine($"Average FPS: {performance.AverageFps:F1}");
        report.AppendLine($"Average frame: {performance.AverageMilliseconds:F2} ms");
        report.AppendLine($"Worst frame: {performance.WorstMilliseconds:F2} ms");
        report.AppendLine();
        report.AppendLine("SPACE JUMP");
        report.AppendLine($"Waterline Y: {waterlineY:F3}");
        report.AppendLine($"Maximum Y: {maxY:F3}");
        report.AppendLine($"Jump height: {maxY - waterlineY:F3}");
        report.AppendLine($"Landed Y: {landedY:F3}");
        report.AppendLine($"Ascent captured: {capturedAscent}");
        report.AppendLine($"Apex captured: {capturedApex}");
        report.AppendLine($"Result: {(jumpPassed ? "PASS" : "FAIL")}");
        File.WriteAllText(
            Path.Combine(outputDirectory, "Level02AuditReport.txt"),
            report.ToString(),
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(outputDirectory, jumpPassed ? "AUDIT_PASS.txt" : "AUDIT_FAILED.txt"),
            jumpPassed ? "Level 02 render capture and Space jump passed." : "Space jump audit failed.",
            Encoding.UTF8);

        Debug.Log($"[Level02 Audit] Complete. Jump result: {(jumpPassed ? "PASS" : "FAIL")}");
        Application.Quit(jumpPassed ? 0 : 3);
    }

    private static void SetChaseView(Camera camera, Transform player)
    {
        Vector3 position = player.position - player.forward * 15.5f + Vector3.up * 6.2f;
        Vector3 target = player.position + player.forward * 6.2f + Vector3.up * 1.15f;
        camera.transform.SetPositionAndRotation(
            position,
            Quaternion.LookRotation((target - position).normalized, Vector3.up));
        camera.fieldOfView = 59f;
        camera.farClipPlane = 1600f;
    }

    private static void SetElevatedView(Camera camera, Transform player)
    {
        Vector3 position = player.position - player.forward * 32f + Vector3.up * 22f;
        Vector3 target = player.position + player.forward * 16f;
        camera.transform.SetPositionAndRotation(
            position,
            Quaternion.LookRotation((target - position).normalized, Vector3.up));
        camera.fieldOfView = 58f;
    }

    private static void CaptureCamera(Camera camera, string path)
    {
        RenderTexture target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        float previousAspect = camera.aspect;
        target.Create();
        camera.targetTexture = target;
        camera.aspect = (float)CaptureWidth / CaptureHeight;
        camera.Render();
        RenderTexture.active = target;
        Texture2D image = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0);
        image.Apply(false, false);
        FlipVertically(image);
        File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = previousTarget;
        camera.aspect = previousAspect;
        RenderTexture.active = previousActive;
        Destroy(image);
        target.Release();
        Destroy(target);
    }

    private static void FlipVertically(Texture2D image)
    {
        Color32[] pixels = image.GetPixels32();
        int rowLength = image.width;
        int halfHeight = image.height / 2;
        for (int row = 0; row < halfHeight; row++)
        {
            int oppositeRow = image.height - row - 1;
            int topStart = row * rowLength;
            int bottomStart = oppositeRow * rowLength;
            for (int column = 0; column < rowLength; column++)
            {
                Color32 swap = pixels[topStart + column];
                pixels[topStart + column] = pixels[bottomStart + column];
                pixels[bottomStart + column] = swap;
            }
        }
        image.SetPixels32(pixels);
        image.Apply(false, false);
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
        return Path.GetFullPath(Path.Combine(Application.dataPath, "../Level02RenderAudit"));
    }

    private sealed class PerformanceSample
    {
        private float totalSeconds;
        private float worstSeconds;

        public int FrameCount { get; private set; }
        public float AverageFps => totalSeconds > 0f ? FrameCount / totalSeconds : 0f;
        public float AverageMilliseconds => FrameCount > 0 ? totalSeconds * 1000f / FrameCount : 0f;
        public float WorstMilliseconds => worstSeconds * 1000f;

        public void Add(float deltaTime)
        {
            if (deltaTime <= 0f || deltaTime > 0.25f)
            {
                return;
            }
            FrameCount++;
            totalSeconds += deltaTime;
            worstSeconds = Mathf.Max(worstSeconds, deltaTime);
        }
    }
}
