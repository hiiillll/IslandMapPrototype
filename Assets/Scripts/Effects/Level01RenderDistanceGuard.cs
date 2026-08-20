using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps Level 01 scenery visible at long range without changing the shared
/// Miami Beach prefabs used by other scenes.
/// </summary>
public sealed class Level01RenderDistanceGuard : MonoBehaviour
{
    private const string SceneName = "IslandMap";
    private const float MinimumLodBias = 3f;
    private const float LastLodTransitionHeight = 0.001f;
    private const float MinimumFarClipPlane = 3000f;

    private float previousLodBias;
    private int previousTextureMipmapLimit;
    private AnisotropicFiltering previousAnisotropicFiltering;
    private bool settingsCaptured;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoader()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneName)
        {
            return;
        }

        GameObject guardObject = new GameObject("SYS_Level01RenderDistanceGuard");
        SceneManager.MoveGameObjectToScene(guardObject, scene);
        guardObject.AddComponent<Level01RenderDistanceGuard>();
    }

    private void Awake()
    {
        previousLodBias = QualitySettings.lodBias;
        previousTextureMipmapLimit = QualitySettings.globalTextureMipmapLimit;
        previousAnisotropicFiltering = QualitySettings.anisotropicFiltering;
        settingsCaptured = true;
        QualitySettings.lodBias = Mathf.Max(MinimumLodBias, previousLodBias);
        QualitySettings.globalTextureMipmapLimit = 0;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;

        Scene scene = gameObject.scene;
        Camera mainCamera = FindSceneMainCamera(scene);
        if (mainCamera != null)
        {
            mainCamera.farClipPlane = Mathf.Max(
                MinimumFarClipPlane,
                mainCamera.farClipPlane);
        }

        int adjustedLodGroups = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (LODGroup lodGroup in root.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = lodGroup.GetLODs();
                if (lods.Length == 0)
                {
                    continue;
                }

                int lastIndex = lods.Length - 1;
                if (lods[lastIndex].screenRelativeTransitionHeight
                    <= LastLodTransitionHeight)
                {
                    continue;
                }

                lods[lastIndex].screenRelativeTransitionHeight =
                    LastLodTransitionHeight;
                lodGroup.SetLODs(lods);
                adjustedLodGroups++;
            }
        }

        Debug.Log(
            $"[Level 01 Render Distance] Adjusted {adjustedLodGroups} LOD groups, "
            + $"LOD bias {QualitySettings.lodBias:0.##}, "
            + $"far clip {(mainCamera != null ? mainCamera.farClipPlane : 0f):0}, "
            + "full texture resolution with forced anisotropic filtering."
        );
    }

    private void OnDestroy()
    {
        if (settingsCaptured)
        {
            QualitySettings.lodBias = previousLodBias;
            QualitySettings.globalTextureMipmapLimit = previousTextureMipmapLimit;
            QualitySettings.anisotropicFiltering = previousAnisotropicFiltering;
            settingsCaptured = false;
        }
    }

    private static Camera FindSceneMainCamera(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera sceneCamera in root.GetComponentsInChildren<Camera>(true))
            {
                if (sceneCamera.CompareTag("MainCamera"))
                {
                    return sceneCamera;
                }
            }
        }

        return null;
    }
}
