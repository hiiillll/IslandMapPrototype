using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneHierarchyAndCollisionSetup
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string MarkerPath = "Library/HierarchyAndCollisionSetup.v1";
    private const string PhysicsMaterialFolder = "Assets/PhysicsMaterials";

    [MenuItem("Tools/Island Map/Organize Hierarchy And Collisions")]
    public static void TrySetup()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TrySetup;
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            EditorApplication.delayCall += TrySetup;
            return;
        }

        GameObject ocean = FindAny("Ocean", "ENV_Ground_Ocean");
        GameObject beach = FindAny("Beach", "ENV_Ground_Beach");
        GameObject grass = FindAny("City Grass", "ENV_Ground_Grass");
        GameObject roads = FindAny("Tian Road Layout", "ENV_Roads_Tian");
        if (ocean == null || beach == null || grass == null || roads == null)
        {
            EditorApplication.delayCall += TrySetup;
            return;
        }

        Transform environmentRoot = GetOrCreateRoot(scene, "ENVIRONMENT");
        Transform groundRoot = GetOrCreateChild(environmentRoot, "ENV_GroundVisuals");
        Transform lightingRoot = GetOrCreateChild(environmentRoot, "ENV_Lighting");
        Transform systemsRoot = GetOrCreateRoot(scene, "SYSTEMS");
        Transform buildingsRoot = PrepareBuildings(scene);
        Transform propsRoot = PrepareProps(scene);

        MoveAndRename(ocean, groundRoot, "ENV_Ground_Ocean");
        MoveAndRename(beach, groundRoot, "ENV_Ground_Beach");
        MoveAndRename(grass, groundRoot, "ENV_Ground_Grass");
        MoveAndRename(roads, environmentRoot, "ENV_Roads_Tian");
        RenameRoadChildren(roads.transform);

        GameObject light = FindAny("Directional Light", "ENV_DirectionalLight");
        if (light != null)
        {
            MoveAndRename(light, lightingRoot, "ENV_DirectionalLight");
        }

        GameObject camera = FindAny("Main Camera", "SYS_MainCamera");
        if (camera != null)
        {
            MoveAndRename(camera, systemsRoot, "SYS_MainCamera");
        }

        Transform collisionRoot = RecreateRoot(scene, "COLLISION");
        PhysicMaterial roadMaterial = CreatePhysicsMaterial("PM_Road", 0.92f);
        PhysicMaterial beachMaterial = CreatePhysicsMaterial("PM_Beach", 0.58f);
        PhysicMaterial grassMaterial = CreatePhysicsMaterial("PM_Grass", 0.72f);
        CreateRoadColliders(roads, collisionRoot, roadMaterial);
        CreateSurfaceCollider(beach, collisionRoot, "COL_Beach", beachMaterial);
        CreateSurfaceCollider(grass, collisionRoot, "COL_Grass", grassMaterial);
        DisableVisualColliders(ocean);
        DisableVisualColliders(beach);
        DisableVisualColliders(grass);
        DisableVisualColliders(roads);
        RemoveEmptyLegacyRoot(scene, "Environment");

        environmentRoot.SetSiblingIndex(0);
        buildingsRoot.SetSiblingIndex(1);
        propsRoot.SetSiblingIndex(2);
        collisionRoot.SetSiblingIndex(3);
        systemsRoot.SetSiblingIndex(4);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("O"));
        Selection.activeGameObject = collisionRoot.gameObject;
        Debug.Log("Organized scene hierarchy and created invisible road, beach and grass collision plates.");
    }

    private static Transform PrepareBuildings(Scene scene)
    {
        GameObject buildings = FindAny("Placed Buildings", "BUILDINGS");
        Transform root = buildings != null ? buildings.transform : GetOrCreateRoot(scene, "BUILDINGS");
        root.name = "BUILDINGS";
        Transform residential = GetOrCreateChild(root, "BLD_Residential");
        Transform commercial = GetOrCreateChild(root, "BLD_Commercial");
        Transform civic = GetOrCreateChild(root, "BLD_Civic");

        MoveNamedBuilding(root, residential, "Residential North West", "BLD_House_NW");
        MoveNamedBuilding(root, residential, "Residential South East", "BLD_House_SE");
        MoveNamedBuilding(root, commercial, "Supermarket North East", "BLD_Supermarket_NE");
        MoveNamedBuilding(root, civic, "Police Station South West", "BLD_PoliceStation_SW");
        return root;
    }

    private static Transform PrepareProps(Scene scene)
    {
        GameObject props = FindAny("Scene Props", "PROPS");
        Transform root = props != null ? props.transform : GetOrCreateRoot(scene, "PROPS");
        root.name = "PROPS";

        RenameGroup(root, "Ice Cream Trucks", "PROP_IceCreamTrucks", "PROP_IceCreamTruck");
        RenameGroup(root, "Oil Barrels", "PROP_OilBarrels", "PROP_OilBarrel");
        RenameGroup(root, "Tires Beside Police Station", "PROP_Tires", "PROP_Tire");
        return root;
    }

    private static void RenameGroup(Transform root, string oldName, string newName, string itemPrefix)
    {
        Transform group = root.Find(oldName) ?? root.Find(newName);
        if (group == null)
        {
            group = GetOrCreateChild(root, newName);
        }
        group.name = newName;

        for (int index = 0; index < group.childCount; index++)
        {
            group.GetChild(index).name = $"{itemPrefix}_{index + 1:00}";
        }
    }

    private static void MoveNamedBuilding(Transform root, Transform target, string oldName, string newName)
    {
        Transform building = FindDescendant(root, oldName) ?? FindDescendant(root, newName);
        if (building == null)
        {
            return;
        }

        building.SetParent(target, true);
        building.name = newName;
    }

    private static void RenameRoadChildren(Transform roads)
    {
        Dictionary<string, string> names = new Dictionary<string, string>
        {
            { "North Road", "ENV_Road_North" },
            { "South Road", "ENV_Road_South" },
            { "West Road", "ENV_Road_West" },
            { "East Road", "ENV_Road_East" },
            { "Center North", "ENV_Road_CenterNorth" },
            { "Center South", "ENV_Road_CenterSouth" },
            { "Center West", "ENV_Road_CenterWest" },
            { "Center East", "ENV_Road_CenterEast" },
            { "Center Intersection", "ENV_Road_CenterIntersection" }
        };

        foreach (Transform child in roads)
        {
            if (names.TryGetValue(child.name, out string newName))
            {
                child.name = newName;
            }
        }
    }

    private static void CreateRoadColliders(GameObject roads, Transform collisionRoot, PhysicMaterial material)
    {
        Transform roadRoot = GetOrCreateChild(collisionRoot, "COL_Roads");
        Renderer[] renderers = roads.GetComponentsInChildren<Renderer>();
        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            string suffix = renderer.gameObject.name.Replace("ENV_Road_", string.Empty);
            CreateColliderPlate(roadRoot, $"COL_Road_{suffix}", renderer.bounds, material);
        }
    }

    private static void CreateSurfaceCollider(GameObject surface, Transform collisionRoot, string name, PhysicMaterial material)
    {
        Renderer renderer = surface.GetComponent<Renderer>();
        if (renderer != null)
        {
            CreateColliderPlate(collisionRoot, name, renderer.bounds, material);
        }
    }

    private static void CreateColliderPlate(Transform parent, string name, Bounds surfaceBounds, PhysicMaterial material)
    {
        const float thickness = 0.2f;
        GameObject plate = new GameObject(name);
        plate.transform.SetParent(parent, false);
        plate.isStatic = true;
        BoxCollider collider = plate.AddComponent<BoxCollider>();
        collider.center = new Vector3(surfaceBounds.center.x, surfaceBounds.max.y - thickness * 0.5f, surfaceBounds.center.z);
        collider.size = new Vector3(surfaceBounds.size.x, thickness, surfaceBounds.size.z);
        collider.sharedMaterial = material;
    }

    private static PhysicMaterial CreatePhysicsMaterial(string name, float friction)
    {
        Directory.CreateDirectory(PhysicsMaterialFolder);
        string path = $"{PhysicsMaterialFolder}/{name}.physicMaterial";
        PhysicMaterial material = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
        if (material == null)
        {
            material = new PhysicMaterial(name);
            AssetDatabase.CreateAsset(material, path);
        }

        material.dynamicFriction = friction;
        material.staticFriction = friction;
        material.bounciness = 0f;
        material.frictionCombine = PhysicMaterialCombine.Maximum;
        material.bounceCombine = PhysicMaterialCombine.Minimum;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void DisableVisualColliders(GameObject root)
    {
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }
    }

    private static Transform RecreateRoot(Scene scene, string name)
    {
        GameObject existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }

        GameObject root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root.transform;
    }

    private static bool HasLegacyEnvironmentRoot()
    {
        Scene scene = SceneManager.GetActiveScene();
        return scene.IsValid() && scene.GetRootGameObjects().Any(root => root.name == "Environment");
    }

    private static void RemoveEmptyLegacyRoot(Scene scene, string name)
    {
        GameObject legacyRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
        if (legacyRoot != null && legacyRoot.transform.childCount == 0)
        {
            UnityEngine.Object.DestroyImmediate(legacyRoot);
        }
    }

    private static Transform GetOrCreateRoot(Scene scene, string name)
    {
        GameObject existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
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

        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(transform => transform.name == name);
    }

    private static GameObject FindAny(params string[] names)
    {
        foreach (string name in names)
        {
            GameObject found = GameObject.Find(name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private static void MoveAndRename(GameObject gameObject, Transform parent, string name)
    {
        gameObject.transform.SetParent(parent, true);
        gameObject.name = name;
    }
}
