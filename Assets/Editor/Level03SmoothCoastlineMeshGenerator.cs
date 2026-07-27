using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Generates a continuous, non-colliding beach ribbon over Level03's Terrain-hole
/// coastline. The ribbon visually hides finite Terrain-hole grid steps while the
/// Terrain remains authoritative for height editing and collision.
/// </summary>
[InitializeOnLoad]
public static class Level03SmoothCoastlineMeshGenerator
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string EnvironmentName = "ENVIRONMENT_Level03";
    private const string ConvertedRootName = "ENV_Level03_ConvertedTerrain";
    private const string CoastlineObjectName = "ENV_Level03_SmoothBeachCoastline";
    private const string OutputFolder = "Assets/Level03/SmoothCoastline";
    private const string MeshAssetPath = OutputFolder + "/MESH_Level03_SmoothBeachCoastline.asset";
    private const string BeachMaterialPath = "Assets/Art/Materials/Beach.mat";
    private const string RequestAssetPath =
        "Assets/Editor/Level03SmoothCoastlineMeshGenerator.request";
    private const string ReportPath = "Library/CodexLevel03SmoothCoastlineReport.json";

    private const int TileCount = 4;
    private const int TileHoleResolution = 1024;
    private const int GlobalResolution = TileCount * TileHoleResolution;
    private const float WorldSize = 4000f;
    private const float WorldMinimum = -2000f;
    private const float CellSize = WorldSize / GlobalResolution;
    private const float CurveSampleSpacing = 4f;
    private const int ChaikinPasses = 3;
    private const float MinimumLoopPerimeter = 60f;
    private const float InnerWidth = 10f;
    private const float InnerNearWidth = 1f;
    private const float OuterCoverWidth = 1.75f;
    private const float OuterWidth = 7f;
    private const float SurfaceOffset = 0.04f;
    private const float OceanEdgeHeight = 0.025f;
    private const float TextureWorldSize = 42f;

    [Serializable]
    private sealed class GenerationReport
    {
        public bool success;
        public string message;
        public int terrainCount;
        public int rawBoundaryLoops;
        public int generatedLoops;
        public int vertexCount;
        public int triangleCount;
        public float totalCoastlineLength;
        public float innerWidth;
        public float outerWidth;
        public bool hasCollider;
        public string meshAssetPath;
        public string materialAssetPath;
        public string completedAt;
    }

    private readonly struct GridPoint : IEquatable<GridPoint>
    {
        public readonly int x;
        public readonly int y;

        public GridPoint(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public bool Equals(GridPoint other)
        {
            return x == other.x && y == other.y;
        }

        public override bool Equals(object obj)
        {
            return obj is GridPoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (x * 397) ^ y;
            }
        }

        public static bool operator ==(GridPoint left, GridPoint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GridPoint left, GridPoint right)
        {
            return !left.Equals(right);
        }
    }

    private sealed class BoundaryEdge
    {
        public GridPoint start;
        public GridPoint end;
        public bool used;
    }

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    private static string RequestFilePath => Path.Combine(ProjectRoot, RequestAssetPath);
    private static string ReportFilePath => Path.Combine(ProjectRoot, ReportPath);

    static Level03SmoothCoastlineMeshGenerator()
    {
        if (File.Exists(RequestFilePath))
        {
            EditorApplication.delayCall += GenerateOnce;
        }
    }

    [MenuItem("Tools/Island Map/Level03/Generate Smooth Beach Coastline Mesh")]
    public static void GenerateFromMenu()
    {
        try
        {
            GenerationReport report = Generate();
            WriteReport(report);
            EditorUtility.DisplayDialog("Level03 Smooth Beach Coastline", report.message, "OK");
        }
        catch (Exception exception)
        {
            WriteFailureReport(exception);
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Level03 Smooth Beach Coastline",
                exception.Message,
                "OK");
        }
    }

    private static void GenerateOnce()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += GenerateOnce;
            return;
        }

        try
        {
            GenerationReport report = Generate();
            WriteReport(report);
            Debug.Log("[Level03 Smooth Beach Coastline] " + report.message);
        }
        catch (Exception exception)
        {
            WriteFailureReport(exception);
            Debug.LogException(exception);
        }
        finally
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

            AssetDatabase.Refresh();
        }
    }

    private static GenerationReport Generate()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            throw new InvalidOperationException("Level03 must be the active scene.");
        }

        GameObject environment = FindSceneObject(scene, EnvironmentName);
        GameObject terrainRoot = FindSceneObject(scene, ConvertedRootName);
        if (FindSceneObjectOrNull(scene, CoastlineObjectName) != null)
        {
            throw new InvalidOperationException(
                $"'{CoastlineObjectName}' already exists. Remove it before generating again.");
        }

        if (AssetDatabase.IsValidFolder(OutputFolder))
        {
            throw new InvalidOperationException(
                $"'{OutputFolder}' already exists. Remove it before generating again.");
        }

        Material beachMaterial = AssetDatabase.LoadAssetAtPath<Material>(BeachMaterialPath);
        if (beachMaterial == null)
        {
            throw new FileNotFoundException("The beach material was not found.", BeachMaterialPath);
        }

        Terrain[,] terrainGrid = BuildTerrainGrid(terrainRoot);
        Terrain[] terrains = terrainGrid.Cast<Terrain>().ToArray();
        bool[,] landMask = ReadGlobalHoles(terrainGrid);

        EditorUtility.DisplayProgressBar(
            "Level03 Smooth Beach Coastline",
            "Extracting continuous coastline contours",
            0.15f);

        List<BoundaryEdge> edges = BuildBoundaryEdges(landMask);
        List<List<GridPoint>> gridLoops = TraceBoundaryLoops(edges);
        List<List<Vector2>> smoothLoops = new List<List<Vector2>>();

        foreach (List<GridPoint> gridLoop in gridLoops)
        {
            List<Vector2> worldLoop = GridLoopToWorld(gridLoop);
            float perimeter = CalculatePerimeter(worldLoop);
            if (perimeter < MinimumLoopPerimeter)
            {
                continue;
            }

            List<Vector2> sampled = ResampleClosedLoop(worldLoop, CurveSampleSpacing);
            if (sampled.Count < 3)
            {
                continue;
            }

            for (int pass = 0; pass < ChaikinPasses; pass++)
            {
                sampled = ChaikinClosed(sampled);
            }

            smoothLoops.Add(sampled);
        }

        if (smoothLoops.Count == 0)
        {
            throw new InvalidOperationException("No usable coastline loops were extracted.");
        }

        EditorUtility.DisplayProgressBar(
            "Level03 Smooth Beach Coastline",
            "Generating Terrain-conforming beach ribbon",
            0.55f);

        Mesh mesh = BuildCoastlineMesh(
            smoothLoops,
            landMask,
            terrainGrid,
            environment.transform,
            out float totalLength);
        mesh.name = "MESH_Level03_SmoothBeachCoastline";

        CreateFolder(OutputFolder);
        AssetDatabase.CreateAsset(mesh, MeshAssetPath);

        GameObject coastline = new GameObject(CoastlineObjectName);
        Undo.RegisterCreatedObjectUndo(coastline, "Generate Smooth Beach Coastline Mesh");
        coastline.transform.SetParent(environment.transform, false);
        coastline.layer = terrains[0].gameObject.layer;

        MeshFilter filter = coastline.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = coastline.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = beachMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        GameObjectUtility.SetStaticEditorFlags(
            coastline,
            StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.ReflectionProbeStatic);

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene))
        {
            UnityEngine.Object.DestroyImmediate(coastline);
            AssetDatabase.DeleteAsset(MeshAssetPath);
            AssetDatabase.DeleteAsset(OutputFolder);
            throw new IOException("Unity could not save the Level03 scene.");
        }

        EditorUtility.ClearProgressBar();
        return new GenerationReport
        {
            success = true,
            message =
                "Generated a continuous Terrain-conforming beach coastline mesh. " +
                "It has no collider and visually covers the finite Terrain-hole grid edge.",
            terrainCount = terrains.Length,
            rawBoundaryLoops = gridLoops.Count,
            generatedLoops = smoothLoops.Count,
            vertexCount = mesh.vertexCount,
            triangleCount = mesh.triangles.Length / 3,
            totalCoastlineLength = totalLength,
            innerWidth = InnerWidth,
            outerWidth = OuterWidth,
            hasCollider = coastline.GetComponent<Collider>() != null,
            meshAssetPath = MeshAssetPath,
            materialAssetPath = BeachMaterialPath,
            completedAt = DateTime.Now.ToString("O")
        };
    }

    private static Terrain[,] BuildTerrainGrid(GameObject root)
    {
        Terrain[] terrains = root.GetComponentsInChildren<Terrain>(true);
        if (terrains.Length != TileCount * TileCount)
        {
            throw new InvalidOperationException(
                $"Expected {TileCount * TileCount} Terrain tiles, but found {terrains.Length}.");
        }

        Terrain[,] grid = new Terrain[TileCount, TileCount];
        foreach (Terrain terrain in terrains)
        {
            if (terrain.terrainData.holesResolution != TileHoleResolution)
            {
                throw new InvalidOperationException(
                    $"Terrain '{terrain.name}' is not at 1024 holes resolution.");
            }

            int column = Mathf.RoundToInt((terrain.transform.position.x + 2000f) / 1000f);
            int row = Mathf.RoundToInt((terrain.transform.position.z + 2000f) / 1000f);
            if (column < 0 || column >= TileCount || row < 0 || row >= TileCount)
            {
                throw new InvalidOperationException(
                    $"Terrain '{terrain.name}' is outside the expected 4x4 grid.");
            }

            grid[column, row] = terrain;
        }

        return grid;
    }

    private static bool[,] ReadGlobalHoles(Terrain[,] grid)
    {
        bool[,] result = new bool[GlobalResolution, GlobalResolution];
        for (int row = 0; row < TileCount; row++)
        {
            for (int column = 0; column < TileCount; column++)
            {
                bool[,] tile = grid[column, row].terrainData.GetHoles(
                    0,
                    0,
                    TileHoleResolution,
                    TileHoleResolution);
                int startX = column * TileHoleResolution;
                int startY = row * TileHoleResolution;
                for (int y = 0; y < TileHoleResolution; y++)
                {
                    for (int x = 0; x < TileHoleResolution; x++)
                    {
                        result[startY + y, startX + x] = tile[y, x];
                    }
                }
            }
        }

        return result;
    }

    private static List<BoundaryEdge> BuildBoundaryEdges(bool[,] land)
    {
        List<BoundaryEdge> edges = new List<BoundaryEdge>(100000);
        for (int y = 0; y < GlobalResolution; y++)
        {
            for (int x = 0; x < GlobalResolution; x++)
            {
                if (!land[y, x])
                {
                    continue;
                }

                if (y == 0 || !land[y - 1, x])
                {
                    edges.Add(NewEdge(x, y, x + 1, y));
                }

                if (x == GlobalResolution - 1 || !land[y, x + 1])
                {
                    edges.Add(NewEdge(x + 1, y, x + 1, y + 1));
                }

                if (y == GlobalResolution - 1 || !land[y + 1, x])
                {
                    edges.Add(NewEdge(x + 1, y + 1, x, y + 1));
                }

                if (x == 0 || !land[y, x - 1])
                {
                    edges.Add(NewEdge(x, y + 1, x, y));
                }
            }
        }

        return edges;
    }

    private static BoundaryEdge NewEdge(int startX, int startY, int endX, int endY)
    {
        return new BoundaryEdge
        {
            start = new GridPoint(startX, startY),
            end = new GridPoint(endX, endY)
        };
    }

    private static List<List<GridPoint>> TraceBoundaryLoops(List<BoundaryEdge> edges)
    {
        Dictionary<GridPoint, List<int>> outgoing = new Dictionary<GridPoint, List<int>>();
        for (int index = 0; index < edges.Count; index++)
        {
            GridPoint start = edges[index].start;
            if (!outgoing.TryGetValue(start, out List<int> list))
            {
                list = new List<int>(2);
                outgoing.Add(start, list);
            }

            list.Add(index);
        }

        List<List<GridPoint>> loops = new List<List<GridPoint>>();
        for (int edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
        {
            if (edges[edgeIndex].used)
            {
                continue;
            }

            List<GridPoint> loop = new List<GridPoint>();
            BoundaryEdge firstEdge = edges[edgeIndex];
            GridPoint start = firstEdge.start;
            GridPoint previous = firstEdge.start;
            int currentEdgeIndex = edgeIndex;
            int guard = 0;

            while (guard++ <= edges.Count)
            {
                BoundaryEdge currentEdge = edges[currentEdgeIndex];
                if (currentEdge.used)
                {
                    break;
                }

                currentEdge.used = true;
                if (loop.Count == 0)
                {
                    loop.Add(currentEdge.start);
                }

                loop.Add(currentEdge.end);
                GridPoint current = currentEdge.end;
                if (current == start)
                {
                    loop.RemoveAt(loop.Count - 1);
                    if (loop.Count >= 3)
                    {
                        loops.Add(loop);
                    }

                    break;
                }

                if (!outgoing.TryGetValue(current, out List<int> candidates))
                {
                    break;
                }

                int next = SelectNextEdge(edges, candidates, previous, current);
                if (next < 0)
                {
                    break;
                }

                previous = currentEdge.start;
                currentEdgeIndex = next;
            }
        }

        return loops;
    }

    private static int SelectNextEdge(
        IReadOnlyList<BoundaryEdge> edges,
        IEnumerable<int> candidates,
        GridPoint previous,
        GridPoint current)
    {
        int selected = -1;
        float selectedAngle = float.PositiveInfinity;
        Vector2 incoming = new Vector2(current.x - previous.x, current.y - previous.y);

        foreach (int candidateIndex in candidates)
        {
            BoundaryEdge candidate = edges[candidateIndex];
            if (candidate.used)
            {
                continue;
            }

            Vector2 outgoing = new Vector2(
                candidate.end.x - current.x,
                candidate.end.y - current.y);
            float cross = incoming.x * outgoing.y - incoming.y * outgoing.x;
            float dot = Vector2.Dot(incoming, outgoing);
            float angle = Mathf.Atan2(cross, dot);
            if (angle < selectedAngle)
            {
                selectedAngle = angle;
                selected = candidateIndex;
            }
        }

        return selected;
    }

    private static List<Vector2> GridLoopToWorld(IEnumerable<GridPoint> loop)
    {
        return loop
            .Select(point => new Vector2(
                WorldMinimum + point.x * CellSize,
                WorldMinimum + point.y * CellSize))
            .ToList();
    }

    private static float CalculatePerimeter(IReadOnlyList<Vector2> loop)
    {
        float length = 0f;
        for (int index = 0; index < loop.Count; index++)
        {
            length += Vector2.Distance(loop[index], loop[(index + 1) % loop.Count]);
        }

        return length;
    }

    private static List<Vector2> ResampleClosedLoop(
        IReadOnlyList<Vector2> loop,
        float spacing)
    {
        List<Vector2> sampled = new List<Vector2> { loop[0] };
        float carried = 0f;

        for (int index = 0; index < loop.Count; index++)
        {
            Vector2 start = loop[index];
            Vector2 end = loop[(index + 1) % loop.Count];
            float segmentLength = Vector2.Distance(start, end);
            if (segmentLength <= 0.0001f)
            {
                continue;
            }

            Vector2 direction = (end - start) / segmentLength;
            float traveled = spacing - carried;
            while (traveled < segmentLength)
            {
                sampled.Add(start + direction * traveled);
                traveled += spacing;
            }

            carried = Mathf.Max(0f, segmentLength - (traveled - spacing));
        }

        if (sampled.Count > 1 &&
            Vector2.Distance(sampled[0], sampled[sampled.Count - 1]) < spacing * 0.35f)
        {
            sampled.RemoveAt(sampled.Count - 1);
        }

        return sampled;
    }

    private static List<Vector2> ChaikinClosed(IReadOnlyList<Vector2> source)
    {
        List<Vector2> result = new List<Vector2>(source.Count * 2);
        for (int index = 0; index < source.Count; index++)
        {
            Vector2 current = source[index];
            Vector2 next = source[(index + 1) % source.Count];
            result.Add(Vector2.Lerp(current, next, 0.25f));
            result.Add(Vector2.Lerp(current, next, 0.75f));
        }

        return result;
    }

    private static Mesh BuildCoastlineMesh(
        IReadOnlyList<List<Vector2>> loops,
        bool[,] landMask,
        Terrain[,] terrainGrid,
        Transform meshParent,
        out float totalLength)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        totalLength = 0f;

        foreach (List<Vector2> loop in loops)
        {
            int baseVertex = vertices.Count;
            int count = loop.Count;
            float[] cumulative = new float[count + 1];
            for (int index = 0; index < count; index++)
            {
                cumulative[index + 1] =
                    cumulative[index] +
                    Vector2.Distance(loop[index], loop[(index + 1) % count]);
            }

            totalLength += cumulative[count];

            for (int index = 0; index <= count; index++)
            {
                int wrapped = index % count;
                Vector2 point = loop[wrapped];
                Vector2 previous = loop[(wrapped - 1 + count) % count];
                Vector2 next = loop[(wrapped + 1) % count];
                Vector2 tangent = (next - previous).normalized;
                Vector2 landNormal = new Vector2(-tangent.y, tangent.x);

                if (!IsLandAtWorld(landMask, point + landNormal * 4f) &&
                    IsLandAtWorld(landMask, point - landNormal * 4f))
                {
                    landNormal = -landNormal;
                }

                Vector2 inner = point + landNormal * InnerWidth;
                Vector2 innerNear = point + landNormal * InnerNearWidth;
                Vector2 outerCover = point - landNormal * OuterCoverWidth;
                Vector2 outer = point - landNormal * OuterWidth;

                float innerHeight =
                    SampleTerrainHeight(terrainGrid, inner) + SurfaceOffset;
                float nearHeight =
                    SampleTerrainHeight(terrainGrid, innerNear) + SurfaceOffset;
                float coverHeight =
                    SampleTerrainHeight(terrainGrid, point) + SurfaceOffset;

                AddVertex(vertices, uvs, meshParent, inner, innerHeight, cumulative[index], 0f);
                AddVertex(vertices, uvs, meshParent, innerNear, nearHeight, cumulative[index], 0.42f);
                AddVertex(vertices, uvs, meshParent, outerCover, coverHeight, cumulative[index], 0.68f);
                AddVertex(vertices, uvs, meshParent, outer, OceanEdgeHeight, cumulative[index], 1f);
            }

            for (int segment = 0; segment < count; segment++)
            {
                int rowStart = baseVertex + segment * 4;
                int nextRowStart = rowStart + 4;
                for (int band = 0; band < 3; band++)
                {
                    int bottomLeft = rowStart + band;
                    int bottomRight = rowStart + band + 1;
                    int topLeft = nextRowStart + band;
                    int topRight = nextRowStart + band + 1;

                    triangles.Add(bottomLeft);
                    triangles.Add(topLeft);
                    triangles.Add(topRight);
                    triangles.Add(bottomLeft);
                    triangles.Add(topRight);
                    triangles.Add(bottomRight);
                }
            }
        }

        Mesh mesh = new Mesh
        {
            indexFormat = IndexFormat.UInt32
        };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddVertex(
        ICollection<Vector3> vertices,
        ICollection<Vector2> uvs,
        Transform parent,
        Vector2 point,
        float worldHeight,
        float cumulativeLength,
        float across)
    {
        Vector3 world = new Vector3(point.x, worldHeight, point.y);
        vertices.Add(parent.InverseTransformPoint(world));
        uvs.Add(new Vector2(cumulativeLength / TextureWorldSize, across));
    }

    private static bool IsLandAtWorld(bool[,] landMask, Vector2 world)
    {
        int x = Mathf.FloorToInt((world.x - WorldMinimum) / CellSize);
        int y = Mathf.FloorToInt((world.y - WorldMinimum) / CellSize);
        if (x < 0 || x >= GlobalResolution || y < 0 || y >= GlobalResolution)
        {
            return false;
        }

        return landMask[y, x];
    }

    private static float SampleTerrainHeight(Terrain[,] grid, Vector2 world)
    {
        int column = Mathf.Clamp(Mathf.FloorToInt((world.x + 2000f) / 1000f), 0, TileCount - 1);
        int row = Mathf.Clamp(Mathf.FloorToInt((world.y + 2000f) / 1000f), 0, TileCount - 1);
        Terrain terrain = grid[column, row];
        return terrain.SampleHeight(new Vector3(world.x, 0f, world.y)) +
               terrain.transform.position.y;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        GameObject result = FindSceneObjectOrNull(scene, objectName);
        if (result == null)
        {
            throw new InvalidOperationException(
                $"Could not find '{objectName}' in the active Level03 scene.");
        }

        return result;
    }

    private static GameObject FindSceneObjectOrNull(Scene scene, string objectName)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(candidate =>
                candidate.scene == scene &&
                candidate.name == objectName);
    }

    private static void CreateFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }

            current = next;
        }
    }

    private static void WriteFailureReport(Exception exception)
    {
        EditorUtility.ClearProgressBar();
        WriteReport(new GenerationReport
        {
            success = false,
            message = exception.GetType().Name + ": " + exception.Message,
            completedAt = DateTime.Now.ToString("O")
        });
    }

    private static void WriteReport(GenerationReport report)
    {
        File.WriteAllText(ReportFilePath, JsonUtility.ToJson(report, true));
    }
}
