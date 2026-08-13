using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Level01FloridaPalmInstaller
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string PalmPrefabPath =
        "Assets/Miami_Beach/Prefabs/Vegetation/MB_Palm_Florida_01.prefab";

    public static void ReplaceFromCommandLine()
    {
        try
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform group = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "PROP_PalmTrees");
            GameObject palmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PalmPrefabPath);
            if (group == null || palmPrefab == null)
            {
                throw new MissingReferenceException("Level01 palm group or Florida palm prefab is missing.");
            }

            Transform[] originals = group.Cast<Transform>().ToArray();
            Debug.Log($"[Level01] PROP_PalmTrees direct children before replacement: {originals.Length}");
            int replaced = 0;
            foreach (Transform original in originals)
            {
                Transform oldVisual = original.Find("MiamiPalm_Florida01");
                if (oldVisual != null)
                {
                    UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);
                }

                foreach (Renderer renderer in original.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(palmPrefab, original);
                instance.name = "MiamiPalm_Florida01";
                Transform visual = instance.transform;
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
                foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                }
                replaced++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Unity could not save " + ScenePath);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Level01] Replaced {replaced} palm trees with MB_Palm_Florida_01.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
