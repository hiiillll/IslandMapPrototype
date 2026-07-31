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
    private const string NavigationName = "AI_NAVIGATION_Level03_CarSurface";
    private const string NavigationFolder = "Assets/Scenes/Level03";
    private const string NavigationAssetPath =
        NavigationFolder + "/NavMesh-AI_NAVIGATION_Level03_CarSurface.asset";
    private const string ReportPath = "Library/CodexLevel03EnemySystemReport.json";
    private const string GuardrailRemovalRequestPath =
        "Assets/Editor/Level03GuardrailRemoval.request";
    private const string GuardrailAssetPath =
        "Assets/Models/Imported/Model_18/8beefe0096835a5fc9f6a14d6b194b0e.obj";
    private const string StatueAssetPath =
        "Assets/Models/Imported/Model_19/04c0ffae5a8c4056204063aa4c69582a.obj";
    private const string BenchAssetPath =
        "Assets/Models/Imported/Model_20/004fa4a26d815acdb0f6df66efd781d7.obj";
    private const float BuildingClearance = 2.25f;
    private const float PropNavigationClearance = 0.4f;
    private const float GuardrailMass = 30f;
    private const float SpawnSampleRadius = 30f;

    static Level03EnemySystemInstaller()
    {
        EditorApplication.delayCall += ProcessGuardrailRemovalRequest;
    }

    private static readonly Vector3[] RequestedSpawnPositions =
    {
        new Vector3(-440f, 0f, -357f),
        new Vector3(135.1f, 0f, 481f),
        new Vector3(386.5f, 0f, -175.2f)
    };

    private static readonly string[] SpawnNames =
    {
        "SPAWN_Level03_PoliceStation_Southwest",
        "SPAWN_Level03_PoliceStation_North",
        "SPAWN_Level03_PoliceStation_East"
    };

    private static readonly string[] BuildingAssetPrefixes =
    {
        "Assets/Models/Imported/Apartment/",
        "Assets/Models/Imported/Model_11/",
        "Assets/Models/Imported/Police/"
    };

    private static readonly string[] ResidentialBuildingAssetPaths =
    {
        "Assets/Models/Imported/Apartment/f107add5ea68f5a00af639a36564417a.obj",
        "Assets/Models/Imported/Model_11/6eb03c288ccd1d188ca79ea31aa326aa.obj"
    };

    [Serializable]
    private sealed class SpawnReport
    {
        public string name;
        public Vector3 requestedPosition;
        public Vector3 bakedPosition;
        public float adjustmentDistance;
        public string pathStatus;
        public float pathLength;
    }

    [Serializable]
    private sealed class PropObstacleSetup
    {
        public int benchCount;
        public int statueCount;
        public int guardrailCount;
    }

    [Serializable]
    private sealed class InstallReport
    {
        public bool success;
        public string message;
        public SpawnReport[] spawns;
        public int addedPoliceStationColliderCount;
        public int residentialBuildingColliderCount;
        public int benchColliderCount;
        public int statueColliderCount;
        public int knockableGuardrailCount;
        public int walkableSourceCount;
        public int buildingVolumeCount;
        public int navMeshVertexCount;
        public int navMeshTriangleCount;
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
            spawner.SpawnPoints == null ||
            spawner.SpawnPoints.Count != RequestedSpawnPositions.Length ||
            spawner.SpawnPoints.Any(spawn => spawn == null) ||
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

        PathCheck[] pathChecks = spawner.SpawnPoints
            .Select(spawn => CheckNavigationPath(spawn.position, spawner.Player.position))
            .ToArray();
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        TerrainCollider[] terrainColliders = UnityEngine.Object
            .FindObjectsOfType<TerrainCollider>(true)
            .Where(candidate => candidate.gameObject.scene == scene)
            .ToArray();
        GameObject road = FindSceneObject(
            scene,
            "ENV_Level03_RoadNetwork_FromReference");
        Collider roadCollider = road != null ? road.GetComponent<Collider>() : null;
        List<GameObject> newPoliceStations = FindNewPoliceStations(scene);
        bool validNewStationColliders = newPoliceStations.Count == 2 &&
            newPoliceStations.All(station =>
            {
                BoxCollider collider = station.GetComponent<BoxCollider>();
                return collider != null && collider.enabled && !collider.isTrigger;
            });
        List<GameObject> residentialBuildings = FindResidentialBuildings(scene);
        bool validResidentialColliders = residentialBuildings.Count > 0 &&
            residentialBuildings.All(building =>
            {
                BoxCollider collider = building.GetComponent<BoxCollider>();
                return collider != null && collider.enabled && !collider.isTrigger;
            });
        PropObstacleSetup propSetup = ValidatePropObstacles(scene);
        bool validProps = propSetup.benchCount == 18 &&
                          propSetup.statueCount == 7 &&
                          propSetup.guardrailCount == 0;
        bool validSpawnPaths = pathChecks.All(path =>
            path.spawnSampled &&
            path.targetSampled &&
            path.status == NavMeshPathStatus.PathComplete);
        bool validSpawnPositions = spawner.SpawnPoints
            .Select((spawn, index) => PlanarDistance(
                spawn.position,
                RequestedSpawnPositions[index]) <= SpawnSampleRadius)
            .All(valid => valid);
        if (!validSpawnPaths ||
            !validSpawnPositions ||
            triangulation.vertices.Length == 0 ||
            terrainColliders.Length == 0 ||
            terrainColliders.Any(collider =>
                !NavMeshEnemyCarChaser.IsDrivingSurface(collider)) ||
            roadCollider == null ||
            !NavMeshEnemyCarChaser.IsDrivingSurface(roadCollider) ||
            !validNewStationColliders ||
            !validResidentialColliders ||
            !validProps)
        {
            throw new InvalidOperationException(
                "The police station spawn point cannot reach the Level03 player by NavMesh.");
        }

        string statueColliderSummary = string.Join(
            "; ",
            FindPrefabInstances(scene, StatueAssetPath)
                .OrderBy(statue => statue.name)
                .Select(statue =>
                {
                    BoxCollider collider = statue.GetComponent<BoxCollider>();
                    return collider == null
                        ? $"{statue.name}=missing"
                        : $"{statue.name}=center{collider.center}/size{collider.size}";
                }));
        Debug.Log(
            $"[Level03 Police Enemy Validation] PASS. Spawns=" +
            $"{string.Join(", ", spawner.SpawnPoints.Select(spawn => spawn.position))}; " +
            $"paths={string.Join(", ", pathChecks.Select(path => path.status))}; " +
            $"NavMesh vertices={triangulation.vertices.Length}; " +
            $"new station colliders={newPoliceStations.Count}; " +
            $"residential colliders={residentialBuildings.Count}; " +
            $"props={propSetup.benchCount} benches/{propSetup.statueCount} statues/" +
            $"{propSetup.guardrailCount} knockable guardrails; " +
            $"statue colliders=[{statueColliderSummary}]; " +
            $"driving surfaces={terrainColliders.Length + 1}; rhythm=4.3s/6 -> 1.2s/16.");
    }

    [MenuItem("Tools/Island Map/Level03/Remove All Guardrails And Rebuild Navigation")]
    public static void RemoveGuardrailsFromMenu()
    {
        RemoveGuardrailsAndRebuildNavigation();
    }

    public static void RemoveGuardrailsFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RemoveGuardrailsAndRebuildNavigation();
    }

    private static void ProcessGuardrailRemovalRequest()
    {
        if (!File.Exists(GuardrailRemovalRequestPath))
        {
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ProcessGuardrailRemovalRequest;
            return;
        }

        AssetDatabase.DeleteAsset(GuardrailRemovalRequestPath);
        RemoveGuardrailsFromCommandLine();
    }

    private static void RemoveGuardrailsAndRebuildNavigation()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException("Level03 must be the active scene.");
        }

        List<GameObject> guardrails = FindPrefabInstances(scene, GuardrailAssetPath);
        foreach (GameObject guardrail in guardrails)
        {
            UnityEngine.Object.DestroyImmediate(guardrail);
        }

        GameObject guardrailGroup = FindSceneObject(scene, "PROP_Level03_Guardrails");
        if (guardrailGroup != null)
        {
            UnityEngine.Object.DestroyImmediate(guardrailGroup);
        }

        InstallAndBake();
        Debug.Log(
            $"[Level03 Guardrails] Removed {guardrails.Count} guardrails and " +
            "rebuilt the Level03 navigation mesh.");
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
        Transform[] spawnPoints = new Transform[RequestedSpawnPositions.Length];
        for (int index = 0; index < RequestedSpawnPositions.Length; index++)
        {
            GameObject spawn = new GameObject(SpawnNames[index]);
            spawn.transform.SetParent(system.transform, false);
            spawn.transform.SetPositionAndRotation(
                SampleTerrainPosition(RequestedSpawnPositions[index]),
                Quaternion.identity);
            spawnPoints[index] = spawn.transform;
        }

        Level03PoliceEnemySpawner spawner =
            system.AddComponent<Level03PoliceEnemySpawner>();
        spawner.Configure(player.transform, policeCar, spawnPoints);
        EditorUtility.SetDirty(spawner);

        int addedColliderCount = EnsureNewPoliceStationColliders(scene);
        int residentialColliderCount = EnsureResidentialBuildingColliders(scene);

        int notWalkableArea = NavMesh.GetAreaFromName("Not Walkable");
        if (notWalkableArea < 0)
        {
            throw new InvalidOperationException(
                "The Unity Navigation 'Not Walkable' area is missing.");
        }

        PropObstacleSetup propSetup = ConfigurePropObstacles(scene, notWalkableArea);

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
        SpawnReport[] spawnReports = new SpawnReport[RequestedSpawnPositions.Length];
        bool success = triangulation.vertices.Length > 0;
        for (int index = 0; index < RequestedSpawnPositions.Length; index++)
        {
            Vector3 requested = RequestedSpawnPositions[index];
            if (!NavMesh.SamplePosition(
                    requested,
                    out NavMeshHit spawnHit,
                    SpawnSampleRadius,
                    areaMask))
            {
                throw new InvalidOperationException(
                    $"Police spawn '{SpawnNames[index]}' has no nearby NavMesh.");
            }

            spawnPoints[index].position = spawnHit.position;
            EditorUtility.SetDirty(spawnPoints[index]);
            PathCheck pathCheck = CheckNavigationPath(spawnHit.position, player.transform.position);
            success &= pathCheck.spawnSampled &&
                       pathCheck.targetSampled &&
                       pathCheck.status == NavMeshPathStatus.PathComplete;
            spawnReports[index] = new SpawnReport
            {
                name = SpawnNames[index],
                requestedPosition = requested,
                bakedPosition = spawnHit.position,
                adjustmentDistance = PlanarDistance(requested, spawnHit.position),
                pathStatus = pathCheck.status.ToString(),
                pathLength = pathCheck.length
            };
        }

        InstallReport report = new InstallReport
        {
            success = success,
            message = success
                ? "Police cars can spawn at all three station doors and navigate to the player."
                : "One or more baked police routes are incomplete.",
            spawns = spawnReports,
            addedPoliceStationColliderCount = addedColliderCount,
            residentialBuildingColliderCount = residentialColliderCount,
            benchColliderCount = propSetup.benchCount,
            statueColliderCount = propSetup.statueCount,
            knockableGuardrailCount = propSetup.guardrailCount,
            walkableSourceCount = walkableSourceCount,
            buildingVolumeCount = buildingVolumeCount,
            navMeshVertexCount = triangulation.vertices.Length,
            navMeshTriangleCount = triangulation.indices.Length / 3,
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
        Selection.activeGameObject = system;
        Debug.Log(
            $"[Level03 Police Enemy] Installed {spawnPoints.Length} spawn points; " +
            $"NavMesh={triangulation.vertices.Length} vertices; " +
            $"buildings={buildingVolumeCount}; new station colliders={addedColliderCount}; " +
            $"residential colliders={residentialColliderCount}; " +
            $"props={propSetup.benchCount}/{propSetup.statueCount}/" +
            $"{propSetup.guardrailCount}.");
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

    private static PropObstacleSetup ConfigurePropObstacles(
        Scene scene,
        int notWalkableArea)
    {
        List<GameObject> benches = FindPrefabInstances(scene, BenchAssetPath);
        List<GameObject> statues = FindPrefabInstances(scene, StatueAssetPath);
        List<GameObject> guardrails = FindPrefabInstances(scene, GuardrailAssetPath);

        foreach (GameObject bench in benches)
        {
            ConfigureStaticProp(bench, notWalkableArea);
        }
        foreach (GameObject statue in statues)
        {
            ConfigureStatue(statue, notWalkableArea);
        }
        foreach (GameObject guardrail in guardrails)
        {
            ConfigureKnockableGuardrail(guardrail, notWalkableArea);
        }

        return new PropObstacleSetup
        {
            benchCount = benches.Count,
            statueCount = statues.Count,
            guardrailCount = guardrails.Count
        };
    }

    private static PropObstacleSetup ValidatePropObstacles(Scene scene)
    {
        int notWalkableArea = NavMesh.GetAreaFromName("Not Walkable");
        List<GameObject> benches = FindPrefabInstances(scene, BenchAssetPath);
        List<GameObject> statues = FindPrefabInstances(scene, StatueAssetPath);
        List<GameObject> guardrails = FindPrefabInstances(scene, GuardrailAssetPath);
        return new PropObstacleSetup
        {
            benchCount = benches.Count(item =>
                HasSolidColliderAndNavigationVolume(item, notWalkableArea)),
            statueCount = statues.Count(item =>
                HasSolidColliderAndNavigationVolume(item, notWalkableArea)),
            guardrailCount = guardrails.Count(item =>
            {
                Rigidbody body = item.GetComponent<Rigidbody>();
                return HasSolidColliderAndNavigationVolume(item, notWalkableArea) &&
                       item.GetComponent<Level03KnockableGuardrail>() != null &&
                       body != null &&
                       !body.isKinematic &&
                       body.useGravity;
            })
        };
    }

    private static bool HasSolidColliderAndNavigationVolume(
        GameObject gameObject,
        int notWalkableArea)
    {
        BoxCollider collider = gameObject.GetComponent<BoxCollider>();
        NavMeshModifierVolume volume = gameObject.GetComponent<NavMeshModifierVolume>();
        return collider != null &&
               collider.enabled &&
               !collider.isTrigger &&
               volume != null &&
               volume.enabled &&
               volume.area == notWalkableArea;
    }

    private static void ConfigureStaticProp(GameObject prop, int notWalkableArea)
    {
        ConfigureBoxCollider(prop, Vector3.one);
        ConfigureNavigationVolume(prop, notWalkableArea);
    }

    private static void ConfigureStatue(GameObject statue, int notWalkableArea)
    {
        BoxCollider collider = statue.GetComponent<BoxCollider>();
        if (collider == null)
        {
            ConfigureBoxCollider(statue, Vector3.one);
            collider = statue.GetComponent<BoxCollider>();
        }
        collider.enabled = true;
        collider.isTrigger = false;
        EditorUtility.SetDirty(collider);

        NavMeshModifierVolume volume = statue.GetComponent<NavMeshModifierVolume>();
        if (volume == null)
        {
            volume = statue.AddComponent<NavMeshModifierVolume>();
        }
        volume.enabled = true;
        volume.area = notWalkableArea;
        SetLocalBounds(
            volume,
            new Bounds(collider.center, collider.size),
            PropNavigationClearance);
        EditorUtility.SetDirty(volume);
    }

    private static void ConfigureKnockableGuardrail(
        GameObject guardrail,
        int notWalkableArea)
    {
        ConfigureBoxCollider(guardrail, new Vector3(0.92f, 1f, 0.92f));
        ConfigureNavigationVolume(guardrail, notWalkableArea);
        if (guardrail.GetComponent<Level03KnockableGuardrail>() == null)
        {
            guardrail.AddComponent<Level03KnockableGuardrail>();
        }

        Rigidbody body = guardrail.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = guardrail.AddComponent<Rigidbody>();
        }
        body.mass = GuardrailMass;
        body.drag = 0.1f;
        body.angularDrag = 0.5f;
        body.useGravity = true;
        body.isKinematic = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.constraints = RigidbodyConstraints.None;
        body.maxAngularVelocity = 20f;
        body.maxDepenetrationVelocity = 12f;
        guardrail.isStatic = false;
        GameObjectUtility.SetStaticEditorFlags(guardrail, 0);
        EditorUtility.SetDirty(body);
        EditorUtility.SetDirty(guardrail);
    }

    private static void ConfigureBoxCollider(GameObject root, Vector3 sizeMultiplier)
    {
        if (!TryCalculateLocalRendererBounds(root, out Bounds localBounds))
        {
            throw new InvalidOperationException(
                $"Prop '{root.name}' has no renderer bounds for collision.");
        }

        BoxCollider collider = root.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = root.AddComponent<BoxCollider>();
        }
        collider.enabled = true;
        collider.isTrigger = false;
        collider.center = localBounds.center;
        collider.size = Vector3.Scale(localBounds.size, sizeMultiplier);
        EditorUtility.SetDirty(collider);
    }

    private static void ConfigureNavigationVolume(
        GameObject root,
        int notWalkableArea)
    {
        if (!TryCalculateLocalRendererBounds(root, out Bounds localBounds))
        {
            throw new InvalidOperationException(
                $"Prop '{root.name}' has no renderer bounds for navigation.");
        }

        NavMeshModifierVolume volume = root.GetComponent<NavMeshModifierVolume>();
        if (volume == null)
        {
            volume = root.AddComponent<NavMeshModifierVolume>();
        }
        volume.enabled = true;
        volume.area = notWalkableArea;
        SetLocalBounds(volume, localBounds, PropNavigationClearance);
        EditorUtility.SetDirty(volume);
    }

    private static List<GameObject> FindPrefabInstances(Scene scene, string assetPath)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .Where(candidate =>
                PrefabUtility.GetNearestPrefabInstanceRoot(candidate) == candidate)
            .Where(candidate => string.Equals(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate),
                assetPath,
                StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
    }

    private static int EnsureNewPoliceStationColliders(Scene scene)
    {
        List<GameObject> newPoliceStations = FindNewPoliceStations(scene);
        int configuredCount = 0;
        foreach (GameObject station in newPoliceStations)
        {
            if (!TryCalculateLocalRendererBounds(station, out Bounds localBounds))
            {
                throw new InvalidOperationException(
                    $"Police station '{station.name}' has no renderer bounds for collision.");
            }

            BoxCollider collider = station.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = station.AddComponent<BoxCollider>();
            }
            collider.enabled = true;
            collider.isTrigger = false;
            collider.center = localBounds.center;
            collider.size = localBounds.size;
            EditorUtility.SetDirty(collider);
            configuredCount++;
        }
        return configuredCount;
    }

    private static int EnsureResidentialBuildingColliders(Scene scene)
    {
        List<GameObject> residentialBuildings = FindResidentialBuildings(scene);
        foreach (GameObject building in residentialBuildings)
        {
            ConfigureBoxCollider(building, Vector3.one);
        }
        return residentialBuildings.Count;
    }

    private static List<GameObject> FindResidentialBuildings(Scene scene)
    {
        return ResidentialBuildingAssetPaths
            .SelectMany(path => FindPrefabInstances(scene, path))
            .Distinct()
            .Where(building =>
                TryCalculateLocalRendererBounds(building, out _))
            .ToList();
    }

    private static List<GameObject> FindNewPoliceStations(Scene scene)
    {
        List<GameObject> policeStations = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .Where(candidate =>
                PrefabUtility.GetNearestPrefabInstanceRoot(candidate) == candidate)
            .Where(candidate => PrefabUtility
                .GetPrefabAssetPathOfNearestInstanceRoot(candidate)
                .StartsWith(
                    "Assets/Models/Imported/Police/",
                    StringComparison.OrdinalIgnoreCase))
            .ToList();
        HashSet<GameObject> assigned = new HashSet<GameObject>();
        List<GameObject> result = new List<GameObject>();
        for (int spawnIndex = 1; spawnIndex < RequestedSpawnPositions.Length; spawnIndex++)
        {
            Vector3 requestedSpawn = RequestedSpawnPositions[spawnIndex];
            GameObject station = policeStations
                .Where(candidate => !assigned.Contains(candidate))
                .OrderBy(candidate => PlanarDistance(
                    candidate.transform.position,
                    requestedSpawn))
                .FirstOrDefault();
            if (station == null ||
                PlanarDistance(station.transform.position, requestedSpawn) > 100f)
            {
                throw new MissingReferenceException(
                    $"No new police station was found near '{SpawnNames[spawnIndex]}'.");
            }

            assigned.Add(station);
            result.Add(station);
        }
        return result;
    }

    private static bool TryCalculateLocalRendererBounds(
        GameObject root,
        out Bounds localBounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool initialized = false;
        localBounds = default;
        foreach (Renderer renderer in renderers)
        {
            EncapsulateTransformedBounds(
                root.transform,
                renderer.transform,
                renderer.localBounds,
                ref localBounds,
                ref initialized);
        }

        if (initialized)
        {
            return true;
        }

        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
            {
                continue;
            }

            EncapsulateTransformedBounds(
                root.transform,
                filter.transform,
                filter.sharedMesh.bounds,
                ref localBounds,
                ref initialized);
        }
        return initialized;
    }

    private static void EncapsulateTransformedBounds(
        Transform root,
        Transform source,
        Bounds sourceBounds,
        ref Bounds localBounds,
        ref bool initialized)
    {
        Vector3 min = sourceBounds.min;
        Vector3 max = sourceBounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };
        foreach (Vector3 corner in corners)
        {
            Vector3 localPoint = root.InverseTransformPoint(source.TransformPoint(corner));
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

    private static void SetLocalBounds(
        NavMeshModifierVolume volume,
        Bounds localBounds,
        float horizontalClearance)
    {
        Vector3 scale = volume.transform.lossyScale;
        volume.center = localBounds.center;
        volume.size = new Vector3(
            localBounds.size.x + horizontalClearance * 2f /
                Mathf.Max(Mathf.Abs(scale.x), 0.001f),
            Mathf.Max(
                localBounds.size.y,
                2f / Mathf.Max(Mathf.Abs(scale.y), 0.001f)),
            localBounds.size.z + horizontalClearance * 2f /
                Mathf.Max(Mathf.Abs(scale.z), 0.001f));
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
