using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class Level03EnemySystemInstaller
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string PoliceCarAssetPath =
        "Assets/Models/Imported/Model_13/9b561855f56fe0a1be7cd3e3e952e77c.obj";
    private const string SystemName = "SYS_Level03_PoliceEnemySpawner";
    private const string SpawnName = "SPAWN_Level03_PoliceStationDoor";
    private const string NavigationName = "AI_NAVIGATION_Level03_CarSurface";
    private const string NavigationFolder = "Assets/Scenes/Level03";
    private const string NavigationAssetPath =
        NavigationFolder + "/NavMesh-AI_NAVIGATION_Level03_CarSurface.asset";
    private const string ReportPath = "Library/CodexLevel03EnemySystemReport.json";
    private const float BuildingClearance = 2.25f;
    private const float SpawnSampleRadius = 30f;

    private static readonly Vector3 RequestedSpawnPosition =
        new Vector3(-440f, 0f, -357f);

    private static readonly string[] BuildingAssetPrefixes =
    {
        "Assets/Models/Imported/Apartment/",
        "Assets/Models/Imported/Model_11/",
        "Assets/Models/Imported/Police/"
    };

    [Serializable]
    private sealed class InstallReport
    {
        public bool success;
        public string message;
        public Vector3 requestedSpawnPosition;
        public Vector3 bakedSpawnPosition;
        public float spawnAdjustmentDistance;
        public int walkableSourceCount;
        public int buildingVolumeCount;
        public int navMeshVertexCount;
        public int navMeshTriangleCount;
        public string pathStatus;
        public float pathLength;
        public float initialSpawnInterval;
        public int initialMaximumEnemies;
        public float finalSpawnInterval;
        public int finalMaximumEnemies;
        public string completedAt;
    }

    [MenuItem("Tools/Island Map/Level03/Install Police Enemy System And Bake Navigation")]
    public static void InstallFromMenu()
    {
        InstallAndBake();
    }

    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        InstallAndBake();
    }

    public static void ValidateFromCommandLine()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Level03PoliceEnemySpawner spawner =
            FindSceneObject(scene, SystemName)?.GetComponent<Level03PoliceEnemySpawner>();
        NavMeshSurface surface = FindNavigationSurface(scene);
        if (spawner == null ||
            spawner.Player == null ||
            spawner.PoliceCarVisualPrefab == null ||
            spawner.SpawnPoint == null ||
            surface == null ||
            surface.navMeshData == null)
        {
            throw new MissingReferenceException(
                "The Level03 police spawner or baked navigation data is missing.");
        }

        string policeAssetPath = AssetDatabase.GetAssetPath(spawner.PoliceCarVisualPrefab);
        if (!string.Equals(
                policeAssetPath,
                PoliceCarAssetPath,
                StringComparison.OrdinalIgnoreCase) ||
            !Mathf.Approximately(spawner.InitialSpawnInterval, 4.3f) ||
            spawner.InitialMaximumEnemies != 6 ||
            !Mathf.Approximately(spawner.FinalSpawnInterval, 1.2f) ||
            spawner.FinalMaximumEnemies != 16)
        {
            throw new InvalidOperationException(
                "The Level03 police model or spawn rhythm does not match Level01.");
        }

        PathCheck pathCheck = CheckNavigationPath(
            spawner.SpawnPoint.position,
            spawner.Player.position);
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        TerrainCollider[] terrainColliders = UnityEngine.Object
            .FindObjectsOfType<TerrainCollider>(true)
            .Where(candidate => candidate.gameObject.scene == scene)
            .ToArray();
        GameObject road = FindSceneObject(
            scene,
            "ENV_Level03_RoadNetwork_FromReference");
        Collider roadCollider = road != null ? road.GetComponent<Collider>() : null;
        if (!pathCheck.spawnSampled ||
            !pathCheck.targetSampled ||
            pathCheck.status != NavMeshPathStatus.PathComplete ||
            triangulation.vertices.Length == 0 ||
            terrainColliders.Length == 0 ||
            terrainColliders.Any(collider =>
                !NavMeshEnemyCarChaser.IsDrivingSurface(collider)) ||
            roadCollider == null ||
            !NavMeshEnemyCarChaser.IsDrivingSurface(roadCollider) ||
            Vector3.Distance(
                new Vector3(
                    spawner.SpawnPoint.position.x,
                    0f,
                    spawner.SpawnPoint.position.z),
                RequestedSpawnPosition) > SpawnSampleRadius)
        {
            throw new InvalidOperationException(
                "The police station spawn point cannot reach the Level03 player by NavMesh.");
        }

        Debug.Log(
            $"[Level03 Police Enemy Validation] PASS. Spawn={spawner.SpawnPoint.position}; " +
            $"path={pathCheck.status}; length={pathCheck.length:F1}; " +
            $"NavMesh vertices={triangulation.vertices.Length}; " +
            $"driving surfaces={terrainColliders.Length + 1}; rhythm=4.3s/6 -> 1.2s/16.");
    }

    private static void InstallAndBake()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException("Level03 must be the active scene.");
        }

        SimpleAutoDriveController player = UnityEngine.Object
            .FindObjectsOfType<SimpleAutoDriveController>(true)
            .FirstOrDefault(candidate => candidate.gameObject.scene == scene);
        GameObject policeCar = AssetDatabase.LoadAssetAtPath<GameObject>(PoliceCarAssetPath);
        if (player == null || policeCar == null)
        {
            throw new MissingReferenceException(
                "The Level03 player or Model_13 police car asset is missing.");
        }

        GameObject existingSystem = FindSceneObject(scene, SystemName);
        if (existingSystem != null)
        {
            UnityEngine.Object.DestroyImmediate(existingSystem);
        }

        GameObject system = new GameObject(SystemName);
        SceneManager.MoveGameObjectToScene(system, scene);
        GameObject spawn = new GameObject(SpawnName);
        spawn.transform.SetParent(system.transform, false);
        spawn.transform.SetPositionAndRotation(
            SampleTerrainPosition(RequestedSpawnPosition),
            Quaternion.identity);

        Level03PoliceEnemySpawner spawner =
            system.AddComponent<Level03PoliceEnemySpawner>();
        spawner.Configure(player.transform, policeCar, spawn.transform);
        EditorUtility.SetDirty(spawner);

        int notWalkableArea = NavMesh.GetAreaFromName("Not Walkable");
        if (notWalkableArea < 0)
        {
            throw new InvalidOperationException(
                "The Unity Navigation 'Not Walkable' area is missing.");
        }

        NavMeshSurface surface = GetOrCreateNavigationSurface(scene);
        ConfigureSurface(surface, notWalkableArea);
        int walkableSourceCount = ConfigureWalkableSources(notWalkableArea);
        int buildingVolumeCount = ConfigureBuildingVolumes(scene, notWalkableArea);
        if (walkableSourceCount == 0)
        {
            throw new InvalidOperationException(
                "No Level03 Terrain or road colliders were available for navigation.");
        }

        EnsureAssetFolder();
        surface.RemoveData();
        surface.navMeshData = null;
        AssetDatabase.DeleteAsset(NavigationAssetPath);
        surface.BuildNavMesh();
        if (surface.navMeshData == null)
        {
            throw new InvalidOperationException(
                "Unity could not generate Level03 police-car NavMesh data.");
        }

        AssetDatabase.CreateAsset(surface.navMeshData, NavigationAssetPath);
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        int walkableArea = NavMesh.GetAreaFromName("Walkable");
        int areaMask = walkableArea >= 0 ? 1 << walkableArea : NavMesh.AllAreas;
        if (!NavMesh.SamplePosition(
                RequestedSpawnPosition,
                out NavMeshHit spawnHit,
                SpawnSampleRadius,
                areaMask))
        {
            throw new InvalidOperationException(
                "The requested police-station door coordinate has no nearby NavMesh.");
        }

        spawn.transform.position = spawnHit.position;
        EditorUtility.SetDirty(spawn.transform);
        PathCheck pathCheck = CheckNavigationPath(spawnHit.position, player.transform.position);
        bool success = pathCheck.spawnSampled &&
            pathCheck.targetSampled &&
            pathCheck.status == NavMeshPathStatus.PathComplete &&
            triangulation.vertices.Length > 0;

        InstallReport report = new InstallReport
        {
            success = success,
            message = success
                ? "Police cars can spawn at the station door and navigate to the player."
                : "The baked police route is incomplete.",
            requestedSpawnPosition = RequestedSpawnPosition,
            bakedSpawnPosition = spawnHit.position,
            spawnAdjustmentDistance = PlanarDistance(
                RequestedSpawnPosition,
                spawnHit.position),
            walkableSourceCount = walkableSourceCount,
            buildingVolumeCount = buildingVolumeCount,
            navMeshVertexCount = triangulation.vertices.Length,
            navMeshTriangleCount = triangulation.indices.Length / 3,
            pathStatus = pathCheck.status.ToString(),
            pathLength = pathCheck.length,
            initialSpawnInterval = spawner.InitialSpawnInterval,
            initialMaximumEnemies = spawner.InitialMaximumEnemies,
            finalSpawnInterval = spawner.FinalSpawnInterval,
            finalMaximumEnemies = spawner.FinalMaximumEnemies,
            completedAt = DateTime.Now.ToString("O")
        };
        WriteReport(report);
        if (!success)
        {
            throw new InvalidOperationException(report.message);
        }

        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new IOException("Unity could not save Level03 after enemy setup.");
        }

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = spawn;
        Debug.Log(
            $"[Level03 Police Enemy] Installed at {spawnHit.position}; " +
            $"path={pathCheck.status}; length={pathCheck.length:F1}; " +
            $"NavMesh={triangulation.vertices.Length} vertices; " +
            $"buildings={buildingVolumeCount}.");
    }

    private static NavMeshSurface GetOrCreateNavigationSurface(Scene scene)
    {
        NavMeshSurface surface = FindNavigationSurface(scene);
        if (surface != null)
        {
            return surface;
        }

        GameObject navigationObject = new GameObject(NavigationName);
        SceneManager.MoveGameObjectToScene(navigationObject, scene);
        return navigationObject.AddComponent<NavMeshSurface>();
    }

    private static NavMeshSurface FindNavigationSurface(Scene scene)
    {
        return UnityEngine.Object.FindObjectsOfType<NavMeshSurface>(true)
            .FirstOrDefault(candidate =>
                candidate.gameObject.scene == scene &&
                candidate.name == NavigationName);
    }

    private static void ConfigureSurface(NavMeshSurface surface, int notWalkableArea)
    {
        surface.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        surface.transform.localScale = Vector3.one;
        surface.agentTypeID = 0;
        surface.collectObjects = CollectObjects.All;
        surface.layerMask = ~0;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.defaultArea = notWalkableArea;
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = true;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.4f;
        surface.overrideTileSize = true;
        surface.tileSize = 256;
        surface.minRegionArea = 6f;
        EditorUtility.SetDirty(surface);
    }

    private static int ConfigureWalkableSources(int notWalkableArea)
    {
        int sourceCount = 0;
        foreach (Collider collider in UnityEngine.Object.FindObjectsOfType<Collider>(true))
        {
            if (!IsStaticBakeCollider(collider) || !IsWalkableSurface(collider))
            {
                continue;
            }

            NavMeshModifier modifier = collider.GetComponent<NavMeshModifier>();
            if (modifier == null)
            {
                modifier = collider.gameObject.AddComponent<NavMeshModifier>();
            }

            modifier.overrideArea = true;
            modifier.area = 0;
            modifier.ignoreFromBuild = false;
            modifier.applyToChildren = false;
            EditorUtility.SetDirty(modifier);
            sourceCount++;
        }

        return sourceCount;
    }

    private static int ConfigureBuildingVolumes(Scene scene, int notWalkableArea)
    {
        int volumeCount = 0;
        IEnumerable<GameObject> prefabRoots = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .Where(candidate =>
                PrefabUtility.GetNearestPrefabInstanceRoot(candidate) == candidate);
        foreach (GameObject building in prefabRoots)
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(building);
            if (!BuildingAssetPrefixes.Any(prefix =>
                    path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ||
                !TryCalculateRendererBounds(building, out Bounds bounds))
            {
                continue;
            }

            NavMeshModifierVolume volume = building.GetComponent<NavMeshModifierVolume>();
            if (volume == null)
            {
                volume = building.AddComponent<NavMeshModifierVolume>();
            }

            SetWorldBounds(volume, bounds, BuildingClearance);
            volume.area = notWalkableArea;
            EditorUtility.SetDirty(volume);
            volumeCount++;
        }

        return volumeCount;
    }

    private static bool IsStaticBakeCollider(Collider collider)
    {
        return collider != null &&
            collider.enabled &&
            !collider.isTrigger &&
            collider.gameObject.activeInHierarchy &&
            collider.attachedRigidbody == null;
    }

    private static bool IsWalkableSurface(Collider collider)
    {
        if (collider is TerrainCollider)
        {
            return true;
        }

        for (Transform current = collider.transform; current != null; current = current.parent)
        {
            if (current.name == "ENV_Level03_RoadNetwork_FromReference")
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCalculateRendererBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }
        return true;
    }

    private static void SetWorldBounds(
        NavMeshModifierVolume volume,
        Bounds worldBounds,
        float horizontalClearance)
    {
        Transform transform = volume.transform;
        Vector3 scale = transform.lossyScale;
        volume.center = transform.InverseTransformPoint(worldBounds.center);
        volume.size = new Vector3(
            (worldBounds.size.x + horizontalClearance * 2f) /
                Mathf.Max(Mathf.Abs(scale.x), 0.001f),
            Mathf.Max(worldBounds.size.y, 2f) /
                Mathf.Max(Mathf.Abs(scale.y), 0.001f),
            (worldBounds.size.z + horizontalClearance * 2f) /
                Mathf.Max(Mathf.Abs(scale.z), 0.001f));
    }

    private static Vector3 SampleTerrainPosition(Vector3 requestedPosition)
    {
        Vector2 point = new Vector2(requestedPosition.x, requestedPosition.z);
        foreach (Terrain terrain in UnityEngine.Object.FindObjectsOfType<Terrain>(true))
        {
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            if (point.x < origin.x || point.x > origin.x + size.x ||
                point.y < origin.z || point.y > origin.z + size.z)
            {
                continue;
            }

            float height = terrain.SampleHeight(requestedPosition) + origin.y;
            return new Vector3(requestedPosition.x, height, requestedPosition.z);
        }

        return requestedPosition;
    }

    private sealed class PathCheck
    {
        public bool spawnSampled;
        public bool targetSampled;
        public NavMeshPathStatus status = NavMeshPathStatus.PathInvalid;
        public float length;
    }

    private static PathCheck CheckNavigationPath(Vector3 spawn, Vector3 target)
    {
        int walkableArea = NavMesh.GetAreaFromName("Walkable");
        int areaMask = walkableArea >= 0 ? 1 << walkableArea : NavMesh.AllAreas;
        PathCheck result = new PathCheck
        {
            spawnSampled = NavMesh.SamplePosition(
                spawn,
                out NavMeshHit spawnHit,
                SpawnSampleRadius,
                areaMask),
            targetSampled = NavMesh.SamplePosition(
                target,
                out NavMeshHit targetHit,
                80f,
                areaMask)
        };
        if (!result.spawnSampled || !result.targetSampled)
        {
            return result;
        }

        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(spawnHit.position, targetHit.position, areaMask, path))
        {
            return result;
        }

        result.status = path.status;
        for (int index = 1; index < path.corners.Length; index++)
        {
            result.length += Vector3.Distance(path.corners[index - 1], path.corners[index]);
        }
        return result;
    }

    private static float PlanarDistance(Vector3 first, Vector3 second)
    {
        return Vector2.Distance(
            new Vector2(first.x, first.z),
            new Vector2(second.x, second.z));
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .FirstOrDefault(candidate => candidate.name == objectName);
    }

    private static void EnsureAssetFolder()
    {
        if (!AssetDatabase.IsValidFolder(NavigationFolder))
        {
            AssetDatabase.CreateFolder("Assets/Scenes", "Level03");
        }
    }

    private static void WriteReport(InstallReport report)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        File.WriteAllText(
            Path.Combine(projectRoot, ReportPath),
            JsonUtility.ToJson(report, true));
    }
}
