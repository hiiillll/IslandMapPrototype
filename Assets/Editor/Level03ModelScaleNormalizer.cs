using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Level03ModelScaleNormalizer
{
    private const string Level03Path = "Assets/Scenes/Level03.unity";
    private const string IslandMapPath = "Assets/Scenes/IslandMap.unity";
    private const string ImportedModelsRoot = "Assets/Models/Imported/";
    private const string RequestAssetPath = "Assets/Editor/Level03ModelScaleNormalizer.request";
    private const string ReportPath = "Library/CodexLevel03ModelScaleNormalizationReport.json";

    [Serializable]
    private sealed class InstanceChange
    {
        public string name;
        public string hierarchyPath;
        public string prefabAssetPath;
        public string baselineSource;
        public float scaleFactor;
        public Vector3 previousLocalScale;
        public Vector3 newLocalScale;
        public Vector3 previousBoundsSize;
        public Vector3 newBoundsSize;
        public float targetMaximumDimension;
        public float previousMaximumDimension;
        public float newMaximumDimension;
        public float groundHeightDrift;
        public float horizontalCenterDrift;
    }

    [Serializable]
    private sealed class NormalizationReport
    {
        public bool success;
        public string message;
        public int changedInstanceCount;
        public int sameAssetBaselineCount;
        public int derivedBaselineCount;
        public int commonAssetCount;
        public float derivedGlobalScaleFactor;
        public string level03ScenePath;
        public string islandMapScenePath;
        public InstanceChange[] changes;
        public string completedAt;
    }

    private sealed class ModelInstance
    {
        public GameObject root;
        public string assetPath;
        public Bounds bounds;
    }

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    private static string RequestFilePath => Path.Combine(ProjectRoot, RequestAssetPath);
    private static string ReportFilePath => Path.Combine(ProjectRoot, ReportPath);

    static Level03ModelScaleNormalizer()
    {
        if (File.Exists(RequestFilePath))
        {
            EditorApplication.delayCall += NormalizeFromRequest;
        }
    }

    [MenuItem("Tools/Island Map/Level03/Match Model Sizes To IslandMap")]
    private static void NormalizeFromMenu()
    {
        NormalizeAndReport();
    }

    private static void NormalizeFromRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += NormalizeFromRequest;
            return;
        }

        try
        {
            NormalizeAndReport();
        }
        finally
        {
            DeleteRequestFiles();
            AssetDatabase.Refresh();
        }
    }

    private static void NormalizeAndReport()
    {
        List<Scene> openedScenes = new List<Scene>();
        try
        {
            Scene level03 = GetOrOpenScene(Level03Path, openedScenes);
            Scene islandMap = GetOrOpenScene(IslandMapPath, openedScenes);

            List<ModelInstance> level03Models = FindImportedModelInstances(level03);
            List<ModelInstance> islandMapModels = FindImportedModelInstances(islandMap);
            if (level03Models.Count == 0)
            {
                throw new InvalidOperationException("Level03 does not contain imported model prefab instances.");
            }

            Dictionary<string, float> islandBaselines = BuildAssetBaselines(islandMapModels);
            Dictionary<string, float> level03Baselines = BuildAssetBaselines(level03Models);
            List<float> commonAssetRatios = level03Baselines
                .Where(pair => islandBaselines.TryGetValue(pair.Key, out float islandSize) &&
                               islandSize > Mathf.Epsilon)
                .Select(pair => pair.Value / islandBaselines[pair.Key])
                .Where(ratio => ratio > Mathf.Epsilon && !float.IsInfinity(ratio) && !float.IsNaN(ratio))
                .ToList();

            if (commonAssetRatios.Count == 0)
            {
                throw new InvalidOperationException(
                    "Level03 and IslandMap do not share any imported model assets, so a safe scale baseline cannot be derived.");
            }

            float globalScaleFactor = 1f / Median(commonAssetRatios);
            globalScaleFactor = Mathf.Clamp(globalScaleFactor, 0.15f, 1.5f);

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Match Level03 Model Sizes To IslandMap");
            List<InstanceChange> changes = new List<InstanceChange>();
            int sameAssetBaselineCount = 0;
            int derivedBaselineCount = 0;

            foreach (ModelInstance model in level03Models
                         .OrderBy(item => item.assetPath)
                         .ThenBy(item => GetHierarchyPath(item.root.transform)))
            {
                Bounds previousBounds = model.bounds;
                float previousMaximumDimension = MaximumDimension(previousBounds);
                if (previousMaximumDimension <= Mathf.Epsilon)
                {
                    continue;
                }

                bool hasSameAssetBaseline = islandBaselines.TryGetValue(model.assetPath, out float targetSize);
                float scaleFactor = hasSameAssetBaseline
                    ? targetSize / previousMaximumDimension
                    : globalScaleFactor;
                scaleFactor = Mathf.Clamp(scaleFactor, 0.15f, 1.5f);

                Transform transform = model.root.transform;
                Vector3 previousLocalScale = transform.localScale;
                Undo.RecordObject(transform, "Normalize imported model scale");
                transform.localScale = previousLocalScale * scaleFactor;
                Physics.SyncTransforms();

                Bounds scaledBounds = CalculateBounds(model.root);
                Vector3 placementCorrection = new Vector3(
                    previousBounds.center.x - scaledBounds.center.x,
                    previousBounds.min.y - scaledBounds.min.y,
                    previousBounds.center.z - scaledBounds.center.z);
                transform.position += placementCorrection;
                Physics.SyncTransforms();

                Bounds finalBounds = CalculateBounds(model.root);
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
                EditorUtility.SetDirty(transform);

                changes.Add(new InstanceChange
                {
                    name = model.root.name,
                    hierarchyPath = GetHierarchyPath(transform),
                    prefabAssetPath = model.assetPath,
                    baselineSource = hasSameAssetBaseline
                        ? "IslandMap same asset median"
                        : "Median scale ratio of shared assets",
                    scaleFactor = scaleFactor,
                    previousLocalScale = previousLocalScale,
                    newLocalScale = transform.localScale,
                    previousBoundsSize = previousBounds.size,
                    newBoundsSize = finalBounds.size,
                    targetMaximumDimension = hasSameAssetBaseline
                        ? targetSize
                        : previousMaximumDimension * globalScaleFactor,
                    previousMaximumDimension = previousMaximumDimension,
                    newMaximumDimension = MaximumDimension(finalBounds),
                    groundHeightDrift = finalBounds.min.y - previousBounds.min.y,
                    horizontalCenterDrift = Vector2.Distance(
                        new Vector2(finalBounds.center.x, finalBounds.center.z),
                        new Vector2(previousBounds.center.x, previousBounds.center.z))
                });

                if (hasSameAssetBaseline)
                {
                    sameAssetBaselineCount++;
                }
                else
                {
                    derivedBaselineCount++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(level03);
            if (!EditorSceneManager.SaveScene(level03))
            {
                throw new IOException("Unity could not save Level03 after model normalization.");
            }

            NormalizationReport report = new NormalizationReport
            {
                success = true,
                message = "Level03 imported environment models now use IslandMap scale baselines.",
                changedInstanceCount = changes.Count,
                sameAssetBaselineCount = sameAssetBaselineCount,
                derivedBaselineCount = derivedBaselineCount,
                commonAssetCount = commonAssetRatios.Count,
                derivedGlobalScaleFactor = globalScaleFactor,
                level03ScenePath = level03.path,
                islandMapScenePath = islandMap.path,
                changes = changes.ToArray(),
                completedAt = DateTime.Now.ToString("O")
            };
            WriteReport(report);
            Debug.Log(
                $"[Level03 Model Scale Normalizer] Updated {changes.Count} imported model instances. " +
                $"Shared assets: {commonAssetRatios.Count}, fallback scale factor: {globalScaleFactor:F4}.");
        }
        catch (Exception exception)
        {
            WriteReport(new NormalizationReport
            {
                success = false,
                message = exception.GetType().Name + ": " + exception.Message,
                completedAt = DateTime.Now.ToString("O")
            });
            Debug.LogException(exception);
            throw;
        }
        finally
        {
            for (int index = openedScenes.Count - 1; index >= 0; index--)
            {
                Scene scene = openedScenes[index];
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }

    private static Scene GetOrOpenScene(string path, ICollection<Scene> openedScenes)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        if (scene.IsValid() && scene.isLoaded)
        {
            return scene;
        }

        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        openedScenes.Add(scene);
        return scene;
    }

    private static List<ModelInstance> FindImportedModelInstances(Scene scene)
    {
        List<ModelInstance> models = new List<ModelInstance>();
        foreach (Transform transform in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
        {
            GameObject candidate = transform.gameObject;
            if (PrefabUtility.GetNearestPrefabInstanceRoot(candidate) != candidate)
            {
                continue;
            }

            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate);
            if (string.IsNullOrEmpty(assetPath) ||
                !assetPath.StartsWith(ImportedModelsRoot, StringComparison.OrdinalIgnoreCase) ||
                !TryCalculateBounds(candidate, out Bounds bounds))
            {
                continue;
            }

            models.Add(new ModelInstance
            {
                root = candidate,
                assetPath = assetPath,
                bounds = bounds
            });
        }

        return models;
    }

    private static Dictionary<string, float> BuildAssetBaselines(IEnumerable<ModelInstance> models)
    {
        return models
            .GroupBy(model => model.assetPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => Median(group.Select(model => MaximumDimension(model.bounds))),
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryCalculateBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
            .ToArray();
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return true;
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        if (!TryCalculateBounds(root, out Bounds bounds))
        {
            throw new InvalidOperationException("Model has no mesh renderers: " + root.name);
        }

        return bounds;
    }

    private static float MaximumDimension(Bounds bounds)
    {
        return Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
    }

    private static float Median(IEnumerable<float> values)
    {
        float[] sorted = values.OrderBy(value => value).ToArray();
        if (sorted.Length == 0)
        {
            throw new InvalidOperationException("Cannot calculate a median from an empty collection.");
        }

        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) * 0.5f
            : sorted[middle];
    }

    private static string GetHierarchyPath(Transform transform)
    {
        Stack<string> names = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private static void WriteReport(NormalizationReport report)
    {
        File.WriteAllText(ReportFilePath, JsonUtility.ToJson(report, true));
    }

    private static void DeleteRequestFiles()
    {
        if (File.Exists(RequestFilePath))
        {
            File.Delete(RequestFilePath);
        }

        string metaPath = RequestFilePath + ".meta";
        if (File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }
    }
}
