using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps Level03's Terrain tiles as the authoritative land surface by disabling
/// only the legacy generated land mesh. Roads, ocean, and other environment
/// children are intentionally left untouched.
/// </summary>
[InitializeOnLoad]
public static class Level03TerrainAuthoringSetup
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string LegacyLandName = "ENV_Level03_FlatIslands_And_MainMountain";
    private const string ConvertedTerrainRootName = "ENV_Level03_ConvertedTerrain";
    private const string RequestAssetPath = "Assets/Editor/Level03TerrainAuthoringSetup.request";
    private const string ReportPath = "Library/CodexLevel03TerrainAuthoringReport.json";

    [Serializable]
    private sealed class SetupReport
    {
        public bool success;
        public string message;
        public bool meshRendererDisabled;
        public bool meshColliderDisabled;
        public string completedAt;
    }

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    private static string RequestFilePath => Path.Combine(ProjectRoot, RequestAssetPath);
    private static string ReportFilePath => Path.Combine(ProjectRoot, ReportPath);

    static Level03TerrainAuthoringSetup()
    {
        if (File.Exists(RequestFilePath))
        {
            EditorApplication.delayCall += ProcessOneShotRequest;
        }
    }

    [MenuItem("Tools/Island Map/Level03/Use Terrain As Main Land Surface")]
    public static void UseTerrainAsMainLandSurfaceFromMenu()
    {
        try
        {
            SetupReport report = Apply();
            WriteReport(report);
            EditorUtility.DisplayDialog("Level03 Terrain Authoring", report.message, "OK");
        }
        catch (Exception exception)
        {
            WriteFailureReport(exception);
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Level03 Terrain Authoring", exception.Message, "OK");
        }
    }

    [MenuItem("Tools/Island Map/Level03/Restore Original Mountain Land Mesh")]
    public static void RestoreOriginalMountainLandMeshFromMenu()
    {
        try
        {
            SetupReport report = RestoreOriginalMountainLandMesh();
            WriteReport(report);
            EditorUtility.DisplayDialog("Level03 Terrain Authoring", report.message, "OK");
        }
        catch (Exception exception)
        {
            WriteFailureReport(exception);
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Level03 Terrain Authoring", exception.Message, "OK");
        }
    }

    private static void ProcessOneShotRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ProcessOneShotRequest;
            return;
        }

        try
        {
            SetupReport report = RestoreOriginalMountainLandMesh();
            WriteReport(report);
            Debug.Log("[Level03 Terrain Authoring] " + report.message);
        }
        catch (Exception exception)
        {
            WriteFailureReport(exception);
            Debug.LogException(exception);
        }
        finally
        {
            if (File.Exists(RequestFilePath))
            {
                File.Delete(RequestFilePath);
            }

            string requestMetaPath = RequestFilePath + ".meta";
            if (File.Exists(requestMetaPath))
            {
                File.Delete(requestMetaPath);
            }

            AssetDatabase.Refresh();
        }
    }

    private static SetupReport RestoreOriginalMountainLandMesh()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException(
                "Level03 must be the active scene. Open Assets/Scenes/Level03.unity and run the tool again.");
        }

        GameObject legacyLand = FindOriginalMountainLand(scene);
        MeshRenderer meshRenderer = legacyLand.GetComponent<MeshRenderer>();
        MeshCollider meshCollider = legacyLand.GetComponent<MeshCollider>();
        if (meshRenderer == null || meshCollider == null)
        {
            throw new InvalidOperationException(
                "The generated land object does not contain the expected MeshRenderer and MeshCollider.");
        }

        Undo.RecordObjects(
            new UnityEngine.Object[] { meshRenderer, meshCollider },
            "Restore Original Level03 Mountain Land Mesh");

        GameObject convertedTerrainRoot = FindSceneObjectOrNull(scene, ConvertedTerrainRootName);
        if (convertedTerrainRoot != null)
        {
            Undo.RecordObject(convertedTerrainRoot, "Restore Original Level03 Mountain Land Mesh");
            convertedTerrainRoot.SetActive(false);
            EditorUtility.SetDirty(convertedTerrainRoot);
        }

        meshRenderer.enabled = true;
        meshCollider.enabled = true;
        EditorUtility.SetDirty(meshRenderer);
        EditorUtility.SetDirty(meshCollider);
        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new IOException("Unity could not save the Level03 scene.");
        }

        return new SetupReport
        {
            success = true,
            message =
                "The original mountain land MeshRenderer and MeshCollider are restored. " +
                "The converted Terrain root is disabled to prevent overlapping surfaces.",
            meshRendererDisabled = false,
            meshColliderDisabled = false,
            completedAt = DateTime.Now.ToString("O")
        };
    }

    private static SetupReport Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException(
                "Level03 must be the active scene. Open Assets/Scenes/Level03.unity and run the tool again.");
        }

        GameObject legacyLand = FindOriginalMountainLand(scene);

        MeshRenderer meshRenderer = legacyLand.GetComponent<MeshRenderer>();
        MeshCollider meshCollider = legacyLand.GetComponent<MeshCollider>();
        if (meshRenderer == null || meshCollider == null)
        {
            throw new InvalidOperationException(
                "The generated land object does not contain the expected MeshRenderer and MeshCollider.");
        }

        Undo.RecordObjects(
            new UnityEngine.Object[] { meshRenderer, meshCollider },
            "Use Level03 Terrain As Main Land Surface");

        GameObject convertedTerrainRoot = FindSceneObjectOrNull(scene, ConvertedTerrainRootName);
        if (convertedTerrainRoot != null)
        {
            Undo.RecordObject(convertedTerrainRoot, "Use Level03 Terrain As Main Land Surface");
            convertedTerrainRoot.SetActive(true);
            EditorUtility.SetDirty(convertedTerrainRoot);
        }

        meshRenderer.enabled = false;
        meshCollider.enabled = false;
        EditorUtility.SetDirty(meshRenderer);
        EditorUtility.SetDirty(meshCollider);
        EditorSceneManager.MarkSceneDirty(scene);

        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new IOException("Unity could not save the Level03 scene.");
        }

        return new SetupReport
        {
            success = true,
            message =
                "Terrain is now the persistent main land surface. The legacy land MeshRenderer " +
                "and MeshCollider are disabled; roads, ocean, and all Terrain tiles are unchanged.",
            meshRendererDisabled = !meshRenderer.enabled,
            meshColliderDisabled = !meshCollider.enabled,
            completedAt = DateTime.Now.ToString("O")
        };
    }

    private static GameObject FindOriginalMountainLand(Scene scene)
    {
        GameObject legacyLand = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(candidate =>
                candidate.scene == scene &&
                candidate.name == LegacyLandName);

        if (legacyLand == null)
        {
            throw new InvalidOperationException(
                $"Could not find the generated land object '{LegacyLandName}' in Level03.");
        }

        return legacyLand;
    }

    private static GameObject FindSceneObjectOrNull(Scene scene, string objectName)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(candidate =>
                candidate.scene == scene &&
                candidate.name == objectName);
    }

    private static void WriteFailureReport(Exception exception)
    {
        WriteReport(new SetupReport
        {
            success = false,
            message = exception.GetType().Name + ": " + exception.Message,
            completedAt = DateTime.Now.ToString("O")
        });
    }

    private static void WriteReport(SetupReport report)
    {
        File.WriteAllText(ReportFilePath, JsonUtility.ToJson(report, true));
    }
}
