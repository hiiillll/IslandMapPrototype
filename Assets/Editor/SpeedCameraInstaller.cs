using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SpeedCameraInstaller
{
    private const string ScenePath = "Assets/Scenes/IslandMap.unity";
    private const string MarkerPath = "Library/SpeedCameraInstalled.v1";

    [MenuItem("Tools/Island Map/Install Speed Camera")]
    public static void TryInstall()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryInstall;
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject cameraObject = GameObject.Find("SYS_MainCamera");
        GameObject player = GameObject.Find("PLAYER_Car");
        if (!scene.IsValid() || scene.path != ScenePath || cameraObject == null || player == null)
        {
            EditorApplication.delayCall += TryInstall;
            return;
        }

        Camera cameraComponent = cameraObject.GetComponent<Camera>();
        SimpleSpeedCameraFollow cameraFollow = cameraObject.GetComponent<SimpleSpeedCameraFollow>();
        if (cameraFollow == null)
        {
            cameraFollow = cameraObject.AddComponent<SimpleSpeedCameraFollow>();
        }

        cameraFollow.target = player.transform;
        cameraComponent.orthographic = true;
        cameraComponent.orthographicSize = 7f;
        cameraComponent.fieldOfView = 60f;
        cameraComponent.nearClipPlane = 0.3f;
        cameraComponent.farClipPlane = 1000f;
        cameraComponent.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
        cameraObject.transform.position = player.transform.position + Vector3.up * 19f;
        cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("O"));
        Selection.activeGameObject = cameraObject;
        Debug.Log("Installed Speed-style top-down camera follow with C view toggle.");
    }
}
