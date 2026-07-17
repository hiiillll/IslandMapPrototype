using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class EnemyNavigationBaker
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string AssetFolder = "Assets/Scenes/IslandMap";
    private const string AssetPath = AssetFolder + "/NavMesh-AI_NAVIGATION_CarSurface.asset";
    private const string SurfaceName = "AI_NAVIGATION_CarSurface";
    private const float AdditionalObstacleClearance = 2.25f;

    [MenuItem("Tools/Island Map/Bake Enemy Navigation")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Exit Play Mode before baking enemy navigation.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnvironmentCollisionRefiner.RefineAll();

        int notWalkableArea = NavMesh.GetAreaFromName("Not Walkable");
        if (notWalkableArea < 0)
        {
            Debug.LogError("Enemy navigation bake failed: the Not Walkable area is missing.");
            return;
        }

        NavMeshSurface surface = GetOrCreateSurface();
        ConfigureSurface(surface, notWalkableArea);
        int sourceCount = ConfigureColliderModifiers(notWalkableArea);
        int clearanceVolumeCount = ConfigureObstacleClearanceVolumes(notWalkableArea);
        if (sourceCount == 0)
        {
            Debug.LogError("Enemy navigation bake failed: no road, grass, or beach collider was found.");
            return;
        }

        EnsureAssetFolder();
        surface.RemoveData();
        surface.navMeshData = null;
        AssetDatabase.DeleteAsset(AssetPath);
        surface.BuildNavMesh();
        if (surface.navMeshData == null)
        {
            Debug.LogError("Enemy navigation bake failed: NavMeshSurface could not create NavMesh data.");
            return;
        }

        AssetDatabase.CreateAsset(surface.navMeshData, AssetPath);
        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        Debug.Log(
            $"Baked car NavMesh from {sourceCount} colliders and {clearanceVolumeCount} clearance volumes " +
            $"({triangulation.vertices.Length} vertices) to {AssetPath}.",
            surface);
    }

    public static void BakeForCommandLine()
    {
        Bake();
    }

    private static NavMeshSurface GetOrCreateSurface()
    {
        foreach (NavMeshSurface existingSurface in Object.FindObjectsOfType<NavMeshSurface>(true))
        {
            if (existingSurface.name == SurfaceName)
            {
                return existingSurface;
            }
        }

        GameObject systems = GameObject.Find("SYSTEMS");
        if (systems == null)
        {
            systems = new GameObject("SYSTEMS");
        }

        GameObject surfaceObject = new GameObject(SurfaceName);
        surfaceObject.transform.SetParent(systems.transform, false);
        return surfaceObject.AddComponent<NavMeshSurface>();
    }

    private static void ConfigureSurface(NavMeshSurface surface, int notWalkableArea)
    {
        surface.transform.localPosition = Vector3.zero;
        surface.transform.localRotation = Quaternion.identity;
        surface.transform.localScale = Vector3.one;
        surface.agentTypeID = 0;
        surface.collectObjects = CollectObjects.MarkedWithModifier;
        surface.layerMask = ~0;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.defaultArea = notWalkableArea;
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = true;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.15f;
        surface.minRegionArea = 2f;
        EditorUtility.SetDirty(surface);
    }

    private static int ConfigureObstacleClearanceVolumes(int notWalkableArea)
    {
        foreach (NavMeshModifierVolume existingVolume in Object.FindObjectsOfType<NavMeshModifierVolume>(true))
        {
            Object.DestroyImmediate(existingVolume);
        }

        int volumeCount = 0;
        foreach (Collider collider in Object.FindObjectsOfType<Collider>(true))
        {
            if (!ShouldBakeCollider(collider) || IsWalkableSurface(collider) || !IsClearanceObstacle(collider))
            {
                continue;
            }

            Bounds bounds = GetLocalBounds(collider);
            Vector3 scale = collider.transform.lossyScale;
            bounds.Expand(new Vector3(
                AdditionalObstacleClearance * 2f / Mathf.Max(Mathf.Abs(scale.x), 0.001f),
                1f / Mathf.Max(Mathf.Abs(scale.y), 0.001f),
                AdditionalObstacleClearance * 2f / Mathf.Max(Mathf.Abs(scale.z), 0.001f)));

            NavMeshModifierVolume volume = collider.GetComponent<NavMeshModifierVolume>();
            if (volume == null)
            {
                volume = collider.gameObject.AddComponent<NavMeshModifierVolume>();
            }

            volume.center = bounds.center;
            volume.size = bounds.size;
            volume.area = notWalkableArea;
            EditorUtility.SetDirty(volume);
            volumeCount++;
        }

        return volumeCount;
    }

    private static bool IsClearanceObstacle(Collider collider)
    {
        for (Transform current = collider.transform; current != null; current = current.parent)
        {
            if (current.name.StartsWith("BLD_") || current.name.StartsWith("PROP_PalmTree")
                || current.name.StartsWith("PROP_StreetLight") || current.name.StartsWith("PROP_Barricade"))
            {
                return true;
            }
        }

        return false;
    }

    private static Bounds GetLocalBounds(Collider collider)
    {
        Bounds worldBounds = collider.bounds;
        Bounds localBounds = new Bounds();
        bool initialized = false;
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
            Vector3 localPoint = collider.transform.InverseTransformPoint(corner);
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

        return localBounds;
    }

    private static int ConfigureColliderModifiers(int notWalkableArea)
    {
        foreach (NavMeshModifier modifier in Object.FindObjectsOfType<NavMeshModifier>(true))
        {
            modifier.ignoreFromBuild = true;
            modifier.applyToChildren = false;
            EditorUtility.SetDirty(modifier);
        }

        int sourceCount = 0;
        foreach (Collider collider in Object.FindObjectsOfType<Collider>(true))
        {
            if (!ShouldBakeCollider(collider))
            {
                continue;
            }

            NavMeshModifier modifier = collider.GetComponent<NavMeshModifier>();
            if (modifier == null)
            {
                modifier = collider.gameObject.AddComponent<NavMeshModifier>();
            }

            modifier.overrideArea = true;
            modifier.area = IsWalkableSurface(collider) ? 0 : notWalkableArea;
            modifier.ignoreFromBuild = false;
            modifier.applyToChildren = false;
            EditorUtility.SetDirty(modifier);
            sourceCount++;
        }

        return sourceCount;
    }

    private static bool ShouldBakeCollider(Collider collider)
    {
        if (collider == null || !collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy
            || collider.attachedRigidbody != null)
        {
            return false;
        }

        for (Transform current = collider.transform; current != null; current = current.parent)
        {
            if (current.name.StartsWith("SPAWN_") || current.name.StartsWith("ENV_Warship_")
                || current.name.StartsWith("ENV_Ground_"))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWalkableSurface(Collider collider)
    {
        string objectName = collider.gameObject.name;
        return objectName == "COL_Grass" || objectName == "COL_Beach" || objectName.StartsWith("COL_Road");
    }

    private static void EnsureAssetFolder()
    {
        if (!AssetDatabase.IsValidFolder(AssetFolder))
        {
            AssetDatabase.CreateFolder("Assets/Scenes", "IslandMap");
        }
    }
}
