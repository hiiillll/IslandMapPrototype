using UnityEngine;

public static class EnemyExplosionEffect
{
    private const string ParticleMaterialResourcePath = "Effects/ExplosionParticle";

    private static Material flameMaterial;
    private static Material smokeMaterial;
    private static Material particleMaterialTemplate;
    private static Texture2D radialTexture;

    public static void Spawn(Vector3 position)
    {
        GameObject effect = new GameObject("VFX_LegacyExplosion");
        effect.transform.position = position + Vector3.up * 0.6f;
        CreateFlameBurst(effect.transform);
        CreateSmokePlume(effect.transform);
        Object.Destroy(effect, 1.8f);
    }

    private static void CreateFlameBurst(Transform parent)
    {
        ParticleSystem particles = CreateParticleSystem("Flame", parent);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.85f, 0.15f),
            new Color(1f, 0.25f, 0f));
        main.maxParticles = 25;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)20) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.6f;

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.6f), 0f),
                new GradientColorKey(new Color(1f, 0.5f, 0f), 0.25f),
                new GradientColorKey(new Color(0.8f, 0.15f, 0f), 0.6f),
                new GradientColorKey(new Color(0.2f, 0.05f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.9f, 0.15f),
                new GradientAlphaKey(0.5f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.3f),
            new Keyframe(0.2f, 1f),
            new Keyframe(0.6f, 0.7f),
            new Keyframe(1f, 0f)));

        ConfigureRenderer(particles, GetFlameMaterial(), 1);
        particles.Play();
    }

    private static void CreateSmokePlume(Transform parent)
    {
        ParticleSystem particles = CreateParticleSystem("Smoke", parent);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 1.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startColor = new Color(0.25f, 0.22f, 0.2f, 0.7f);
        main.gravityModifier = 0.3f;
        main.maxParticles = 17;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)12) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.8f;

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.2f, 0.18f, 0.15f), 0f),
                new GradientColorKey(new Color(0.35f, 0.32f, 0.28f), 0.3f),
                new GradientColorKey(new Color(0.5f, 0.48f, 0.45f), 0.7f),
                new GradientColorKey(new Color(0.6f, 0.58f, 0.55f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0.6f, 0.2f),
                new GradientAlphaKey(0.3f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.4f, 1f),
            new Keyframe(0.8f, 2.5f),
            new Keyframe(1f, 3.5f)));

        ConfigureRenderer(particles, GetSmokeMaterial(), 0);
        particles.Play();
    }

    private static ParticleSystem CreateParticleSystem(string name, Transform parent)
    {
        GameObject particleObject = new GameObject(name);
        particleObject.transform.SetParent(parent, false);
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    private static void ConfigureRenderer(ParticleSystem particles, Material material, int sortingOrder)
    {
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (material != null)
        {
            renderer.material = material;
        }
        renderer.sortingOrder = sortingOrder;
    }

    private static Material GetFlameMaterial()
    {
        if (flameMaterial == null)
        {
            flameMaterial = CreateParticleMaterial();
        }
        return flameMaterial;
    }

    private static Material GetSmokeMaterial()
    {
        if (smokeMaterial == null)
        {
            smokeMaterial = CreateParticleMaterial();
        }
        return smokeMaterial;
    }

    private static Material CreateParticleMaterial()
    {
        if (particleMaterialTemplate == null)
        {
            particleMaterialTemplate = Resources.Load<Material>(ParticleMaterialResourcePath);
        }
        if (particleMaterialTemplate == null)
        {
            return null;
        }

        Material material = new Material(particleMaterialTemplate);
        material.mainTexture = GetRadialTexture();
        return material;
    }

    private static Texture2D GetRadialTexture()
    {
        if (radialTexture != null)
        {
            return radialTexture;
        }

        radialTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        radialTexture.wrapMode = TextureWrapMode.Clamp;
        radialTexture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
                radialTexture.SetPixel(x, y, new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, distance)));
            }
        }
        radialTexture.Apply();
        return radialTexture;
    }
}
