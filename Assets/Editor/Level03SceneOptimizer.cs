using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class Level03SceneOptimizer
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string BackupFolder = "Assets/Scenes/Backups";
    private const string BackupPath =
        BackupFolder + "/Level03_BeforeFullOptimization.unity";
    private const string HeightReferencePath =
        "Assets/Level03/References/Level03_HeightReference.png";
    private const string RoadPlanPath =
        "Assets/Level03/References/Level03_RoadPlan_Active.png";
    private const string PalmMeshPath =
        "Assets/Level03/GeneratedTerrainRoad/MESH_Level03_LowPolyPalm.asset";
    private const string PalmTrunkMaterialPath =
        "Assets/Level03/GeneratedTerrainRoad/MAT_Level03_PalmTrunk.mat";
    private const string PalmLeavesMaterialPath =
        "Assets/Level03/GeneratedTerrainRoad/MAT_Level03_PalmLeaves.mat";
    private const string BeachSourcePath = "Assets/Art/Materials/Beach.mat";
    private const string BeachMaterialPath =
        "Assets/Level03/GeneratedTerrainRoad/MAT_Level03_Beach.mat";
    private const string BeachObjectName = "ENV_Level03_SmoothBeachCoastline";
    private const string VegetationRootName = "DECOR_Level03_Vegetation_Optimized";
    private const string BuildingsRootName = "DECOR_Level03_Buildings_Optimized";
    private const string ReportPath = "Library/CodexLevel03OptimizationReport.json";

    private const float LandWidth = 4000f;
    private const float LandThreshold = 0.075f;
    private const float MountainThreshold = 0.27f;
    private const float FlatHeight = 0.35f;
    private const int FlatPalmCount = 170;
    private const int PalmCount = 220;
    private const int BuildingCount = 36;

    private static readonly string[] BuildingPaths =
    {
        "Assets/Models/Imported/Apartment/f107add5ea68f5a00af639a36564417a.obj",
        "Assets/Models/Imported/Model_01/9498e2f865705b86c752d4d3b8d7e24b.obj",
        "Assets/Models/Imported/Model_03/10b67dffda31e98bdcddadc39e38f395.obj",
        "Assets/Models/Imported/Model_04/6f2ca0eaa721829020e423d06725b5a5.obj",
        "Assets/Models/Imported/Model_05/b2816f4a505ecf996551c44d93e54c96.obj",
        "Assets/Models/Imported/Model_06/dd38c186d37ebf30eb74d0e49a4bb096.obj",
        "Assets/Models/Imported/Model_08/c35fc0dc610025076fe6bc10e2accfb8.obj",
        "Assets/Models/Imported/Model_09/fd2b01498366eab8e32f3aebdfb73b81.obj",
        "Assets/Models/Imported/Model_10/b5c8f55557398b32c28348c199d56f02.obj",
        "Assets/Models/Imported/Model_13/9b561855f56fe0a1be7cd3e3e952e77c.obj",
        "Assets/Models/Imported/Model_14/a5eaf798d26bff6425ebe060b124a710.obj"
    };

    private static readonly PlacementZone[] BuildingZones =
    {
        new PlacementZone(-760f, 620f, 760f, 1510f),
        new PlacementZone(-460f, 980f, -1510f, -820f),
        new PlacementZone(850f, 1450f, -720f, 650f)
    };

    [Serializable]
    private sealed class OptimizationReport
    {
        public string scenePath;
        public string backupPath;
        public int palmCount;
        public int buildingCount;
        public string beachMaterialPath;
        public bool lightingUpdated;
    }

    private readonly struct PlacementZone
    {
        public PlacementZone(float xMin, float xMax, float zMin, float zMax)
        {
            XMin = xMin;
            XMax = xMax;
            ZMin = zMin;
            ZMax = zMax;
        }

        public float XMin { get; }
        public float XMax { get; }
        public float ZMin { get; }
        public float ZMax { get; }
    }

    public static void OptimizeFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Optimize();
        Level03ActivePlanSplineRoadRebuilder.RenderVerificationPreview();
    }

    public static void RemoveAllDecorationFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RemoveAllDecoration();
        Level03ActivePlanSplineRoadRebuilder.RenderVerificationPreview();
    }

    [MenuItem("Tools/Island Map/Level03/Remove All Decoration Models")]
    public static void RemoveAllDecoration()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException("Level03 must be the active scene.");
        }

        HashSet<GameObject> targets = new HashSet<GameObject>();
        foreach (string generatedRoot in new[] { VegetationRootName, BuildingsRootName })
        {
            GameObject root = GameObject.Find(generatedRoot);
            if (root != null && root.scene == scene)
            {
                targets.Add(root);
            }
        }

        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate.scene != scene)
            {
                continue;
            }

            GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(candidate);
            if (instanceRoot == null || instanceRoot.scene != scene)
            {
                continue;
            }

            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate);
            if (assetPath.StartsWith("Assets/Models/Imported/", StringComparison.OrdinalIgnoreCase) ||
                assetPath.StartsWith("Assets/Art/Prefabs/Vegetation/", StringComparison.OrdinalIgnoreCase))
            {
                targets.Add(instanceRoot);
            }
        }

        int removed = targets.Count;
        foreach (GameObject target in targets.OrderByDescending(GetHierarchyDepth))
        {
            if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Level03 Scene Optimizer] Removed {removed} decoration model roots.");
    }

    [MenuItem("Tools/Island Map/Level03/Optimize Full Scene")]
    public static void Optimize()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException("Level03 must be the active scene.");
        }

        EnsureFolder(BackupFolder);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupPath) == null)
        {
            AssetDatabase.CopyAsset(ScenePath, BackupPath);
        }

        Texture2D heightReference = LoadTexture(HeightReferencePath);
        Texture2D roadPlan = LoadTexture(RoadPlanPath);
        if (heightReference == null || roadPlan == null)
        {
            DestroyTexture(heightReference);
            DestroyTexture(roadPlan);
            throw new FileNotFoundException("Level03 reference images are missing.");
        }

        try
        {
            float landDepth = LandWidth * heightReference.height / heightReference.width;
            Material beachMaterial = CreateLevel03BeachMaterial();
            AssignBeachMaterial(beachMaterial);
            int palms = RebuildVegetation(scene, heightReference, roadPlan, landDepth);
            int buildings = RebuildBuildings(scene, heightReference, roadPlan, landDepth);
            ConfigureLighting();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            WriteReport(new OptimizationReport
            {
                scenePath = ScenePath,
                backupPath = BackupPath,
                palmCount = palms,
                buildingCount = buildings,
                beachMaterialPath = BeachMaterialPath,
                lightingUpdated = true
            });
            Debug.Log(
                $"[Level03 Scene Optimizer] Added {palms} palms and {buildings} " +
                "buildings; updated beach and tropical lighting.");
        }
        finally
        {
            DestroyTexture(heightReference);
            DestroyTexture(roadPlan);
        }
    }

    private static Material CreateLevel03BeachMaterial()
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(BeachSourcePath);
        if (source == null)
        {
            throw new FileNotFoundException("Beach material is missing.", BeachSourcePath);
        }

        Material material = new Material(source)
        {
            name = "MAT_Level03_Beach",
            color = new Color(0.88f, 0.79f, 0.59f, 1f)
        };
        material.SetFloat("_Glossiness", 0.06f);
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(BeachMaterialPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(material, BeachMaterialPath);
            return material;
        }

        EditorUtility.CopySerialized(material, existing);
        UnityEngine.Object.DestroyImmediate(material);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static void AssignBeachMaterial(Material material)
    {
        GameObject beach = GameObject.Find(BeachObjectName);
        if (beach == null)
        {
            throw new InvalidOperationException("The Level03 coastline object is missing.");
        }

        MeshRenderer renderer = beach.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
    }

    private static int RebuildVegetation(
        Scene scene,
        Texture2D heightReference,
        Texture2D roadPlan,
        float landDepth)
    {
        Mesh palmMesh = CreateLowPolyPalmMesh();
        Material[] palmMaterials = CreateLowPolyPalmMaterials();

        Transform root = RecreateGeneratedRoot(scene, VegetationRootName);
        System.Random random = new System.Random(34031);
        List<Vector2> accepted = new List<Vector2>(PalmCount);
        int attempts = 0;
        while (accepted.Count < FlatPalmCount && attempts++ < FlatPalmCount * 120)
        {
            Vector2 point = new Vector2(
                NextRange(random, -LandWidth * 0.47f, LandWidth * 0.47f),
                NextRange(random, -landDepth * 0.47f, landDepth * 0.47f));
            if (!IsFlatLandCandidate(
                    point,
                    heightReference,
                    roadPlan,
                    landDepth,
                    4,
                    3) ||
                IsInsideCentralMountain(point) ||
                !HasClearance(point, accepted, 58f))
            {
                continue;
            }

            CreateLowPolyPalm(
                scene,
                root,
                $"Palm_{accepted.Count + 1:000}",
                point,
                NextRange(random, 0f, 360f),
                NextRange(random, 0.82f, 1.18f),
                palmMesh,
                palmMaterials);
            accepted.Add(point);
        }

        // Break up the broad, dark central mass with a restrained band of palms
        // on the lower mountain slopes, matching the tropical reference without
        // covering the readable mountain silhouette.
        attempts = 0;
        while (accepted.Count < PalmCount && attempts++ < PalmCount * 160)
        {
            Vector2 point = new Vector2(
                NextRange(random, -650f, 880f),
                NextRange(random, -880f, 880f));
            if (!IsMountainCandidate(point, heightReference, roadPlan, landDepth) ||
                !HasClearance(point, accepted, 78f))
            {
                continue;
            }

            CreateLowPolyPalm(
                scene,
                root,
                $"MountainPalm_{accepted.Count - FlatPalmCount + 1:000}",
                point,
                NextRange(random, 0f, 360f),
                NextRange(random, 0.72f, 1.02f),
                palmMesh,
                palmMaterials);
            accepted.Add(point);
        }

        return accepted.Count;
    }

    private static Mesh CreateLowPolyPalmMesh()
    {
        const int trunkSides = 7;
        const int trunkSegments = 5;
        const int frondCount = 9;
        const int frondSegments = 3;
        List<Vector3> vertices = new List<Vector3>(256);
        List<Vector2> uvs = new List<Vector2>(256);
        List<int> trunkTriangles = new List<int>(128);
        List<int> leafTriangles = new List<int>(256);

        for (int segment = 0; segment <= trunkSegments; segment++)
        {
            float t = (float)segment / trunkSegments;
            float radius = Mathf.Lerp(1.55f, 0.72f, t);
            Vector3 centre = new Vector3(1.5f * t * t, t * 30f, 0.65f * t);
            for (int side = 0; side < trunkSides; side++)
            {
                float angle = Mathf.PI * 2f * side / trunkSides;
                vertices.Add(centre + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius));
                uvs.Add(new Vector2((float)side / trunkSides, t));
            }
        }

        for (int segment = 0; segment < trunkSegments; segment++)
        {
            for (int side = 0; side < trunkSides; side++)
            {
                int next = (side + 1) % trunkSides;
                int lower = segment * trunkSides + side;
                int lowerNext = segment * trunkSides + next;
                int upper = (segment + 1) * trunkSides + side;
                int upperNext = (segment + 1) * trunkSides + next;
                trunkTriangles.Add(lower);
                trunkTriangles.Add(upper);
                trunkTriangles.Add(lowerNext);
                trunkTriangles.Add(lowerNext);
                trunkTriangles.Add(upper);
                trunkTriangles.Add(upperNext);
            }
        }

        Vector3 crown = new Vector3(1.5f, 30.4f, 0.65f);
        for (int frond = 0; frond < frondCount; frond++)
        {
            float angle = Mathf.PI * 2f * frond / frondCount;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 right = new Vector3(-direction.z, 0f, direction.x);
            for (int segment = 0; segment < frondSegments; segment++)
            {
                float t0 = (float)segment / frondSegments;
                float t1 = (float)(segment + 1) / frondSegments;
                float width0 = Mathf.Lerp(6.2f, 1.4f, t0);
                float width1 = Mathf.Lerp(6.2f, 1.4f, t1);
                Vector3 centre0 = crown + direction * (t0 * 18f) +
                                  Vector3.down * (t0 * t0 * 6.5f);
                Vector3 centre1 = crown + direction * (t1 * 18f) +
                                  Vector3.down * (t1 * t1 * 6.5f);
                int start = vertices.Count;
                vertices.Add(centre0 - right * width0 * 0.5f);
                vertices.Add(centre0 + right * width0 * 0.5f);
                vertices.Add(centre1 - right * width1 * 0.5f);
                vertices.Add(centre1 + right * width1 * 0.5f);
                uvs.Add(new Vector2(0f, t0));
                uvs.Add(new Vector2(1f, t0));
                uvs.Add(new Vector2(0f, t1));
                uvs.Add(new Vector2(1f, t1));

                int[] indices =
                {
                    start, start + 1, start + 2,
                    start + 1, start + 3, start + 2
                };
                leafTriangles.AddRange(indices);
            }
        }

        Mesh generated = new Mesh
        {
            name = "MESH_Level03_LowPolyPalm",
            subMeshCount = 2
        };
        generated.SetVertices(vertices);
        generated.SetUVs(0, uvs);
        generated.SetTriangles(trunkTriangles, 0, false);
        generated.SetTriangles(leafTriangles, 1, true);
        generated.RecalculateNormals();
        generated.RecalculateBounds();

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(PalmMeshPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, PalmMeshPath);
            return generated;
        }

        EditorUtility.CopySerialized(generated, existing);
        UnityEngine.Object.DestroyImmediate(generated);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static Material[] CreateLowPolyPalmMaterials()
    {
        return new[]
        {
            CreateOrUpdateColoredMaterial(
                PalmTrunkMaterialPath,
                "MAT_Level03_PalmTrunk",
                new Color(0.34f, 0.20f, 0.09f, 1f)),
            CreateOrUpdateColoredMaterial(
                PalmLeavesMaterialPath,
                "MAT_Level03_PalmLeaves",
                new Color(0.07f, 0.30f, 0.08f, 1f))
        };
    }

    private static Material CreateOrUpdateColoredMaterial(
        string path,
        string name,
        Color color)
    {
        Material generated = new Material(Shader.Find("Standard"))
        {
            name = name,
            color = color,
            enableInstancing = true
        };
        generated.SetFloat("_Glossiness", 0.12f);
        generated.SetInt("_Cull", 0);
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }

        EditorUtility.CopySerialized(generated, existing);
        UnityEngine.Object.DestroyImmediate(generated);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static void CreateLowPolyPalm(
        Scene scene,
        Transform root,
        string name,
        Vector2 point,
        float yaw,
        float scale,
        Mesh mesh,
        Material[] materials)
    {
        GameObject palm = new GameObject(name);
        SceneManager.MoveGameObjectToScene(palm, scene);
        palm.transform.SetParent(root, true);
        palm.transform.position = new Vector3(
            point.x,
            SampleGroundHeight(point),
            point.y);
        palm.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        palm.transform.localScale = Vector3.one * scale;
        palm.layer = 5;
        MeshFilter filter = palm.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = palm.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        GameObjectUtility.SetStaticEditorFlags(palm, StaticEditorFlags.OccludeeStatic);
    }

    private static int RebuildBuildings(
        Scene scene,
        Texture2D heightReference,
        Texture2D roadPlan,
        float landDepth)
    {
        List<GameObject> assets = new List<GameObject>();
        foreach (string path in BuildingPaths)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset != null)
            {
                assets.Add(asset);
            }
        }

        if (assets.Count == 0)
        {
            throw new FileNotFoundException("No Level03 building models were found.");
        }

        Transform root = RecreateGeneratedRoot(scene, BuildingsRootName);
        System.Random random = new System.Random(34032);
        List<Vector2> accepted = new List<Vector2>(BuildingCount);
        int attempts = 0;
        while (accepted.Count < BuildingCount && attempts++ < BuildingCount * 180)
        {
            PlacementZone zone = BuildingZones[attempts % BuildingZones.Length];
            Vector2 point = new Vector2(
                NextRange(random, zone.XMin, zone.XMax),
                NextRange(random, zone.ZMin, zone.ZMax));
            if (!IsFlatLandCandidate(
                    point,
                    heightReference,
                    roadPlan,
                    landDepth,
                    10,
                    5) ||
                IsInsideCentralMountain(point) ||
                !HasRoadNearby(point, roadPlan, landDepth, 28) ||
                !HasClearance(point, accepted, 72f))
            {
                continue;
            }

            GameObject asset = assets[accepted.Count % assets.Count];
            GameObject building = PrefabUtility.InstantiatePrefab(asset, scene) as GameObject;
            building.name = $"Building_{accepted.Count + 1:000}_{asset.name}";
            building.transform.SetParent(root, true);
            building.transform.position = new Vector3(
                point.x,
                SampleGroundHeight(point),
                point.y);
            building.transform.rotation = Quaternion.Euler(
                0f,
                Mathf.Round(NextRange(random, 0f, 4f)) * 90f,
                0f);
            building.transform.localScale = Vector3.one;
            FitBuildingToFootprint(
                building,
                NextRange(random, 42f, 68f));
            SetLayerRecursively(building, 5);
            SetStaticRecursively(building);
            accepted.Add(point);
        }

        return accepted.Count;
    }

    private static void FitBuildingToFootprint(GameObject building, float targetSize)
    {
        Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
        if (footprint <= 0.001f)
        {
            return;
        }

        building.transform.localScale *= targetSize / footprint;
    }

    private static bool IsFlatLandCandidate(
        Vector2 point,
        Texture2D heightReference,
        Texture2D roadPlan,
        float landDepth,
        int roadClearancePixels,
        int coastClearancePixels)
    {
        float u = point.x / LandWidth + 0.5f;
        float v = point.y / landDepth + 0.5f;
        float height = heightReference.GetPixelBilinear(u, v).grayscale;
        if (height <= LandThreshold || height >= MountainThreshold)
        {
            return false;
        }

        for (int y = -coastClearancePixels; y <= coastClearancePixels; y++)
        {
            for (int x = -coastClearancePixels; x <= coastClearancePixels; x++)
            {
                float sampleU = u + (float)x / heightReference.width;
                float sampleV = v + (float)y / heightReference.height;
                if (sampleU < 0f || sampleU > 1f || sampleV < 0f || sampleV > 1f ||
                    heightReference.GetPixelBilinear(sampleU, sampleV).grayscale <=
                    LandThreshold)
                {
                    return false;
                }
            }
        }

        for (int y = -roadClearancePixels; y <= roadClearancePixels; y++)
        {
            for (int x = -roadClearancePixels; x <= roadClearancePixels; x++)
            {
                float sampleU = u + (float)x / roadPlan.width;
                float sampleV = v + (float)y / roadPlan.height;
                if (roadPlan.GetPixelBilinear(sampleU, sampleV).grayscale > 0.55f)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsInsideCentralMountain(Vector2 point)
    {
        float x = (point.x - 120f) / 900f;
        float z = point.y / 1120f;
        return x * x + z * z < 1f;
    }

    private static bool IsMountainCandidate(
        Vector2 point,
        Texture2D heightReference,
        Texture2D roadPlan,
        float landDepth)
    {
        float u = point.x / LandWidth + 0.5f;
        float v = point.y / landDepth + 0.5f;
        if (u < 0f || u > 1f || v < 0f || v > 1f)
        {
            return false;
        }

        float height = heightReference.GetPixelBilinear(u, v).grayscale;
        if (height < 0.34f || height > 0.84f)
        {
            return false;
        }

        for (int y = -5; y <= 5; y++)
        {
            for (int x = -5; x <= 5; x++)
            {
                float sampleU = u + (float)x / roadPlan.width;
                float sampleV = v + (float)y / roadPlan.height;
                if (roadPlan.GetPixelBilinear(sampleU, sampleV).grayscale > 0.55f)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HasRoadNearby(
        Vector2 point,
        Texture2D roadPlan,
        float landDepth,
        int radiusPixels)
    {
        float u = point.x / LandWidth + 0.5f;
        float v = point.y / landDepth + 0.5f;
        for (int y = -radiusPixels; y <= radiusPixels; y += 2)
        {
            for (int x = -radiusPixels; x <= radiusPixels; x += 2)
            {
                if (x * x + y * y > radiusPixels * radiusPixels)
                {
                    continue;
                }

                float sampleU = u + (float)x / roadPlan.width;
                float sampleV = v + (float)y / roadPlan.height;
                if (sampleU >= 0f && sampleU <= 1f && sampleV >= 0f && sampleV <= 1f &&
                    roadPlan.GetPixelBilinear(sampleU, sampleV).grayscale > 0.55f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static float SampleGroundHeight(Vector2 point)
    {
        Vector3 world = new Vector3(point.x, 0f, point.y);
        foreach (Terrain terrain in UnityEngine.Object.FindObjectsOfType<Terrain>())
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (point.x < origin.x || point.x > origin.x + size.x ||
                point.y < origin.z || point.y > origin.z + size.z)
            {
                continue;
            }

            return terrain.SampleHeight(world) + origin.y;
        }

        return FlatHeight;
    }

    private static Transform RecreateGeneratedRoot(Scene scene, string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }

        GameObject root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        root.layer = 5;
        return root.transform;
    }

    private static void ConfigureLighting()
    {
        Light light = null;
        foreach (Light candidate in UnityEngine.Object.FindObjectsOfType<Light>(true))
        {
            if (candidate.gameObject.name == "SYS_Level03_DirectionalLight")
            {
                light = candidate;
                break;
            }
        }

        if (light != null)
        {
            light.transform.rotation = Quaternion.Euler(66f, -32f, 0f);
            light.color = new Color(1f, 0.94f, 0.82f);
            light.intensity = 1.02f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.58f;
            light.shadowBias = 0.045f;
            light.shadowNormalBias = 0.32f;
            light.shadowAngle = 4f;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.67f, 0.76f);
        RenderSettings.ambientEquatorColor = new Color(0.38f, 0.48f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.18f, 0.22f, 0.20f);
        RenderSettings.ambientIntensity = 0.92f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.55f, 0.70f, 0.74f);
        RenderSettings.fogStartDistance = 2100f;
        RenderSettings.fogEndDistance = 5200f;
    }

    private static bool HasClearance(
        Vector2 point,
        List<Vector2> accepted,
        float minimumDistance)
    {
        float minimumSquared = minimumDistance * minimumDistance;
        foreach (Vector2 existing in accepted)
        {
            if ((existing - point).sqrMagnitude < minimumSquared)
            {
                return false;
            }
        }

        return true;
    }

    private static float NextRange(System.Random random, float minimum, float maximum)
    {
        return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static void SetStaticRecursively(GameObject root)
    {
        GameObjectUtility.SetStaticEditorFlags(
            root,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        foreach (Transform child in root.transform)
        {
            SetStaticRecursively(child.gameObject);
        }
    }

    private static int GetHierarchyDepth(GameObject gameObject)
    {
        int depth = 0;
        Transform current = gameObject.transform;
        while (current.parent != null)
        {
            depth++;
            current = current.parent;
        }

        return depth;
    }

    private static Texture2D LoadTexture(string assetPath)
    {
        if (!File.Exists(assetPath))
        {
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false, true)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        return texture.LoadImage(File.ReadAllBytes(assetPath), false) ? texture : null;
    }

    private static void DestroyTexture(Texture2D texture)
    {
        if (texture != null)
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        string name = Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void WriteReport(OptimizationReport report)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        File.WriteAllText(
            Path.Combine(projectRoot, ReportPath),
            JsonUtility.ToJson(report, true));
    }
}
