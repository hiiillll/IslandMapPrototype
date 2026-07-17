using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ScenePropOrganizer
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string MarkerPath = "Library/ScenePropsOrganized.v1";
    private const string IceCreamFolder = "Assets/Models/Imported/Model_04";
    private const string BarrelFolder = "Assets/Models/Imported/Model_03";
    private const string TireFolder = "Assets/Models/Imported/Model_08";

    [MenuItem("Tools/Island Map/Organize Scene Props")]
    public static void TryOrganize()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryOrganize;
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            EditorApplication.delayCall += TryOrganize;
            return;
        }

        GameObject policeStation = GameObject.Find("Police Station South West");
        GameObject grass = GameObject.Find("City Grass");
        if (policeStation == null || grass == null)
        {
            EditorApplication.delayCall += TryOrganize;
            return;
        }

        List<GameObject> iceCreamTrucks = FindInstances(scene, IceCreamFolder);
        List<GameObject> barrels = FindInstances(scene, BarrelFolder);
        List<GameObject> tires = FindInstances(scene, TireFolder);
        if (tires.Count == 0)
        {
            EditorApplication.delayCall += TryOrganize;
            return;
        }

        Transform propsRoot = GetOrCreateRoot(scene, "Scene Props");
        Transform iceCreamRoot = GetOrCreateChild(propsRoot, "Ice Cream Trucks");
        Transform barrelRoot = GetOrCreateChild(propsRoot, "Oil Barrels");
        Transform tireRoot = GetOrCreateChild(propsRoot, "Tires Beside Police Station");

        OrganizeInstances(iceCreamTrucks, iceCreamRoot, "Ice Cream Truck");
        OrganizeInstances(barrels, barrelRoot, "Oil Barrel");
        OrganizeInstances(tires, tireRoot, "Tire");
        ArrangeTires(tires, policeStation, grass.GetComponent<Renderer>().bounds.max.y);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("O"));
        Selection.activeGameObject = tireRoot.gameObject;
        Debug.Log($"Organized scene props and arranged {tires.Count} tires beside the police station.");
    }

    private static List<GameObject> FindInstances(Scene scene, string sourceFolder)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .Where(PrefabUtility.IsAnyPrefabInstanceRoot)
            .Where(gameObject =>
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                string sourcePath = source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
                return sourcePath.StartsWith(sourceFolder, StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(gameObject => gameObject.transform.position.x)
            .ThenBy(gameObject => gameObject.transform.position.z)
            .ToList();
    }

    private static void OrganizeInstances(List<GameObject> instances, Transform parent, string baseName)
    {
        for (int index = 0; index < instances.Count; index++)
        {
            GameObject instance = instances[index];
            instance.transform.SetParent(parent, true);
            instance.name = $"{baseName} {index + 1:00}";
        }
    }

    private static void ArrangeTires(List<GameObject> tires, GameObject policeStation, float groundY)
    {
        Bounds policeBounds = CalculateBounds(policeStation);
        Vector3 toMapCenter = -policeStation.transform.position;
        toMapCenter.y = 0f;
        if (toMapCenter.sqrMagnitude < 0.01f)
        {
            toMapCenter = Vector3.forward;
        }
        toMapCenter.Normalize();

        Vector3 rowDirection = new Vector3(-toMapCenter.z, 0f, toMapCenter.x);
        float tireSize = tires.Max(tire =>
        {
            Bounds tireBounds = CalculateBounds(tire);
            return Mathf.Max(tireBounds.size.x, tireBounds.size.z);
        });
        float policeRadius = Mathf.Max(policeBounds.extents.x, policeBounds.extents.z);
        Vector3 rowCenter = policeBounds.center + toMapCenter * (policeRadius + tireSize * 1.35f);
        rowCenter.y = groundY;
        float spacing = tireSize * 1.25f;

        for (int index = 0; index < tires.Count; index++)
        {
            GameObject tire = tires[index];
            float centeredIndex = index - (tires.Count - 1) * 0.5f;
            tire.transform.rotation = Quaternion.Euler(0f, policeStation.transform.eulerAngles.y, 0f);
            tire.transform.position = rowCenter + rowDirection * (centeredIndex * spacing);
            Bounds tireBounds = CalculateBounds(tire);
            tire.transform.position += Vector3.up * (groundY - tireBounds.min.y);
        }
    }

    private static Transform GetOrCreateRoot(Scene scene, string name)
    {
        GameObject existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
        if (existing != null)
        {
            return existing.transform;
        }

        GameObject root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root.transform;
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing;
        }

        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
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
