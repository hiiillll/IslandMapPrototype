using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DockLevelTransitionInstaller
{
    private const string LevelOneScenePath = "Assets/Scenes/IslandMap.unity";
    private const string LevelTwoScenePath = "Assets/Scenes/Level02.unity";

    [MenuItem("Tools/Island Map/Install Dock Level Transition")]
    public static void Install()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Install;
            return;
        }

        EnsureLevelTwoScene();

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != LevelOneScenePath)
        {
            Debug.LogError("Open Assets/Scenes/IslandMap.unity before installing the dock level transition.");
            return;
        }

        Transform systems = GetOrCreateRoot(scene, "SYSTEMS");
        Transform transitionTransform = systems.Find("SYS_DockLevelTransition");
        GameObject transitionObject = transitionTransform != null
            ? transitionTransform.gameObject
            : new GameObject("SYS_DockLevelTransition");
        transitionObject.transform.SetParent(systems, false);

        if (transitionObject.GetComponent<DockLevelTransition>() == null)
        {
            transitionObject.AddComponent<DockLevelTransition>();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        ConfigureBuildScenes();
        Selection.activeGameObject = transitionObject;
        Debug.Log("Installed dock objective, cinematic transition, and Level02 scene.");
    }

    private static void EnsureLevelTwoScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LevelTwoScenePath) != null)
        {
            return;
        }

        Scene levelTwo = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SaveScene(levelTwo, LevelTwoScenePath);
        EditorSceneManager.CloseScene(levelTwo, true);
    }

    private static void ConfigureBuildScenes()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
            .Where(scene => scene.path != LevelOneScenePath && scene.path != LevelTwoScenePath)
            .ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(LevelOneScenePath, true));
        scenes.Insert(1, new EditorBuildSettingsScene(LevelTwoScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static Transform GetOrCreateRoot(Scene scene, string name)
    {
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == name);
        if (root == null)
        {
            root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
        }
        return root.transform;
    }
}
