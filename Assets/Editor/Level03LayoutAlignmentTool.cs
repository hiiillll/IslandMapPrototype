using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Level03LayoutAlignmentTool
{
    // Kept editor-only so alignment and verification never affect runtime builds.
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string ImportedModelsRoot = "Assets/Models/Imported/";
    private const string RoadName = "ENV_Level03_RoadNetwork_FromReference";
    private const string MarkingName = "ENV_Level03_RoadMarkings";
    private const string ConvertedTerrainName = "ENV_Level03_ConvertedTerrain";
    private const string RequestAssetPath = "Assets/Editor/Level03LayoutAlignmentTool.request";
    private const string ReportPath = "Library/CodexLevel03LayoutAlignmentReport.json";
    private const float ModelGroundClearance = 0.02f;

    [Serializable]
    private sealed class ModelPlacement
    {
        public string name;
        public string hierarchyPath;
        public string prefabAssetPath;
        public float previousBoundsBottom;
        public float sampledGroundHeight;
        public float verticalAdjustment;
        public float finalBoundsBottom;
    }

    [Serializable]
    private sealed class AlignmentReport
    {
        public bool success;
        public string message;
        public int terrainCount;
        public int adjustedModelCount;
        public Vector3 previousRoadLocalPosition;
        public Vector3 finalRoadLocalPosition;
        public Vector3 previousMarkingLocalPosition;
        public Vector3 finalMarkingLocalPosition;
        public float roadSurfaceHeight;
        public float markingSurfaceHeight;
        public float flatTerrainHeight;
        public int roadVertexCount;
        public int roadTriangleCount;
        public int markingVertexCount;
        public int markingTriangleCount;
        public ModelPlacement[] modelPlacements;
        public string completedAt;
    }

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    private static string RequestFilePath => Path.Combine(ProjectRoot, RequestAssetPath);
    private static string ReportFilePath => Path.Combine(ProjectRoot, ReportPath);

    static Level03LayoutAlignmentTool()
    {
        if (File.Exists(RequestFilePath))
        {
            EditorApplication.delayCall += AlignFromRequest;
        }
    }

    [MenuItem("Tools/Island Map/Level03/Align Models Ground Roads And Markings")]
    private static void AlignFromMenu()
    {
        AlignAndReport();
    }

    private static void AlignFromRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AlignFromRequest;
            return;
        }

        try
        {
            AlignAndReport();
        }
        finally
        {
            DeleteRequestFiles();
            AssetDatabase.Refresh();
        }
    }

    private static void AlignAndReport()
    {
        Scene previousActiveScene = SceneManager.GetActiveScene();
        Scene level03 = SceneManager.GetSceneByPath(ScenePath);
        bool openedLevel03 = !level03.IsValid() || !level03.isLoaded;
        try
        {
            if (openedLevel03)
            {
                level03 = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            EditorSceneManager.SetActiveScene(level03);
            Level03ActivePlanSplineRoadRebuilder.Rebuild();

            GameObject road = FindSceneObject(level03, RoadName);
            GameObject markings = FindSceneObject(level03, MarkingName);
            GameObject convertedTerrain = FindSceneObject(level03, ConvertedTerrainName);
            Terrain[] terrains = convertedTerrain.GetComponentsInChildren<Terrain>(true);
            if (terrains.Length == 0)
            {
                throw new InvalidOperationException("Level03 converted Terrain tiles are missing.");
            }

            Vector3 previousRoadPosition = road.transform.localPosition;
            Vector3 previousMarkingPosition = markings.transform.localPosition;
            RecordAndResetTransform(road.transform, "Align Level03 road to Terrain");
            RecordAndResetTransform(markings.transform, "Align Level03 markings to road centre");
            RecordAndResetTransform(convertedTerrain.transform, "Align Level03 Terrain root");
            Physics.SyncTransforms();

            List<ModelPlacement> placements = new List<ModelPlacement>();
            foreach (GameObject model in FindImportedModelRoots(level03))
            {
                if (!TryCalculateBounds(model, out Bounds previousBounds) ||
                    !TrySampleGroundUnderBounds(terrains, previousBounds, out float groundHeight))
                {
                    continue;
                }

                float targetBottom = groundHeight + ModelGroundClearance;
                float verticalAdjustment = targetBottom - previousBounds.min.y;
                if (Mathf.Abs(verticalAdjustment) > 0.0001f)
                {
                    Undo.RecordObject(model.transform, "Snap Level03 model to Terrain");
                    model.transform.position += Vector3.up * verticalAdjustment;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
                    EditorUtility.SetDirty(model.transform);
                    Physics.SyncTransforms();
                }

                Bounds finalBounds = CalculateBounds(model);
                placements.Add(new ModelPlacement
                {
                    name = model.name,
                    hierarchyPath = GetHierarchyPath(model.transform),
                    prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model),
                    previousBoundsBottom = previousBounds.min.y,
                    sampledGroundHeight = groundHeight,
                    verticalAdjustment = verticalAdjustment,
                    finalBoundsBottom = finalBounds.min.y
                });
            }

            Mesh roadMesh = road.GetComponent<MeshFilter>()?.sharedMesh;
            Mesh markingMesh = markings.GetComponent<MeshFilter>()?.sharedMesh;
            if (roadMesh == null || markingMesh == null)
            {
                throw new InvalidOperationException("The rebuilt Level03 road or marking Mesh is missing.");
            }

            MeshCollider roadCollider = road.GetComponent<MeshCollider>();
            if (roadCollider != null && roadCollider.sharedMesh != roadMesh)
            {
                roadCollider.sharedMesh = null;
                roadCollider.sharedMesh = roadMesh;
                EditorUtility.SetDirty(roadCollider);
            }

            EditorSceneManager.MarkSceneDirty(level03);
            if (!EditorSceneManager.SaveScene(level03))
            {
                throw new IOException("Unity could not save Level03 after layout alignment.");
            }

            AssetDatabase.SaveAssets();
            Level03SceneAudit.Audit();
            Level03ActivePlanSplineRoadRebuilder.RenderVerificationPreview();

            Bounds roadBounds = road.GetComponent<Renderer>().bounds;
            Bounds markingBounds = markings.GetComponent<Renderer>().bounds;
            float flatTerrainHeight = SampleMedianTerrainHeight(terrains);
            AlignmentReport report = new AlignmentReport
            {
                success = true,
                message = "Level03 models, Terrain, roads, and centre markings are aligned.",
                terrainCount = terrains.Length,
                adjustedModelCount = placements.Count(item => Mathf.Abs(item.verticalAdjustment) > 0.0001f),
                previousRoadLocalPosition = previousRoadPosition,
                finalRoadLocalPosition = road.transform.localPosition,
                previousMarkingLocalPosition = previousMarkingPosition,
                finalMarkingLocalPosition = markings.transform.localPosition,
                roadSurfaceHeight = roadBounds.max.y,
                markingSurfaceHeight = markingBounds.max.y,
                flatTerrainHeight = flatTerrainHeight,
                roadVertexCount = roadMesh.vertexCount,
                roadTriangleCount = roadMesh.triangles.Length / 3,
                markingVertexCount = markingMesh.vertexCount,
                markingTriangleCount = markingMesh.triangles.Length / 3,
                modelPlacements = placements.ToArray(),
                completedAt = DateTime.Now.ToString("O")
            };
            WriteReport(report);
            Debug.Log(
                $"[Level03 Layout Alignment] Rebuilt uniform continuous roads, centred markings, " +
                $"and aligned {report.adjustedModelCount} imported models to {terrains.Length} Terrain tiles.");
        }
        catch (Exception exception)
        {
            WriteReport(new AlignmentReport
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
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                EditorSceneManager.SetActiveScene(previousActiveScene);
            }

            if (openedLevel03 && level03.IsValid() && level03.isLoaded)
            {
                EditorSceneManager.CloseScene(level03, true);
            }
        }
    }

    private static void RecordAndResetTransform(Transform transform, string undoName)
    {
        if (transform.localPosition == Vector3.zero &&
            transform.localRotation == Quaternion.identity &&
            transform.localScale == Vector3.one)
        {
            return;
        }

        Undo.RecordObject(transform, undoName);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        EditorUtility.SetDirty(transform);
    }

    private static IEnumerable<GameObject> FindImportedModelRoots(Scene scene)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .Where(candidate => PrefabUtility.GetNearestPrefabInstanceRoot(candidate) == candidate)
            .Where(candidate =>
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate);
                return !string.IsNullOrEmpty(path) &&
                       path.StartsWith(ImportedModelsRoot, StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(candidate => GetHierarchyPath(candidate.transform));
    }

    private static bool TrySampleGroundUnderBounds(
        IReadOnlyCollection<Terrain> terrains,
        Bounds bounds,
        out float height)
    {
        float extentX = Mathf.Max(0.1f, bounds.extents.x * 0.8f);
        float extentZ = Mathf.Max(0.1f, bounds.extents.z * 0.8f);
        float[] offsets = { -1f, 0f, 1f };
        List<float> samples = new List<float>(9);
        foreach (float zOffset in offsets)
        {
            foreach (float xOffset in offsets)
            {
                float worldX = bounds.center.x + extentX * xOffset;
                float worldZ = bounds.center.z + extentZ * zOffset;
                if (TrySampleTerrainHeight(terrains, worldX, worldZ, out float sample))
                {
                    samples.Add(sample);
                }
            }
        }

        if (samples.Count == 0)
        {
            height = 0f;
            return false;
        }

        samples.Sort();
        // The upper quartile avoids sinking a large footprint into a slope without
        // letting a single edge sample make the entire building visibly float.
        height = samples[Mathf.Clamp(
            Mathf.FloorToInt((samples.Count - 1) * 0.75f),
            0,
            samples.Count - 1)];
        return true;
    }

    private static bool TrySampleTerrainHeight(
        IEnumerable<Terrain> terrains,
        float worldX,
        float worldZ,
        out float height)
    {
        foreach (Terrain terrain in terrains)
        {
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            float normalizedX = (worldX - origin.x) / size.x;
            float normalizedZ = (worldZ - origin.z) / size.z;
            if (normalizedX < 0f || normalizedX > 1f ||
                normalizedZ < 0f || normalizedZ > 1f)
            {
                continue;
            }

            int holeX = Mathf.Clamp(
                Mathf.FloorToInt(normalizedX * data.holesResolution),
                0,
                data.holesResolution - 1);
            int holeZ = Mathf.Clamp(
                Mathf.FloorToInt(normalizedZ * data.holesResolution),
                0,
                data.holesResolution - 1);
            if (data.IsHole(holeX, holeZ))
            {
                continue;
            }

            height = origin.y + terrain.SampleHeight(new Vector3(worldX, origin.y, worldZ));
            return true;
        }

        height = 0f;
        return false;
    }

    private static float SampleMedianTerrainHeight(IEnumerable<Terrain> terrains)
    {
        List<float> heights = terrains
            .Select(terrain =>
            {
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                return origin.y + terrain.SampleHeight(
                    origin + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f));
            })
            .OrderBy(value => value)
            .ToList();
        return heights.Count == 0 ? 0f : heights[heights.Count / 2];
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
            throw new InvalidOperationException("Model has no renderers: " + root.name);
        }

        return bounds;
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        GameObject found = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .FirstOrDefault(candidate => candidate.name == name);
        if (found == null)
        {
            throw new MissingReferenceException($"Level03 object '{name}' was not found.");
        }

        return found;
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

    private static void WriteReport(AlignmentReport report)
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
