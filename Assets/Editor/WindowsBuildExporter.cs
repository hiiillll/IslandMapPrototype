using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WindowsBuildExporter
{
    public static string BuildDirectory => Path.Combine(
        Directory.GetParent(Application.dataPath).FullName,
        "Builds",
        "SpeedEscape_Windows");

    public static string ExecutablePath => Path.Combine(BuildDirectory, "SpeedEscape.exe");

    [MenuItem("Tools/Island Map/Windows Build/Build Now")]
    public static void BuildWindows()
    {
        Directory.CreateDirectory(BuildDirectory);
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled scenes are configured for the build.");
        }

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = ExecutablePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"Windows build failed: {report.summary.result}");
        }

        Debug.Log($"Windows build completed: {ExecutablePath}");
    }

    [MenuItem("Tools/Island Map/Windows Build/Open Build Folder")]
    private static void OpenBuildFolder()
    {
        Directory.CreateDirectory(BuildDirectory);
        EditorUtility.RevealInFinder(BuildDirectory);
    }
}
