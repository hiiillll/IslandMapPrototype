using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Level03SceneAudit
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string ReportPath = "Library/CodexLevel03SceneAudit.json";
    private const string RoadName = "ENV_Level03_RoadNetwork_FromReference";
    private const string RoadCollisionMeshPath =
        "Assets/Level03/GeneratedTerrainRoad/MESH_Level03_RoadCollision_Thin.asset";
    private const string MarkingName = "ENV_Level03_RoadMarkings";
    private const string FlatGrassName = "ENV_Level03_FlatGrass_FirstLevel";
    private const string OceanName = "ENV_Level03_Ocean_4000x4000";
    private const string VegetationName = "DECOR_Level03_Vegetation_Optimized";
    private const string BuildingsName = "DECOR_Level03_Buildings_Optimized";
    private const string FirstLevelGrassPath = "Assets/Art/Materials/Grass.mat";

    [Serializable]
    private sealed class AuditReport
    {
        public bool success;
        public int errorCount;
        public int warningCount;
        public int activeRendererCount;
        public int meshFilterCount;
        public int uniqueMeshCount;
        public long renderedTriangleInstances;
        public long maximumLod0TriangleInstances;
        public long minimumLodTriangleInstances;
        public long uniqueMeshTriangles;
        public int colliderCount;
        public int terrainCount;
        public int terrainHeightmapResolution;
        public int lodGroupCount;
        public int[] samplePalmLodTriangles;
        public int palmCount;
        public int buildingCount;
        public int roadVertices;
        public int roadTriangles;
        public int roadDownwardTriangles;
        public bool roadColliderMatchesMesh;
        public bool roadMarkingsPresent;
        public bool flatGrassUsesFirstLevelMaterial;
        public string oceanMaterial;
        public string[] errors;
        public string[] warnings;
        public string completedAt;
    }

    public static void AuditFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Audit();
        Level03ActivePlanSplineRoadRebuilder.RenderVerificationPreview();
    }

    [MenuItem("Tools/Island Map/Level03/Audit Optimized Scene")]
    public static void Audit()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException("Level03 must be the active scene.");
        }

        List<string> errors = new List<string>();
        List<string> warnings = new List<string>();
        Renderer[] renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true)
            .Where(item => item.gameObject.scene == scene && item.enabled &&
                           item.gameObject.activeInHierarchy)
            .ToArray();
        MeshFilter[] filters = UnityEngine.Object.FindObjectsOfType<MeshFilter>(true)
            .Where(item => item.gameObject.scene == scene &&
                           item.gameObject.activeInHierarchy)
            .ToArray();
        HashSet<Mesh> uniqueMeshes = new HashSet<Mesh>();
        long renderedTriangles = 0;
        foreach (MeshFilter filter in filters)
        {
            Mesh mesh = filter.sharedMesh;
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (mesh == null || renderer == null || !renderer.enabled)
            {
                continue;
            }

            uniqueMeshes.Add(mesh);
            renderedTriangles += mesh.triangles.LongLength / 3;
            if (renderer.sharedMaterials.Any(material => material == null))
            {
                errors.Add($"Missing material on {filter.gameObject.name}.");
            }
        }

        foreach (SkinnedMeshRenderer renderer in renderers.OfType<SkinnedMeshRenderer>())
        {
            if (renderer.sharedMesh == null)
            {
                continue;
            }

            uniqueMeshes.Add(renderer.sharedMesh);
            renderedTriangles += renderer.sharedMesh.triangles.LongLength / 3;
        }

        GameObject road = FindSceneObject(scene, RoadName);
        Mesh roadMesh = road != null ? road.GetComponent<MeshFilter>()?.sharedMesh : null;
        MeshCollider roadCollider = road != null ? road.GetComponent<MeshCollider>() : null;
        if (roadMesh == null)
        {
            errors.Add("Road mesh is missing.");
        }

        Mesh roadCollisionMesh = roadCollider != null ? roadCollider.sharedMesh : null;
        bool colliderMatches = roadCollisionMesh != null &&
                               AssetDatabase.GetAssetPath(roadCollisionMesh) ==
                               RoadCollisionMeshPath;
        if (!colliderMatches)
        {
            errors.Add("Road MeshCollider does not use the dedicated thin collision mesh.");
        }

        int downward = roadMesh != null ? CountDownwardTriangles(roadMesh) : 0;
        if (downward > 0)
        {
            errors.Add($"Road mesh contains {downward} downward-facing triangles.");
        }

        GameObject marking = FindSceneObject(scene, MarkingName);
        bool markingsPresent = marking != null &&
                               marking.GetComponent<MeshFilter>()?.sharedMesh != null;
        if (!markingsPresent)
        {
            warnings.Add("Road marking mesh is missing.");
        }

        GameObject flatGrass = FindSceneObject(scene, FlatGrassName);
        Material firstLevelGrass = AssetDatabase.LoadAssetAtPath<Material>(FirstLevelGrassPath);
        bool correctGrass = flatGrass != null && firstLevelGrass != null &&
                            flatGrass.GetComponent<MeshRenderer>()?.sharedMaterial ==
                            firstLevelGrass;
        if (!correctGrass)
        {
            errors.Add("Zero-height grass does not use the exact Level01 grass material.");
        }

        GameObject ocean = FindSceneObject(scene, OceanName);
        Material oceanMaterial = ocean != null
            ? ocean.GetComponent<MeshRenderer>()?.sharedMaterial
            : null;
        if (oceanMaterial == null)
        {
            errors.Add("Ocean material is missing.");
        }

        Terrain[] terrains = UnityEngine.Object.FindObjectsOfType<Terrain>(true)
            .Where(item => item.gameObject.scene == scene)
            .ToArray();
        int terrainResolution = terrains.Length > 0
            ? terrains.Min(item => item.terrainData.heightmapResolution)
            : 0;
        if (terrains.Length != 16)
        {
            warnings.Add($"Expected 16 Terrain tiles, found {terrains.Length}.");
        }

        LODGroup[] lodGroups = UnityEngine.Object.FindObjectsOfType<LODGroup>(true)
            .Where(item => item.gameObject.scene == scene)
            .ToArray();
        HashSet<Renderer> lodManagedRenderers = new HashSet<Renderer>();
        foreach (LODGroup group in lodGroups)
        {
            foreach (LOD lod in group.GetLODs())
            {
                foreach (Renderer renderer in lod.renderers)
                {
                    if (renderer != null)
                    {
                        lodManagedRenderers.Add(renderer);
                    }
                }
            }
        }

        long nonLodTriangles = renderers
            .Where(renderer => !lodManagedRenderers.Contains(renderer))
            .Sum(CountRendererTriangles);
        long maximumLodTriangles = nonLodTriangles;
        long minimumLodTriangles = nonLodTriangles;
        foreach (LODGroup group in lodGroups)
        {
            LOD[] lods = group.GetLODs();
            if (lods.Length == 0)
            {
                continue;
            }

            maximumLodTriangles += lods[0].renderers.Sum(CountRendererTriangles);
            minimumLodTriangles += lods[lods.Length - 1].renderers.Sum(CountRendererTriangles);
        }

        GameObject vegetation = FindSceneObject(scene, VegetationName);
        LODGroup samplePalm = vegetation != null
            ? vegetation.GetComponentInChildren<LODGroup>(true)
            : null;
        int[] samplePalmLods = samplePalm != null
            ? samplePalm.GetLODs()
                .Select(lod => (int)lod.renderers.Sum(CountRendererTriangles))
                .ToArray()
            : Array.Empty<int>();

        if (maximumLodTriangles > 12000000)
        {
            warnings.Add(
                $"LOD0 worst case totals {maximumLodTriangles:N0} triangles; " +
                "profile the target platform before adding more decoration.");
        }

        AuditReport report = new AuditReport
        {
            success = errors.Count == 0,
            errorCount = errors.Count,
            warningCount = warnings.Count,
            activeRendererCount = renderers.Length,
            meshFilterCount = filters.Length,
            uniqueMeshCount = uniqueMeshes.Count,
            renderedTriangleInstances = renderedTriangles,
            maximumLod0TriangleInstances = maximumLodTriangles,
            minimumLodTriangleInstances = minimumLodTriangles,
            uniqueMeshTriangles = uniqueMeshes.Sum(mesh => mesh.triangles.LongLength / 3),
            colliderCount = UnityEngine.Object.FindObjectsOfType<Collider>(true)
                .Count(item => item.gameObject.scene == scene &&
                               item.gameObject.activeInHierarchy),
            terrainCount = terrains.Length,
            terrainHeightmapResolution = terrainResolution,
            lodGroupCount = lodGroups.Length,
            samplePalmLodTriangles = samplePalmLods,
            palmCount = FindSceneObject(scene, VegetationName)?.transform.childCount ?? 0,
            buildingCount = FindSceneObject(scene, BuildingsName)?.transform.childCount ?? 0,
            roadVertices = roadMesh != null ? roadMesh.vertexCount : 0,
            roadTriangles = roadMesh != null ? roadMesh.triangles.Length / 3 : 0,
            roadDownwardTriangles = downward,
            roadColliderMatchesMesh = colliderMatches,
            roadMarkingsPresent = markingsPresent,
            flatGrassUsesFirstLevelMaterial = correctGrass,
            oceanMaterial = oceanMaterial != null
                ? AssetDatabase.GetAssetPath(oceanMaterial)
                : string.Empty,
            errors = errors.ToArray(),
            warnings = warnings.ToArray(),
            completedAt = DateTime.Now.ToString("O")
        };

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        File.WriteAllText(
            Path.Combine(projectRoot, ReportPath),
            JsonUtility.ToJson(report, true));
        Debug.Log(
            $"[Level03 Scene Audit] Success={report.success}; " +
            $"errors={report.errorCount}; warnings={report.warningCount}; " +
            $"visible triangles={report.renderedTriangleInstances:N0}.");
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(item => item.scene == scene && item.name == name);
    }

    private static int CountDownwardTriangles(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        int count = 0;
        for (int index = 0; index < triangles.Length; index += 3)
        {
            Vector3 first = vertices[triangles[index]];
            Vector3 second = vertices[triangles[index + 1]];
            Vector3 third = vertices[triangles[index + 2]];
            if (Vector3.Cross(second - first, third - first).y < -0.00001f)
            {
                count++;
            }
        }

        return count;
    }

    private static long CountRendererTriangles(Renderer renderer)
    {
        if (renderer == null)
        {
            return 0;
        }

        Mesh mesh = renderer is SkinnedMeshRenderer skinned
            ? skinned.sharedMesh
            : renderer.GetComponent<MeshFilter>()?.sharedMesh;
        return mesh != null ? mesh.triangles.LongLength / 3 : 0;
    }
}
