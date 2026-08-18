using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class Level01ShadowStability : MonoBehaviour
{
    [SerializeField, Min(10f)] private float shadowDistance = 150f;

    private int previousCascadeCount;
    private ShadowProjection previousProjection;
    private float previousShadowDistance;
    private bool settingsCaptured;

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!settingsCaptured)
        {
            previousCascadeCount = QualitySettings.shadowCascades;
            previousProjection = QualitySettings.shadowProjection;
            previousShadowDistance = QualitySettings.shadowDistance;
            settingsCaptured = true;
        }

        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.shadowCascades = 0;
        QualitySettings.shadowDistance = shadowDistance;
    }

    private void OnDisable()
    {
        RestoreSettings();
    }

    private void OnDestroy()
    {
        RestoreSettings();
    }

    private void RestoreSettings()
    {
        if (!settingsCaptured)
        {
            return;
        }

        QualitySettings.shadowCascades = previousCascadeCount;
        QualitySettings.shadowProjection = previousProjection;
        QualitySettings.shadowDistance = previousShadowDistance;
        settingsCaptured = false;
    }
}
