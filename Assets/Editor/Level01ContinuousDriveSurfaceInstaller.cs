using System;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Level01ContinuousDriveSurfaceInstaller
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string MaterialPath = "Assets/PhysicsMaterials/PM_DriveSurface.physicMaterial";
    private const string SurfaceName = "COL_DriveSurface";
    private const float SurfaceTopY = 0.045f;
    private const float SurfaceThickness = 0.01f;
    private const float SurfaceSize = 300f;

    [MenuItem("Tools/Island Map/Build Continuous Drive Surface")]
    public static void ApplyFromMenu()
    {
        Apply();
    }

    public static void ApplyFromCommandLine()
    {
        try
        {
            Apply();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void VerifyFromCommandLine()
    {
        try
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int triggerZones = 0;
            int legacySolidColliders = 0;
            BoxCollider continuousSurface = null;
            foreach (Collider collider in UnityEngine.Object.FindObjectsOfType<Collider>(true))
            {
                if (collider.gameObject.scene != scene || !collider.enabled)
                {
                    continue;
                }

                if (collider.name == SurfaceName)
                {
                    continuousSurface = collider as BoxCollider;
                }
                else if (IsLegacyDriveSurface(collider.transform))
                {
                    if (collider.isTrigger)
                    {
                        triggerZones++;
                    }
                    else
                    {
                        legacySolidColliders++;
                    }
                }
            }

            if (continuousSurface == null || !continuousSurface.enabled || continuousSurface.isTrigger)
            {
                throw new InvalidOperationException("COL_DriveSurface is missing or is not a solid BoxCollider.");
            }
            if (!NavMeshEnemyCarChaser.IsDrivingSurface(continuousSurface))
            {
                throw new InvalidOperationException("Enemy collision logic does not recognize COL_DriveSurface as drivable ground.");
            }
            if (legacySolidColliders != 0)
            {
                throw new InvalidOperationException(
                    $"{legacySolidColliders} legacy drive-surface colliders are still solid.");
            }
            if (triggerZones == 0)
            {
                throw new InvalidOperationException("No drive-surface trigger zones were found.");
            }
            if (continuousSurface.sharedMaterial == null
                || continuousSurface.sharedMaterial.bounciness > 0f
                || continuousSurface.sharedMaterial.frictionCombine != PhysicMaterialCombine.Minimum)
            {
                throw new InvalidOperationException("COL_DriveSurface does not use the zero-bounce minimum-friction material.");
            }

            Debug.Log($"[Level01 Continuous Drive Surface] Verification passed: one solid continuous surface, "
                + $"{triggerZones} trigger zones, and zero legacy solid drive surfaces.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Apply()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform collisionRoot = FindInScene(scene, "COLLISION")?.transform;
        if (collisionRoot == null)
        {
            throw new InvalidOperationException("COLLISION root was not found in " + ScenePath);
        }

        PhysicMaterial material = GetOrCreateDriveMaterial();
        GameObject surfaceObject = FindInScene(scene, SurfaceName);
        if (surfaceObject == null)
        {
            surfaceObject = new GameObject(SurfaceName);
            SceneManager.MoveGameObjectToScene(surfaceObject, scene);
        }

        surfaceObject.transform.SetParent(collisionRoot, false);
        surfaceObject.transform.localPosition = Vector3.zero;
        surfaceObject.transform.localRotation = Quaternion.identity;
        surfaceObject.transform.localScale = Vector3.one;
        surfaceObject.isStatic = true;

        BoxCollider continuousSurface = surfaceObject.GetComponent<BoxCollider>();
        if (continuousSurface == null)
        {
            continuousSurface = surfaceObject.AddComponent<BoxCollider>();
        }
        continuousSurface.enabled = true;
        continuousSurface.isTrigger = false;
        continuousSurface.center = new Vector3(0f, SurfaceTopY - SurfaceThickness * 0.5f, 0f);
        continuousSurface.size = new Vector3(SurfaceSize, SurfaceThickness, SurfaceSize);
        continuousSurface.sharedMaterial = material;

        int convertedZones = 0;
        foreach (Collider collider in UnityEngine.Object.FindObjectsOfType<Collider>(true))
        {
            if (collider.gameObject.scene != scene || collider == continuousSurface
                || !collider.enabled || !IsLegacyDriveSurface(collider.transform))
            {
                continue;
            }

            collider.isTrigger = true;
            NavMeshModifier modifier = collider.GetComponent<NavMeshModifier>();
            if (modifier != null)
            {
                modifier.ignoreFromBuild = true;
            }
            EditorUtility.SetDirty(collider);
            convertedZones++;
        }

        EditorUtility.SetDirty(surfaceObject);
        EditorUtility.SetDirty(continuousSurface);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Level01 Continuous Drive Surface] Created one {SurfaceSize:F0}m continuous collider "
            + $"at Y={SurfaceTopY:F3} and converted {convertedZones} legacy surface colliders to trigger zones.");
    }

    private static PhysicMaterial GetOrCreateDriveMaterial()
    {
        PhysicMaterial material = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(MaterialPath);
        if (material == null)
        {
            material = new PhysicMaterial("PM_DriveSurface");
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.dynamicFriction = 0f;
        material.staticFriction = 0f;
        material.bounciness = 0f;
        material.frictionCombine = PhysicMaterialCombine.Minimum;
        material.bounceCombine = PhysicMaterialCombine.Minimum;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static bool IsLegacyDriveSurface(Transform transform)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            string objectName = current.name;
            if (objectName == "COL_Beach" || objectName == "COL_Grass" || objectName.StartsWith("COL_Road")
                || objectName.StartsWith("MB_Coastal_Sidewalk_")
                || objectName.StartsWith("MB_Sidewalk_")
                || objectName.StartsWith("MB_Road_") && !objectName.StartsWith("MB_Road_Barrier_")
                || objectName.StartsWith("MB_Bike_Path_")
                || objectName == "MB_Promenade")
            {
                return true;
            }
        }

        return false;
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (gameObject.scene == scene && gameObject.name == objectName)
            {
                return gameObject;
            }
        }
        return null;
    }
}
