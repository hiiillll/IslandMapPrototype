using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RoadMarkingPrefabGenerator
{
    private const string PrefabDirectory = "Assets/Art/Prefabs/RoadMarkings";
    private const string MaterialDirectory = "Assets/Art/Materials/RoadMarkings";
    private const string PrefabPath = PrefabDirectory + "/PF_RoadMarking_WhiteDash.prefab";
    private const string MaterialPath = MaterialDirectory + "/MAT_RoadMarking_White.mat";
    private const string LevelOneScenePath = "Assets/Scenes/IslandMap.unity";
    private const string MarkingRootName = "ENV_RoadMarkings_WhiteDashes";
    private const float DashStep = 4.5f;
    private const float EndpointMargin = 2.5f;

    static RoadMarkingPrefabGenerator()
    {
        EditorApplication.delayCall += EnsureWhiteDashExists;
    }

    [MenuItem("Tools/Island Map/Road Markings/Create White Dash Prefab")]
    public static void CreateWhiteDashPrefab()
    {
        EnsureDirectory(PrefabDirectory);
        EnsureDirectory(MaterialDirectory);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"))
            {
                name = "MAT_RoadMarking_White",
                color = new Color(0.82f, 0.84f, 0.82f, 1f),
                enableInstancing = true
            };
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", 0.08f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.DisableKeyword("_SPECULARHIGHLIGHTS_ON");
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dash.name = "PF_RoadMarking_WhiteDash";
        dash.transform.localPosition = new Vector3(0f, 0.0075f, 0f);
        dash.transform.localRotation = Quaternion.identity;
        dash.transform.localScale = new Vector3(0.15f, 0.015f, 2.5f);

        Object.DestroyImmediate(dash.GetComponent<Collider>());
        MeshRenderer renderer = dash.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        PrefabUtility.SaveAsPrefabAsset(dash, PrefabPath);
        Object.DestroyImmediate(dash);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Debug.Log($"White road dash prefab created: {PrefabPath}");
    }

    [MenuItem("Tools/Island Map/Road Markings/Place White Dashes In Level One")]
    public static void PlaceWhiteDashesInLevelOne()
    {
        if (CompleteRoadMarkingGenerator.IsLayoutLocked())
        {
            Debug.LogWarning("Level 01 road layout is locked. Legacy dash placement was cancelled to preserve manual edits.");
            return;
        }
        EnsureWhiteDashExists();
        GameObject dashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (dashPrefab == null)
        {
            throw new FileNotFoundException("White road dash prefab was not found.", PrefabPath);
        }

        Scene scene = EditorSceneManager.OpenScene(LevelOneScenePath, OpenSceneMode.Single);
        if (GameObject.Find("ENV_RoadMarkings_Complete") != null)
        {
            Debug.LogWarning("The saved complete road-marking layout already exists. Legacy dash placement was cancelled to preserve manual edits.");
            return;
        }
        GameObject existingRoot = GameObject.Find(MarkingRootName);
        if (existingRoot != null)
        {
            Object.DestroyImmediate(existingRoot);
        }

        GameObject colliderRoot = GameObject.Find("COL_Roads");
        if (colliderRoot == null)
        {
            throw new MissingReferenceException("COL_Roads was not found in the level-one scene.");
        }

        GameObject markingRoot = new GameObject(MarkingRootName);
        int dashCount = 0;
        foreach (Transform roadTransform in colliderRoot.transform)
        {
            if (!roadTransform.name.StartsWith("COL_Road_")
                || roadTransform.name.Contains("Intersection"))
            {
                continue;
            }

            BoxCollider roadCollider = roadTransform.GetComponent<BoxCollider>();
            if (roadCollider == null || !roadCollider.enabled)
            {
                continue;
            }

            Bounds roadBounds = roadCollider.bounds;
            bool runsAlongX = roadBounds.size.x >= roadBounds.size.z;
            float length = runsAlongX ? roadBounds.size.x : roadBounds.size.z;
            float usableLength = Mathf.Max(0f, length - EndpointMargin * 2f);
            int segmentCount = Mathf.Max(1, Mathf.FloorToInt(usableLength / DashStep) + 1);
            float firstOffset = -0.5f * (segmentCount - 1) * DashStep;
            float roadSurfaceY = roadBounds.max.y + 0.002f;

            GameObject segmentRoot = new GameObject(roadTransform.name.Replace("COL_", "MARK_"));
            segmentRoot.transform.SetParent(markingRoot.transform, false);
            for (int index = 0; index < segmentCount; index++)
            {
                float offset = firstOffset + index * DashStep;
                GameObject dash = (GameObject)PrefabUtility.InstantiatePrefab(dashPrefab, scene);
                dash.name = $"WhiteDash_{index + 1:000}";
                dash.transform.SetParent(segmentRoot.transform, true);
                dash.transform.position = roadBounds.center
                    + (runsAlongX ? Vector3.right : Vector3.forward) * offset
                    + Vector3.up * (roadSurfaceY - roadBounds.center.y);
                dash.transform.rotation = runsAlongX
                    ? Quaternion.Euler(0f, 90f, 0f)
                    : Quaternion.identity;
                dashCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = markingRoot;
        Debug.Log($"Placed {dashCount} white road dashes in {LevelOneScenePath}.");
    }

    private static void EnsureWhiteDashExists()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            CreateWhiteDashPrefab();
        }
    }

    private static void EnsureDirectory(string path)
    {
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(path) && !string.IsNullOrEmpty(parent))
        {
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
