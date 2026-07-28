using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Level03VehicleCameraSynchronizer
{
    private const string ReferenceScenePath = "Assets/Scenes/IslandMap.unity";
    private const string TargetScenePath = "Assets/Scenes/Level03.unity";
    private const string RequestPath =
        "Assets/Editor/Level03VehicleCameraSynchronizer.request";
    private const string ReportPath =
        "Library/CodexLevel03VehicleCameraSyncReport.json";

    [Serializable]
    private sealed class SyncReport
    {
        public bool success;
        public string referenceScene;
        public string targetScene;
        public string modelAsset;
        public Vector3 referenceCarScale;
        public Vector3 targetCarScale;
        public Vector3 referenceVisualSize;
        public Vector3 targetVisualSize;
        public float referenceSpeedMultiplier;
        public float targetSpeedMultiplier;
        public bool referencePreserveInitialHeading;
        public bool targetPreserveInitialHeading;
        public float referenceCameraHeight;
        public float targetCameraHeight;
        public float referenceOrthographicSize;
        public float targetOrthographicSize;
        public Vector3 referenceThirdPersonCameraOffset;
        public Vector3 targetThirdPersonCameraOffset;
        public Vector3 referenceThirdPersonLookOffset;
        public Vector3 targetThirdPersonLookOffset;
        public bool cameraTargetsLevel03Player;
    }

    static Level03VehicleCameraSynchronizer()
    {
        if (File.Exists(RequestPath))
        {
            EditorApplication.delayCall += SynchronizeFromRequest;
        }
    }

    [MenuItem("Tools/Island Map/Level03/Match Vehicle And Camera To IslandMap")]
    public static void SynchronizeFromMenu()
    {
        Synchronize();
    }

    private static void SynchronizeFromRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += SynchronizeFromRequest;
            return;
        }

        try
        {
            Synchronize();
        }
        finally
        {
            AssetDatabase.DeleteAsset(RequestPath);
        }
    }

    private static void Synchronize()
    {
        Scene targetScene = SceneManager.GetActiveScene();
        if (!targetScene.IsValid() || targetScene.path != TargetScenePath)
        {
            throw new InvalidOperationException(
                "Level03 must be the active scene before synchronizing its vehicle.");
        }

        Scene referenceScene =
            EditorSceneManager.OpenScene(ReferenceScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject referenceCar = FindInScene(referenceScene, "PLAYER_Car");
            GameObject targetCar = FindInScene(targetScene, "PLAYER_Car");
            GameObject referenceCamera = FindInScene(referenceScene, "SYS_MainCamera");
            GameObject targetCamera = FindInScene(targetScene, "SYS_MainCamera");
            if (referenceCar == null || targetCar == null ||
                referenceCamera == null || targetCamera == null)
            {
                throw new InvalidOperationException(
                    "IslandMap or Level03 is missing PLAYER_Car or SYS_MainCamera.");
            }

            SimpleAutoDriveController referenceController =
                RequireComponent<SimpleAutoDriveController>(referenceCar);
            SimpleAutoDriveController targetController =
                RequireComponent<SimpleAutoDriveController>(targetCar);
            SimpleSpeedCameraFollow referenceFollow =
                RequireComponent<SimpleSpeedCameraFollow>(referenceCamera);
            SimpleSpeedCameraFollow targetFollow =
                RequireComponent<SimpleSpeedCameraFollow>(targetCamera);
            Camera referenceCameraComponent = RequireComponent<Camera>(referenceCamera);
            Camera targetCameraComponent = RequireComponent<Camera>(targetCamera);

            Transform referenceVisual = FindChild(referenceCar.transform, "Visual_Sedan");
            Transform targetVisual = FindChild(targetCar.transform, "Visual_Sedan");
            if (referenceVisual == null || targetVisual == null)
            {
                throw new InvalidOperationException(
                    "IslandMap or Level03 is missing the Visual_Sedan child.");
            }

            string referenceModelPath = GetPrefabAssetPath(referenceVisual.gameObject);
            string targetModelPath = GetPrefabAssetPath(targetVisual.gameObject);
            if (!string.Equals(
                    referenceModelPath,
                    targetModelPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Level03 uses '{targetModelPath}', but IslandMap uses " +
                    $"'{referenceModelPath}'. Replace the model explicitly before syncing.");
            }

            float originalVisualBottom = CalculateBounds(targetVisual.gameObject).min.y;
            targetCar.transform.localScale = referenceCar.transform.localScale;
            targetVisual.localPosition = referenceVisual.localPosition;
            targetVisual.localRotation = referenceVisual.localRotation;
            targetVisual.localScale = referenceVisual.localScale;
            Physics.SyncTransforms();
            float synchronizedVisualBottom = CalculateBounds(targetVisual.gameObject).min.y;
            targetCar.transform.position +=
                Vector3.up * (originalVisualBottom - synchronizedVisualBottom);

            EditorUtility.CopySerializedManagedFieldsOnly(
                referenceController,
                targetController);
            EditorUtility.SetDirty(targetController);

            EditorUtility.CopySerialized(referenceCameraComponent, targetCameraComponent);
            EditorUtility.CopySerializedManagedFieldsOnly(referenceFollow, targetFollow);
            targetFollow.target = targetCar.transform;
            targetCamera.transform.rotation = referenceCamera.transform.rotation;
            targetCamera.transform.localScale = referenceCamera.transform.localScale;
            targetCamera.transform.position =
                targetCar.transform.position +
                (referenceCamera.transform.position - referenceCar.transform.position);
            EditorUtility.SetDirty(targetCameraComponent);
            EditorUtility.SetDirty(targetFollow);

            AudioListener referenceListener =
                referenceCamera.GetComponent<AudioListener>();
            AudioListener targetListener = targetCamera.GetComponent<AudioListener>();
            if (referenceListener != null && targetListener != null)
            {
                targetListener.enabled = referenceListener.enabled;
                EditorUtility.SetDirty(targetListener);
            }

            EditorUtility.SetDirty(targetCar);
            EditorUtility.SetDirty(targetVisual);
            EditorUtility.SetDirty(targetCamera);
            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorSceneManager.SaveScene(targetScene, TargetScenePath);

            SyncReport report = BuildReport(
                referenceCar,
                targetCar,
                referenceVisual,
                targetVisual,
                referenceController,
                targetController,
                referenceCameraComponent,
                targetCameraComponent,
                referenceFollow,
                targetFollow,
                referenceModelPath);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            File.WriteAllText(
                Path.Combine(projectRoot, ReportPath),
                JsonUtility.ToJson(report, true));
            Debug.Log(
                "[Level03 Vehicle/Camera Sync] Matched car model scale, driving " +
                "speed, and follow camera to IslandMap.");
        }
        finally
        {
            EditorSceneManager.CloseScene(referenceScene, true);
        }
    }

    private static SyncReport BuildReport(
        GameObject referenceCar,
        GameObject targetCar,
        Transform referenceVisual,
        Transform targetVisual,
        SimpleAutoDriveController referenceController,
        SimpleAutoDriveController targetController,
        Camera referenceCamera,
        Camera targetCamera,
        SimpleSpeedCameraFollow referenceFollow,
        SimpleSpeedCameraFollow targetFollow,
        string modelAsset)
    {
        SerializedObject referenceControllerData =
            new SerializedObject(referenceController);
        SerializedObject targetControllerData = new SerializedObject(targetController);
        SerializedObject referenceFollowData = new SerializedObject(referenceFollow);
        SerializedObject targetFollowData = new SerializedObject(targetFollow);
        Bounds referenceBounds = CalculateBounds(referenceVisual.gameObject);
        Bounds targetBounds = CalculateBounds(targetVisual.gameObject);

        return new SyncReport
        {
            success = true,
            referenceScene = ReferenceScenePath,
            targetScene = TargetScenePath,
            modelAsset = modelAsset,
            referenceCarScale = referenceCar.transform.localScale,
            targetCarScale = targetCar.transform.localScale,
            referenceVisualSize = referenceBounds.size,
            targetVisualSize = targetBounds.size,
            referenceSpeedMultiplier =
                FindProperty(referenceControllerData, "speedMultiplier").floatValue,
            targetSpeedMultiplier =
                FindProperty(targetControllerData, "speedMultiplier").floatValue,
            referencePreserveInitialHeading =
                FindProperty(referenceControllerData, "preserveInitialHeading").boolValue,
            targetPreserveInitialHeading =
                FindProperty(targetControllerData, "preserveInitialHeading").boolValue,
            referenceCameraHeight =
                FindProperty(referenceFollowData, "topDownHeight").floatValue,
            targetCameraHeight =
                FindProperty(targetFollowData, "topDownHeight").floatValue,
            referenceOrthographicSize = referenceCamera.orthographicSize,
            targetOrthographicSize = targetCamera.orthographicSize,
            referenceThirdPersonCameraOffset =
                FindProperty(referenceFollowData, "thirdPersonCameraOffset").vector3Value,
            targetThirdPersonCameraOffset =
                FindProperty(targetFollowData, "thirdPersonCameraOffset").vector3Value,
            referenceThirdPersonLookOffset =
                FindProperty(referenceFollowData, "thirdPersonLookOffset").vector3Value,
            targetThirdPersonLookOffset =
                FindProperty(targetFollowData, "thirdPersonLookOffset").vector3Value,
            cameraTargetsLevel03Player = targetFollow.target == targetCar.transform
        };
    }

    private static SerializedProperty FindProperty(
        SerializedObject serializedObject,
        string name)
    {
        SerializedProperty property = serializedObject.FindProperty(name);
        if (property == null)
        {
            throw new InvalidOperationException(
                $"Serialized property '{name}' is missing from " +
                $"{serializedObject.targetObject.GetType().Name}.");
        }

        return property;
    }

    private static T RequireComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null)
        {
            throw new InvalidOperationException(
                $"{gameObject.name} is missing {typeof(T).Name}.");
        }

        return component;
    }

    private static string GetPrefabAssetPath(GameObject instance)
    {
        UnityEngine.Object source =
            PrefabUtility.GetCorrespondingObjectFromOriginalSource(instance);
        return source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindChild(root.transform, name);
            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        foreach (Transform child in root)
        {
            Transform found = FindChild(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            throw new InvalidOperationException(
                $"{root.name} has no Renderer for model-size verification.");
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds;
    }
}
