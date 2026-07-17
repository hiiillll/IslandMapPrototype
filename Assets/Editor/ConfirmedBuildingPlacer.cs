using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ConfirmedBuildingPlacer
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string MarkerPath = "Library/ConfirmedBuildings.placed";
    private const string HouseFolder = "Assets/Models/Imported/Model_11";
    private const string SupermarketFolder = "Assets/Models/Imported/Model_02";
    private const string PoliceFolder = "Assets/Models/Imported/Police";

    [MenuItem("Tools/Island Map/Place Confirmed Buildings")]
    public static void TryPlaceBuildings()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryPlaceBuildings;
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            EditorApplication.delayCall += TryPlaceBuildings;
            return;
        }

        GameObject houseModel = LoadModel(HouseFolder);
        GameObject supermarketModel = LoadModel(SupermarketFolder);
        GameObject policeModel = LoadModel(PoliceFolder);
        GameObject roads = GameObject.Find("Tian Road Layout");
        GameObject grass = GameObject.Find("City Grass");
        if (houseModel == null || supermarketModel == null || policeModel == null || roads == null || grass == null)
        {
            EditorApplication.delayCall += TryPlaceBuildings;
            return;
        }

        GameObject existingGroup = GameObject.Find("Placed Buildings");
        if (existingGroup != null)
        {
            UnityEngine.Object.DestroyImmediate(existingGroup);
        }

        GameObject group = new GameObject("Placed Buildings");
        SceneManager.MoveGameObjectToScene(group, scene);

        Bounds roadBounds = CalculateBounds(roads);
        Renderer centerRoadRenderer = GameObject.Find("Center Intersection")?.GetComponent<Renderer>();
        float centerRoadWidth = centerRoadRenderer != null
            ? Mathf.Max(centerRoadRenderer.bounds.size.x, centerRoadRenderer.bounds.size.z)
            : roadBounds.size.x * 0.09f;
        float outerRoadWidth = centerRoadWidth;
        float innerEdge = roadBounds.extents.x - outerRoadWidth;
        float centerEdge = centerRoadWidth * 0.5f;
        float quadrantSpan = Mathf.Max(innerEdge - centerEdge, roadBounds.size.x * 0.25f);
        float offset = centerEdge + quadrantSpan * 0.5f;
        float groundY = grass.GetComponent<Renderer>().bounds.max.y;
        Vector3 center = roadBounds.center;

        PlaceBuilding(houseModel, "Residential North West", center + new Vector3(-offset, 0f, offset), quadrantSpan * 0.48f, groundY, center, group.transform);
        PlaceBuilding(supermarketModel, "Supermarket North East", center + new Vector3(offset, 0f, offset), quadrantSpan * 0.58f, groundY, center, group.transform);
        PlaceBuilding(policeModel, "Police Station South West", center + new Vector3(-offset, 0f, -offset), quadrantSpan * 0.58f, groundY, center, group.transform);
        PlaceBuilding(houseModel, "Residential South East", center + new Vector3(offset, 0f, -offset), quadrantSpan * 0.48f, groundY, center, group.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("O"));
        Selection.activeGameObject = group;
        Debug.Log("Placed confirmed house, supermarket and police station models without rebuilding the environment.");
    }

    private static GameObject LoadModel(string folder)
    {
        string assetPath = AssetDatabase.FindAssets("t:Model", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(path => path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
    }

    private static void PlaceBuilding(
        GameObject modelAsset,
        string name,
        Vector3 targetPosition,
        float targetFootprint,
        float groundY,
        Vector3 mapCenter,
        Transform parent)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
        if (instance == null)
        {
            return;
        }

        instance.name = name;
        instance.transform.SetParent(parent);
        instance.transform.position = targetPosition;
        instance.transform.rotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        Bounds initialBounds = CalculateBounds(instance);
        float largestFootprint = Mathf.Max(initialBounds.size.x, initialBounds.size.z);
        float uniformScale = targetFootprint / Mathf.Max(largestFootprint, 0.01f);
        instance.transform.localScale = Vector3.one * uniformScale;

        Vector3 lookTarget = new Vector3(mapCenter.x, targetPosition.y, mapCenter.z);
        instance.transform.LookAt(lookTarget, Vector3.up);
        Bounds placedBounds = CalculateBounds(instance);
        instance.transform.position += Vector3.up * (groundY - placedBounds.min.y);

        AddRootCollider(instance);
    }

    private static void AddRootCollider(GameObject instance)
    {
        Bounds worldBounds = CalculateBounds(instance);
        BoxCollider collider = instance.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = instance.AddComponent<BoxCollider>();
        }

        collider.center = instance.transform.InverseTransformPoint(worldBounds.center);
        Vector3 scale = instance.transform.lossyScale;
        collider.size = new Vector3(
            worldBounds.size.x / Mathf.Max(Mathf.Abs(scale.x), 0.001f),
            worldBounds.size.y / Mathf.Max(Mathf.Abs(scale.y), 0.001f),
            worldBounds.size.z / Mathf.Max(Mathf.Abs(scale.z), 0.001f));
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(root.transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds;
    }
}
