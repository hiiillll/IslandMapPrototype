using System.Collections;
using UnityEngine;

/// <summary>
/// Applies the quality-first standalone baseline on every Windows launch:
/// Ultra quality, true 1080p fullscreen, and a stable 100 FPS cap.
/// </summary>
public sealed class NativeFullscreenBootstrap : MonoBehaviour
{
    private const int TargetFrameRate = 100;
    private const int TargetWidth = 1920;
    private const int TargetHeight = 1080;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        foreach (string argument in System.Environment.GetCommandLineArgs())
        {
            if (string.Equals(
                argument,
                "-renderAudit",
                System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        GameObject bootstrapObject = new GameObject("SYS_NativeFullscreenBootstrap");
        DontDestroyOnLoad(bootstrapObject);
        bootstrapObject.AddComponent<NativeFullscreenBootstrap>();
#endif
    }

    private IEnumerator Start()
    {
        ApplyQualityFirstRuntimeProfile();
        ApplyNativeFullscreen();
        yield return null;
        // Reapply after Unity has restored any saved standalone preferences.
        ApplyQualityFirstRuntimeProfile();
        ApplyNativeFullscreen();
        yield return null;
        Debug.Log(
            $"[Display] Quality-first profile applied: "
            + $"{Screen.width}x{Screen.height}, mode {Screen.fullScreenMode}, "
            + $"quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, "
            + $"target {Application.targetFrameRate} FPS, "
            + $"VSync {QualitySettings.vSyncCount}.");
    }

    private static void ApplyQualityFirstRuntimeProfile()
    {
        int highestQualityLevel = QualitySettings.names.Length - 1;
        if (highestQualityLevel >= 0 &&
            QualitySettings.GetQualityLevel() != highestQualityLevel)
        {
            QualitySettings.SetQualityLevel(highestQualityLevel, true);
        }

        // Application.targetFrameRate is ignored while VSync is enabled.
        QualitySettings.vSyncCount = 0;
        QualitySettings.globalTextureMipmapLimit = 0;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        Application.targetFrameRate = TargetFrameRate;
    }

    private static void ApplyNativeFullscreen()
    {
        Screen.SetResolution(
            TargetWidth,
            TargetHeight,
            // FullScreenWindow always uses the desktop's native resolution on
            // Windows (2560x1440 on the target machine), even when 1920x1080 is
            // requested. Exclusive fullscreen is required for a real 1080p
            // back buffer while still filling the display.
            FullScreenMode.ExclusiveFullScreen);
    }
}
