using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class Level03GorillaMountainPropInstaller
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string EnvironmentName = "ENVIRONMENT_Level03";
    private const string RootName = "DECOR_Level03_GorillaMountainProps";
    private const string TreePath =
        "Assets/Level03/GorillaModels/TropicalTree/TropicalTree_Gorilla.obj";
    private const string RockPath =
        "Assets/Level03/GorillaModels/VolcanicRock/VolcanicRock_Gorilla.obj";
    private const int TreeCount = 14;
    private const int RockCount = 22;

    private sealed class SurfacePoint
    {
        public Vector3 position;
        public Vector3 normal;
        public float grassWeight;
        public float rockWeight;
        public float slope;
    }

    public static void ApplyFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Apply();
        Level03ActivePlanSplineRoadRebuilder.RenderVerificationPreview();
    }

    [MenuItem("Tools/Island Map/Level03/Place Gorilla Mountain Props")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException("Level03 must be the active scene.");
        }

        ConfigureModelImport(TreePath);
        ConfigureModelImport(RockPath);
        GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePath);
        GameObject rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RockPath);
        if (treePrefab == null || rockPrefab == null)
        {
            throw new InvalidOperationException("The converted Gorilla OBJ models were not imported.");
        }

        int treeTriangles = CountTriangles(treePrefab);
        int rockTriangles = CountTriangles(rockPrefab);
        if ((long)treeTriangles * TreeCount + (long)rockTriangles * RockCount > 350000)
        {
            throw new InvalidOperationException(
                "The Gorilla prop set exceeds the 350,000 triangle placement budget.");
        }

        GameObject environment = FindSceneObject(scene, EnvironmentName);
        GameObject previousRoot = FindSceneObjectOrNull(scene, RootName);
        if (previousRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(previousRoot);
        }

        GameObject root = new GameObject(RootName);
        root.transform.SetParent(environment.transform, false);
        root.layer = environment.layer;
        GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);

        Terrain[] terrains = UnityEngine.Object.FindObjectsOfType<Terrain>(true)
            .Where(item => item.gameObject.scene == scene && item.terrainData != null)
            .ToArray();
        System.Random random = new System.Random(34033);
        List<Vector2> occupied = new List<Vector2>(TreeCount + RockCount);

        int placedTrees = PlaceProps(
            treePrefab,
            root.transform,
            terrains,
            random,
            occupied,
            TreeCount,
            70f,
            point => point.grassWeight > 0.38f && point.slope < 26f,
            16f,
            23f,
            false,
            "Tree");
        int placedRocks = PlaceProps(
            rockPrefab,
            root.transform,
            terrains,
            random,
            occupied,
            RockCount,
            42f,
            point => point.rockWeight > 0.34f && point.slope > 10f,
            3.8f,
            7.5f,
            true,
            "Rock");

        if (placedTrees < TreeCount || placedRocks < RockCount)
        {
            throw new InvalidOperationException(
                $"Only placed {placedTrees}/{TreeCount} trees and " +
                $"{placedRocks}/{RockCount} rocks on valid mountain surfaces.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log(
            $"[Level03 Gorilla Props] Placed {placedTrees} trees ({treeTriangles:N0} triangles each) " +
            $"and {placedRocks} rocks ({rockTriangles:N0} triangles each). " +
            $"Total prop instances: {root.transform.childCount}.");
    }

    private static int PlaceProps(
        GameObject prefab,
        Transform parent,
        Terrain[] terrains,
        System.Random random,
        List<Vector2> occupied,
        int count,
        float clearance,
        Func<SurfacePoint, bool> predicate,
        float minimumHeight,
        float maximumHeight,
        bool alignToSurface,
        string namePrefix)
    {
        int placed = 0;
        int attempts = 0;
        while (placed < count && attempts++ < count * 500)
        {
            float x = Mathf.Lerp(-670f, 900f, (float)random.NextDouble());
            float z = Mathf.Lerp(-900f, 900f, (float)random.NextDouble());
            Vector2 horizontal = new Vector2(x, z);
            if (occupied.Any(point => Vector2.SqrMagnitude(point - horizontal) < clearance * clearance) ||
                !TryGetSurfacePoint(terrains, x, z, out SurfacePoint surface) ||
                !predicate(surface))
            {
                continue;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                continue;
            }

            instance.name = $"{namePrefix}_{placed + 1:000}";
            instance.transform.SetParent(parent, true);
            instance.transform.position = surface.position;
            Quaternion yaw = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            instance.transform.rotation = alignToSurface
                ? Quaternion.FromToRotation(Vector3.up, surface.normal) * yaw
                : yaw;

            Bounds bounds = CalculateBounds(instance);
            float targetHeight = Mathf.Lerp(
                minimumHeight,
                maximumHeight,
                (float)random.NextDouble());
            float scale = targetHeight / Mathf.Max(bounds.size.y, 0.01f);
            instance.transform.localScale = Vector3.one * scale;
            bounds = CalculateBounds(instance);
            instance.transform.position += Vector3.up * (surface.position.y - bounds.min.y);

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        continue;
                    }
                    if (material.HasProperty("_Glossiness"))
                    {
                        material.SetFloat("_Glossiness", namePrefix == "Rock" ? 0.08f : 0.03f);
                    }
                    if (material.HasProperty("_Metallic"))
                    {
                        material.SetFloat("_Metallic", 0f);
                    }
                    EditorUtility.SetDirty(material);
                }
            }

            SetLayerRecursively(instance, parent.gameObject.layer);
            SetStaticRecursively(instance);
            occupied.Add(horizontal);
            placed++;
        }
        return placed;
    }

    private static bool TryGetSurfacePoint(
        IEnumerable<Terrain> terrains,
        float worldX,
        float worldZ,
        out SurfacePoint point)
    {
        foreach (Terrain terrain in terrains)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (worldX < origin.x || worldX > origin.x + size.x ||
                worldZ < origin.z || worldZ > origin.z + size.z)
            {
                continue;
            }

            float normalizedX = (worldX - origin.x) / size.x;
            float normalizedZ = (worldZ - origin.z) / size.z;
            TerrainData data = terrain.terrainData;
            int alphaX = Mathf.Clamp(
                Mathf.FloorToInt(normalizedX * data.alphamapWidth),
                0,
                data.alphamapWidth - 1);
            int alphaZ = Mathf.Clamp(
                Mathf.FloorToInt(normalizedZ * data.alphamapHeight),
                0,
                data.alphamapHeight - 1);
            float[,,] weights = data.GetAlphamaps(alphaX, alphaZ, 1, 1);
            TerrainLayer[] layers = data.terrainLayers;
            int grassIndex = Array.FindIndex(
                layers,
                layer => layer != null && layer.name == "TL_Level03_MountainGrass");
            int rockIndex = Array.FindIndex(
                layers,
                layer => layer != null && layer.name == "TL_Level03_MountainRock");
            float height = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + origin.y;
            point = new SurfacePoint
            {
                position = new Vector3(worldX, height, worldZ),
                normal = data.GetInterpolatedNormal(normalizedX, normalizedZ),
                slope = data.GetSteepness(normalizedX, normalizedZ),
                grassWeight = grassIndex >= 0 ? weights[0, 0, grassIndex] : 0f,
                rockWeight = rockIndex >= 0 ? weights[0, 0, rockIndex] : 0f
            };
            return true;
        }

        point = null;
        return false;
    }

    private static void ConfigureModelImport(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"Gorilla OBJ import failed: {assetPath}");
        }
        importer.globalScale = 1f;
        importer.isReadable = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();
    }

    private static int CountTriangles(GameObject prefab)
    {
        return prefab.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter.sharedMesh != null)
            .Sum(filter => filter.sharedMesh.triangles.Length / 3);
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
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

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        return FindSceneObjectOrNull(scene, objectName) ??
               throw new InvalidOperationException($"Scene object was not found: {objectName}");
    }

    private static GameObject FindSceneObjectOrNull(Scene scene, string objectName)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(item => item.scene == scene && item.name == objectName);
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
            StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.ReflectionProbeStatic);
        foreach (Transform child in root.transform)
        {
            SetStaticRecursively(child.gameObject);
        }
    }
}
