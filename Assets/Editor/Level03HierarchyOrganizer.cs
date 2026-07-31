using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Level03HierarchyOrganizer
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";

    private static readonly string[] EnvironmentGroundObjects =
    {
        "ENV_Level03_ConvertedTerrain",
        "ENV_Level03_FlatGrass_FirstLevel",
        "ENV_Level03_FlatIslands_And_MainMountain",
        "ENV_Level03_SmoothBeachCoastline"
    };

    private static readonly string[] EnvironmentRoadObjects =
    {
        "ENV_Level03_RoadNetwork_FromReference",
        "ENV_Level03_RoadMarkings"
    };

    private static readonly string[] GameplayObjects =
    {
        "SYS_Level03_TreasureObjective",
        "SYS_Level03_PlaneExtraction"
    };

    private sealed class PrefabGroup
    {
        public string AssetPath { get; }
        public string GroupName { get; }
        public string ItemPrefix { get; }

        public PrefabGroup(string assetPath, string groupName, string itemPrefix)
        {
            AssetPath = assetPath;
            GroupName = groupName;
            ItemPrefix = itemPrefix;
        }
    }

    private static readonly PrefabGroup[] BuildingPrefabGroups =
    {
        new PrefabGroup(
            "Assets/Models/Imported/Apartment/f107add5ea68f5a00af639a36564417a.obj",
            "BLD_Level03_Apartments",
            "BLD_Level03_Apartment"),
        new PrefabGroup(
            "Assets/Models/Imported/Model_11/6eb03c288ccd1d188ca79ea31aa326aa.obj",
            "BLD_Level03_Houses",
            "BLD_Level03_House"),
        new PrefabGroup(
            "Assets/Models/Imported/Police/f29a5dd13bf889ff8daea366ee0ba69e.obj",
            "BLD_Level03_PoliceStations",
            "BLD_Level03_PoliceStation"),
        new PrefabGroup(
            "Assets/Models/Imported/GasStation/444fdab36851aedf361ccfc2cf991f21.obj",
            "BLD_Level03_GasStations",
            "BLD_Level03_GasStation"),
        new PrefabGroup(
            "Assets/Models/Imported/Model_02/2e14fdf541f8fec64aca4a348cc03c59.obj",
            "BLD_Level03_Supermarkets",
            "BLD_Level03_Supermarket")
    };

    private static readonly PrefabGroup[] PropPrefabGroups =
    {
        new PrefabGroup(
            "Assets/Models/Imported/Model_19/04c0ffae5a8c4056204063aa4c69582a.obj",
            "PROP_Level03_Statues",
            "PROP_Level03_Statue"),
        new PrefabGroup(
            "Assets/Models/Imported/Model_20/004fa4a26d815acdb0f6df66efd781d7.obj",
            "PROP_Level03_ParkBenches",
            "PROP_Level03_ParkBench")
    };

    private const string PlaneAssetPath =
        "Assets/Resources/Level02/PlaneObjective/a20e928798d90a32b7b6c4b41a481066.obj";

    private static readonly string[] AiObjects =
    {
        "AI_NAVIGATION_Level03_CarSurface",
        "SYS_Level03_PoliceEnemySpawner"
    };

    private static readonly string[] SystemObjects =
    {
        "SYS_MainCamera",
        "SYSTEMS_Level03_Preview",
        "SYS_Level03_StrictScale40_Applied"
    };

    [MenuItem("Tools/Island Map/Level03/Organize Scene Hierarchy")]
    public static void OrganizeActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException("Level03 must be the active scene.");
        }

        Organize(scene);
    }

    public static void OrganizeFromCommandLine()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Organize(scene);
    }

    public static void AuditRootsFromCommandLine()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            string componentNames = string.Join(
                ",",
                root.GetComponents<Component>()
                    .Where(component => component != null)
                    .Select(component => component.GetType().Name));
            Debug.Log(
                $"[Level03 Root Audit] name={root.name}; prefab={prefabPath}; " +
                $"components={componentNames}; children={root.transform.childCount}");
        }
    }

    private static void Organize(Scene scene)
    {
        Transform environment = RequireSceneObject(scene, "ENVIRONMENT_Level03").transform;
        Transform buildings = GetOrCreateRoot(scene, "BUILDINGS_Level03");
        Transform props = GetOrCreateRoot(scene, "PROPS_Level03");
        Transform gameplay = GetOrCreateRoot(scene, "GAMEPLAY_Level03");
        Transform ai = GetOrCreateRoot(scene, "AI_Level03");
        Transform systems = GetOrCreateRoot(scene, "SYSTEMS_Level03");

        Transform ground = GetOrCreateChild(environment, "ENV_Level03_Ground");
        Transform roads = GetOrCreateChild(environment, "ENV_Level03_Roads");
        Transform water = GetOrCreateChild(environment, "ENV_Level03_Water");
        Transform objectives = GetOrCreateChild(gameplay, "OBJECTIVES_Level03");

        MoveNamedObjects(scene, EnvironmentGroundObjects, ground);
        MoveNamedObjects(scene, EnvironmentRoadObjects, roads);
        MoveNamedObjects(scene, new[] { "ENV_Level03_Ocean_4000x4000" }, water);
        MoveNamedObjects(scene, new[] { "PLAYER_Car" }, gameplay);
        MoveNamedObjects(scene, GameplayObjects, objectives);
        MoveNamedObjects(scene, AiObjects, ai);
        MoveNamedObjects(scene, SystemObjects, systems);
        int organizedPrefabCount =
            OrganizePrefabGroups(scene, buildings, BuildingPrefabGroups) +
            OrganizePrefabGroups(scene, props, PropPrefabGroups) +
            OrganizePlane(scene, objectives);

        SetChildOrder(environment, ground, roads, water);
        SetNamedChildOrder(gameplay, new[] { "PLAYER_Car", "OBJECTIVES_Level03" });
        SetNamedChildOrder(
            objectives,
            new[]
            {
                "SYS_Level03_TreasureObjective",
                "SYS_Level03_PlaneExtraction",
                "VEH_Level03_EscapePlane"
            });
        SetNamedChildOrder(ai, AiObjects);
        SetNamedChildOrder(systems, SystemObjects);
        SetRootOrder(scene, environment, buildings, props, gameplay, ai, systems);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            throw new InvalidOperationException("Unity could not save the organized Level03 scene.");
        }

        Validate(scene);
        Selection.activeGameObject = environment.gameObject;
        Debug.Log(
            $"[Level03 Hierarchy] Organized {organizedPrefabCount} placed prefab " +
            "instances and all Level03 systems without changing world transforms.");
    }

    private static int OrganizePrefabGroups(
        Scene scene,
        Transform categoryRoot,
        IReadOnlyList<PrefabGroup> groups)
    {
        int organizedCount = 0;
        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            PrefabGroup definition = groups[groupIndex];
            Transform group = GetOrCreateChild(categoryRoot, definition.GroupName);
            List<GameObject> instances = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(candidate => candidate.scene == scene)
                .Where(candidate =>
                    PrefabUtility.GetNearestPrefabInstanceRoot(candidate) == candidate)
                .Where(candidate => string.Equals(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate),
                    definition.AssetPath,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(candidate => candidate.transform.position.x)
                .ThenBy(candidate => candidate.transform.position.z)
                .ToList();

            for (int index = 0; index < instances.Count; index++)
            {
                MovePreservingWorldTransform(instances[index], group);
                instances[index].name = $"{definition.ItemPrefix}_{index + 1:000}";
            }

            organizedCount += instances.Count;
            group.SetSiblingIndex(groupIndex);
        }
        return organizedCount;
    }

    private static int OrganizePlane(Scene scene, Transform objectives)
    {
        GameObject plane = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(candidate =>
                candidate.scene == scene &&
                PrefabUtility.GetNearestPrefabInstanceRoot(candidate) == candidate &&
                string.Equals(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate),
                    PlaneAssetPath,
                    StringComparison.OrdinalIgnoreCase));
        if (plane == null)
        {
            throw new MissingReferenceException("The placed Level03 escape plane is missing.");
        }

        MovePreservingWorldTransform(plane, objectives);
        plane.name = "VEH_Level03_EscapePlane";
        return 1;
    }

    private static void MoveNamedObjects(Scene scene, IEnumerable<string> names, Transform parent)
    {
        foreach (string name in names)
        {
            GameObject gameObject = FindSceneObject(scene, name);
            if (gameObject == null)
            {
                throw new MissingReferenceException($"Level03 object '{name}' is missing.");
            }

            MovePreservingWorldTransform(gameObject, parent);
        }
    }

    private static void MovePreservingWorldTransform(GameObject gameObject, Transform parent)
    {
        Vector3 position = gameObject.transform.position;
        Quaternion rotation = gameObject.transform.rotation;
        Vector3 scale = gameObject.transform.lossyScale;
        gameObject.transform.SetParent(parent, true);

        if (!Approximately(position, gameObject.transform.position) ||
            Quaternion.Angle(rotation, gameObject.transform.rotation) > 0.001f ||
            !Approximately(scale, gameObject.transform.lossyScale))
        {
            throw new InvalidOperationException(
                $"Organizing '{gameObject.name}' changed its world transform.");
        }
    }

    private static Transform GetOrCreateRoot(Scene scene, string name)
    {
        GameObject existing = scene.GetRootGameObjects()
            .FirstOrDefault(candidate => candidate.name == name);
        if (existing != null)
        {
            return existing.transform;
        }

        GameObject root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root.transform;
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing;
        }

        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static GameObject RequireSceneObject(Scene scene, string name)
    {
        return FindSceneObject(scene, name) ??
            throw new MissingReferenceException($"Level03 object '{name}' is missing.");
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(candidate => candidate.scene == scene && candidate.name == name);
    }

    private static void SetRootOrder(Scene scene, params Transform[] roots)
    {
        for (int index = 0; index < roots.Length; index++)
        {
            roots[index].SetSiblingIndex(index);
        }
    }

    private static void SetChildOrder(Transform parent, params Transform[] children)
    {
        for (int index = 0; index < children.Length; index++)
        {
            if (children[index].parent == parent)
            {
                children[index].SetSiblingIndex(index);
            }
        }
    }

    private static void SetNamedChildOrder(Transform parent, IReadOnlyList<string> names)
    {
        for (int index = 0; index < names.Count; index++)
        {
            Transform child = parent.Cast<Transform>()
                .FirstOrDefault(candidate => candidate.name == names[index]);
            if (child != null)
            {
                child.SetSiblingIndex(index);
            }
        }
    }

    private static void Validate(Scene scene)
    {
        string[] expectedRoots =
        {
            "ENVIRONMENT_Level03",
            "BUILDINGS_Level03",
            "PROPS_Level03",
            "GAMEPLAY_Level03",
            "AI_Level03",
            "SYSTEMS_Level03"
        };
        string[] actualRoots = scene.GetRootGameObjects()
            .Select(root => root.name)
            .ToArray();
        if (!actualRoots.SequenceEqual(expectedRoots))
        {
            throw new InvalidOperationException(
                $"Unexpected Level03 root hierarchy: {string.Join(", ", actualRoots)}");
        }
    }

    private static bool Approximately(Vector3 first, Vector3 second)
    {
        return (first - second).sqrMagnitude <= 0.000001f;
    }
}
