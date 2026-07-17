using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ImportedModelPreviewGenerator
{
    private const string ModelRoot = "Assets/Models/Imported";
    private const string PreviewRoot = "Assets/ModelPreviews";
    private const string MarkerPath = "Library/ImportedModelPreviews.generated";

    [MenuItem("Tools/Island Map/Generate Model Previews")]
    public static void Generate()
    {
        string[] modelPaths = AssetDatabase.FindAssets("t:Model", new[] { ModelRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path)
            .ToArray();

        if (modelPaths.Length < 14)
        {
            EditorApplication.delayCall += Generate;
            return;
        }

        Directory.CreateDirectory(PreviewRoot);
        for (int index = 0; index < modelPaths.Length; index++)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPaths[index]);
            if (model == null)
            {
                continue;
            }

            Texture2D preview = RenderModel(model);
            if (preview == null)
            {
                continue;
            }

            string modelFolder = modelPaths[index].Split('/').Reverse().Skip(1).First();
            string outputPath = $"{PreviewRoot}/{modelFolder}.png";
            File.WriteAllBytes(outputPath, preview.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(preview);
        }

        File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("O"));
        AssetDatabase.Refresh();
        Debug.Log($"Generated {modelPaths.Length} imported model previews.");
    }

    private static Texture2D RenderModel(GameObject modelAsset)
    {
        PreviewRenderUtility previewUtility = new PreviewRenderUtility();
        try
        {
            GameObject instance = UnityEngine.Object.Instantiate(modelAsset);
            instance.hideFlags = HideFlags.HideAndDontSave;
            Bounds bounds = CalculateBounds(instance);
            instance.transform.position -= bounds.center;
            previewUtility.AddSingleGO(instance);

            float radius = Mathf.Max(bounds.extents.magnitude, 0.1f);
            previewUtility.cameraFieldOfView = 30f;
            previewUtility.camera.transform.position = new Vector3(radius * 2.6f, radius * 1.8f, radius * 2.6f);
            previewUtility.camera.transform.LookAt(Vector3.zero);
            previewUtility.camera.nearClipPlane = Mathf.Max(0.01f, radius * 0.01f);
            previewUtility.camera.farClipPlane = radius * 12f;
            previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            previewUtility.camera.backgroundColor = new Color(0.18f, 0.20f, 0.23f, 1f);
            previewUtility.lights[0].intensity = 1.25f;
            previewUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            previewUtility.lights[1].intensity = 0.75f;
            previewUtility.ambientColor = new Color(0.45f, 0.45f, 0.45f);

            previewUtility.BeginStaticPreview(new Rect(0f, 0f, 512f, 512f));
            previewUtility.camera.Render();
            return previewUtility.EndStaticPreview();
        }
        finally
        {
            previewUtility.Cleanup();
        }
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds;
    }
}
