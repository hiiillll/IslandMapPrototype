using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class LegacySkillVfx
{
    public static void CreateGravityVortex(Transform parent, float radius, float duration)
    {
        CreateVortexRing(
            parent,
            radius,
            duration,
            1f,
            80,
            new Color(0.4f, 0f, 0.8f),
            new Color(0.8f, 0.2f, 1f));
        CreateVortexRing(
            parent,
            radius * 0.5f,
            duration,
            1.2f,
            120,
            new Color(0.6f, 0.1f, 1f),
            new Color(1f, 0.4f, 1f));
    }

    public static void FlashPlayer(Transform player, float duration)
    {
        if (player == null)
        {
            return;
        }

        GameObject flashObject = new GameObject("SKILL_LegacyTeleportFlash");
        LegacyPlayerFlash flash = flashObject.AddComponent<LegacyPlayerFlash>();
        flash.Initialize(player, duration);
    }

    public static void SpawnHornShockwave(Vector3 center, float maximumRadius)
    {
        HornShockwaveRing.Spawn(center, maximumRadius);
        GameObject visual = new GameObject("SKILL_LegacyHornShockwave");
        visual.transform.position = center + Vector3.up * 0.55f;

        ParticleSystem particles = visual.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.38f, 0.82f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 1f, 0.95f),
            new Color(0.4f, 0.55f, 1f, 0.7f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)Random.Range(50, 70))
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.55f;
        shape.radiusThickness = 0.01f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.55f, 0.7f, 1f), 0.2f),
                new GradientColorKey(new Color(0.15f, 0.3f, 0.7f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.65f, 0.2f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(0.1f, 1f),
                new Keyframe(0.5f, 0.8f),
                new Keyframe(1f, 0f)));

        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = particles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(maximumRadius * 2.5f, maximumRadius * 3.5f);

        ConfigureParticleRenderer(particles, 2);
        particles.Play();
        Object.Destroy(visual, 1.5f);
    }

    public static void CreateFlameNodeVisual(Transform parent, float radius, float lifetime)
    {
        float scale = radius / 1.45f;
        ParticleSystem particles = SkillVisuals.CreateParticleSystem("LegacyFlameParticles", parent);
        particles.transform.localPosition = Vector3.up * 0.45f;
        ParticleSystem.MainModule main = particles.main;
        main.duration = lifetime;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.15f * scale, 2.2f * scale);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.55f, 0.05f, 0.85f),
            new Color(0.9f, 0.2f, 0f, 0.7f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)Random.Range(12, 20))
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.68f * scale;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.7f, 0.1f), 0f),
                new GradientColorKey(new Color(1f, 0.3f, 0f), 0.35f),
                new GradientColorKey(new Color(0.6f, 0.08f, 0f), 0.75f),
                new GradientColorKey(new Color(0.1f, 0.02f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.8f, 0.3f),
                new GradientAlphaKey(0.5f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.6f),
                new Keyframe(0.1f, 1f),
                new Keyframe(0.5f, 0.8f),
                new Keyframe(1f, 0f)));

        ParticleSystem.RotationOverLifetimeModule rotationOverLifetime = particles.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-45f, 45f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        noise.frequency = 1.5f;

        ConfigureParticleRenderer(particles, 1);
        particles.Play();
    }

    public static void CreateCannonballVisual(Transform parent)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "LegacyCannonballVisual";
        visual.transform.SetParent(parent, false);
        visual.transform.localScale = Vector3.one * 1.25f;
        Collider visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null)
        {
            Object.Destroy(visualCollider);
        }
        Renderer renderer = visual.GetComponent<Renderer>();
        renderer.material.color = new Color(0.6f, 0.1f, 0f);
    }

    public static void AttachCannonExhaust(Transform parent, float duration)
    {
        ParticleSystem particles = SkillVisuals.CreateParticleSystem("LegacyCannonFlameTrail", parent);
        particles.transform.localPosition = Vector3.back * 0.2f;

        ParticleSystem.MainModule main = particles.main;
        main.duration = duration;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.48f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.6f, 0.1f, 0.9f),
            new Color(0.9f, 0.2f, 0f, 0.6f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 35f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.18f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.7f, 0.15f), 0f),
                new GradientColorKey(new Color(1f, 0.3f, 0f), 0.25f),
                new GradientColorKey(new Color(0.5f, 0.05f, 0f), 0.6f),
                new GradientColorKey(new Color(0.05f, 0.01f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.65f, 0.25f),
                new GradientAlphaKey(0.2f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(0.1f, 1f),
                new Keyframe(0.4f, 0.5f),
                new Keyframe(1f, 0f)));

        ConfigureParticleRenderer(particles, 1);
        particles.Play();
    }

    public static void SpawnCannonImpact(Vector3 position)
    {
        GameObject impact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        impact.name = "SKILL_LegacyCannonImpact";
        impact.transform.position = position + Vector3.up * 0.65f;
        impact.transform.localScale = Vector3.one * 2.2f;
        Collider impactCollider = impact.GetComponent<Collider>();
        if (impactCollider != null)
        {
            Object.Destroy(impactCollider);
        }
        impact.GetComponent<Renderer>().material.color = new Color(1f, 0.5f, 0f, 0.9f);
        Object.Destroy(impact, 0.4f);
    }

    private static void CreateVortexRing(
        Transform parent,
        float radius,
        float duration,
        float lifetimeScale,
        int particleCount,
        Color minimumColor,
        Color maximumColor)
    {
        ParticleSystem particles = SkillVisuals.CreateParticleSystem("LegacyVortexRing", parent);
        particles.transform.localPosition = Vector3.up * 0.45f;
        ParticleSystem.MainModule main = particles.main;
        main.duration = duration;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f * lifetimeScale, 0.85f * lifetimeScale);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.25f);
        main.startColor = new ParticleSystem.MinMaxGradient(minimumColor, maximumColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startRotation3D = true;
        main.maxParticles = particleCount + 30;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = particleCount / duration;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0.02f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = particles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.orbitalZ = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(-2.5f, -0.8f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(maximumColor, 0f),
                new GradientColorKey(Color.Lerp(minimumColor, maximumColor, 0.5f), 0.4f),
                new GradientColorKey(minimumColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.65f, 0.35f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.2f, 0.85f),
                new Keyframe(1f, 0f)));

        ParticleSystem.RotationOverLifetimeModule rotationOverLifetime = particles.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-60f, 60f);

        ConfigureParticleRenderer(particles, 1);
        particles.Play();
    }

    private static void ConfigureParticleRenderer(ParticleSystem particles, int sortingOrder)
    {
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        Material material = SkillVisuals.GetParticleMaterial();
        if (material != null)
        {
            renderer.material = material;
        }
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = sortingOrder;
    }
}

public sealed class HornShockwaveRing : MonoBehaviour
{
    private const float Duration = 0.55f;

    private LineRenderer lineRenderer;
    private Material material;
    private float maximumRadius;
    private float startTime;

    public static void Spawn(Vector3 center, float radius)
    {
        GameObject ringObject = new GameObject("SKILL_LegacyHornVisibleRing");
        ringObject.transform.position = center + Vector3.up * 0.35f;
        HornShockwaveRing ring = ringObject.AddComponent<HornShockwaveRing>();
        ring.Initialize(radius);
    }

    private void Initialize(float radius)
    {
        maximumRadius = radius;
        startTime = Time.time;
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.positionCount = 72;
        lineRenderer.sortingOrder = 12;
        for (int index = 0; index < lineRenderer.positionCount; index++)
        {
            float angle = index * Mathf.PI * 2f / lineRenderer.positionCount;
            lineRenderer.SetPosition(index, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
        }

        material = SkillVisuals.CreateTintedParticleMaterial(new Color(0.5f, 0.75f, 1f, 1f));
        if (material != null)
        {
            material.mainTexture = Texture2D.whiteTexture;
            lineRenderer.material = material;
        }
        SetGradient(1f);
    }

    private void Update()
    {
        if (lineRenderer == null)
        {
            Destroy(gameObject);
            return;
        }

        float progress = Mathf.Clamp01((Time.time - startTime) / Duration);
        float radius = Mathf.Lerp(0.45f, maximumRadius, Mathf.SmoothStep(0f, 1f, progress));
        transform.localScale = new Vector3(radius, 1f, radius);
        lineRenderer.widthMultiplier = Mathf.Lerp(0.52f, 0.16f, progress);
        SetGradient(1f - progress);
        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void SetGradient(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.3f, 0.65f, 1f), 0.45f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(alpha, 0f),
                new GradientAlphaKey(alpha * 0.85f, 0.5f),
                new GradientAlphaKey(alpha, 1f)
            });
        lineRenderer.colorGradient = gradient;
    }

    private void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
    }
}

public sealed class HornBlastEffect : MonoBehaviour
{
    private Transform owner;
    private float radius;
    private float minimumForce;
    private float maximumForce;
    private int pulseCount;

    public static void Spawn(Transform player, float radius, float minimumForce, float maximumForce, int pulseCount)
    {
        if (player == null)
        {
            return;
        }

        GameObject effectObject = new GameObject("SKILL_LegacyHornBlast");
        HornBlastEffect effect = effectObject.AddComponent<HornBlastEffect>();
        effect.owner = player;
        effect.radius = radius;
        effect.minimumForce = minimumForce;
        effect.maximumForce = maximumForce;
        effect.pulseCount = pulseCount;
    }

    private IEnumerator Start()
    {
        for (int pulse = 0; pulse < pulseCount; pulse++)
        {
            if (owner == null)
            {
                break;
            }

            Vector3 center = owner.position;
            NavMeshEnemyCarChaser[] enemies = FindObjectsOfType<NavMeshEnemyCarChaser>();
            foreach (NavMeshEnemyCarChaser enemy in enemies)
            {
                Vector3 direction = enemy.transform.position - center;
                direction.y = 0f;
                float distance = direction.magnitude;
                if (distance > radius)
                {
                    continue;
                }

                float falloff = 1f - Mathf.Clamp01(distance / radius);
                float force = Mathf.Lerp(minimumForce, maximumForce, falloff);
                enemy.ApplyKnockback(direction, force, 0.6f);
            }

            LegacySkillVfx.SpawnHornShockwave(center, radius);
            if (pulse < pulseCount - 1)
            {
                yield return new WaitForSeconds(0.22f);
            }
        }

        Destroy(gameObject);
    }
}

public sealed class LegacyPlayerFlash : MonoBehaviour
{
    private Transform owner;
    private float duration;

    public void Initialize(Transform player, float flashDuration)
    {
        owner = player;
        duration = flashDuration;
    }

    private IEnumerator Start()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            yield break;
        }

        Renderer[] renderers = owner.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Destroy(gameObject);
            yield break;
        }

        List<Material> materials = new List<Material>();
        List<Color> originalColors = new List<Color>();
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.materials)
            {
                if (material == null || !material.HasProperty("_Color"))
                {
                    continue;
                }
                materials.Add(material);
                originalColors.Add(material.color);
                material.color = Color.cyan;
            }
        }
        yield return new WaitForSeconds(duration);
        for (int index = 0; index < materials.Count; index++)
        {
            if (materials[index] != null)
            {
                materials[index].color = originalColors[index];
            }
        }
        Destroy(gameObject);
    }
}
