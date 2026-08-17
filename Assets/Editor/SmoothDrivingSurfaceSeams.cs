using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SmoothDrivingSurfaceSeams
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string MarkerPath = "Library/DrivingSurfaceSeamsSmoothed.v1";
    // PhysX requires non-zero volume; 0.01 is effectively a surface while
    // remaining reliable for continuous collision detection.
    private const float PlateThickness = 0.01f;
    private const float GrassTopOffset = 0.01f;
    private const float BeachTopOffset = 0.015f;

    public static void ApplyFromBatchMode()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        TryApply();
    }

    [MenuItem("Tools/Island Map/Smooth Driving Surface Seams")]
    public static void TryApply()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryApply;
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject collisionRoot = GameObject.Find("COLLISION");
        GameObject player = GameObject.Find("PLAYER_Car");
        if (!scene.IsValid() || scene.path != ScenePath || collisionRoot == null || player == null)
        {
            EditorApplication.delayCall += TryApply;
            return;
        }

        BoxCollider[] surfaceColliders = collisionRoot.GetComponentsInChildren<BoxCollider>(true)
            .Where(collider => collider.gameObject.name.StartsWith("COL_Road", StringComparison.Ordinal)
                || collider.gameObject.name == "COL_Grass"
                || collider.gameObject.name == "COL_Beach")
            .ToArray();
        if (surfaceColliders.Length == 0)
        {
            EditorApplication.delayCall += TryApply;
            return;
        }

        float commonTop = surfaceColliders
            .Where(collider => collider.gameObject.name.StartsWith("COL_Road", StringComparison.Ordinal))
            .Max(collider => collider.bounds.max.y);

        foreach (BoxCollider surfaceCollider in surfaceColliders)
        {
            float targetTop = commonTop;
            if (surfaceCollider.gameObject.name == "COL_Grass")
            {
                targetTop -= GrassTopOffset;
            }
            else if (surfaceCollider.gameObject.name == "COL_Beach")
            {
                targetTop -= BeachTopOffset;
            }

            Vector3 worldCenter = surfaceCollider.bounds.center;
            worldCenter.y = targetTop - PlateThickness * 0.5f;
            surfaceCollider.center = surfaceCollider.transform.InverseTransformPoint(worldCenter);
            Vector3 size = surfaceCollider.size;
            size.y = PlateThickness / Mathf.Max(Mathf.Abs(surfaceCollider.transform.lossyScale.y), 0.001f);
            surfaceCollider.size = size;
        }

        BoxCollider oldPlayerCollider = player.GetComponent<BoxCollider>();
        CapsuleCollider playerCollider = player.GetComponent<CapsuleCollider>();
        if (playerCollider == null)
        {
            playerCollider = player.AddComponent<CapsuleCollider>();
        }

        if (oldPlayerCollider != null)
        {
            playerCollider.direction = 2;
            playerCollider.center = oldPlayerCollider.center;
            playerCollider.radius = Mathf.Min(oldPlayerCollider.size.x, oldPlayerCollider.size.y) * 0.45f;
            playerCollider.height = Mathf.Max(
                oldPlayerCollider.size.z * 0.92f,
                playerCollider.radius * 2f);
            UnityEngine.Object.DestroyImmediate(oldPlayerCollider);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("O"));
        Selection.activeGameObject = collisionRoot;
        Debug.Log(
            $"Aligned {surfaceColliders.Length} thin driving colliders with road top {commonTop:F3}, " +
            $"grass top {commonTop - GrassTopOffset:F3}, and beach top {commonTop - BeachTopOffset:F3}.");
    }
}
