using System.Collections.Generic;
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
    private const string EnclosedAreaGroupName = "AI_NAVIGATION_EnclosedAreas";
    private const float AdditionalObstacleClearance = 2.25f;
    private const float TentClusterDistance = 10f;
    private const float TentAreaPadding = 0.4f;
    private const int MinimumTentClusterSize = 3;

    [MenuItem("Tools/Island Map/Bake Enemy Navigation")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Exit Play Mode before baking enemy navigation.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

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
        int enclosedAreaCount = ConfigureTentEnclosedVolumes(notWalkableArea);
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
            $"Baked car NavMesh from {sourceCount} colliders, {clearanceVolumeCount} clearance volumes, " +
            $"and {enclosedAreaCount} enclosed areas " +
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
            if (IsInActiveScene(existingSurface) && existingSurface.name == SurfaceName)
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
        GameObject existingEnclosedAreas = GameObject.Find(EnclosedAreaGroupName);
        if (existingEnclosedAreas != null)
        {
            Object.DestroyImmediate(existingEnclosedAreas);
        }

        foreach (NavMeshModifierVolume existingVolume in Object.FindObjectsOfType<NavMeshModifierVolume>(true))
        {
            if (IsInActiveScene(existingVolume))
            {
                Object.DestroyImmediate(existingVolume);
            }
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

    private static int ConfigureTentEnclosedVolumes(int notWalkableArea)
    {
        List<Transform> tents = new List<Transform>();
        foreach (Transform candidate in Object.FindObjectsOfType<Transform>(true))
        {
            if (!IsInActiveScene(candidate) || !candidate.gameObject.activeInHierarchy
                || !candidate.name.StartsWith("MB_Tent_") || HasTentAncestor(candidate))
            {
                continue;
            }

            tents.Add(candidate);
        }

        GameObject systems = GameObject.Find("SYSTEMS");
        if (systems == null)
        {
            systems = new GameObject("SYSTEMS");
        }

        GameObject generatedGroup = new GameObject(EnclosedAreaGroupName);
        generatedGroup.transform.SetParent(systems.transform, false);
        int volumeCount = 0;
        HashSet<Transform> unassigned = new HashSet<Transform>(tents);
        while (unassigned.Count > 0)
        {
            Transform seed = null;
            foreach (Transform candidate in unassigned)
            {
                seed = candidate;
                break;
            }

            List<Transform> cluster = new List<Transform>();
            Queue<Transform> pending = new Queue<Transform>();
            pending.Enqueue(seed);
            unassigned.Remove(seed);
            while (pending.Count > 0)
            {
                Transform current = pending.Dequeue();
                cluster.Add(current);

                List<Transform> neighbours = new List<Transform>();
                foreach (Transform candidate in unassigned)
                {
                    Vector2 delta = new Vector2(
                        candidate.position.x - current.position.x,
                        candidate.position.z - current.position.z);
                    if (delta.sqrMagnitude <= TentClusterDistance * TentClusterDistance)
                    {
                        neighbours.Add(candidate);
                    }
                }

                foreach (Transform neighbour in neighbours)
                {
                    unassigned.Remove(neighbour);
                    pending.Enqueue(neighbour);
                }
            }

            if (cluster.Count < MinimumTentClusterSize || !TryGetClusterBounds(cluster, out Bounds bounds))
            {
                continue;
            }

            GameObject volumeObject = new GameObject($"ENCLOSED_TENT_CLUSTER_{volumeCount + 1:00}");
            volumeObject.transform.SetParent(generatedGroup.transform, false);
            volumeObject.transform.position = bounds.center;

            NavMeshModifierVolume volume = volumeObject.AddComponent<NavMeshModifierVolume>();
            volume.center = Vector3.zero;
            volume.size = new Vector3(
                bounds.size.x + TentAreaPadding * 2f,
                Mathf.Max(4f, bounds.size.y + 2f),
                bounds.size.z + TentAreaPadding * 2f);
            volume.area = notWalkableArea;
            EditorUtility.SetDirty(volume);
            volumeCount++;
        }

        if (volumeCount == 0)
        {
            Object.DestroyImmediate(generatedGroup);
        }

        return volumeCount;
    }

    private static bool HasTentAncestor(Transform candidate)
    {
        for (Transform parent = candidate.parent; parent != null; parent = parent.parent)
        {
            if (parent.name.StartsWith("MB_Tent_"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetClusterBounds(List<Transform> cluster, out Bounds bounds)
    {
        bounds = default;
        bool initialized = false;
        foreach (Transform tent in cluster)
        {
            foreach (Collider collider in tent.GetComponentsInChildren<Collider>(true))
            {
                if (!ShouldBakeCollider(collider))
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = collider.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
        }

        if (!initialized && cluster.Count > 0)
        {
            bounds = new Bounds(cluster[0].position, new Vector3(2f, 4f, 2f));
            for (int index = 1; index < cluster.Count; index++)
            {
                bounds.Encapsulate(cluster[index].position);
            }
            initialized = true;
        }

        return initialized;
    }

    private static bool IsClearanceObstacle(Collider collider)
    {
        for (Transform current = collider.transform; current != null; current = current.parent)
        {
            if (current.name.StartsWith("BLD_") || current.name.StartsWith("PROP_PalmTree")
                || current.name.StartsWith("PROP_StreetLight") || current.name.StartsWith("PROP_Barricade")
                || current.name.StartsWith("PROP_OilBarrel"))
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
            if (!IsInActiveScene(modifier))
            {
                continue;
            }

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
        if (!IsInActiveScene(collider) || !collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy
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

    private static bool IsInActiveScene(Component component)
    {
        if (component == null)
        {
            return false;
        }

        Scene objectScene = component.gameObject.scene;
        return objectScene.IsValid() && objectScene == SceneManager.GetActiveScene();
    }

    private static bool IsWalkableSurface(Collider collider)
    {
        for (Transform current = collider.transform; current != null; current = current.parent)
        {
            string objectName = current.name;
            if (objectName == "COL_DriveSurface" || objectName == "COL_Grass" || objectName == "COL_Beach"
                || objectName.StartsWith("COL_Road")
                || objectName.StartsWith("MB_Coastal_Sidewalk_")
                || objectName.StartsWith("MB_Sidewalk_")
                || objectName.StartsWith("MB_Road_") && !objectName.StartsWith("MB_Road_Barrier_")
                || objectName.StartsWith("MB_Bike_Path_")
                || objectName == "MB_Promenade")
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureAssetFolder()
    {
        if (!AssetDatabase.IsValidFolder(AssetFolder))
        {
            AssetDatabase.CreateFolder("Assets/Scenes", "IslandMap");
        }
    }
}
