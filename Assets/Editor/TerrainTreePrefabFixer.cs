using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TerrainTreePrefabFixer
{
    private const string Level03ScenePath = "Assets/Scenes/Level03.unity";
    private const string SourceModelPath =
        "Assets/Models/Imported/Model_12/81777c3b447a7477dabdbaf804c9550c.obj";
    private const string VegetationFolder = "Assets/Art/Prefabs/Vegetation";
    private const string TerrainPrefabPath =
        VegetationFolder + "/PF_PalmTree_Terrain.prefab";
    private const float PalmTreeScale = 50f;

    static TerrainTreePrefabFixer()
    {
        EditorApplication.delayCall += RepairLevel03PalmTreePrototype;
    }

    [MenuItem("Tools/Island Map/Fix Level 03 Palm Tree Brush")]
    public static void RepairLevel03PalmTreePrototype()
    {
        if (EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += RepairLevel03PalmTreePrototype;
            return;
        }

        GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModelPath);
        if (sourceModel == null)
        {
            Debug.LogError($"Palm tree source model is missing: {SourceModelPath}");
            return;
        }

        GameObject terrainPrefab = LoadOrCreateTerrainPrefab(sourceModel);
        if (terrainPrefab == null)
        {
            return;
        }

        Terrain[] terrains = Resources.FindObjectsOfTypeAll<Terrain>()
            .Where(IsLevel03SceneTerrain)
            .ToArray();
        int changedTerrainCount = 0;
        int replacedPrototypeCount = 0;
        int addedPrototypeCount = 0;

        foreach (Terrain terrain in terrains)
        {
            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null)
            {
                continue;
            }

            TreePrototype[] currentPrototypes = terrainData.treePrototypes;
            List<TreePrototype> repairedPrototypes =
                new List<TreePrototype>(currentPrototypes.Length + 1);
            bool containsTerrainPrefab = false;
            bool changed = false;

            foreach (TreePrototype currentPrototype in currentPrototypes)
            {
                if (ReferencesTerrainPrefab(currentPrototype, terrainPrefab))
                {
                    containsTerrainPrefab = true;
                    repairedPrototypes.Add(currentPrototype);
                    continue;
                }

                if (ShouldReplacePrototype(currentPrototype))
                {
                    repairedPrototypes.Add(new TreePrototype
                    {
                        prefab = terrainPrefab,
                        bendFactor = currentPrototype != null
                            ? currentPrototype.bendFactor
                            : 0f
                    });
                    containsTerrainPrefab = true;
                    changed = true;
                    replacedPrototypeCount++;
                    continue;
                }

                repairedPrototypes.Add(currentPrototype);
            }

            if (!containsTerrainPrefab)
            {
                repairedPrototypes.Add(new TreePrototype
                {
                    prefab = terrainPrefab,
                    bendFactor = 0f
                });
                changed = true;
                addedPrototypeCount++;
            }

            if (!changed)
            {
                continue;
            }

            terrainData.treePrototypes = repairedPrototypes.ToArray();
            EditorUtility.SetDirty(terrainData);
            terrain.Flush();
            changedTerrainCount++;
        }

        if (changedTerrainCount > 0)
        {
            AssetDatabase.SaveAssets();
        }

        Debug.Log(
            $"Level 03 palm tree brush repaired. Prefab: {TerrainPrefabPath}; "
            + $"terrains changed: {changedTerrainCount}; "
            + $"prototypes replaced: {replacedPrototypeCount}; "
            + $"prototypes added: {addedPrototypeCount}.");
    }

    private static GameObject LoadOrCreateTerrainPrefab(GameObject sourceModel)
    {
        GameObject existingPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(TerrainPrefabPath);
        if (HasValidTerrainRenderers(existingPrefab))
        {
            return existingPrefab;
        }

        EnsureVegetationFolder();
        GameObject root = new GameObject("PF_PalmTree_Terrain");
        try
        {
            GameObject visual =
                PrefabUtility.InstantiatePrefab(sourceModel) as GameObject;
            if (visual == null)
            {
                visual = Object.Instantiate(sourceModel);
            }

            visual.name = "Visual_PalmTree";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * PalmTreeScale;

            Renderer[] renderers = visual
                .GetComponentsInChildren<MeshRenderer>(true)
                .Cast<Renderer>()
                .ToArray();
            if (renderers.Length == 0)
            {
                Debug.LogError(
                    $"Palm tree source model contains no MeshRenderer: {SourceModelPath}");
                return null;
            }

            LODGroup lodGroup = root.AddComponent<LODGroup>();
            lodGroup.SetLODs(new[]
            {
                new LOD(0.01f, renderers)
            });
            lodGroup.RecalculateBounds();

            GameObject savedPrefab =
                PrefabUtility.SaveAsPrefabAsset(root, TerrainPrefabPath);
            if (!HasValidTerrainRenderers(savedPrefab))
            {
                Debug.LogError(
                    $"Created palm tree prefab is not valid for Terrain: {TerrainPrefabPath}");
                return null;
            }

            return savedPrefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static bool ShouldReplacePrototype(TreePrototype prototype)
    {
        if (prototype == null || prototype.prefab == null)
        {
            return true;
        }

        string prototypePath = AssetDatabase.GetAssetPath(prototype.prefab);
        if (prototypePath == SourceModelPath)
        {
            return true;
        }

        return !HasValidTerrainRenderers(prototype.prefab);
    }

    private static bool ReferencesTerrainPrefab(
        TreePrototype prototype,
        GameObject terrainPrefab)
    {
        return prototype != null && prototype.prefab == terrainPrefab;
    }

    private static bool HasValidTerrainRenderers(GameObject prefab)
    {
        if (prefab == null)
        {
            return false;
        }

        LODGroup lodGroup = prefab.GetComponent<LODGroup>();
        if (lodGroup != null)
        {
            return lodGroup.GetLODs()
                .Any(lod => lod.renderers != null
                    && lod.renderers.Any(renderer => renderer is MeshRenderer));
        }

        return prefab.GetComponentsInChildren<MeshRenderer>(true).Length > 0;
    }

    private static bool IsLevel03SceneTerrain(Terrain terrain)
    {
        if (terrain == null)
        {
            return false;
        }

        Scene scene = terrain.gameObject.scene;
        return scene.IsValid()
            && scene.isLoaded
            && scene.path == Level03ScenePath;
    }

    private static void EnsureVegetationFolder()
    {
        if (!AssetDatabase.IsValidFolder(VegetationFolder))
        {
            AssetDatabase.CreateFolder(
                "Assets/Art/Prefabs",
                "Vegetation");
        }
    }
}
