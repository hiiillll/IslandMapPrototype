using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class ExplosionParticleMaterialGenerator : IPreprocessBuildWithReport
{
    private const string MaterialDirectory = "Assets/Resources/Effects";
    private const string MaterialPath = MaterialDirectory + "/ExplosionParticle.mat";

    public int callbackOrder => 0;

    [InitializeOnLoadMethod]
    private static void CreateMaterialAfterEditorLoad()
    {
        EditorApplication.delayCall += EnsureMaterialAsset;
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        EnsureMaterialAsset();
    }

    private static void EnsureMaterialAsset()
    {
        Directory.CreateDirectory(MaterialDirectory);
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
        {
            throw new BuildFailedException("The Particles/Standard Unlit shader is required for the explosion particle material.");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = "ExplosionParticle"
            };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
    }
}
