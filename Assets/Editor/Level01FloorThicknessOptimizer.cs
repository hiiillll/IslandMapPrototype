using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Level01FloorThicknessOptimizer
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const float RoadSurfaceTopY = 0.05f;
    private const float MiamiFloorTopY = 0.045f;
    private const float GrassSurfaceTopY = 0.04f;
    private const float BeachSurfaceTopY = 0.035f;

    [MenuItem("Tools/Island Map/Align Drive Surface Tops")]
    public static void ApplyForCommandLine()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int floorCount = AlignMiamiFloors();
        int groundCount = AlignGroundVisuals();
        int disabledColliderCount = DisableRedundantSurfaceColliders();
        int enabledBarrelColliderCount = EnableBarrelColliders();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log(
            $"Aligned {floorCount} Miami floor instances and {groundCount} ground visuals " +
            $"to floor Y={MiamiFloorTopY:F3}, grass Y={GrassSurfaceTopY:F3}, " +
            $"and beach Y={BeachSurfaceTopY:F3}; road remains at Y={RoadSurfaceTopY:F2}. " +
            $"Disabled {disabledColliderCount} redundant visual-surface colliders and enabled " +
            $"{enabledBarrelColliderCount} barrel colliders.");
    }

    public static void AnalyzeForCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        HashSet<GameObject> roots = new HashSet<GameObject>();
        foreach (Renderer renderer in Object.FindObjectsOfType<Renderer>(true))
        {
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject);
            if (root == null || !IsFloor(root.name) || !roots.Add(root))
            {
                continue;
            }

            Bounds bounds = GetRendererBounds(root);
            if (bounds.center.x < 15f || bounds.center.x > 85f
                || bounds.center.z < 10f || bounds.center.z > 85f)
            {
                continue;
            }

            Collider collider = root.GetComponentInChildren<Collider>(true);
            string colliderBounds = collider != null
                ? $"colliderCenterY={collider.bounds.center.y:F3}|colliderSizeY={collider.bounds.size.y:F3}"
                : "noCollider";
            Debug.Log(
                $"FLOOR_DIAG|{root.name}|parent={(root.transform.parent != null ? root.transform.parent.name : "<root>")}|" +
                $"position={root.transform.position.x:F2},{root.transform.position.y:F3},{root.transform.position.z:F2}|" +
                $"scaleY={root.transform.lossyScale.y:F3}|rendererMinY={bounds.min.y:F3}|" +
                $"rendererMaxY={bounds.max.y:F3}|rendererSizeY={bounds.size.y:F3}|{colliderBounds}");
        }
    }

    private static int AlignMiamiFloors()
    {
        int alignedCount = 0;
        HashSet<GameObject> roots = new HashSet<GameObject>();
        foreach (Renderer renderer in Object.FindObjectsOfType<Renderer>(true))
        {
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject);
            if (root == null || !IsFloor(root.name) || !roots.Add(root))
            {
                continue;
            }

            AlignTop(root, GetRendererBounds(root), MiamiFloorTopY);
            alignedCount++;
        }

        return alignedCount;
    }

    private static int AlignGroundVisuals()
    {
        int alignedCount = 0;
        foreach (GameObject gameObject in Object.FindObjectsOfType<GameObject>(true))
        {
            if (!gameObject.scene.IsValid()
                || gameObject.scene != SceneManager.GetActiveScene()
                || !gameObject.name.StartsWith("ENV_Ground_Grass")
                    && gameObject.name != "ENV_Ground_Beach")
            {
                continue;
            }

            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                continue;
            }

            float targetTop = gameObject.name == "ENV_Ground_Beach"
                ? BeachSurfaceTopY
                : GrassSurfaceTopY;
            AlignTop(gameObject, renderer.bounds, targetTop);
            ApplyGroundSeamOverlap(gameObject);
            alignedCount++;
        }

        return alignedCount;
    }

    private static void ApplyGroundSeamOverlap(GameObject gameObject)
    {
        Vector3 scale = gameObject.transform.localScale;
        if (gameObject.name == "ENV_Ground_Grass")
        {
            scale.x = 141.29329f;
            scale.z = 234.5718f;
        }
        else if (gameObject.name == "ENV_Ground_Grass (1)")
        {
            scale.x = 109.21164f;
            scale.z = 124.13108f;
        }
        else
        {
            return;
        }

        gameObject.transform.localScale = scale;
        EditorUtility.SetDirty(gameObject.transform);
    }

    private static int DisableRedundantSurfaceColliders()
    {
        int disabledCount = 0;
        foreach (Collider collider in Object.FindObjectsOfType<Collider>(true))
        {
            if (!collider.enabled || !IsRedundantVisualSurface(collider.transform))
            {
                continue;
            }

            Undo.RecordObject(collider, "Disable redundant visual surface collider");
            collider.enabled = false;
            EditorUtility.SetDirty(collider);
            disabledCount++;
        }

        return disabledCount;
    }

    private static bool IsRedundantVisualSurface(Transform transform)
    {
        GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(transform.gameObject);
        string objectName = prefabRoot != null ? prefabRoot.name : transform.gameObject.name;
        return objectName.StartsWith("ENV_Ground_Grass") || objectName == "ENV_Ground_Beach"
            || objectName.StartsWith("ENV_Road_")
            || objectName.StartsWith("MB_Coastal_Sidewalk_")
            || objectName.StartsWith("MB_Sidewalk_")
            || objectName.StartsWith("MB_Bike_Path_")
            || objectName == "MB_Promenade"
            || objectName.StartsWith("MB_Road_")
                && !objectName.StartsWith("MB_Road_Barrier_");
    }

    private static int EnableBarrelColliders()
    {
        int enabledCount = 0;
        foreach (Collider collider in Object.FindObjectsOfType<Collider>(true))
        {
            if (collider.enabled || !HasNamedAncestor(collider.transform, "PROP_OilBarrel"))
            {
                continue;
            }

            Undo.RecordObject(collider, "Enable barrel collider");
            collider.enabled = true;
            EditorUtility.SetDirty(collider);
            enabledCount++;
        }

        return enabledCount;
    }

    private static bool HasNamedAncestor(Transform transform, string prefix)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            if (current.name.StartsWith(prefix))
            {
                return true;
            }
        }

        return false;
    }

    private static void AlignTop(GameObject gameObject, Bounds bounds, float targetTopY)
    {
        float verticalOffset = targetTopY - bounds.max.y;
        if (Mathf.Abs(verticalOffset) <= 0.0001f)
        {
            return;
        }

        Undo.RecordObject(gameObject.transform, "Align drive surface top");
        gameObject.transform.position += Vector3.up * verticalOffset;
        EditorUtility.SetDirty(gameObject.transform);
    }

    private static bool IsFloor(string objectName)
    {
        return objectName.StartsWith("MB_Sidewalk_")
            || objectName.StartsWith("MB_Coastal_Sidewalk_")
            || objectName == "MB_Promenade";
    }

    private static Bounds GetRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds;
    }
}
