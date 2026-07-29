using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Level03StrictScale40
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string TerrainFolder = "Assets/Level03/ScaledTerrain40_Strict";
    private const string EnvironmentName = "ENVIRONMENT_Level03";
    private const string TerrainRootName = "ENV_Level03_ConvertedTerrain";
    private const string RoadName = "ENV_Level03_RoadNetwork_FromReference";
    private const string PlayerName = "PLAYER_Car";
    private const string MainCameraName = "SYS_MainCamera";
    private const string OverviewCameraName = "SYS_Level03_OverviewCamera";
    private const string MarkerName = "SYS_Level03_StrictScale40_Applied";
    private const string ReportPath = "Library/CodexLevel03StrictScale40Report.json";
    private const string CarPreviewPath = "Library/CodexLevel03Scale40CarPreview.png";
    private const float Factor = 0.4f;

    [Serializable]
    private sealed class Report
    {
        public bool success;
        public float scaleFactor;
        public int terrainCount;
        public int scaledEnvironmentChildren;
        public int scaledExternalModelRoots;
        public int terrainTreeCount;
        public Vector3 terrainTileSize;
        public Vector3 roadWorldScale;
        public Vector3 carScaleBefore;
        public Vector3 carScaleAfter;
        public Vector3 carColliderSizeBefore;
        public Vector3 carColliderSizeAfter;
        public Vector3 carPositionBefore;
        public Vector3 carPositionAfter;
        public Vector3 cameraOffsetBefore;
        public Vector3 cameraOffsetAfter;
        public float surfaceHeightBefore;
        public float surfaceHeightAfter;
        public string completedAt;
    }

    public static void ApplyFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Apply();
        Level03ActivePlanSplineRoadRebuilder.RenderVerificationPreview();
        RenderCarPreview();
    }

    [MenuItem("Tools/Island Map/Level03/Strictly Scale Map To 40 Percent")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException("Level03 must be the active scene.");
        }

        if (Find(scene, MarkerName) != null)
        {
            Debug.Log("[Level03 Strict Scale 40] Already applied; no changes made.");
            return;
        }

        GameObject environment = Require(scene, EnvironmentName);
        GameObject terrainRoot = Require(scene, TerrainRootName);
        GameObject road = Require(scene, RoadName);
        GameObject player = Require(scene, PlayerName);
        GameObject mainCamera = Require(scene, MainCameraName);

        Vector3 carScaleBefore = player.transform.localScale;
        Vector3 carPositionBefore = player.transform.position;
        Vector3 cameraOffsetBefore = mainCamera.transform.position - player.transform.position;
        Vector3 colliderSizeBefore = GetColliderBounds(player).size;
        float surfaceBefore = SampleDrivingSurface(carPositionBefore);

        int environmentChildren = ScaleEnvironment(environment.transform, terrainRoot.transform);
        TerrainResult terrainResult = ScaleTerrains(terrainRoot);
        int externalRoots = ScaleExternalRoots(scene, environment, player, mainCamera);
        Physics.SyncTransforms();

        Vector3 newSpawn = new Vector3(
            carPositionBefore.x * Factor,
            carPositionBefore.y,
            carPositionBefore.z * Factor);
        float surfaceAfter = SampleDrivingSurface(newSpawn);
        newSpawn.y = IsFinite(surfaceBefore) && IsFinite(surfaceAfter)
            ? carPositionBefore.y + surfaceAfter - surfaceBefore
            : carPositionBefore.y * Factor;

        player.transform.position = newSpawn;
        player.transform.localScale = carScaleBefore;
        mainCamera.transform.position = player.transform.position + cameraOffsetBefore;

        GameObject marker = new GameObject(MarkerName);
        SceneManager.MoveGameObjectToScene(marker, scene);
        Physics.SyncTransforms();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Terrain[] terrains = terrainRoot.GetComponentsInChildren<Terrain>(true);
        Vector3 colliderSizeAfter = GetColliderBounds(player).size;
        bool terrainCorrect = terrains.Length == 16 && terrains.All(terrain =>
            Approximately(terrain.terrainData.size, new Vector3(400f, 160f, 400f)));
        bool carCorrect = Approximately(player.transform.localScale, carScaleBefore) &&
                          Approximately(colliderSizeAfter, colliderSizeBefore);
        bool cameraCorrect = Approximately(
            mainCamera.transform.position - player.transform.position,
            cameraOffsetBefore);
        bool roadCorrect = Approximately(road.transform.lossyScale, Vector3.one * Factor);

        Report report = new Report
        {
            success = terrainCorrect && carCorrect && cameraCorrect && roadCorrect,
            scaleFactor = Factor,
            terrainCount = terrains.Length,
            scaledEnvironmentChildren = environmentChildren,
            scaledExternalModelRoots = externalRoots,
            terrainTreeCount = terrainResult.treeCount,
            terrainTileSize = terrains[0].terrainData.size,
            roadWorldScale = road.transform.lossyScale,
            carScaleBefore = carScaleBefore,
            carScaleAfter = player.transform.localScale,
            carColliderSizeBefore = colliderSizeBefore,
            carColliderSizeAfter = colliderSizeAfter,
            carPositionBefore = carPositionBefore,
            carPositionAfter = player.transform.position,
            cameraOffsetBefore = cameraOffsetBefore,
            cameraOffsetAfter = mainCamera.transform.position - player.transform.position,
            surfaceHeightBefore = surfaceBefore,
            surfaceHeightAfter = surfaceAfter,
            completedAt = DateTime.Now.ToString("O")
        };
        WriteReport(report);
        Debug.Log(
            $"[Level03 Strict Scale 40] success={report.success}; Terrain={terrains.Length}; " +
            $"environment children={environmentChildren}; external roots={externalRoots}; " +
            $"car {carPositionBefore} -> {player.transform.position}; scale=" +
            $"{player.transform.localScale}.");
    }

    private static int ScaleEnvironment(Transform environment, Transform terrainRoot)
    {
        int count = 0;
        foreach (Transform child in environment)
        {
            if (child == terrainRoot)
            {
                child.localPosition *= Factor;
                foreach (Transform nested in child)
                {
                    if (nested.GetComponent<Terrain>() == null)
                    {
                        ScaleTransform(nested);
                        count++;
                    }
                }
                continue;
            }

            ScaleTransform(child);
            count++;
        }
        return count;
    }

    private static TerrainResult ScaleTerrains(GameObject terrainRoot)
    {
        Terrain[] terrains = terrainRoot.GetComponentsInChildren<Terrain>(true);
        if (terrains.Length != 16)
        {
            throw new InvalidOperationException($"Expected 16 Terrain tiles, found {terrains.Length}.");
        }

        EnsureFolder(TerrainFolder);
        int treeCount = 0;
        foreach (Terrain terrain in terrains)
        {
            TerrainData source = terrain.terrainData;
            string path = $"{TerrainFolder}/TD_{terrain.gameObject.name}_Scale40.asset";
            TerrainData scaled = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (scaled == null)
            {
                scaled = UnityEngine.Object.Instantiate(source);
                scaled.name = $"TD_{terrain.gameObject.name}_Scale40";
                scaled.size = source.size * Factor;

                TreeInstance[] trees = scaled.treeInstances;
                for (int index = 0; index < trees.Length; index++)
                {
                    TreeInstance tree = trees[index];
                    tree.widthScale *= Factor;
                    tree.heightScale *= Factor;
                    trees[index] = tree;
                }
                scaled.treeInstances = trees;

                DetailPrototype[] details = scaled.detailPrototypes;
                foreach (DetailPrototype detail in details)
                {
                    detail.minWidth *= Factor;
                    detail.maxWidth *= Factor;
                    detail.minHeight *= Factor;
                    detail.maxHeight *= Factor;
                }
                scaled.detailPrototypes = details;
                AssetDatabase.CreateAsset(scaled, path);
            }

            treeCount += scaled.treeInstanceCount;
            terrain.transform.localPosition *= Factor;
            terrain.terrainData = scaled;
            TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null)
            {
                collider.terrainData = scaled;
                EditorUtility.SetDirty(collider);
            }
            terrain.Flush();
            EditorUtility.SetDirty(terrain);
        }

        ReconnectTerrains(terrains);
        return new TerrainResult(treeCount);
    }

    private static int ScaleExternalRoots(
        Scene scene,
        GameObject environment,
        GameObject player,
        GameObject mainCamera)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == environment || root == player || root == mainCamera || IsSystem(root))
            {
                continue;
            }

            bool visual = root.GetComponentsInChildren<Renderer>(true).Length > 0;
            bool marker = root.name.StartsWith("SPAWN", StringComparison.OrdinalIgnoreCase) ||
                          root.name.StartsWith("CHECKPOINT", StringComparison.OrdinalIgnoreCase) ||
                          root.name.StartsWith("OBJECTIVE", StringComparison.OrdinalIgnoreCase) ||
                          root.name.StartsWith("DECOR", StringComparison.OrdinalIgnoreCase);
            if (!visual && !marker)
            {
                continue;
            }

            ScaleTransform(root.transform);
            count++;
        }
        return count;
    }

    private static bool IsSystem(GameObject root)
    {
        return root.name.StartsWith("SYS_", StringComparison.OrdinalIgnoreCase) ||
               root.name.StartsWith("SYSTEM", StringComparison.OrdinalIgnoreCase) ||
               root.name.StartsWith("UI_", StringComparison.OrdinalIgnoreCase) ||
               root.name.IndexOf("EventSystem", StringComparison.OrdinalIgnoreCase) >= 0 ||
               root.GetComponentInChildren<Camera>(true) != null ||
               root.GetComponentInChildren<Light>(true) != null;
    }

    private static void ScaleTransform(Transform transform)
    {
        transform.position *= Factor;
        transform.localScale *= Factor;
        EditorUtility.SetDirty(transform);
    }

    private static float SampleDrivingSurface(Vector3 position)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            new Vector3(position.x, 2000f, position.z),
            Vector3.down,
            4000f,
            ~0,
            QueryTriggerInteraction.Ignore);
        float highest = float.NegativeInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider is TerrainCollider || hit.collider.gameObject.name == RoadName)
            {
                highest = Mathf.Max(highest, hit.point.y);
            }
        }
        return highest;
    }

    private static void ReconnectTerrains(Terrain[] terrains)
    {
        foreach (Terrain terrain in terrains)
        {
            Vector3 p = terrain.transform.position;
            terrain.SetNeighbors(
                FindTerrain(terrains, p + Vector3.left * 400f),
                FindTerrain(terrains, p + Vector3.forward * 400f),
                FindTerrain(terrains, p + Vector3.right * 400f),
                FindTerrain(terrains, p + Vector3.back * 400f));
        }
    }

    private static Terrain FindTerrain(Terrain[] terrains, Vector3 position)
    {
        return terrains.FirstOrDefault(candidate =>
            Mathf.Abs(candidate.transform.position.x - position.x) < 0.1f &&
            Mathf.Abs(candidate.transform.position.z - position.z) < 0.1f);
    }

    private static Bounds GetColliderBounds(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        Bounds bounds = colliders.Length > 0
            ? colliders[0].bounds
            : new Bounds(root.transform.position, Vector3.zero);
        for (int index = 1; index < colliders.Length; index++)
        {
            bounds.Encapsulate(colliders[index].bounds);
        }
        return bounds;
    }

    private static void RenderCarPreview()
    {
        Scene scene = SceneManager.GetActiveScene();
        Camera camera = Resources.FindObjectsOfTypeAll<Camera>()
            .FirstOrDefault(item => item.gameObject.scene == scene &&
                                    item.gameObject.name == OverviewCameraName);
        GameObject player = Find(scene, PlayerName);
        if (camera == null || player == null)
        {
            return;
        }

        Vector3 oldPosition = camera.transform.position;
        Quaternion oldRotation = camera.transform.rotation;
        bool oldOrthographic = camera.orthographic;
        float oldSize = camera.orthographicSize;
        RenderTexture oldTarget = camera.targetTexture;
        RenderTexture oldActive = RenderTexture.active;
        RenderTexture target = new RenderTexture(800, 800, 24, RenderTextureFormat.ARGB32);
        Texture2D image = new Texture2D(800, 800, TextureFormat.RGB24, false);
        try
        {
            camera.orthographic = true;
            camera.orthographicSize = 45f;
            camera.transform.position = new Vector3(
                player.transform.position.x,
                500f,
                player.transform.position.z);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0f, 0f, 800f, 800f), 0, 0);
            image.Apply();
            string root = Directory.GetParent(Application.dataPath).FullName;
            File.WriteAllBytes(Path.Combine(root, CarPreviewPath), image.EncodeToPNG());
        }
        finally
        {
            camera.transform.position = oldPosition;
            camera.transform.rotation = oldRotation;
            camera.orthographic = oldOrthographic;
            camera.orthographicSize = oldSize;
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static GameObject Require(Scene scene, string name)
    {
        return Find(scene, name) ?? throw new InvalidOperationException($"Missing '{name}'.");
    }

    private static GameObject Find(Scene scene, string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(item => item.scene == scene && item.name == name);
    }

    private static bool Approximately(Vector3 a, Vector3 b)
    {
        return Vector3.SqrMagnitude(a - b) < 0.0001f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static void WriteReport(Report report)
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        File.WriteAllText(Path.Combine(root, ReportPath), JsonUtility.ToJson(report, true));
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }

    private readonly struct TerrainResult
    {
        public TerrainResult(int treeCount)
        {
            this.treeCount = treeCount;
        }

        public readonly int treeCount;
    }
}
