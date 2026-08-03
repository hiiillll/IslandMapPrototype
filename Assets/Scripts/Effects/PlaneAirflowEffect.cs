using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Rigidbody))]
public sealed class PlaneAirflowEffect : MonoBehaviour
{
    private const string ParticleMaterialResourcePath = "Effects/ExplosionParticle";

    [SerializeField] private Transform bankPivot;
    [SerializeField, Min(0f)] private float minimumVisibleSpeed = 12f;
    [SerializeField, Min(0f)] private float fullEffectSpeed = 30f;
    [SerializeField, Min(0f)] private float maximumAirStreakRate = 72f;

    private static Material airflowMaterial;
    private static Texture2D airflowTexture;

    private Rigidbody body;
    private TrailRenderer leftWingTrail;
    private TrailRenderer rightWingTrail;
    private ParticleSystem airStreaks;

    public void Configure(Transform visualBankPivot)
    {
        bankPivot = visualBankPivot;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        EnsureEffects();
    }

    private void Update()
    {
        if (body == null)
        {
            return;
        }

        float strength = Mathf.InverseLerp(
            minimumVisibleSpeed,
            Mathf.Max(minimumVisibleSpeed + 0.01f, fullEffectSpeed),
            body.velocity.magnitude);
        bool showTrails = strength > 0.03f;
        if (leftWingTrail != null)
        {
            leftWingTrail.emitting = showTrails;
            leftWingTrail.startWidth = Mathf.Lerp(0.05f, 0.18f, strength);
        }
        if (rightWingTrail != null)
        {
            rightWingTrail.emitting = showTrails;
            rightWingTrail.startWidth = Mathf.Lerp(0.05f, 0.18f, strength);
        }
        if (airStreaks != null)
        {
            ParticleSystem.EmissionModule emission = airStreaks.emission;
            emission.rateOverTime = maximumAirStreakRate * strength;
            ParticleSystem.VelocityOverLifetimeModule velocity = airStreaks.velocityOverLifetime;
            velocity.z = -Mathf.Lerp(32f, 58f, strength);
        }
    }

    private void EnsureEffects()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }
        if (bankPivot == null)
        {
            bankPivot = transform.Find("BankPivot");
        }
        if (bankPivot == null)
        {
            return;
        }

        if (leftWingTrail == null)
        {
            leftWingTrail = CreateWingTrail("VFX_LeftWingContrail", new Vector3(-3.69f, 0f, -0.54f));
        }
        if (rightWingTrail == null)
        {
            rightWingTrail = CreateWingTrail("VFX_RightWingContrail", new Vector3(3.69f, 0f, -0.54f));
        }
        if (airStreaks == null)
        {
            airStreaks = CreateAirStreaks();
        }
    }

    private TrailRenderer CreateWingTrail(string objectName, Vector3 localPosition)
    {
        GameObject trailObject = new GameObject(objectName);
        trailObject.transform.SetParent(bankPivot, false);
        trailObject.transform.localPosition = localPosition;

        TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
        trail.time = 0.85f;
        trail.minVertexDistance = 0.2f;
        trail.startWidth = 0.18f;
        trail.endWidth = 0.015f;
        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.35f, 0.72f),
            new Keyframe(1f, 0f));
        trail.colorGradient = CreateContrailGradient();
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.sharedMaterial = GetAirflowMaterial();
        trail.emitting = false;
        return trail;
    }

    private ParticleSystem CreateAirStreaks()
    {
        GameObject streakObject = new GameObject("VFX_HighSpeedAirflow");
        streakObject.transform.SetParent(bankPivot, false);
        streakObject.transform.localPosition = new Vector3(0f, 1.2f, 15f);
        streakObject.transform.localRotation = Quaternion.identity;

        ParticleSystem streaks = streakObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = streaks.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 0.95f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.72f, 0.9f, 1f, 0.24f),
            new Color(1f, 1f, 1f, 0.55f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 160;

        ParticleSystem.EmissionModule emission = streaks.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = streaks.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(19f, 8f, 12f);

        ParticleSystem.VelocityOverLifetimeModule velocity = streaks.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.z = -58f;

        ParticleSystem.ColorOverLifetimeModule color = streaks.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.78f, 0.92f, 1f), 0f),
                new GradientColorKey(Color.white, 0.5f),
                new GradientColorKey(new Color(0.64f, 0.84f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.62f, 0.18f),
                new GradientAlphaKey(0.36f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;

        ParticleSystemRenderer renderer = streaks.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.11f;
        renderer.lengthScale = 3.2f;
        renderer.cameraVelocityScale = 0f;
        renderer.sharedMaterial = GetAirflowMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = 3;

        streaks.Play();
        return streaks;
    }

    private static Gradient CreateContrailGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.68f, 0.88f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0.3f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static Material GetAirflowMaterial()
    {
        if (airflowMaterial != null)
        {
            return airflowMaterial;
        }

        Material template = Resources.Load<Material>(ParticleMaterialResourcePath);
        if (template != null)
        {
            airflowMaterial = new Material(template);
        }
        else
        {
            Shader shader = Shader.Find("Particles/Standard Unlit");
            airflowMaterial = new Material(shader);
        }
        airflowMaterial.name = "Runtime_PlaneAirflow";
        airflowMaterial.mainTexture = GetAirflowTexture();
        return airflowMaterial;
    }

    private static Texture2D GetAirflowTexture()
    {
        if (airflowTexture != null)
        {
            return airflowTexture;
        }

        const int width = 64;
        const int height = 8;
        airflowTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        airflowTexture.name = "Runtime_PlaneAirflowTexture";
        airflowTexture.wrapMode = TextureWrapMode.Clamp;
        airflowTexture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < height; y++)
        {
            float vertical = 1f - Mathf.Abs((y + 0.5f) / height * 2f - 1f);
            for (int x = 0; x < width; x++)
            {
                float horizontal = Mathf.Sin((x + 0.5f) / width * Mathf.PI);
                float alpha = Mathf.Pow(vertical, 1.6f) * Mathf.Pow(horizontal, 0.7f);
                airflowTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        airflowTexture.Apply();
        return airflowTexture;
    }
}
