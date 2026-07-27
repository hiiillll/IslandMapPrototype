using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Level03BuildRequestRunner
{
    private const string RequestPath = "Assets/Level03/BUILD_LEVEL03.request";

    static Level03BuildRequestRunner()
    {
        EditorApplication.delayCall += TryRun;
    }

    private static void TryRun()
    {
        if (!File.Exists(RequestPath))
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryRun;
            return;
        }

        AssetDatabase.DeleteAsset(RequestPath);
        Level03TerrainRoadBuilder.Build();
    }
}

public static class Level03TerrainRoadBuilder
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string HeightReferencePath = "Assets/Level03/References/Level03_HeightReference.png";
    private const string RoadPlanPath = "Assets/Level03/References/Level03_RoadPlan_Active.png";
    private const string GeneratedFolder = "Assets/Level03/GeneratedTerrainRoad";
    private const string LandMeshPath = GeneratedFolder + "/MESH_Level03_Land.asset";
    private const string RoadMeshPath = GeneratedFolder + "/MESH_Level03_Roads.asset";
    private const string GrassMaterialPath = GeneratedFolder + "/MAT_Level03_Grass.mat";
    private const string RoadMaterialPath = GeneratedFolder + "/MAT_Level03_Road.mat";
    private const string OceanMaterialPath = GeneratedFolder + "/MAT_Level03_Ocean.mat";
    private const string PreviewFolder = "Assets/Level03/Preview";
    private const string PreviewPath = PreviewFolder + "/Level03_TerrainRoadPreview.png";
    private const string SourceGrassMaterialPath = "Assets/Art/Materials/Grass.mat";
    private const string SourceRoadMaterialPath = "Assets/Art/Materials/Road.mat";
    private const string SourceOceanMaterialPath = "Assets/Art/Materials/Ocean.mat";
    private const string SkyMaterialPath = "Assets/Art/Sky/MAT_Sky_TropicalNoon.mat";

    private const int PreviewLayer = 31;
    private const int LandColumns = 512;
    private const int RoadColumns = 1024;
    private const float OceanSize = 4000f;
    private const float LandWidth = 4000f;
    private const float FlatLandHeight = 0.35f;
    private const float RoadHeight = 0.62f;
    private const float MaximumMountainHeight = 360f;
    private const float LandThreshold = 0.075f;
    private const float MountainStartLuminance = 0.28f;
    private const float MountainPeakLuminance = 0.94f;
    private const float RoadThreshold = 0.55f;

    [MenuItem("Tools/Island Map/Build Level 03 Terrain And Roads")]
    public static void Build()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Build;
            return;
        }

        EnsureFolder("Assets/Level03");
        EnsureFolder(GeneratedFolder);
        EnsureFolder(PreviewFolder);

        Texture2D heightReference = LoadTextureFromFile(HeightReferencePath);
        Texture2D roadPlan = LoadTextureFromFile(RoadPlanPath);
        if (heightReference == null || roadPlan == null)
        {
            Debug.LogError("Level03 build failed: height or road reference image is missing.");
            DestroyTexture(heightReference);
            DestroyTexture(roadPlan);
            return;
        }

        float landDepth = LandWidth * heightReference.height / heightReference.width;
        Mesh landMesh = BuildLandMesh(heightReference, roadPlan, landDepth);
        Mesh roadMesh = BuildRoadMesh(roadPlan, landDepth);
        SaveMeshAsset(landMesh, LandMeshPath);
        SaveMeshAsset(roadMesh, RoadMeshPath);

        Material grassMaterial = CreateMaterialCopy(
            SourceGrassMaterialPath,
            GrassMaterialPath,
            new Vector2(1f, 1f));
        Material roadMaterial = CreateMaterialCopy(
            SourceRoadMaterialPath,
            RoadMaterialPath,
            new Vector2(1f, 1f));
        Material oceanMaterial = CreateMaterialCopy(
            SourceOceanMaterialPath,
            OceanMaterialPath,
            new Vector2(12f, 12f));

        Scene previousScene = SceneManager.GetActiveScene();
        bool rebuildingActiveLevel03 = previousScene.IsValid()
            && previousScene.path == ScenePath;
        bool hasSavedPreviousScene = previousScene.IsValid()
            && previousScene.isLoaded
            && !string.IsNullOrEmpty(previousScene.path)
            && !rebuildingActiveLevel03;
        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            hasSavedPreviousScene ? NewSceneMode.Additive : NewSceneMode.Single);
        EditorSceneManager.SetActiveScene(scene);

        GameObject environment = CreateRoot("ENVIRONMENT_Level03", scene);
        GameObject land = CreateMeshObject(
            "ENV_Level03_FlatIslands_And_MainMountain",
            landMesh,
            grassMaterial,
            environment.transform,
            true);
        GameObject roads = CreateMeshObject(
            "ENV_Level03_RoadNetwork_FromReference",
            roadMesh,
            roadMaterial,
            environment.transform,
            true);
        GameObject ocean = CreateOcean(oceanMaterial, environment.transform);

        GameObject systems = CreateRoot("SYSTEMS_Level03_Preview", scene);
        Light directionalLight = CreateLighting(systems.transform);
        Camera overviewCamera = CreateOverviewCamera(systems.transform);
        ConfigureRenderSettings();

        SetLayerRecursively(environment, PreviewLayer);
        SetLayerRecursively(systems, PreviewLayer);
        directionalLight.cullingMask = 1 << PreviewLayer;
        overviewCamera.cullingMask = 1 << PreviewLayer;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        SavePreview(overviewCamera);

        directionalLight.cullingMask = ~0;
        overviewCamera.cullingMask = ~0;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeGameObject = roads;
        if (hasSavedPreviousScene && previousScene.IsValid() && previousScene.isLoaded)
        {
            EditorSceneManager.SetActiveScene(previousScene);
            EditorSceneManager.CloseScene(scene, true);
        }

        DestroyTexture(heightReference);
        DestroyTexture(roadPlan);
        Debug.Log(
            "Built Level03 terrain and roads from the synchronized composite-map inputs. "
            + "Only the largest island mountain is elevated; all other land and roads are flat.");
    }

    private static Mesh BuildLandMesh(Texture2D heightReference, Texture2D roadPlan, float landDepth)
    {
        int rows = Mathf.RoundToInt(LandColumns * (float)heightReference.height / heightReference.width);
        int vertexColumns = LandColumns + 1;
        int vertexRows = rows + 1;
        int vertexCount = vertexColumns * vertexRows;
        bool[] landMask = new bool[vertexCount];
        float[] luminance = new float[vertexCount];
        bool[] roadMask = new bool[vertexCount];

        for (int row = 0; row < vertexRows; row++)
        {
            float v = (float)row / rows;
            for (int column = 0; column < vertexColumns; column++)
            {
                float u = (float)column / LandColumns;
                int index = row * vertexColumns + column;
                float value = SampleLuminance(heightReference, u, 1f - v);
                luminance[index] = value;
                landMask[index] = value > LandThreshold;
                roadMask[index] = IsRoadNear(roadPlan, u, 1f - v, 3);
            }
        }

        bool[] mainIslandMask = FindLargestConnectedComponent(landMask, vertexColumns, vertexRows);
        float[] smoothedLuminance = SmoothLuminance(
            luminance,
            mainIslandMask,
            vertexColumns,
            vertexRows,
            4);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        for (int row = 0; row < vertexRows; row++)
        {
            float v = (float)row / rows;
            float z = landDepth * (0.5f - v);
            for (int column = 0; column < vertexColumns; column++)
            {
                float u = (float)column / LandColumns;
                int index = row * vertexColumns + column;
                float height = FlatLandHeight;
                if (mainIslandMask[index] && !roadMask[index])
                {
                    float mountain = Mathf.InverseLerp(
                        MountainStartLuminance,
                        MountainPeakLuminance,
                        smoothedLuminance[index]);
                    mountain = Mathf.SmoothStep(0f, 1f, mountain);
                    height += mountain * MaximumMountainHeight;
                }

                vertices[index] = new Vector3(
                    LandWidth * (u - 0.5f),
                    height,
                    z);
                uvs[index] = new Vector2(vertices[index].x / 42f, vertices[index].z / 42f);
            }
        }

        List<int> triangles = new List<int>(LandColumns * rows * 4);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < LandColumns; column++)
            {
                int topLeft = row * vertexColumns + column;
                int topRight = topLeft + 1;
                int bottomLeft = topLeft + vertexColumns;
                int bottomRight = bottomLeft + 1;
                if (landMask[bottomLeft] && landMask[topLeft] && landMask[topRight])
                {
                    triangles.Add(bottomLeft);
                    triangles.Add(topLeft);
                    triangles.Add(topRight);
                }
                if (landMask[bottomLeft] && landMask[topRight] && landMask[bottomRight])
                {
                    triangles.Add(bottomLeft);
                    triangles.Add(topRight);
                    triangles.Add(bottomRight);
                }
            }
        }

        Mesh mesh = new Mesh
        {
            name = "MESH_Level03_Land",
            indexFormat = IndexFormat.UInt32,
            vertices = vertices,
            uv = uvs,
            triangles = triangles.ToArray()
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildRoadMesh(Texture2D roadPlan, float landDepth)
    {
        int rows = Mathf.RoundToInt(RoadColumns * (float)roadPlan.height / roadPlan.width);
        List<Vector3> vertices = new List<Vector3>(rows * 40);
        List<Vector2> uvs = new List<Vector2>(rows * 40);
        List<int> triangles = new List<int>(rows * 60);

        for (int row = 0; row < rows; row++)
        {
            float sampleV = 1f - (row + 0.5f) / rows;
            int column = 0;
            while (column < RoadColumns)
            {
                float sampleU = (column + 0.5f) / RoadColumns;
                if (SampleLuminance(roadPlan, sampleU, sampleV) <= RoadThreshold)
                {
                    column++;
                    continue;
                }

                int runStart = column;
                do
                {
                    column++;
                    sampleU = (column + 0.5f) / RoadColumns;
                }
                while (column < RoadColumns
                    && SampleLuminance(roadPlan, sampleU, sampleV) > RoadThreshold);

                int runEnd = column;
                float xLeft = -LandWidth * 0.5f + LandWidth * runStart / RoadColumns;
                float xRight = -LandWidth * 0.5f + LandWidth * runEnd / RoadColumns;
                float zTop = landDepth * (0.5f - (float)row / rows);
                float zBottom = landDepth * (0.5f - (float)(row + 1) / rows);
                AddRoadQuad(vertices, uvs, triangles, xLeft, xRight, zBottom, zTop);
            }
        }

        Mesh mesh = new Mesh
        {
            name = "MESH_Level03_RoadNetwork",
            indexFormat = IndexFormat.UInt32
        };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddRoadQuad(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        float xLeft,
        float xRight,
        float zBottom,
        float zTop)
    {
        int firstVertex = vertices.Count;
        vertices.Add(new Vector3(xLeft, RoadHeight, zBottom));
        vertices.Add(new Vector3(xLeft, RoadHeight, zTop));
        vertices.Add(new Vector3(xRight, RoadHeight, zTop));
        vertices.Add(new Vector3(xRight, RoadHeight, zBottom));
        uvs.Add(new Vector2(xLeft / 24f, zBottom / 24f));
        uvs.Add(new Vector2(xLeft / 24f, zTop / 24f));
        uvs.Add(new Vector2(xRight / 24f, zTop / 24f));
        uvs.Add(new Vector2(xRight / 24f, zBottom / 24f));
        triangles.Add(firstVertex);
        triangles.Add(firstVertex + 1);
        triangles.Add(firstVertex + 2);
        triangles.Add(firstVertex);
        triangles.Add(firstVertex + 2);
        triangles.Add(firstVertex + 3);
    }

    private static bool[] FindLargestConnectedComponent(bool[] source, int width, int height)
    {
        int[] labels = new int[source.Length];
        for (int index = 0; index < labels.Length; index++)
        {
            labels[index] = -1;
        }

        Queue<int> queue = new Queue<int>();
        int largestLabel = -1;
        int largestCount = 0;
        int nextLabel = 0;
        for (int start = 0; start < source.Length; start++)
        {
            if (!source[start] || labels[start] >= 0)
            {
                continue;
            }

            int count = 0;
            labels[start] = nextLabel;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                count++;
                int x = index % width;
                int y = index / width;
                TryQueue(index - 1, x > 0, nextLabel, source, labels, queue);
                TryQueue(index + 1, x + 1 < width, nextLabel, source, labels, queue);
                TryQueue(index - width, y > 0, nextLabel, source, labels, queue);
                TryQueue(index + width, y + 1 < height, nextLabel, source, labels, queue);
            }

            if (count > largestCount)
            {
                largestCount = count;
                largestLabel = nextLabel;
            }
            nextLabel++;
        }

        bool[] result = new bool[source.Length];
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = labels[index] == largestLabel;
        }
        return result;
    }

    private static float[] SmoothLuminance(
        float[] source,
        bool[] mask,
        int width,
        int height,
        int passes)
    {
        float[] current = (float[])source.Clone();
        float[] next = new float[source.Length];
        for (int pass = 0; pass < passes; pass++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (!mask[index])
                    {
                        next[index] = current[index];
                        continue;
                    }

                    float total = 0f;
                    float totalWeight = 0f;
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        int sampleY = y + offsetY;
                        if (sampleY < 0 || sampleY >= height)
                        {
                            continue;
                        }
                        for (int offsetX = -1; offsetX <= 1; offsetX++)
                        {
                            int sampleX = x + offsetX;
                            if (sampleX < 0 || sampleX >= width)
                            {
                                continue;
                            }

                            int sampleIndex = sampleY * width + sampleX;
                            if (!mask[sampleIndex])
                            {
                                continue;
                            }

                            float weight = offsetX == 0 && offsetY == 0 ? 4f
                                : offsetX == 0 || offsetY == 0 ? 2f
                                : 1f;
                            total += current[sampleIndex] * weight;
                            totalWeight += weight;
                        }
                    }
                    next[index] = totalWeight > 0f ? total / totalWeight : current[index];
                }
            }

            float[] swap = current;
            current = next;
            next = swap;
        }
        return current;
    }

    private static void TryQueue(
        int index,
        bool isInBounds,
        int label,
        bool[] source,
        int[] labels,
        Queue<int> queue)
    {
        if (!isInBounds || !source[index] || labels[index] >= 0)
        {
            return;
        }

        labels[index] = label;
        queue.Enqueue(index);
    }

    private static bool IsRoadNear(Texture2D roadPlan, float u, float v, int pixelRadius)
    {
        float offsetU = 1f / roadPlan.width;
        float offsetV = 1f / roadPlan.height;
        for (int y = -pixelRadius; y <= pixelRadius; y++)
        {
            for (int x = -pixelRadius; x <= pixelRadius; x++)
            {
                if (SampleLuminance(
                        roadPlan,
                        Mathf.Clamp01(u + x * offsetU),
                        Mathf.Clamp01(v + y * offsetV)) > RoadThreshold)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static float SampleLuminance(Texture2D texture, float u, float v)
    {
        Color color = texture.GetPixelBilinear(Mathf.Clamp01(u), Mathf.Clamp01(v));
        return color.grayscale;
    }

    private static Texture2D LoadTextureFromFile(string assetPath)
    {
        if (!File.Exists(assetPath))
        {
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false, true)
        {
            name = Path.GetFileNameWithoutExtension(assetPath),
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        return texture.LoadImage(File.ReadAllBytes(assetPath), false) ? texture : null;
    }

    private static void DestroyTexture(Texture2D texture)
    {
        if (texture != null)
        {
            Object.DestroyImmediate(texture);
        }
    }

    private static void SaveMeshAsset(Mesh mesh, string assetPath)
    {
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(mesh, assetPath);
    }

    private static Material CreateMaterialCopy(
        string sourcePath,
        string targetPath,
        Vector2 textureScale)
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
        Material material = source != null
            ? new Material(source)
            : new Material(Shader.Find("Standard"));
        material.name = Path.GetFileNameWithoutExtension(targetPath);
        material.mainTextureScale = textureScale;
        AssetDatabase.DeleteAsset(targetPath);
        AssetDatabase.CreateAsset(material, targetPath);
        return material;
    }

    private static GameObject CreateRoot(string name, Scene scene)
    {
        GameObject root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root;
    }

    private static GameObject CreateMeshObject(
        string name,
        Mesh mesh,
        Material material,
        Transform parent,
        bool addCollider)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        gameObject.isStatic = true;
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        if (addCollider)
        {
            gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        return gameObject;
    }

    private static GameObject CreateOcean(Material oceanMaterial, Transform parent)
    {
        GameObject ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ocean.name = "ENV_Level03_Ocean_4000x4000";
        ocean.transform.SetParent(parent, false);
        ocean.transform.position = Vector3.zero;
        ocean.transform.localScale = new Vector3(OceanSize / 10f, 1f, OceanSize / 10f);
        ocean.GetComponent<MeshRenderer>().sharedMaterial = oceanMaterial;
        Object.DestroyImmediate(ocean.GetComponent<Collider>());
        ocean.isStatic = true;
        return ocean;
    }

    private static Light CreateLighting(Transform parent)
    {
        GameObject lightObject = new GameObject("SYS_Level03_DirectionalLight");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.intensity = 1.08f;
        light.shadows = LightShadows.Soft;
        return light;
    }

    private static Camera CreateOverviewCamera(Transform parent)
    {
        GameObject cameraObject = new GameObject("SYS_Level03_OverviewCamera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.position = new Vector3(0f, 2200f, 0f);
        cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = OceanSize * 0.52f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 5000f;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.allowHDR = true;
        return camera;
    }

    private static void ConfigureRenderSettings()
    {
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.68f, 0.76f, 0.84f);
        RenderSettings.ambientEquatorColor = new Color(0.48f, 0.54f, 0.52f);
        RenderSettings.ambientGroundColor = new Color(0.26f, 0.29f, 0.30f);
        RenderSettings.fog = false;
    }

    private static void SavePreview(Camera camera)
    {
        const int previewWidth = 1374;
        const int previewHeight = 1145;
        RenderTexture renderTexture = new RenderTexture(
            previewWidth,
            previewHeight,
            24,
            RenderTextureFormat.ARGB32);
        Texture2D preview = new Texture2D(
            previewWidth,
            previewHeight,
            TextureFormat.RGB24,
            false);
        RenderTexture previousActive = RenderTexture.active;
        camera.targetTexture = renderTexture;
        camera.Render();
        RenderTexture.active = renderTexture;
        preview.ReadPixels(new Rect(0f, 0f, previewWidth, previewHeight), 0, 0);
        preview.Apply();
        File.WriteAllBytes(PreviewPath, preview.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = previousActive;
        Object.DestroyImmediate(preview);
        Object.DestroyImmediate(renderTexture);
        AssetDatabase.ImportAsset(PreviewPath, ImportAssetOptions.ForceUpdate);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            transform.gameObject.layer = layer;
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folder = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
