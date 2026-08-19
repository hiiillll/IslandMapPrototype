using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class Level02HighQualityRendering : MonoBehaviour
{
    private int previousAntiAliasing;
    private int previousPixelLightCount;
    private int previousParticleRaycastBudget;
    private float previousLodBias;
    private bool previousSoftParticles;
    private bool previousRealtimeReflectionProbes;
    private AnisotropicFiltering previousAnisotropicFiltering;
    private ShadowResolution previousShadowResolution;
    private SkinWeights previousSkinWeights;
    private bool settingsCaptured;

    private void OnEnable()
    {
        if (!Application.isPlaying || settingsCaptured)
        {
            return;
        }

        previousAntiAliasing = QualitySettings.antiAliasing;
        previousPixelLightCount = QualitySettings.pixelLightCount;
        previousParticleRaycastBudget = QualitySettings.particleRaycastBudget;
        previousLodBias = QualitySettings.lodBias;
        previousSoftParticles = QualitySettings.softParticles;
        previousRealtimeReflectionProbes = QualitySettings.realtimeReflectionProbes;
        previousAnisotropicFiltering = QualitySettings.anisotropicFiltering;
        previousShadowResolution = QualitySettings.shadowResolution;
        previousSkinWeights = QualitySettings.skinWeights;
        settingsCaptured = true;

        QualitySettings.antiAliasing = Mathf.Max(4, previousAntiAliasing);
        QualitySettings.pixelLightCount = Mathf.Max(8, previousPixelLightCount);
        QualitySettings.particleRaycastBudget = Mathf.Max(8192, previousParticleRaycastBudget);
        QualitySettings.lodBias = Mathf.Max(2.75f, previousLodBias);
        QualitySettings.softParticles = true;
        QualitySettings.realtimeReflectionProbes = true;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.skinWeights = SkinWeights.Unlimited;
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

        QualitySettings.antiAliasing = previousAntiAliasing;
        QualitySettings.pixelLightCount = previousPixelLightCount;
        QualitySettings.particleRaycastBudget = previousParticleRaycastBudget;
        QualitySettings.lodBias = previousLodBias;
        QualitySettings.softParticles = previousSoftParticles;
        QualitySettings.realtimeReflectionProbes = previousRealtimeReflectionProbes;
        QualitySettings.anisotropicFiltering = previousAnisotropicFiltering;
        QualitySettings.shadowResolution = previousShadowResolution;
        QualitySettings.skinWeights = previousSkinWeights;
        settingsCaptured = false;
    }
}
