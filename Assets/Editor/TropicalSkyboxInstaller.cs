using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TropicalSkyboxInstaller
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string SkyMaterialPath = "Assets/Art/Sky/MAT_Sky_TropicalNoon.mat";
    private const string MarkerPath = "Library/TropicalSkyboxInstalled.v1";

    static TropicalSkyboxInstaller()
    {
        if (!File.Exists(MarkerPath))
        {
            EditorApplication.delayCall += TryInstall;
        }
    }

    private static void TryInstall()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryInstall;
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject cameraObject = GameObject.Find("SYS_MainCamera");
        if (!scene.IsValid() || scene.path != ScenePath || cameraObject == null)
        {
            EditorApplication.delayCall += TryInstall;
            return;
        }

        EnsureFolder("Assets/Art");
        EnsureFolder("Assets/Art/Sky");
        Material skyMaterial = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        if (skyMaterial == null)
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            skyMaterial = new Material(shader) { name = "MAT_Sky_TropicalNoon" };
            AssetDatabase.CreateAsset(skyMaterial, SkyMaterialPath);
        }

        skyMaterial.SetColor("_SkyTint", new Color(0.28f, 0.68f, 1f, 1f));
        skyMaterial.SetColor("_GroundColor", new Color(0.62f, 0.86f, 0.96f, 1f));
        skyMaterial.SetFloat("_AtmosphereThickness", 0.62f);
        skyMaterial.SetFloat("_Exposure", 1.15f);
        skyMaterial.SetFloat("_SunSize", 0.035f);
        skyMaterial.SetFloat("_SunSizeConvergence", 5f);
        EditorUtility.SetDirty(skyMaterial);

        RenderSettings.skybox = skyMaterial;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.48f, 0.74f, 0.96f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.72f, 0.86f, 0.92f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.36f, 0.42f, 0.38f, 1f);
        DynamicGI.UpdateEnvironment();

        Camera cameraComponent = cameraObject.GetComponent<Camera>();
        cameraComponent.clearFlags = CameraClearFlags.Skybox;
        cameraComponent.farClipPlane = 1000f;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("O"));
        Selection.activeObject = skyMaterial;
        Debug.Log("Installed tropical noon procedural skybox and enabled camera skybox rendering.");
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
