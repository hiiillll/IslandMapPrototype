using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CompleteRoadMarkingGenerator
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string PrefabDirectory = "Assets/Art/Prefabs/RoadMarkings";
    private const string MaterialPath = "Assets/Art/Materials/RoadMarkings/MAT_RoadMarking_White.mat";
    private const string DashPath = PrefabDirectory + "/PF_RoadMarking_WhiteDash.prefab";
    private const string SolidPath = PrefabDirectory + "/PF_RoadMarking_WhiteSolid.prefab";
    private const string CurvePath = PrefabDirectory + "/PF_RoadMarking_WhiteCurve.prefab";
    private const string StopPath = PrefabDirectory + "/PF_RoadMarking_StopLine.prefab";
    private const string RootName = "ENV_RoadMarkings_Complete";
    private const string LockedLayoutPath = PrefabDirectory + "/PF_RoadMarkings_Level01_LockedLayout.prefab";
    private const string BackupScenePath = "Assets/Scenes/Backups/IslandMap_RoadMarkingsLocked.unity";
    internal const string LayoutLockPath = "Assets/Scenes/IslandMap.layout.lock";
    private const float SurfaceOffset = 0.004f;

    private static readonly string[] RoadNames =
    {
        "COL_Road_North", "COL_Road_South", "COL_Road_East", "COL_Road_West",
        "COL_Road_CenterNorth", "COL_Road_CenterSouth",
        "COL_Road_CenterEast", "COL_Road_CenterWest"
    };

    [MenuItem("Tools/Island Map/Road Markings/Create Missing Set (Preserve Existing Layout)")]
    public static void CreateAndPlaceCompleteSet()
    {
        if (IsLayoutLocked())
        {
            Debug.LogWarning("Level 01 road layout is locked. Road-marking generation was cancelled to preserve manual edits.");
            return;
        }
        CreatePrefabs();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (GameObject.Find(RootName) != null)
        {
            Debug.Log("Existing road-marking layout preserved. Use Force Rebuild only if the saved manual layout should be discarded.");
            return;
        }
        PlaceInLevelOne();
    }

    [MenuItem("Tools/Island Map/Road Markings/Force Rebuild Complete Set")]
    private static void ForceRebuildCompleteSet()
    {
        if (IsLayoutLocked())
        {
            EditorUtility.DisplayDialog(
                "Road layout is locked",
                "The manually edited Level 01 road and marking layout is protected. Remove the layout lock file manually before rebuilding.",
                "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog(
                "Overwrite road-marking layout?",
                "This will delete the manually adjusted Level 01 road markings and rebuild them from defaults.",
                "Force Rebuild",
                "Cancel"))
        {
            return;
        }

        CreatePrefabs();
        PlaceInLevelOne();
    }

    [MenuItem("Tools/Island Map/Road Markings/Save Current Layout As Locked Backup")]
    public static void SaveCurrentLayoutAsLockedBackup()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            throw new MissingReferenceException($"{RootName} was not found in Level 01.");
        }

        EnsureDirectory(Path.GetDirectoryName(BackupScenePath)?.Replace('\\', '/'));
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, LockedLayoutPath, InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupScenePath) != null)
        {
            AssetDatabase.DeleteAsset(BackupScenePath);
        }
        if (!AssetDatabase.CopyAsset(ScenePath, BackupScenePath))
        {
            throw new IOException("Could not create the locked road-marking scene backup.");
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Manual road-marking layout locked: {LockedLayoutPath}; scene backup: {BackupScenePath}");
    }

    internal static bool IsLayoutLocked()
    {
        return File.Exists(Path.GetFullPath(LayoutLockPath));
    }

    public static void CreatePrefabs()
    {
        EnsureDirectory(PrefabDirectory);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            throw new FileNotFoundException("Road marking material was not found.", MaterialPath);
        }

        material.color = new Color(0.72f, 0.73f, 0.69f, 1f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Glossiness", 0.015f);
        material.SetFloat("_SpecularHighlights", 0f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);

        SaveCubePrefab(DashPath, "PF_RoadMarking_WhiteDash", new Vector3(0.28f, 0.006f, 3.2f), material);
        SaveCubePrefab(SolidPath, "PF_RoadMarking_WhiteSolid", new Vector3(0.24f, 0.006f, 5f), material);
        SaveCubePrefab(StopPath, "PF_RoadMarking_StopLine", new Vector3(8.2f, 0.007f, 0.5f), material);
        SaveCurvePrefab(material);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void PlaceInLevelOne()
    {
        if (IsLayoutLocked())
        {
            Debug.LogWarning("Level 01 road layout is locked. Scene placement was cancelled.");
            return;
        }
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject oldRoot = GameObject.Find("ENV_RoadMarkings_WhiteDashes");
        if (oldRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(oldRoot);
        }
        oldRoot = GameObject.Find(RootName);
        if (oldRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(oldRoot);
        }

        GameObject root = new GameObject(RootName);
        GameObject dashRoot = NewChild(root, "Center_Dashes");
        GameObject solidRoot = NewChild(root, "Edge_SolidLines");
        NewChild(root, "Curve_Prefab_NotPlaced_NoCurvedRoads");
        GameObject stopRoot = NewChild(root, "Intersection_StopLines");

        GameObject dashPrefab = LoadPrefab(DashPath);
        GameObject solidPrefab = LoadPrefab(SolidPath);
        GameObject stopPrefab = LoadPrefab(StopPath);

        int dashCount = 0;
        int solidCount = 0;
        foreach (string roadName in RoadNames)
        {
            GameObject road = GameObject.Find(roadName);
            BoxCollider collider = road != null ? road.GetComponent<BoxCollider>() : null;
            if (collider == null)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            bool alongX = bounds.size.x >= bounds.size.z;
            float length = alongX ? bounds.size.x : bounds.size.z;
            float y = bounds.max.y + SurfaceOffset;

            dashCount += PlaceRepeated(scene, dashPrefab, dashRoot.transform, bounds.center, y,
                alongX, length, 6.2f, 14f, 0f, "Dash");
        }

        solidCount = PlacePerimeterSolidLines(scene, solidPrefab, solidRoot.transform);

        float centerY = GetRoadSurfaceY("COL_Road_CenterIntersection");
        PlaceStop(scene, stopPrefab, stopRoot.transform, new Vector3(0f, centerY, 11.5f), 0f, "Stop_North");
        PlaceStop(scene, stopPrefab, stopRoot.transform, new Vector3(0f, centerY, -11.5f), 0f, "Stop_South");
        PlaceStop(scene, stopPrefab, stopRoot.transform, new Vector3(12.5f, centerY, 0f), 90f, "Stop_East");
        PlaceStop(scene, stopPrefab, stopRoot.transform, new Vector3(-12.5f, centerY, 0f), 90f, "Stop_West");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Debug.Log($"Road markings rebuilt: {dashCount} dashes, {solidCount} topology-aware solid lines, 4 stop lines.");
    }

    private static int PlacePerimeterSolidLines(Scene scene, GameObject prefab, Transform parent)
    {
        Bounds north = GetRoadBounds("COL_Road_North");
        Bounds south = GetRoadBounds("COL_Road_South");
        Bounds east = GetRoadBounds("COL_Road_East");
        Bounds west = GetRoadBounds("COL_Road_West");
        Bounds centerVertical = GetRoadBounds("COL_Road_CenterNorth");
        Bounds centerHorizontal = GetRoadBounds("COL_Road_CenterEast");

        const float edgeInset = 0.9f;
        const float junctionClearance = 0.65f;
        float y = Mathf.Max(north.max.y, east.max.y) + SurfaceOffset;
        float outerLeft = west.min.x + edgeInset;
        float outerRight = east.max.x - edgeInset;
        float outerTop = north.max.z - edgeInset;
        float outerBottom = south.min.z + edgeInset;
        float innerTop = north.min.z + edgeInset;
        float innerBottom = south.max.z - edgeInset;
        float innerRight = east.min.x + edgeInset;
        float innerLeft = west.max.x - edgeInset;
        float verticalOpening = centerVertical.extents.x + junctionClearance;
        float horizontalOpening = centerHorizontal.extents.z + junctionClearance;

        int count = 0;
        count += PlaceSolidRun(scene, prefab, parent, new Vector3((outerLeft + outerRight) * 0.5f, y, outerTop),
            true, outerRight - outerLeft, "Outer_North");
        count += PlaceSolidRun(scene, prefab, parent, new Vector3((outerLeft + outerRight) * 0.5f, y, outerBottom),
            true, outerRight - outerLeft, "Outer_South");
        count += PlaceSolidRun(scene, prefab, parent, new Vector3(outerRight, y, (outerBottom + outerTop) * 0.5f),
            false, outerTop - outerBottom, "Outer_East");
        count += PlaceSolidRun(scene, prefab, parent, new Vector3(outerLeft, y, (outerBottom + outerTop) * 0.5f),
            false, outerTop - outerBottom, "Outer_West");

        count += PlaceSplitHorizontal(scene, prefab, parent, outerLeft, outerRight, innerTop, y,
            verticalOpening, "Inner_North");
        count += PlaceSplitHorizontal(scene, prefab, parent, outerLeft, outerRight, innerBottom, y,
            verticalOpening, "Inner_South");
        count += PlaceSplitVertical(scene, prefab, parent, outerBottom, outerTop, innerRight, y,
            horizontalOpening, "Inner_East");
        count += PlaceSplitVertical(scene, prefab, parent, outerBottom, outerTop, innerLeft, y,
            horizontalOpening, "Inner_West");
        return count;
    }

    private static int PlaceSplitHorizontal(Scene scene, GameObject prefab, Transform parent,
        float min, float max, float z, float y, float halfOpening, string name)
    {
        PlaceSolidRun(scene, prefab, parent, new Vector3((min - halfOpening) * 0.5f, y, z),
            true, halfOpening - min, name + "_Left");
        PlaceSolidRun(scene, prefab, parent, new Vector3((halfOpening + max) * 0.5f, y, z),
            true, max - halfOpening, name + "_Right");
        return 2;
    }

    private static int PlaceSplitVertical(Scene scene, GameObject prefab, Transform parent,
        float min, float max, float x, float y, float halfOpening, string name)
    {
        PlaceSolidRun(scene, prefab, parent, new Vector3(x, y, (min - halfOpening) * 0.5f),
            false, halfOpening - min, name + "_Bottom");
        PlaceSolidRun(scene, prefab, parent, new Vector3(x, y, (halfOpening + max) * 0.5f),
            false, max - halfOpening, name + "_Top");
        return 2;
    }

    private static int PlaceSolidRun(Scene scene, GameObject prefab, Transform parent,
        Vector3 position, bool alongX, float length, string name)
    {
        GameObject instance = Instantiate(scene, prefab, parent);
        instance.name = name;
        instance.transform.position = position;
        instance.transform.rotation = alongX ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
        Vector3 scale = instance.transform.localScale;
        scale.z *= length / 5f;
        instance.transform.localScale = scale;
        return 1;
    }

    private static Bounds GetRoadBounds(string name)
    {
        GameObject road = GameObject.Find(name);
        BoxCollider collider = road != null ? road.GetComponent<BoxCollider>() : null;
        if (collider == null)
        {
            throw new MissingReferenceException($"Road collider {name} was not found.");
        }
        return collider.bounds;
    }

    private static int PlaceRepeated(Scene scene, GameObject prefab, Transform parent, Vector3 center,
        float y, bool alongX, float length, float step, float endMargin, float lateralOffset, string label)
    {
        float usable = Mathf.Max(0f, length - endMargin * 2f);
        int count = Mathf.Max(1, Mathf.FloorToInt(usable / step) + 1);
        float start = -0.5f * (count - 1) * step;
        Vector3 direction = alongX ? Vector3.right : Vector3.forward;
        Vector3 lateral = alongX ? Vector3.forward : Vector3.right;
        Quaternion rotation = alongX ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
        for (int index = 0; index < count; index++)
        {
            GameObject instance = Instantiate(scene, prefab, parent);
            instance.name = $"{label}_{index + 1:000}";
            instance.transform.position = center + direction * (start + index * step) + lateral * lateralOffset;
            instance.transform.position = new Vector3(instance.transform.position.x, y, instance.transform.position.z);
            instance.transform.rotation = rotation;
        }
        return count;
    }

    private static void PlaceCurve(Scene scene, GameObject prefab, Transform parent, Vector3 position, float yaw, string name)
    {
        GameObject instance = Instantiate(scene, prefab, parent);
        instance.name = name;
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private static void PlaceStop(Scene scene, GameObject prefab, Transform parent, Vector3 position, float yaw, string name)
    {
        GameObject instance = Instantiate(scene, prefab, parent);
        instance.name = name;
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private static GameObject Instantiate(Scene scene, GameObject prefab, Transform parent)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.SetParent(parent, true);
        return instance;
    }

    private static void SaveCubePrefab(string path, string name, Vector3 size, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.localPosition = Vector3.up * size.y * 0.5f;
        cube.transform.localScale = size;
        UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
        ConfigureRenderer(cube.GetComponent<MeshRenderer>(), material);
        PrefabUtility.SaveAsPrefabAsset(cube, path);
        UnityEngine.Object.DestroyImmediate(cube);
    }

    private static void SaveCurvePrefab(Material material)
    {
        GameObject root = new GameObject("PF_RoadMarking_WhiteCurve");
        const int pieces = 16;
        const float radius = 6f;
        float arcLength = Mathf.PI * radius * 0.5f / pieces;
        for (int index = 0; index < pieces; index++)
        {
            float angle = (index + 0.5f) * 90f / pieces;
            float radians = angle * Mathf.Deg2Rad;
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = $"CurvePiece_{index + 1:00}";
            piece.transform.SetParent(root.transform, false);
            piece.transform.localPosition = new Vector3(Mathf.Cos(radians) * radius, 0.003f, Mathf.Sin(radians) * radius);
            piece.transform.localRotation = Quaternion.Euler(0f, -angle, 0f);
            piece.transform.localScale = new Vector3(0.24f, 0.006f, arcLength + 0.04f);
            UnityEngine.Object.DestroyImmediate(piece.GetComponent<Collider>());
            ConfigureRenderer(piece.GetComponent<MeshRenderer>(), material);
        }
        PrefabUtility.SaveAsPrefabAsset(root, CurvePath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static void ConfigureRenderer(MeshRenderer renderer, Material material)
    {
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    private static float GetRoadSurfaceY(string name)
    {
        GameObject road = GameObject.Find(name);
        BoxCollider collider = road != null ? road.GetComponent<BoxCollider>() : null;
        return collider != null ? collider.bounds.max.y + SurfaceOffset : SurfaceOffset;
    }

    private static GameObject LoadPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            throw new FileNotFoundException("Road marking prefab was not found.", path);
        }
        return prefab;
    }

    private static GameObject NewChild(GameObject parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static void EnsureDirectory(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
