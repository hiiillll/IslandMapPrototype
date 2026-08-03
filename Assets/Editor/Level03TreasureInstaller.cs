using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Level03TreasureInstaller
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string TreasureFolder = "Assets/Level03/Treasure";
    private const string PrefabPath = TreasureFolder + "/PF_Level03_TreasureChest.prefab";
    private const string WoodMaterialPath = TreasureFolder + "/MAT_TreasureChest_Wood.mat";
    private const string MetalMaterialPath = TreasureFolder + "/MAT_TreasureChest_Metal.mat";
    private const string BeamMeshPath = TreasureFolder + "/MESH_TreasureBeacon.asset";
    private const string BeamCoreMaterialPath =
        TreasureFolder + "/MAT_TreasureBeacon_Core.mat";
    private const string BeamGlowMaterialPath =
        TreasureFolder + "/MAT_TreasureBeacon_Glow.mat";
    private const string ObjectiveName = "SYS_Level03_TreasureObjective";
    private const string ExtractionName = "SYS_Level03_PlaneExtraction";
    private const string PlaneAssetPath =
        "Assets/Level04/Models/Player/7e82465a5265349baef858b3f34b69a2.obj";
    private const string ReportPath = "Library/CodexLevel03TreasureInstallReport.json";
    private const int TotalChestCount = 5;
    private const int RequiredChestCount = 4;
    private const float MainIslandMaximumRadius = 610f;
    private const float MinimumBuildingGap = 5.5f;
    private const float MaximumBuildingGap = 42f;
    private const float BuildingClearance = 2.6f;
    private const float MinimumChestSeparation = 115f;
    private const float MaximumSlope = 15f;

    private static readonly string[] BuildingAssetPrefixes =
    {
        "Assets/Models/Imported/Apartment/",
        "Assets/Models/Imported/Model_11/"
    };

    [Serializable]
    private sealed class InstallReport
    {
        public bool success;
        public string message;
        public int buildingCount;
        public int candidateCount;
        public int chestCount;
        public ChestReport[] chests;
        public string completedAt;
    }

    [Serializable]
    private sealed class ChestReport
    {
        public string id;
        public Vector3 position;
        public string firstBuilding;
        public string secondBuilding;
        public float firstBuildingDistance;
        public float secondBuildingDistance;
        public float nearestOtherChestDistance;
        public float groundSlope;
        public bool outsideAllBuildings;
    }

    private sealed class BuildingInfo
    {
        public GameObject gameObject;
        public Bounds bounds;
    }

    private sealed class Candidate
    {
        public Vector3 position;
        public BuildingInfo first;
        public BuildingInfo second;
        public float firstDistance;
        public float secondDistance;
        public float slope;
    }

    [MenuItem("Tools/Island Map/Level03/Install Five Treasure Chests")]
    public static void InstallFromMenu()
    {
        Install();
    }

    public static void InstallFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Install();
    }

    public static void ValidateFromCommandLine()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject objectiveObject = FindSceneObject(scene, ObjectiveName);
        Level03TreasureObjective objective = objectiveObject != null
            ? objectiveObject.GetComponent<Level03TreasureObjective>()
            : null;
        if (objective == null)
        {
            throw new MissingReferenceException("The Level03 treasure objective is missing.");
        }

        Level03TreasureChest[] chests =
            objective.GetComponentsInChildren<Level03TreasureChest>(true);
        if (chests.Length != TotalChestCount ||
            objective.RequiredChestCount != RequiredChestCount)
        {
            throw new InvalidOperationException(
                $"Expected {TotalChestCount} treasure chests with " +
                $"{RequiredChestCount} required, found {chests.Length} with " +
                $"{objective.RequiredChestCount} required.");
        }

        if (chests.Any(chest =>
                chest.GetComponent<Level03TreasureBeacon>() == null ||
                !chest.GetComponent<Level03TreasureBeacon>().IsConfigured))
        {
            throw new InvalidOperationException(
                "Every Level03 treasure chest must have a configured light beacon.");
        }

        int collectedEvents = 0;
        int completedEvents = 0;
        objective.ChestCollected += (_, _) => collectedEvents++;
        objective.AllChestsCollected += () => completedEvents++;
        for (int index = 0; index < RequiredChestCount; index++)
        {
            Level03TreasureChest chest = chests[index];
            if (!objective.TryCollect(chest))
            {
                throw new InvalidOperationException(
                    "A unique Level03 treasure chest could not be collected: " + chest.ChestId);
            }
        }

        if (objective.CollectedCount != RequiredChestCount ||
            collectedEvents != RequiredChestCount ||
            completedEvents != 1)
        {
            throw new InvalidOperationException(
                "Collecting four treasures did not unlock completion exactly once.");
        }

        if (!objective.TryCollect(chests[RequiredChestCount]) ||
            objective.TryCollect(chests[0]) ||
            objective.CollectedCount != TotalChestCount ||
            collectedEvents != TotalChestCount ||
            completedEvents != 1)
        {
            throw new InvalidOperationException(
                "The optional fifth treasure or one-time completion contract failed.");
        }

        GameObject extractionObject = FindSceneObject(scene, ExtractionName);
        Level03PlaneExtraction extraction = extractionObject != null
            ? extractionObject.GetComponent<Level03PlaneExtraction>()
            : null;
        if (extraction == null ||
            extraction.TreasureObjective != objective ||
            extraction.PlaneRoot == null ||
            extraction.InteractionRadius <= 0f ||
            extractionObject.GetComponentsInChildren<Collider>(true).Length != 0 ||
            !extraction.CanEvacuateAt(extraction.transform.position) ||
            extraction.CanEvacuateAt(
                extraction.transform.position +
                Vector3.right * (extraction.InteractionRadius + 1f)))
        {
            throw new InvalidOperationException(
                "The Level03 plane extraction objective is missing or invalid.");
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Collider[] prefabColliders = prefab != null
            ? prefab.GetComponentsInChildren<Collider>(true)
            : Array.Empty<Collider>();
        Level03TreasureBeacon prefabBeacon = prefab != null
            ? prefab.GetComponent<Level03TreasureBeacon>()
            : null;
        if (prefab == null || prefabColliders.Length != 1 ||
            !(prefabColliders[0] is SphereCollider) ||
            !prefabColliders[0].isTrigger ||
            prefabBeacon == null ||
            !prefabBeacon.IsConfigured)
        {
            throw new InvalidOperationException(
                "The treasure prefab must contain one trigger, no solid colliders, " +
                "and a configured light beacon.");
        }

        Debug.Log(
            $"[Level03 Treasure Validation] PASS. Chests={chests.Length}; " +
            $"required={objective.RequiredChestCount}; " +
            $"collected events={collectedEvents}; completion events={completedEvents}; " +
            "beacons=5; solid chest colliders=0; plane extraction range is valid.");
    }

    [MenuItem("Tools/Island Map/Level03/Refresh Treasure Chest Light Beacons")]
    public static void RefreshChestBeaconsFromMenu()
    {
        RefreshChestBeacons();
    }

    public static void RefreshChestBeaconsFromCommandLine()
    {
        RefreshChestBeacons();
    }

    private static void RefreshChestBeacons()
    {
        EnsureFolder(TreasureFolder);
        BuildChestPrefab();
        AssetDatabase.SaveAssets();
        Debug.Log(
            "[Level03 Treasure Beacon] Refreshed the treasure prefab with " +
            "a pulsing core and glow column.");
    }

    private static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException("Level03 must be the active scene.");
        }

        EnsureFolder(TreasureFolder);
        GameObject chestPrefab = BuildChestPrefab();
        List<BuildingInfo> buildings = FindCentralIslandBuildings(scene);
        Terrain[] terrains = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Terrain>(true))
            .ToArray();
        List<Candidate> candidates = BuildCandidates(buildings, terrains);
        List<Candidate> selected = SelectDistributedCandidates(candidates);
        if (selected.Count != TotalChestCount)
        {
            WriteReport(new InstallReport
            {
                success = false,
                message = $"Only {selected.Count} valid house-gap positions were found.",
                buildingCount = buildings.Count,
                candidateCount = candidates.Count,
                chestCount = selected.Count,
                completedAt = DateTime.Now.ToString("O")
            });
            throw new InvalidOperationException(
                "Could not find five safe, distributed positions between Level03 houses.");
        }

        GameObject existing = FindSceneObject(scene, ObjectiveName);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }

        GameObject objectiveObject = new GameObject(ObjectiveName);
        SceneManager.MoveGameObjectToScene(objectiveObject, scene);
        Level03TreasureObjective objective =
            objectiveObject.AddComponent<Level03TreasureObjective>();
        List<Level03TreasureChest> placedChests = new List<Level03TreasureChest>();
        for (int index = 0; index < selected.Count; index++)
        {
            GameObject chestObject = (GameObject)PrefabUtility.InstantiatePrefab(
                chestPrefab,
                scene);
            chestObject.name = $"TREASURE_Level03_{index + 1:00}";
            chestObject.transform.SetParent(objectiveObject.transform, true);
            chestObject.transform.position = selected[index].position;
            chestObject.transform.rotation = Quaternion.Euler(
                0f,
                Mathf.Atan2(
                    selected[index].second.bounds.center.x -
                    selected[index].first.bounds.center.x,
                    selected[index].second.bounds.center.z -
                    selected[index].first.bounds.center.z) * Mathf.Rad2Deg,
                0f);
            Level03TreasureChest chest = chestObject.GetComponent<Level03TreasureChest>();
            chest.Configure($"Level03_Chest_{index + 1:00}", objective);
            EditorUtility.SetDirty(chest);
            placedChests.Add(chest);
        }

        objective.Configure(placedChests.ToArray(), RequiredChestCount);
        EditorUtility.SetDirty(objective);
        InstallPlaneExtraction(scene, objective);
        Physics.SyncTransforms();
        InstallReport report = ValidateInstall(buildings, candidates, selected);
        WriteReport(report);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new IOException("Unity could not save Level03 after placing treasure chests.");
        }

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = objectiveObject;
        Debug.Log(
            $"[Level03 Treasure] Installed {placedChests.Count} chests between houses. " +
            $"Candidates={candidates.Count}; validation success={report.success}.");
    }

    private static List<BuildingInfo> FindCentralIslandBuildings(Scene scene)
    {
        List<BuildingInfo> buildings = new List<BuildingInfo>();
        foreach (GameObject candidate in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                     .Select(transform => transform.gameObject)
                     .Where(candidate =>
                         PrefabUtility.GetNearestPrefabInstanceRoot(candidate) == candidate))
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate);
            if (!BuildingAssetPrefixes.Any(prefix =>
                    path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ||
                new Vector2(candidate.transform.position.x, candidate.transform.position.z)
                    .magnitude > MainIslandMaximumRadius ||
                !TryCalculateBounds(candidate, out Bounds bounds))
            {
                continue;
            }

            buildings.Add(new BuildingInfo
            {
                gameObject = candidate,
                bounds = bounds
            });
        }

        return buildings;
    }

    private static List<Candidate> BuildCandidates(
        IReadOnlyList<BuildingInfo> buildings,
        IReadOnlyList<Terrain> terrains)
    {
        List<Candidate> candidates = new List<Candidate>();
        for (int firstIndex = 0; firstIndex < buildings.Count; firstIndex++)
        {
            BuildingInfo first = buildings[firstIndex];
            for (int secondIndex = firstIndex + 1;
                 secondIndex < buildings.Count;
                 secondIndex++)
            {
                BuildingInfo second = buildings[secondIndex];
                Vector2 firstPoint = ClosestPoint(
                    first.bounds,
                    new Vector2(second.bounds.center.x, second.bounds.center.z));
                Vector2 secondPoint = ClosestPoint(
                    second.bounds,
                    new Vector2(first.bounds.center.x, first.bounds.center.z));
                float gap = Vector2.Distance(firstPoint, secondPoint);
                if (gap < MinimumBuildingGap || gap > MaximumBuildingGap)
                {
                    continue;
                }

                Vector2 midpoint = (firstPoint + secondPoint) * 0.5f;
                if (midpoint.magnitude > MainIslandMaximumRadius ||
                    !IsClearOfBuildings(midpoint, buildings, BuildingClearance) ||
                    !TrySampleTerrain(
                        terrains,
                        midpoint,
                        out float groundHeight,
                        out float slope) ||
                    slope > MaximumSlope)
                {
                    continue;
                }

                candidates.Add(new Candidate
                {
                    position = new Vector3(midpoint.x, groundHeight + 0.04f, midpoint.y),
                    first = first,
                    second = second,
                    firstDistance = HorizontalDistance(midpoint, first.bounds),
                    secondDistance = HorizontalDistance(midpoint, second.bounds),
                    slope = slope
                });
            }
        }

        return candidates;
    }

    private static List<Candidate> SelectDistributedCandidates(List<Candidate> candidates)
    {
        float[] targetAngles = { -144f, -72f, 0f, 72f, 144f };
        List<Candidate> selected = new List<Candidate>(TotalChestCount);
        foreach (float targetAngle in targetAngles)
        {
            Candidate best = candidates
                .Where(candidate => selected.All(existing =>
                    HorizontalDistance(candidate.position, existing.position) >=
                    MinimumChestSeparation))
                .OrderBy(candidate => CandidateScore(candidate, targetAngle))
                .FirstOrDefault();
            if (best != null)
            {
                selected.Add(best);
            }
        }

        return selected;
    }

    private static float CandidateScore(Candidate candidate, float targetAngle)
    {
        float angle = Mathf.Atan2(candidate.position.z, candidate.position.x) * Mathf.Rad2Deg;
        float angleDifference = Mathf.Abs(Mathf.DeltaAngle(angle, targetAngle));
        float radius = new Vector2(candidate.position.x, candidate.position.z).magnitude;
        float radiusPenalty = Mathf.Abs(radius - 390f) * 0.08f;
        float gapBalancePenalty = Mathf.Abs(
            candidate.firstDistance - candidate.secondDistance) * 0.5f;
        return angleDifference * 4f + radiusPenalty + gapBalancePenalty;
    }

    private static InstallReport ValidateInstall(
        IReadOnlyList<BuildingInfo> buildings,
        IReadOnlyCollection<Candidate> candidates,
        IReadOnlyList<Candidate> selected)
    {
        ChestReport[] chestReports = new ChestReport[selected.Count];
        bool success = selected.Count == TotalChestCount;
        for (int index = 0; index < selected.Count; index++)
        {
            Candidate candidate = selected[index];
            Vector2 point = new Vector2(candidate.position.x, candidate.position.z);
            bool outside = IsClearOfBuildings(point, buildings, 0.05f);
            float nearestChest = selected
                .Where((item, otherIndex) => otherIndex != index)
                .Select(item => HorizontalDistance(candidate.position, item.position))
                .DefaultIfEmpty(float.PositiveInfinity)
                .Min();
            success &= outside && nearestChest >= MinimumChestSeparation;
            chestReports[index] = new ChestReport
            {
                id = $"Level03_Chest_{index + 1:00}",
                position = candidate.position,
                firstBuilding = candidate.first.gameObject.name,
                secondBuilding = candidate.second.gameObject.name,
                firstBuildingDistance = candidate.firstDistance,
                secondBuildingDistance = candidate.secondDistance,
                nearestOtherChestDistance = nearestChest,
                groundSlope = candidate.slope,
                outsideAllBuildings = outside
            };
        }

        return new InstallReport
        {
            success = success,
            message = success
                ? "Five chests were placed between central-island houses without entering building bounds."
                : "One or more treasure placements failed validation.",
            buildingCount = buildings.Count,
            candidateCount = candidates.Count,
            chestCount = selected.Count,
            chests = chestReports,
            completedAt = DateTime.Now.ToString("O")
        };
    }

    private static void InstallPlaneExtraction(
        Scene scene,
        Level03TreasureObjective objective)
    {
        GameObject plane = FindPlacedPlane(scene);
        if (plane == null)
        {
            throw new MissingReferenceException(
                "Place the Level04 player plane model in Level03 before installing extraction.");
        }

        GameObject existing = FindSceneObject(scene, ExtractionName);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }

        GameObject extractionObject = new GameObject(ExtractionName);
        SceneManager.MoveGameObjectToScene(extractionObject, scene);
        Level03PlaneExtraction extraction =
            extractionObject.AddComponent<Level03PlaneExtraction>();
        extraction.Configure(objective, plane.transform, 32f);
        EditorUtility.SetDirty(extraction);
    }

    private static GameObject FindPlacedPlane(Scene scene)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .Where(candidate =>
                PrefabUtility.GetNearestPrefabInstanceRoot(candidate) == candidate)
            .FirstOrDefault(candidate => string.Equals(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate),
                PlaneAssetPath,
                StringComparison.OrdinalIgnoreCase));
    }

    private static GameObject BuildChestPrefab()
    {
        Material wood = GetOrCreateMaterial(
            WoodMaterialPath,
            new Color(0.28f, 0.105f, 0.035f),
            0f,
            0.22f);
        Material metal = GetOrCreateMaterial(
            MetalMaterialPath,
            new Color(0.42f, 0.29f, 0.08f),
            0.72f,
            0.48f);
        Mesh beamMesh = GetOrCreateBeamMesh();
        Material beamCore = GetOrCreateBeamMaterial(
            BeamCoreMaterialPath,
            new Color(1.35f, 0.82f, 0.2f, 0.72f));
        Material beamGlow = GetOrCreateBeamMaterial(
            BeamGlowMaterialPath,
            new Color(1f, 0.55f, 0.08f, 0.24f));

        GameObject root = new GameObject("PF_Level03_TreasureChest");
        SphereCollider trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.45f, 0f);
        trigger.radius = 2.7f;
        Level03TreasureChest chest = root.AddComponent<Level03TreasureChest>();
        Level03TreasureBeacon beacon = root.AddComponent<Level03TreasureBeacon>();

        CreateBox(root.transform, "WoodenBase", new Vector3(0f, 0.36f, 0f),
            new Vector3(1.5f, 0.72f, 0.95f), wood);
        CreateBox(root.transform, "LowerBand", new Vector3(0f, 0.12f, 0f),
            new Vector3(1.56f, 0.12f, 1.01f), metal);
        CreateBox(root.transform, "UpperBand", new Vector3(0f, 0.67f, 0f),
            new Vector3(1.56f, 0.12f, 1.01f), metal);

        Transform lidPivot = new GameObject("LidPivot").transform;
        lidPivot.SetParent(root.transform, false);
        lidPivot.localPosition = new Vector3(0f, 0.72f, -0.475f);
        CreateBox(lidPivot, "WoodenLid", new Vector3(0f, 0.18f, 0.475f),
            new Vector3(1.5f, 0.36f, 0.95f), wood);
        CreateBox(lidPivot, "LidBand", new Vector3(0f, 0.2f, 0.475f),
            new Vector3(0.18f, 0.42f, 1.01f), metal);
        CreateBox(root.transform, "Lock", new Vector3(0f, 0.61f, 0.51f),
            new Vector3(0.32f, 0.44f, 0.12f), metal);
        MeshRenderer glowRenderer = CreateBeamSegment(
            root.transform,
            "VFX_TreasureBeam_Glow",
            beamMesh,
            beamGlow,
            new Vector3(2.2f, 25f, 2.2f),
            1);
        MeshRenderer coreRenderer = CreateBeamSegment(
            root.transform,
            "VFX_TreasureBeam_Core",
            beamMesh,
            beamCore,
            new Vector3(0.72f, 29f, 0.72f),
            2);
        chest.Configure(string.Empty, null, lidPivot);
        beacon.Configure(
            coreRenderer.transform,
            coreRenderer,
            glowRenderer.transform,
            glowRenderer);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static MeshRenderer CreateBeamSegment(
        Transform parent,
        string name,
        Mesh mesh,
        Material material,
        Vector3 localScale,
        int sortingOrder)
    {
        GameObject beam = new GameObject(name);
        beam.transform.SetParent(parent, false);
        beam.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        beam.transform.localScale = localScale;
        beam.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = beam.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private static Mesh GetOrCreateBeamMesh()
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(BeamMeshPath);
        const int segments = 24;
        float[] ringHeights = { 0f, 0.045f, 0.14f, 0.62f, 0.88f, 1f };
        float[] ringAlpha = { 0f, 0.42f, 1f, 0.82f, 0.3f, 0f };
        int ringVertexCount = segments + 1;
        Vector3[] vertices = new Vector3[ringHeights.Length * ringVertexCount];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[(ringHeights.Length - 1) * segments * 6];

        for (int ring = 0; ring < ringHeights.Length; ring++)
        {
            for (int segment = 0; segment <= segments; segment++)
            {
                float fraction = segment / (float)segments;
                float angle = fraction * Mathf.PI * 2f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                int vertexIndex = ring * ringVertexCount + segment;
                vertices[vertexIndex] = radial * 0.5f + Vector3.up * ringHeights[ring];
                normals[vertexIndex] = radial;
                uvs[vertexIndex] = new Vector2(fraction, ringHeights[ring]);
                colors[vertexIndex] = new Color(1f, 1f, 1f, ringAlpha[ring]);
            }
        }

        int triangleIndex = 0;
        for (int ring = 0; ring < ringHeights.Length - 1; ring++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int lower = ring * ringVertexCount + segment;
                int upper = lower + ringVertexCount;
                triangles[triangleIndex++] = lower;
                triangles[triangleIndex++] = upper;
                triangles[triangleIndex++] = lower + 1;
                triangles[triangleIndex++] = lower + 1;
                triangles[triangleIndex++] = upper;
                triangles[triangleIndex++] = upper + 1;
            }
        }

        Mesh mesh = existing != null ? existing : new Mesh();
        mesh.name = "MESH_TreasureBeacon";
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, BeamMeshPath);
        }
        else
        {
            EditorUtility.SetDirty(mesh);
        }
        return mesh;
    }

    private static Material GetOrCreateBeamMaterial(string path, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Particles/Additive") ?? Shader.Find("Unlit/Transparent");
        if (shader == null)
        {
            throw new MissingReferenceException(
                "No transparent shader is available for the treasure beacon.");
        }

        if (material == null)
        {
            material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(path)
            };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        if (material.HasProperty("_TintColor"))
        {
            material.SetColor("_TintColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
        material.renderQueue = 3000;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateBox(
        Transform parent,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = localScale;
        box.GetComponent<MeshRenderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(box.GetComponent<BoxCollider>());
    }

    private static Material GetOrCreateMaterial(
        string path,
        Color color,
        float metallic,
        float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Standard");
            material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(path)
            };
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Vector2 ClosestPoint(Bounds bounds, Vector2 point)
    {
        return new Vector2(
            Mathf.Clamp(point.x, bounds.min.x, bounds.max.x),
            Mathf.Clamp(point.y, bounds.min.z, bounds.max.z));
    }

    private static bool IsClearOfBuildings(
        Vector2 point,
        IEnumerable<BuildingInfo> buildings,
        float clearance)
    {
        return buildings.All(building =>
            HorizontalDistance(point, building.bounds) >= clearance);
    }

    private static float HorizontalDistance(Vector2 point, Bounds bounds)
    {
        return Vector2.Distance(point, ClosestPoint(bounds, point));
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        return Vector2.Distance(
            new Vector2(first.x, first.z),
            new Vector2(second.x, second.z));
    }

    private static bool TrySampleTerrain(
        IEnumerable<Terrain> terrains,
        Vector2 point,
        out float height,
        out float slope)
    {
        foreach (Terrain terrain in terrains)
        {
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            float normalizedX = (point.x - origin.x) / size.x;
            float normalizedZ = (point.y - origin.z) / size.z;
            if (normalizedX < 0f || normalizedX > 1f ||
                normalizedZ < 0f || normalizedZ > 1f)
            {
                continue;
            }

            int holeX = Mathf.Clamp(
                Mathf.FloorToInt(normalizedX * data.holesResolution),
                0,
                data.holesResolution - 1);
            int holeZ = Mathf.Clamp(
                Mathf.FloorToInt(normalizedZ * data.holesResolution),
                0,
                data.holesResolution - 1);
            if (data.IsHole(holeX, holeZ))
            {
                continue;
            }

            height = origin.y + terrain.SampleHeight(
                new Vector3(point.x, origin.y, point.y));
            slope = Vector3.Angle(
                data.GetInterpolatedNormal(normalizedX, normalizedZ),
                Vector3.up);
            return true;
        }

        height = 0f;
        slope = 90f;
        return false;
    }

    private static bool TryCalculateBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
            .ToArray();
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

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .FirstOrDefault(candidate => candidate.name == name);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string name = Path.GetFileName(folder);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
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
