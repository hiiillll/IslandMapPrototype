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
        cameraFollow.ConfigureCloseChaseView(true);
        cameraComponent.orthographic = false;
        cameraComponent.fieldOfView = 62f;
        cameraComponent.nearClipPlane = 0.3f;
        cameraComponent.farClipPlane = 1000f;
        cameraComponent.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
        cameraObject.transform.position = player.transform.TransformPoint(new Vector3(0f, 8f, -18f));
        cameraObject.transform.LookAt(player.transform.TransformPoint(new Vector3(0f, 1.5f, -2f)));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("O"));
        Selection.activeGameObject = cameraObject;
        Debug.Log("Installed the fixed third-person Speed camera.");
    }
}
