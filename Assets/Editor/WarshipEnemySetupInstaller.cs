using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WarshipEnemySetupInstaller
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string WarshipFolder = "Assets/Models/Imported/Model_15/";
    private const string EnemyModelFolder = "Assets/Models/Imported/Model_14/";
    private const string MarkerPath = "Library/WarshipEnemySetupInstalled.v1";

    [MenuItem("Tools/Island Map/Install Warship Enemy System")]
    public static void TryInstall()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryInstall;
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject player = GameObject.Find("PLAYER_Car");
        GameObject environment = GameObject.Find("ENVIRONMENT");
        GameObject systems = GameObject.Find("SYSTEMS");
        GameObject beachCollider = GameObject.Find("COL_Beach");
        GameObject enemyModel = LoadModel(EnemyModelFolder);
        List<GameObject> warships = FindPrefabInstances(scene, WarshipFolder);
        if (!scene.IsValid() || scene.path != ScenePath || player == null || environment == null || systems == null
            || beachCollider == null || enemyModel == null || warships.Count == 0)
        {
            EditorApplication.delayCall += TryInstall;
            return;
        }

        Transform warshipRoot = GetOrCreateChild(environment.transform, "ENV_Warships");
        Transform spawnRoot = RecreateChild(systems.transform, "SPAWNS_Enemy");
        BoxCollider beachSurfaceCollider = beachCollider.GetComponent<BoxCollider>();
        float beachSurfaceY = beachSurfaceCollider.bounds.max.y;
        List<Transform> spawnPoints = new List<Transform>();

        for (int index = 0; index < warships.Count; index++)
        {
            GameObject warship = warships[index];
            warship.transform.SetParent(warshipRoot, true);
            warship.name = $"ENV_Warship_{index + 1:00}";
            RemoveWarshipCollider(warship);

            GameObject spawn = new GameObject($"SPAWN_Enemy_WarshipRamp_{index + 1:00}");
            spawn.transform.SetParent(spawnRoot, false);
            Vector3 shorePosition = beachSurfaceCollider.ClosestPoint(warship.transform.position);
            Vector3 towardBeach = shorePosition - warship.transform.position;
            towardBeach.y = 0f;
            if (towardBeach.sqrMagnitude < 0.01f)
            {
                towardBeach = warship.transform.forward;
                towardBeach.y = 0f;
            }
            towardBeach.Normalize();
            spawn.transform.rotation = Quaternion.LookRotation(towardBeach, Vector3.up);
            spawn.transform.position = new Vector3(
                shorePosition.x + towardBeach.x * 4f,
                beachSurfaceY + 0.2f,
                shorePosition.z + towardBeach.z * 4f);
            spawnPoints.Add(spawn.transform);
        }

        Transform enemySystemRoot = GetOrCreateChild(systems.transform, "ENEMY_SYSTEM");
        WarshipEnemySpawner spawner = enemySystemRoot.GetComponent<WarshipEnemySpawner>();
        if (spawner == null)
        {
            spawner = enemySystemRoot.gameObject.AddComponent<WarshipEnemySpawner>();
        }
        spawner.Configure(player.transform, enemyModel, spawnPoints.ToArray());

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("O"));
        Selection.activeGameObject = spawnRoot.gameObject;
        Debug.Log($"Configured {warships.Count} warship colliders and {spawnPoints.Count} enemy beach spawn points.");
    }

    private static GameObject LoadModel(string folder)
    {
        string path = AssetDatabase.FindAssets("t:Model", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(assetPath => assetPath.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static List<GameObject> FindPrefabInstances(Scene scene, string sourceFolder)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .Where(PrefabUtility.IsAnyPrefabInstanceRoot)
            .Where(instance =>
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
                string sourcePath = source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
                return sourcePath.StartsWith(sourceFolder, StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(instance => instance.transform.position.x)
            .ThenBy(instance => instance.transform.position.z)
            .ToList();
    }

    private static void RemoveWarshipCollider(GameObject root)
    {
        BoxCollider collider = root.GetComponent<BoxCollider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }
    }

    private static Bounds CalculateLocalBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool initialized = false;
        Bounds bounds = new Bounds();
        foreach (Renderer renderer in renderers)
        {
            Bounds rendererBounds = renderer.bounds;
            Vector3 min = rendererBounds.min;
            Vector3 max = rendererBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z)
            };
            foreach (Vector3 corner in corners)
            {
                Vector3 localPoint = root.transform.InverseTransformPoint(corner);
                if (!initialized)
                {
                    bounds = new Bounds(localPoint, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(localPoint);
                }
            }
        }
        return bounds;
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
        }
        return child;
    }

    private static Transform RecreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }
}
