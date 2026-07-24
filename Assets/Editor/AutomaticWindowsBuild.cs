using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

[InitializeOnLoad]
public static class AutomaticWindowsBuild
{
    private const string MenuPath = "Tools/Island Map/Windows Build/Automatic Build";
    private const string PendingKey = "IslandMapPrototype.AutoBuild.Pending";
    private const string BuildAfterKey = "IslandMapPrototype.AutoBuild.AfterUtcTicks";
    private const double DebounceSeconds = 4d;

    private static readonly string EnabledKey =
        "IslandMapPrototype.AutoBuild.Enabled." + Application.dataPath;

    static AutomaticWindowsBuild()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        EditorApplication.delayCall += ScheduleBuildWhenStale;
    }

    public static bool IsEnabled
    {
        get => EditorPrefs.GetBool(EnabledKey, true);
        private set => EditorPrefs.SetBool(EnabledKey, value);
    }

    [MenuItem(MenuPath)]
    private static void ToggleAutomaticBuild()
    {
        IsEnabled = !IsEnabled;
        Menu.SetChecked(MenuPath, IsEnabled);
        if (IsEnabled)
        {
            ScheduleBuildWhenStale();
        }
        else
        {
            SessionState.EraseBool(PendingKey);
            SessionState.EraseString(BuildAfterKey);
        }

        Debug.Log($"Automatic Windows build {(IsEnabled ? "enabled" : "disabled")}.");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateAutomaticBuild()
    {
        Menu.SetChecked(MenuPath, IsEnabled);
        return true;
    }

    internal static void RequestBuild()
    {
        if (!IsEnabled)
        {
            return;
        }

        long buildAfterTicks = DateTime.UtcNow.AddSeconds(DebounceSeconds).Ticks;
        SessionState.SetBool(PendingKey, true);
        SessionState.SetString(BuildAfterKey, buildAfterTicks.ToString());
    }

    private static void ScheduleBuildWhenStale()
    {
        if (IsEnabled && IsBuildStale())
        {
            RequestBuild();
        }
    }

    private static void Update()
    {
        if (!IsEnabled || !SessionState.GetBool(PendingKey, false))
        {
            return;
        }
        if (EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode
            || BuildPipeline.isBuildingPlayer)
        {
            return;
        }
        if (!HasDebounceElapsed())
        {
            return;
        }

        SessionState.EraseBool(PendingKey);
        SessionState.EraseString(BuildAfterKey);
        try
        {
            Debug.Log("Project files changed. Updating the automatic Windows build...");
            WindowsBuildExporter.BuildWindows();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static bool HasDebounceElapsed()
    {
        string ticksText = SessionState.GetString(BuildAfterKey, "0");
        return !long.TryParse(ticksText, out long buildAfterTicks)
            || DateTime.UtcNow.Ticks >= buildAfterTicks;
    }

    private static bool IsBuildStale()
    {
        if (!File.Exists(WindowsBuildExporter.ExecutablePath))
        {
            return true;
        }

        DateTime buildTime = File.GetLastWriteTimeUtc(WindowsBuildExporter.ExecutablePath);
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        IEnumerable<string> trackedRoots = new[]
        {
            Application.dataPath,
            Path.Combine(projectRoot, "Packages"),
            Path.Combine(projectRoot, "ProjectSettings")
        };

        foreach (string root in trackedRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (File.GetLastWriteTimeUtc(file) > buildTime)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

public sealed class AutomaticWindowsBuildAssetPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (importedAssets.Length > 0
            || deletedAssets.Length > 0
            || movedAssets.Length > 0
            || movedFromAssetPaths.Length > 0)
        {
            AutomaticWindowsBuild.RequestBuild();
        }
    }
}
