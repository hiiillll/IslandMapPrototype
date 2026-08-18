using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Level01ShadowStabilityInstaller
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";

    [MenuItem("Tools/Island Map/Apply Level 01 Stable Shadows")]
    public static void ApplyFromMenu()
    {
        Apply();
    }

    public static void ApplyFromCommandLine()
    {
        try
        {
            Apply();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Apply()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject cameraObject = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(item => item.scene == scene && item.name == "SYS_MainCamera");
        if (cameraObject == null)
        {
            throw new InvalidOperationException("SYS_MainCamera was not found in " + ScenePath);
        }

        Level01ShadowStability stability = cameraObject.GetComponent<Level01ShadowStability>();
        if (stability == null)
        {
            stability = cameraObject.AddComponent<Level01ShadowStability>();
        }

        EditorUtility.SetDirty(stability);
        EditorUtility.SetDirty(cameraObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[Level 01 Stable Shadows] Applied scene-scoped stable directional shadows.");
    }
}
