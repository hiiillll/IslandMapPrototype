using UnityEngine;

[DisallowMultipleComponent]
public sealed class Level03TreasureBeacon : MonoBehaviour
{
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private Transform coreTransform;
    [SerializeField] private Renderer coreRenderer;
    [SerializeField] private Transform glowTransform;
    [SerializeField] private Renderer glowRenderer;
    [SerializeField] private Color coreColor = new Color(1.35f, 0.82f, 0.2f, 0.72f);
    [SerializeField] private Color glowColor = new Color(1f, 0.55f, 0.08f, 0.24f);
    [SerializeField, Min(0f)] private float pulseSpeed = 1.15f;
    [SerializeField, Range(0f, 0.3f)] private float radiusPulse = 0.055f;

    private MaterialPropertyBlock coreProperties;
    private MaterialPropertyBlock glowProperties;
    private Vector3 coreBaseScale;
    private Vector3 glowBaseScale;

    public bool IsConfigured =>
        coreTransform != null &&
        coreRenderer != null &&
        glowTransform != null &&
        glowRenderer != null;

    private void OnEnable()
    {
        CacheBaseScales();
        UpdateBeacon(0f);
    }

    private void Update()
    {
        UpdateBeacon(Time.time * pulseSpeed);
    }

    public void Configure(
        Transform configuredCoreTransform,
        Renderer configuredCoreRenderer,
        Transform configuredGlowTransform,
        Renderer configuredGlowRenderer)
    {
        coreTransform = configuredCoreTransform;
        coreRenderer = configuredCoreRenderer;
        glowTransform = configuredGlowTransform;
        glowRenderer = configuredGlowRenderer;
        CacheBaseScales();
    }

    private void CacheBaseScales()
    {
        if (coreTransform != null)
        {
            coreBaseScale = coreTransform.localScale;
        }
        if (glowTransform != null)
        {
            glowBaseScale = glowTransform.localScale;
        }
    }

    private void UpdateBeacon(float time)
    {
        if (!IsConfigured)
        {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(time);
        float coreRadius = 1f + radiusPulse * (pulse - 0.5f);
        float glowRadius = 1f + radiusPulse * 2f * (0.5f - pulse);
        coreTransform.localScale = ScaleRadius(coreBaseScale, coreRadius);
        glowTransform.localScale = ScaleRadius(glowBaseScale, glowRadius);

        Color pulsingCore = coreColor;
        pulsingCore.a *= Mathf.Lerp(0.82f, 1f, pulse);
        Color pulsingGlow = glowColor;
        pulsingGlow.a *= Mathf.Lerp(0.72f, 1f, 1f - pulse);
        ApplyColor(coreRenderer, pulsingCore, ref coreProperties);
        ApplyColor(glowRenderer, pulsingGlow, ref glowProperties);
    }

    private static Vector3 ScaleRadius(Vector3 baseScale, float multiplier)
    {
        return new Vector3(
            baseScale.x * multiplier,
            baseScale.y,
            baseScale.z * multiplier);
    }

    private static void ApplyColor(
        Renderer target,
        Color color,
        ref MaterialPropertyBlock properties)
    {
        if (properties == null)
        {
            properties = new MaterialPropertyBlock();
        }

        target.GetPropertyBlock(properties);
        properties.SetColor(TintColorId, color);
        properties.SetColor(ColorId, color);
        target.SetPropertyBlock(properties);
    }
}
