using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WindowsBuildExporter
{
    public static void BuildWindows()
    {
        string projectDirectory = Directory.GetParent(Application.dataPath).FullName;
        string buildDirectory = Path.Combine(projectDirectory, "Builds", "SpeedEscape_Windows");
        Directory.CreateDirectory(buildDirectory);

        string executablePath = Path.Combine(buildDirectory, "SpeedEscape.exe");
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
            locationPathName = executablePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"Windows build failed: {report.summary.result}");
        }

        Debug.Log($"Windows build completed: {executablePath}");
    }
}
