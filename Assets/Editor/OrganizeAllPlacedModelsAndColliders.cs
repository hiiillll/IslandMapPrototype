using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OrganizeAllPlacedModelsAndColliders
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string MarkerPath = "Library/AllPlacedModelsOrganized.v1";
    private const string ImportedModelRoot = "Assets/Models/Imported/";

    private sealed class ModelType
    {
        public string Folder;
        public string Root;
        public string Group;
        public string ItemPrefix;

        public ModelType(string folder, string root, string group, string itemPrefix)
        {
            Folder = folder;
            Root = root;
            Group = group;
            ItemPrefix = itemPrefix;
        }
    }

    private static readonly ModelType[] ModelTypes =
    {
        new ModelType("Model_01", "PROPS", "PROP_StreetLights", "PROP_StreetLight"),
        new ModelType("Model_02", "BUILDINGS", "BLD_Commercial", "BLD_Supermarket"),
        new ModelType("Model_03", "PROPS", "PROP_OilBarrels", "PROP_OilBarrel"),
        new ModelType("Model_04", "PROPS", "PROP_IceCreamTrucks", "PROP_IceCreamTruck"),
        new ModelType("Model_05", "PROPS", "PROP_Docks", "PROP_Dock"),
        new ModelType("Model_06", "PROPS", "PROP_Lifebuoys", "PROP_Lifebuoy"),
        new ModelType("Model_07", "PROPS", "PROP_Lighthouses", "PROP_Lighthouse"),
        new ModelType("Model_08", "PROPS", "PROP_Tires", "PROP_Tire"),
        new ModelType("Model_09", "PROPS", "PROP_Barricades", "PROP_Barricade"),
        new ModelType("Model_10", "PROPS", "PROP_Yachts", "PROP_Yacht"),
        new ModelType("Model_11", "BUILDINGS", "BLD_Residential", "BLD_House"),
        new ModelType("Model_12", "PROPS", "PROP_PalmTrees", "PROP_PalmTree"),
        new ModelType("Model_13", "VEHICLES", "VEH_PoliceCars", "VEH_PoliceCar"),
        new ModelType("Model_14", "VEHICLES", "VEH_SheriffCars", "VEH_SheriffCar"),
        new ModelType("Police", "BUILDINGS", "BLD_Civic", "BLD_PoliceStation")
    };

    [MenuItem("Tools/Island Map/Organize All Models And Colliders")]
    public static void TryOrganize()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryOrganize;
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            EditorApplication.delayCall += TryOrganize;
            return;
        }

        List<GameObject> instances = FindImportedModelInstances(scene);
        if (instances.Count == 0)
        {
            EditorApplication.delayCall += TryOrganize;
            return;
        }

        Dictionary<string, Transform> roots = new Dictionary<string, Transform>
        {
            { "BUILDINGS", GetOrCreateRoot(scene, "BUILDINGS") },
            { "PROPS", GetOrCreateRoot(scene, "PROPS") },
            { "VEHICLES", GetOrCreateRoot(scene, "VEHICLES") }
        };

        int colliderCount = 0;
        foreach (ModelType modelType in ModelTypes)
        {
            string folderPrefix = $"{ImportedModelRoot}{modelType.Folder}/";
            List<GameObject> matching = instances
                .Where(instance => GetSourcePath(instance).StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(instance => instance.transform.position.x)
                .ThenBy(instance => instance.transform.position.z)
                .ToList();
            if (matching.Count == 0)
            {
                continue;
            }

            Transform group = GetOrCreateChild(roots[modelType.Root], modelType.Group);
            for (int index = 0; index < matching.Count; index++)
            {
                GameObject instance = matching[index];
                instance.transform.SetParent(group, true);
                instance.name = $"{modelType.ItemPrefix}_{index + 1:00}";
                AddOrFitBoxCollider(instance);
                colliderCount++;
            }
        }

        SortRootOrder(scene, roots);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("O"));
        Selection.activeGameObject = roots["PROPS"].gameObject;
        Debug.Log($"Organized {instances.Count} imported model instances and fitted {colliderCount} root BoxColliders.");
    }

    private static List<GameObject> FindImportedModelInstances(Scene scene)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .Where(PrefabUtility.IsAnyPrefabInstanceRoot)
            .Where(instance => GetSourcePath(instance).StartsWith(ImportedModelRoot, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
    }

    private static string GetSourcePath(GameObject instance)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
        return source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
    }

    private static void AddOrFitBoxCollider(GameObject instance)
    {
        Bounds localBounds = CalculateLocalBounds(instance);
        BoxCollider collider = instance.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = instance.AddComponent<BoxCollider>();
        }

        collider.enabled = true;
        collider.isTrigger = false;
        collider.center = localBounds.center;
        collider.size = localBounds.size;
    }

    private static Bounds CalculateLocalBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        bool initialized = false;
        Bounds localBounds = new Bounds();
        foreach (Renderer renderer in renderers)
        {
            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
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
                    localBounds = new Bounds(localPoint, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    localBounds.Encapsulate(localPoint);
                }
            }
        }

        return localBounds;
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

    private static void SortRootOrder(Scene scene, Dictionary<string, Transform> roots)
    {
        string[] rootOrder = { "ENVIRONMENT", "BUILDINGS", "PROPS", "VEHICLES", "COLLISION", "SYSTEMS" };
        for (int index = 0; index < rootOrder.Length; index++)
        {
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == rootOrder[index]);
            if (root != null)
            {
                root.transform.SetSiblingIndex(index);
            }
        }
    }
}
