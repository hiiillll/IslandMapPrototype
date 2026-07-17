using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerCarInstaller
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string ModelPath = "Assets/PlayerCar/Model/sedan_Visual.fbx";
    private const string MarkerPath = "Library/PlayerCarInstalled.v1";

    [MenuItem("Tools/Island Map/Install Player Car")]
    public static void TryInstall()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryInstall;
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        GameObject southRoad = GameObject.Find("ENV_Road_South");
        if (!scene.IsValid() || scene.path != ScenePath || modelAsset == null || southRoad == null)
        {
            EditorApplication.delayCall += TryInstall;
            return;
        }

        GameObject existingPlayer = GameObject.Find("PLAYER_Car");
        if (existingPlayer != null)
        {
            UnityEngine.Object.DestroyImmediate(existingPlayer);
        }

        Renderer roadRenderer = southRoad.GetComponent<Renderer>();
        Bounds roadBounds = roadRenderer.bounds;
        float roadWidth = Mathf.Min(roadBounds.size.x, roadBounds.size.z);

        GameObject player = new GameObject("PLAYER_Car");
        player.tag = "Player";
        SceneManager.MoveGameObjectToScene(player, scene);
        player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        GameObject visual = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
        visual.name = "Visual_Sedan";
        visual.transform.SetParent(player.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        ResetImportedRootOffset(visual.transform);
        FitVisual(player, visual, roadWidth * 0.30f);

        Bounds localBounds = CalculateLocalBounds(player);
        BoxCollider collider = player.AddComponent<BoxCollider>();
        collider.center = localBounds.center;
        collider.size = localBounds.size;

        Rigidbody body = player.AddComponent<Rigidbody>();
        body.mass = 1200f;
        body.drag = 0.15f;
        body.angularDrag = 4f;
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        player.AddComponent<SimpleAutoDriveController>();

        float startX = roadBounds.center.x - roadBounds.extents.x * 0.55f;
        player.transform.position = new Vector3(startX, roadBounds.max.y + 0.03f, roadBounds.center.z);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("O"));
        Selection.activeGameObject = player;
        Debug.Log("Installed the original sedan model with left/right-only auto-drive controls.");
    }

    private static void ResetImportedRootOffset(Transform visual)
    {
        foreach (Transform child in visual)
        {
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
        }
    }

    private static void FitVisual(GameObject player, GameObject visual, float targetSize)
    {
        Bounds bounds = CalculateWorldBounds(visual);
        float horizontalSize = Mathf.Max(bounds.size.x, bounds.size.z);
        float scale = targetSize / Mathf.Max(horizontalSize, 0.01f);
        visual.transform.localScale = Vector3.one * scale;

        Bounds fittedBounds = CalculateLocalBounds(player);
        visual.transform.localPosition -= new Vector3(fittedBounds.center.x, fittedBounds.min.y, fittedBounds.center.z);
    }

    private static Bounds CalculateWorldBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }
        return bounds;
    }

    private static Bounds CalculateLocalBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool initialized = false;
        Bounds bounds = new Bounds();
        foreach (Renderer renderer in renderers)
        {
            Vector3 min = renderer.bounds.min;
            Vector3 max = renderer.bounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z)
            };
            foreach (Vector3 corner in corners)
            {
                Vector3 localPoint = root.transform.InverseTransformPoint(corner);
                if (!initialized)
                {
                    bounds = new Bounds(localPoint, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(localPoint);
                }
            }
        }
        return bounds;
    }
}
