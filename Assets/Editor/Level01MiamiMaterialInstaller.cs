using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class Level01MiamiMaterialInstaller
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string ShaderName = "Custom/Level01MiamiSurface";
    private const string MaterialFolder = "Assets/Level01/Materials";
    private const string MeshFolder = "Assets/Level01/Meshes";
    private const string OceanMeshPath = MeshFolder + "/MESH_Level01_SuimonoOcean.asset";
    private const string ShallowWaterMeshPath =
        MeshFolder + "/MESH_Level01_ShallowWaterBand.asset";
    private const string BeachMeshPath = MeshFolder + "/MESH_Level01_ShoreBeach.asset";
    private const string TextureFolder = "Assets/Miami_Beach/Textures/Terrain";
    private const string AsphaltTextureFolder = "Assets/Miami_Beach/Textures/MB_Asphalt";
    private const string MacroTexturePath = "Assets/Miami_Beach/Textures/MB_Noise.tga";
    private const string TransitionMeshPath = MeshFolder + "/MESH_Level01_SurfaceTransitions.asset";
    private const string TransitionShaderName = "Custom/Level01SurfaceTransition";
    private const string TransitionMaterialPath = MaterialFolder + "/MAT_Level01_SurfaceTransition.mat";
    private const string RoadMarkingSourcePath =
        "Assets/Art/Materials/RoadMarkings/MAT_RoadMarking_White.mat";
    private const string SuimonoOceanSourcePath =
        "Assets/SUIMONO - WATER SYSTEM 2/TEXTURES/mat_water_surface.mat";
    private const string SuimonoCalmNormalPath =
        "Assets/SUIMONO - WATER SYSTEM 2/TEXTURES/tex_WaveCalm_normal.psd";
    private const string SuimonoTurbulentNormalPath =
        "Assets/SUIMONO - WATER SYSTEM 2/TEXTURES/tex_WaveTurb_normal.psd";
    private const string SuimonoRollingNormalPath =
        "Assets/SUIMONO - WATER SYSTEM 2/TEXTURES/tex_WaveRoll_normal.psd";
    private const string Level04SkyboxPath =
        "Assets/Level04/Materials/MAT_Level04_CoverSunsetSky.mat";

    [MenuItem("Tools/Island Map/Level01/Apply Miami Beach Surface Materials")]
    public static void ApplyFromMenu()
    {
        ApplyToSceneAsset();
    }

    public static void ApplyFromCommandLine()
    {
        try
        {
            ApplyToSceneAsset();
            Debug.Log("[Level01] Miami surfaces, Level02 ocean, and lighting applied.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void CapturePreviewFromCommandLine()
    {
        try
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(item => item.gameObject.name == "SYS_MainCamera");
            if (camera == null)
            {
                throw new MissingReferenceException("The Level01 main camera is missing.");
            }

            const int width = 1280;
            const int height = 720;
            string previewPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../Logs/Level01SurfacePreview.png"));
            string oceanPreviewPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../Logs/Level01OceanPreview.png"));
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D preview = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            Vector3 previousPosition = camera.transform.position;
            Quaternion previousRotation = camera.transform.rotation;
            float previousFieldOfView = camera.fieldOfView;
            Transform warships = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "ENV_Warships");
            bool warshipsWereActive = warships != null && warships.gameObject.activeSelf;
            try
            {
                DynamicGI.UpdateEnvironment();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                preview.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                preview.Apply();
                File.WriteAllBytes(previewPath, preview.EncodeToPNG());

                if (warships != null)
                {
                    warships.gameObject.SetActive(false);
                }
                camera.transform.position = new Vector3(34f, 8f, -132f);
                camera.transform.rotation = Quaternion.LookRotation(
                    new Vector3(16f, -0.25f, -158f) - camera.transform.position,
                    Vector3.up);
                camera.fieldOfView = 54f;
                camera.Render();
                RenderTexture.active = target;
                preview.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                preview.Apply();
                File.WriteAllBytes(oceanPreviewPath, preview.EncodeToPNG());
            }
            finally
            {
                camera.transform.position = previousPosition;
                camera.transform.rotation = previousRotation;
                camera.fieldOfView = previousFieldOfView;
                if (warships != null)
                {
                    warships.gameObject.SetActive(warshipsWereActive);
                }
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(preview);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }

            Debug.Log(
                "[Level01] Surface previews saved to " + previewPath + " and " + oceanPreviewPath);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void ApplyToSceneAsset()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EnsureFolder("Assets/Level01");
        EnsureFolder(MaterialFolder);
        EnsureFolder(MeshFolder);

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            throw new MissingReferenceException("The Level01 Miami surface shader was not found.");
        }

        Material road = CreateMaterial(
            "MAT_Level01_MiamiRoad",
            AsphaltTextureFolder + "/MB_Asphalt_A.tga",
            AsphaltTextureFolder + "/MB_Asphalt_N.tga",
            5f,
            0.62f,
            0.025f,
            0.16f,
            shader);
        road.SetColor("_Color", new Color(0.64f, 0.67f, 0.72f, 1f));
        road.SetFloat("_NormalStrength", 0.72f);
        road.SetFloat("_TileMeters", 3.2f);
        road.SetFloat("_DetailScale", 3.2f);
        road.SetFloat("_DetailStrength", 0.05f);
        road.SetFloat("_MacroMeters", 44f);
        road.SetFloat("_MacroStrength", 0f);
        road.SetColor("_MacroTintA", Color.white);
        road.SetColor("_MacroTintB", Color.white);
        road.SetFloat("_PatchStrength", 0f);
        road.SetFloat("_WearStrength", 0f);
        road.SetColor("_EdgeTint", new Color(0.72f, 0.7f, 0.66f, 1f));
        road.SetFloat("_EdgeWidth", 0.085f);
        road.SetFloat("_EdgeStrength", 0.08f);
        road.SetFloat("_Wetness", 0f);
        road.SetFloat("_Smoothness", 0.12f);
        road.SetFloat("_SmoothnessVariation", 0.015f);
        EditorUtility.SetDirty(road);
        Material grass = CreateMaterial(
            "MAT_Level01_MiamiGrass",
            TextureFolder + "/MB_Grass_01_A.tga",
            TextureFolder + "/MB_Grass_01_N.tga",
            2.4f,
            0.58f,
            0.045f,
            0.02f,
            shader);
        grass.SetColor("_Color", new Color(0.66f, 0.72f, 0.54f, 1f));
        grass.SetFloat("_NormalStrength", 0.66f);
        grass.SetFloat("_DetailScale", 2.7f);
        grass.SetFloat("_DetailStrength", 0.11f);
        grass.SetFloat("_MacroMeters", 48f);
        grass.SetFloat("_MacroStrength", 0.08f);
        grass.SetColor("_MacroTintA", new Color(0.78f, 0.86f, 0.68f, 1f));
        grass.SetColor("_MacroTintB", new Color(0.94f, 0.9f, 0.72f, 1f));
        grass.SetFloat("_PatchStrength", 0.03f);
        grass.SetFloat("_WearStrength", 0.02f);
        grass.SetFloat("_EdgeStrength", 0f);
        grass.SetFloat("_Wetness", 0f);
        grass.SetFloat("_Smoothness", 0.025f);
        grass.SetFloat("_SmoothnessVariation", 0.015f);
        EditorUtility.SetDirty(grass);
        Material sand = CreateMaterial(
            "MAT_Level01_MiamiSand",
            TextureFolder + "/MB_Sand_02_A.tga",
            TextureFolder + "/MB_Sand_02_N.tga",
            3.8f,
            0.62f,
            0.055f,
            0.045f,
            shader);
        sand.SetColor("_Color", new Color(0.9f, 0.8f, 0.64f, 1f));
        sand.SetFloat("_NormalStrength", 0.7f);
        sand.SetFloat("_DetailScale", 2.6f);
        sand.SetFloat("_DetailStrength", 0.1f);
        sand.SetFloat("_MacroMeters", 52f);
        sand.SetFloat("_MacroStrength", 0f);
        sand.SetColor("_MacroTintA", Color.white);
        sand.SetColor("_MacroTintB", Color.white);
        sand.SetFloat("_PatchStrength", 0f);
        sand.SetFloat("_WearStrength", 0f);
        sand.SetFloat("_EdgeStrength", 0f);
        sand.SetFloat("_Wetness", 1f);
        sand.SetFloat("_WetShoreLevel", 145f);
        sand.SetFloat("_WetEdgeStart", 0.4f);
        sand.SetFloat("_WetEdgeWidth", 13f);
        sand.SetFloat("_WetSmoothness", 0.38f);
        sand.SetFloat("_SmoothnessVariation", 0.01f);
        sand.SetFloat("_WetColorStrength", 0.9f);
        sand.SetColor("_WetTint", new Color(0.48f, 0.62f, 0.68f, 1f));
        EditorUtility.SetDirty(sand);
        Material ocean = CreateSuimonoWaterMaterial("MAT_Level01_SuimonoOcean", 0f);
        Material shallowWater = CreateSuimonoWaterMaterial(
            "MAT_Level01_SuimonoShallowWater",
            0.42f);
        Material roadMarking = CreateRoadMarkingMaterial();
        Mesh oceanMesh = CreateOceanMesh();
        Mesh shallowWaterMesh = CreateShallowWaterMesh();
        Mesh beachMesh = CreateBeachVisualMesh();
        Material transition = CreateTransitionMaterial();

        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        try
        {
            Renderer[] renderers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
                .ToArray();

            int roadCount = AssignByName(renderers, "ENV_Road_", road);
            int grassCount = AssignByName(renderers, "ENV_Ground_Grass", grass);
            int sandCount = ConfigureBeach(renderers, sand, beachMesh);
            int oceanCount = ConfigureOcean(renderers, ocean, oceanMesh);
            int shallowWaterCount = ConfigureShallowWater(
                scene,
                shallowWater,
                shallowWaterMesh);
            int roadMarkingCount = AssignRoadMarkingMaterial(renderers, roadMarking);
            ConfigureRoadTransitions(scene, renderers, transition);
            if (roadCount == 0 || grassCount == 0 || sandCount == 0 || oceanCount == 0
                || shallowWaterCount == 0)
            {
                throw new InvalidOperationException(
                    $"Level01 surface objects missing. Road={roadCount}, Grass={grassCount}, "
                    + $"Sand={sandCount}, Ocean={oceanCount}, Shallow={shallowWaterCount}.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new IOException("Unity could not save " + ScenePath);
            }
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[Level01] Assigned Miami materials and SUIMONO ocean: roads={roadCount}, "
                + $"markings={roadMarkingCount}, grass={grassCount}, sand={sandCount}, "
                + $"ocean={oceanCount}, shallow={shallowWaterCount}.");
        }
        finally
        {
            if (previousScene.IsValid() && previousScene.isLoaded && previousScene.path != ScenePath)
            {
                EditorSceneManager.OpenScene(previousScene.path, OpenSceneMode.Single);
            }
        }
    }

    private static int AssignByName(Renderer[] renderers, string prefix, Material material)
    {
        int count = 0;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.gameObject.name.StartsWith(prefix, StringComparison.Ordinal)
                || renderer.gameObject.name.StartsWith("ENV_RoadMarkings", StringComparison.Ordinal))
            {
                continue;
            }

            renderer.sharedMaterial = material;
            count++;
        }
        return count;
    }

    private static int AssignExact(Renderer[] renderers, string objectName, Material material)
    {
        Renderer renderer = renderers.FirstOrDefault(item => item.gameObject.name == objectName);
        if (renderer == null)
        {
            return 0;
        }

        renderer.sharedMaterial = material;
        return 1;
    }

    private static Material CreateMaterial(
        string materialName,
        string diffusePath,
        string normalPath,
        float tileMeters,
        float normalStrength,
        float macroStrength,
        float smoothness,
        Shader shader)
    {
        string materialPath = MaterialFolder + "/" + materialName + ".mat";
        Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        Texture2D macro = AssetDatabase.LoadAssetAtPath<Texture2D>(MacroTexturePath);
        if (diffuse == null || normal == null || macro == null)
        {
            throw new MissingReferenceException(
                $"Miami Beach surface textures are missing for {materialName}.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader) { name = materialName };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.SetTexture("_MainTex", diffuse);
        material.SetTexture("_NormalMap", normal);
        material.SetTexture("_MacroTex", macro);
        material.SetColor("_Color", Color.white);
        material.SetFloat("_TileMeters", tileMeters);
        material.SetFloat("_NormalStrength", normalStrength);
        material.SetFloat("_MacroMeters", 38f);
        material.SetFloat("_MacroStrength", macroStrength);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateSuimonoWaterMaterial(
        string materialName,
        float shorelineFoam)
    {
        string targetPath = MaterialFolder + "/" + materialName + ".mat";
        Material source = AssetDatabase.LoadAssetAtPath<Material>(SuimonoOceanSourcePath);
        if (source == null || source.shader == null)
        {
            throw new MissingReferenceException("The Level02 SUIMONO ocean material is missing.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
        if (material == null)
        {
            material = new Material(source) { name = materialName };
            AssetDatabase.CreateAsset(material, targetPath);
        }
        else
        {
            material.shader = source.shader;
        }

        Texture2D calmNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(SuimonoCalmNormalPath);
        Texture2D turbulentNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(
            SuimonoTurbulentNormalPath);
        Texture2D rollingNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(
            SuimonoRollingNormalPath);
        if (calmNormal == null || turbulentNormal == null || rollingNormal == null)
        {
            throw new MissingReferenceException("The SUIMONO wave normal textures are missing.");
        }

        material.SetTexture("_NormalTexS", calmNormal);
        material.SetTexture("_NormalTexD", turbulentNormal);
        material.SetTexture("_NormalTexR", rollingNormal);
        material.SetFloat("_overallBrightness", 1.05f);
        material.SetFloat("_specularPower", 0.42f);
        material.SetFloat("_roughness", 0.62f);
        material.SetFloat("_roughness2", 0.76f);
        material.SetFloat("_reflecTerm", 0.025f);
        material.SetFloat("_NormalStrength", 0.48f);
        material.SetFloat("_heightScale", 0.12f);
        material.SetFloat("_lgWaveHeight", 0.055f);
        material.SetFloat("_lgWaveScale", 0.075f);
        material.SetFloat("_turbulenceFactor", 0.08f);
        material.SetFloat("_enableFoam", 0f);
        material.SetVector("_suimono_Dir", new Vector4(-0.42f, 1f, -0.91f, 0f));
        material.SetColor("_depthColor", new Color(0.13f, 0.17f, 0.22f, 1f));
        material.SetColor("_shallowColor", new Color(0.23f, 0.27f, 0.3f, 0.72f));
        material.SetColor("_ReflectionColor", new Color(0.42f, 0.3f, 0.2f, 0.18f));
        material.SetColor("_SpecularColor", new Color(0.72f, 0.52f, 0.3f, 0.18f));
        material.SetColor("_SSSColor", new Color(0.002f, 0.008f, 0.025f, 1f));
        material.SetColor("_BlendColor", new Color(0.14f, 0.17f, 0.2f, 1f));
        material.SetColor("_OverlayColor", new Color(0.1f, 0.12f, 0.14f, 0.38f));
        material.SetFloat("_ShorelineLevel", 145f);
        material.SetFloat("_ShorelineWidth", 13f);
        material.SetFloat("_ShorelineFoam", shorelineFoam);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateTransitionMaterial()
    {
        Shader shader = Shader.Find(TransitionShaderName);
        if (shader == null)
        {
            throw new MissingReferenceException("The Level01 surface transition shader was not found.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(TransitionMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "MAT_Level01_SurfaceTransition" };
            AssetDatabase.CreateAsset(material, TransitionMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        Texture2D grassTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
            TextureFolder + "/MB_Grass_01_A.tga");
        Texture2D noiseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(MacroTexturePath);
        material.SetTexture("_MainTex", grassTexture);
        material.SetTexture("_NoiseTex", noiseTexture);
        material.SetColor("_Color", new Color(0.66f, 0.71f, 0.53f, 1f));
        material.SetFloat("_Opacity", 0.32f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureRoadTransitions(
        Scene scene,
        Renderer[] renderers,
        Material material)
    {
        Transform environment = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(item => item.name == "ENVIRONMENT");
        if (environment == null)
        {
            return;
        }

        Transform transitionRoot = environment.Find("ENV_SurfaceTransitions");
        if (transitionRoot == null)
        {
            GameObject rootObject = new GameObject("ENV_SurfaceTransitions");
            rootObject.transform.SetParent(environment, false);
            transitionRoot = rootObject.transform;
        }

        for (int index = transitionRoot.childCount - 1; index >= 0; index--)
        {
            UnityEngine.Object.DestroyImmediate(transitionRoot.GetChild(index).gameObject);
        }

        Mesh transitionMesh = CreateRoadTransitionMesh(renderers, transitionRoot);
        if (transitionMesh == null)
        {
            return;
        }

        GameObject transitionObject = new GameObject("ENV_RoadGrassTransition");
        transitionObject.transform.SetParent(transitionRoot, false);
        MeshFilter meshFilter = transitionObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = transitionMesh;
        MeshRenderer meshRenderer = transitionObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        EditorUtility.SetDirty(transitionObject);
    }

    private static Mesh CreateRoadTransitionMesh(Renderer[] renderers, Transform relativeTo)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        List<Renderer> roadRenderers = renderers
            .Where(IsTransitionRoadRenderer)
            .ToList();

        foreach (Renderer renderer in roadRenderers)
        {
            Bounds bounds = renderer.bounds;
            float length = Mathf.Max(bounds.size.x, bounds.size.z);
            float width = Mathf.Min(bounds.size.x, bounds.size.z);
            if (length < width * 1.5f || width < 2f)
            {
                continue;
            }

            float y = bounds.max.y + 0.006f;
            if (bounds.size.x >= bounds.size.z)
            {
                AddHorizontalTransition(
                    vertices, uvs, triangles, renderer, roadRenderers, bounds, y, 1f, relativeTo);
                AddHorizontalTransition(
                    vertices, uvs, triangles, renderer, roadRenderers, bounds, y, -1f, relativeTo);
            }
            else
            {
                AddVerticalTransition(
                    vertices, uvs, triangles, renderer, roadRenderers, bounds, y, 1f, relativeTo);
                AddVerticalTransition(
                    vertices, uvs, triangles, renderer, roadRenderers, bounds, y, -1f, relativeTo);
            }
        }

        if (vertices.Count == 0)
        {
            return null;
        }

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(TransitionMeshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "MESH_Level01_SurfaceTransitions" };
            AssetDatabase.CreateAsset(mesh, TransitionMeshPath);
        }
        else
        {
            mesh.Clear();
        }

        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static bool IsTransitionRoadRenderer(Renderer renderer)
    {
        return renderer != null
            && renderer.gameObject.name.StartsWith("ENV_Road_", StringComparison.Ordinal)
            && !renderer.gameObject.name.StartsWith("ENV_RoadMarkings", StringComparison.Ordinal)
            && !renderer.gameObject.name.StartsWith("ENV_RoadGrassTransition", StringComparison.Ordinal);
    }

    private static void AddHorizontalTransition(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        Renderer owner,
        List<Renderer> roadRenderers,
        Bounds bounds,
        float y,
        float direction,
        Transform relativeTo)
    {
        // A small controlled overlap breaks the ruler-straight asphalt edge. Road
        // intersections are clipped separately, so this never crosses another road.
        float inner = bounds.center.z + direction * (bounds.size.z * 0.5f - 0.12f);
        float outer = bounds.center.z + direction * (bounds.size.z * 0.5f + 0.95f);
        float min = bounds.min.x + 0.4f;
        float max = bounds.max.x - 0.4f;
        int segments = Mathf.Max(1, Mathf.CeilToInt((max - min) / 0.75f));
        for (int index = 0; index < segments; index++)
        {
            float u0 = (float)index / segments;
            float u1 = (float)(index + 1) / segments;
            float x0 = Mathf.Lerp(min, max, u0);
            float x1 = Mathf.Lerp(min, max, u1);
            if (IsCoveredByAnotherRoad((x0 + x1) * 0.5f, (inner + outer) * 0.5f, owner, roadRenderers))
            {
                continue;
            }

            int start = vertices.Count;
            vertices.Add(relativeTo.InverseTransformPoint(new Vector3(x0, y, inner)));
            vertices.Add(relativeTo.InverseTransformPoint(new Vector3(x0, y, outer)));
            vertices.Add(relativeTo.InverseTransformPoint(new Vector3(x1, y, outer)));
            vertices.Add(relativeTo.InverseTransformPoint(new Vector3(x1, y, inner)));
            uvs.Add(new Vector2(u0, 0f));
            uvs.Add(new Vector2(u0, 1f));
            uvs.Add(new Vector2(u1, 1f));
            uvs.Add(new Vector2(u1, 0f));
            AddQuadTriangles(triangles, start);
        }
    }

    private static void AddVerticalTransition(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        Renderer owner,
        List<Renderer> roadRenderers,
        Bounds bounds,
        float y,
        float direction,
        Transform relativeTo)
    {
        float inner = bounds.center.x + direction * (bounds.size.x * 0.5f - 0.12f);
        float outer = bounds.center.x + direction * (bounds.size.x * 0.5f + 0.95f);
        float min = bounds.min.z + 0.4f;
        float max = bounds.max.z - 0.4f;
        int segments = Mathf.Max(1, Mathf.CeilToInt((max - min) / 0.75f));
        for (int index = 0; index < segments; index++)
        {
            float u0 = (float)index / segments;
            float u1 = (float)(index + 1) / segments;
            float z0 = Mathf.Lerp(min, max, u0);
            float z1 = Mathf.Lerp(min, max, u1);
            if (IsCoveredByAnotherRoad((inner + outer) * 0.5f, (z0 + z1) * 0.5f, owner, roadRenderers))
            {
                continue;
            }

            int start = vertices.Count;
            vertices.Add(relativeTo.InverseTransformPoint(new Vector3(inner, y, z0)));
            vertices.Add(relativeTo.InverseTransformPoint(new Vector3(inner, y, z1)));
            vertices.Add(relativeTo.InverseTransformPoint(new Vector3(outer, y, z1)));
            vertices.Add(relativeTo.InverseTransformPoint(new Vector3(outer, y, z0)));
            uvs.Add(new Vector2(u0, 0f));
            uvs.Add(new Vector2(u1, 0f));
            uvs.Add(new Vector2(u1, 1f));
            uvs.Add(new Vector2(u0, 1f));
            AddQuadTriangles(triangles, start);
        }
    }

    private static bool IsCoveredByAnotherRoad(
        float x,
        float z,
        Renderer owner,
        List<Renderer> roadRenderers)
    {
        const float margin = 0.2f;
        foreach (Renderer renderer in roadRenderers)
        {
            if (renderer == null || renderer == owner)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            if (x >= bounds.min.x - margin && x <= bounds.max.x + margin
                && z >= bounds.min.z - margin && z <= bounds.max.z + margin)
            {
                return true;
            }
        }

        return false;
    }

    private static void AddQuadTriangles(List<int> triangles, int start)
    {
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    private static Material CreateRoadMarkingMaterial()
    {
        const string materialName = "MAT_Level01_RoadMarking_White";
        string targetPath = MaterialFolder + "/" + materialName + ".mat";
        Material source = AssetDatabase.LoadAssetAtPath<Material>(RoadMarkingSourcePath);
        if (source == null)
        {
            throw new MissingReferenceException("The road marking source material is missing.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
        if (material == null)
        {
            material = new Material(source) { name = materialName };
            AssetDatabase.CreateAsset(material, targetPath);
        }
        else
        {
            material.shader = source.shader;
            material.CopyPropertiesFromMaterial(source);
        }

        material.SetColor("_Color", new Color(0.62f, 0.64f, 0.61f, 1f));
        material.SetFloat("_Glossiness", 0.01f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static int AssignRoadMarkingMaterial(Renderer[] renderers, Material material)
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(RoadMarkingSourcePath);
        int count = 0;
        foreach (Renderer renderer in renderers)
        {
            Material[] sharedMaterials = renderer.sharedMaterials;
            bool changed = false;
            for (int index = 0; index < sharedMaterials.Length; index++)
            {
                if (sharedMaterials[index] != source)
                {
                    continue;
                }

                sharedMaterials[index] = material;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = sharedMaterials;
                count++;
            }
        }
        return count;
    }

    private static Mesh CreateOceanMesh()
    {
        return CreateWaterPlaneMesh(
            OceanMeshPath,
            "MESH_Level01_SuimonoOcean",
            128,
            1600f);
    }

    private static Mesh CreateShallowWaterMesh()
    {
        return CreateWaterPlaneMesh(
            ShallowWaterMeshPath,
            "MESH_Level01_ShallowWaterBand",
            96,
            360f);
    }

    private static Mesh CreateWaterPlaneMesh(
        string assetPath,
        string meshName,
        int segments,
        float worldSize)
    {
        int verticesPerSide = segments + 1;
        Vector3[] vertices = new Vector3[verticesPerSide * verticesPerSide];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector4[] tangents = new Vector4[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * segments * 6];

        for (int z = 0; z <= segments; z++)
        {
            float v = (float)z / segments;
            for (int x = 0; x <= segments; x++)
            {
                float u = (float)x / segments;
                int index = z * verticesPerSide + x;
                vertices[index] = new Vector3(
                    (u - 0.5f) * worldSize,
                    0f,
                    (v - 0.5f) * worldSize);
                normals[index] = Vector3.up;
                tangents[index] = new Vector4(1f, 0f, 0f, 1f);
                uvs[index] = new Vector2(u, v);
            }
        }

        int triangleIndex = 0;
        for (int z = 0; z < segments; z++)
        {
            for (int x = 0; x < segments; x++)
            {
                int bottomLeft = z * verticesPerSide + x;
                int topLeft = bottomLeft + verticesPerSide;
                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = bottomLeft + 1;
                triangles[triangleIndex++] = bottomLeft + 1;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topLeft + 1;
            }
        }

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = meshName };
            AssetDatabase.CreateAsset(mesh, assetPath);
        }
        else
        {
            mesh.Clear();
        }

        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.tangents = tangents;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Mesh CreateBeachVisualMesh()
    {
        const int segments = 128;
        const float beachSize = 300f;
        const float shoreLevel = 145f;
        const float slopeHalfWidth = 6f;
        const float innerHeight = 0.5f;
        const float outerHeight = -2.5f;
        int verticesPerSide = segments + 1;
        Vector3[] vertices = new Vector3[verticesPerSide * verticesPerSide];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * segments * 6];

        for (int z = 0; z <= segments; z++)
        {
            float v = (float)z / segments;
            for (int x = 0; x <= segments; x++)
            {
                float u = (float)x / segments;
                float worldX = (u - 0.5f) * beachSize;
                float worldZ = (v - 0.5f) * beachSize;
                float maxCoordinate = Mathf.Max(Mathf.Abs(worldX), Mathf.Abs(worldZ));
                float alongEdge = Mathf.Abs(worldX) > Mathf.Abs(worldZ) ? worldZ : worldX;
                float irregularity = Mathf.Sin(alongEdge * 0.075f) * 2.6f
                    + Mathf.Sin(alongEdge * 0.031f + 1.7f) * 1.8f;
                float localShore = shoreLevel + irregularity;
                float slope = Mathf.InverseLerp(
                    localShore - slopeHalfWidth,
                    localShore + slopeHalfWidth,
                    maxCoordinate);
                slope = slope * slope * (3f - 2f * slope);
                int index = z * verticesPerSide + x;
                vertices[index] = new Vector3(
                    u - 0.5f,
                    Mathf.Lerp(innerHeight, outerHeight, slope),
                    v - 0.5f);
                uvs[index] = new Vector2(u, v);
            }
        }

        int triangleIndex = 0;
        for (int z = 0; z < segments; z++)
        {
            for (int x = 0; x < segments; x++)
            {
                int bottomLeft = z * verticesPerSide + x;
                int topLeft = bottomLeft + verticesPerSide;
                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = bottomLeft + 1;
                triangles[triangleIndex++] = bottomLeft + 1;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topLeft + 1;
            }
        }

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BeachMeshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "MESH_Level01_ShoreBeach" };
            AssetDatabase.CreateAsset(mesh, BeachMeshPath);
        }
        else
        {
            mesh.Clear();
        }

        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static int ConfigureBeach(Renderer[] renderers, Material material, Mesh mesh)
    {
        Renderer renderer = renderers.FirstOrDefault(
            item => item.gameObject.name == "ENV_Ground_Beach");
        if (renderer == null)
        {
            return 0;
        }

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            throw new MissingComponentException("ENV_Ground_Beach has no MeshFilter.");
        }

        renderer.sharedMaterial = material;
        meshFilter.sharedMesh = mesh;
        return 1;
    }

    private static int ConfigureOcean(Renderer[] renderers, Material material, Mesh mesh)
    {
        Renderer renderer = renderers.FirstOrDefault(
            item => item.gameObject.name == "ENV_Ground_Ocean");
        if (renderer == null)
        {
            return 0;
        }

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            throw new MissingComponentException("ENV_Ground_Ocean has no MeshFilter.");
        }

        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        meshFilter.sharedMesh = mesh;
        renderer.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        renderer.transform.localRotation = Quaternion.identity;
        renderer.transform.localScale = Vector3.one;
        renderer.gameObject.isStatic = false;
        return 1;
    }

    private static int ConfigureShallowWater(Scene scene, Material material, Mesh mesh)
    {
        Transform ocean = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(item => item.name == "ENV_Ground_Ocean");
        if (ocean == null)
        {
            return 0;
        }

        Transform shallow = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(item => item.name == "ENV_ShallowWater_Visual");
        if (shallow == null)
        {
            GameObject shallowObject = new GameObject("ENV_ShallowWater_Visual");
            shallow = shallowObject.transform;
            shallow.SetParent(ocean.parent, false);
            shallowObject.AddComponent<MeshFilter>();
            shallowObject.AddComponent<MeshRenderer>();
        }

        if (shallow.GetComponent<Collider>() != null)
        {
            throw new InvalidOperationException(
                "ENV_ShallowWater_Visual must remain visual-only and cannot have a collider.");
        }

        MeshFilter meshFilter = shallow.GetComponent<MeshFilter>();
        MeshRenderer renderer = shallow.GetComponent<MeshRenderer>();
        if (meshFilter == null || renderer == null)
        {
            throw new MissingComponentException(
                "ENV_ShallowWater_Visual requires a MeshFilter and MeshRenderer.");
        }

        shallow.localPosition = new Vector3(0f, -0.285f, 0f);
        shallow.localRotation = Quaternion.identity;
        shallow.localScale = Vector3.one;
        meshFilter.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        shallow.gameObject.isStatic = false;
        return 1;
    }

    private static void ConfigureLighting(Scene scene)
    {
        Material skybox = AssetDatabase.LoadAssetAtPath<Material>(Level04SkyboxPath);
        Light sun = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Light>(true))
            .FirstOrDefault(light => light.gameObject.name == "ENV_DirectionalLight");
        if (skybox == null || sun == null)
        {
            throw new MissingReferenceException("Level01 skybox or directional light is missing.");
        }

        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.34f, 0.38f, 0.47f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.46f, 0.42f, 0.4f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.2f, 0.22f, 0.27f, 1f);
        RenderSettings.ambientIntensity = 1.18f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.defaultReflectionResolution = 128;
        RenderSettings.reflectionIntensity = 0.88f;
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.37f, 0.39f, 0.44f, 1f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 280f;
        RenderSettings.fogEndDistance = 1050f;

        sun.color = new Color(1f, 0.76f, 0.58f, 1f);
        sun.intensity = 1.25f;
        sun.useColorTemperature = false;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.54f;
        sun.shadowBias = 0.035f;
        sun.shadowNormalBias = 0.22f;
        sun.shadowAngle = 1.8f;
        Vector4 skySun = skybox.GetVector("_SunDirection");
        Vector3 lightTravelDirection = new Vector3(
            -skySun.x,
            -0.55f,
            -skySun.z).normalized;
        sun.transform.rotation = Quaternion.LookRotation(lightTravelDirection, Vector3.up);
        RenderSettings.sun = sun;
        EditorUtility.SetDirty(sun);
        EditorUtility.SetDirty(sun.transform);
        RemoveLegacyPlayerFillLight(scene);
        DynamicGI.UpdateEnvironment();
    }

    private static void RemoveLegacyPlayerFillLight(Scene scene)
    {
        Transform player = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(item => item.name == "PLAYER_Car");
        if (player == null)
        {
            throw new MissingReferenceException("Level01 player car is missing.");
        }

        Transform existing = player.Find("FX_PlayerCarFillLight");
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
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
