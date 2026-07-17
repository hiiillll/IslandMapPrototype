using System.Collections.Generic;
using UnityEngine;

public sealed class SkillShockwave : MonoBehaviour
{
    private Material material;
    private float endTime;
    private float duration;
    private float maximumDiameter;
    private Color color;

    public static void Spawn(Vector3 position, Color color, float maximumDiameter, float duration)
    {
        GameObject effectObject = new GameObject("SKILL_Shockwave");
        effectObject.transform.position = position + Vector3.up * 0.03f;
        SkillShockwave shockwave = effectObject.AddComponent<SkillShockwave>();
        shockwave.Initialize(color, maximumDiameter, duration);
    }

    private void Initialize(Color effectColor, float effectDiameter, float effectDuration)
    {
        color = effectColor;
        maximumDiameter = effectDiameter;
        duration = effectDuration;
        endTime = Time.time + duration;
        GameObject ringObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ringObject.name = "ShockwaveRing";
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localScale = new Vector3(0.3f, 0.035f, 0.3f);
        Collider ringCollider = ringObject.GetComponent<Collider>();
        if (ringCollider != null)
        {
            Destroy(ringCollider);
        }

        Renderer ringRenderer = ringObject.GetComponent<Renderer>();
        material = SkillVisuals.CreateTintedParticleMaterial(color);
        if (material != null)
        {
            ringRenderer.material = material;
        }
    }

    private void Update()
    {
        float progress = 1f - Mathf.Clamp01((endTime - Time.time) / duration);
        float diameter = Mathf.Lerp(0.3f, maximumDiameter, Mathf.SmoothStep(0f, 1f, progress));
        transform.GetChild(0).localScale = new Vector3(diameter, 0.035f, diameter);
        if (material != null)
        {
            Color fadingColor = color;
            fadingColor.a *= 1f - progress;
            material.color = fadingColor;
        }
        if (Time.time >= endTime)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
    }
}

public sealed class BlinkAfterimage : MonoBehaviour
{
    private readonly List<Material> materials = new List<Material>();
    private float endTime;
    private float duration;

    public static void Spawn(Vector3 position, Quaternion rotation, float duration)
    {
        GameObject afterimageObject = new GameObject("SKILL_BlinkAfterimage");
        afterimageObject.transform.position = position;
        afterimageObject.transform.rotation = rotation;
        BlinkAfterimage afterimage = afterimageObject.AddComponent<BlinkAfterimage>();
        afterimage.Initialize(duration);
    }

    private void Initialize(float effectDuration)
    {
        duration = effectDuration;
        endTime = Time.time + duration;
        CreatePiece("GhostBody", new Vector3(0f, 0.55f, 0f), new Vector3(1.7f, 0.52f, 3.3f), new Color(0.08f, 0.72f, 1f, 0.55f));
        CreatePiece("GhostCabin", new Vector3(0f, 0.98f, -0.15f), new Vector3(1.35f, 0.42f, 1.55f), new Color(0.35f, 0.9f, 1f, 0.42f));
        SkillVisuals.CreatePointLight(transform, new Color(0.1f, 0.7f, 1f), 4.8f, 1.3f);
    }

    private void Update()
    {
        float progress = 1f - Mathf.Clamp01((endTime - Time.time) / duration);
        foreach (Material material in materials)
        {
            Color color = material.color;
            color.a = Mathf.Lerp(0.58f, 0f, progress);
            material.color = color;
        }
        transform.position += Vector3.up * Time.deltaTime * 0.35f;
        if (Time.time >= endTime)
        {
            Destroy(gameObject);
        }
    }

    private void CreatePiece(string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject pieceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pieceObject.name = name;
        pieceObject.transform.SetParent(transform, false);
        pieceObject.transform.localPosition = localPosition;
        pieceObject.transform.localScale = localScale;
        Collider pieceCollider = pieceObject.GetComponent<Collider>();
        if (pieceCollider != null)
        {
            Destroy(pieceCollider);
        }
        Material material = SkillVisuals.CreateTintedParticleMaterial(color);
        if (material != null)
        {
            pieceObject.GetComponent<Renderer>().material = material;
            materials.Add(material);
        }
    }

    private void OnDestroy()
    {
        foreach (Material material in materials)
        {
            Destroy(material);
        }
    }
}

public sealed class FlameRibbon : MonoBehaviour
{
    private readonly List<Vector3> points = new List<Vector3>();
    private LineRenderer lineRenderer;
    private LineRenderer glowRenderer;
    private Material material;
    private Material glowMaterial;
    private float fadeEndTime;
    private float fadeDuration;
    private bool isFinishing;

    public static FlameRibbon Create()
    {
        GameObject ribbonObject = new GameObject("SKILL_ContinuousFlameRibbon");
        return ribbonObject.AddComponent<FlameRibbon>();
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    public void AddPoint(Vector3 point)
    {
        EnsureInitialized();
        if (isFinishing)
        {
            return;
        }
        if (points.Count > 0 && Vector3.Distance(points[points.Count - 1], point) < 0.45f)
        {
            return;
        }
        points.Add(point + Vector3.up * 0.08f);
        if (points.Count > 32)
        {
            points.RemoveAt(0);
        }
        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPositions(points.ToArray());
        glowRenderer.positionCount = points.Count;
        glowRenderer.SetPositions(points.ToArray());
    }

    public void Finish(float fadeDuration)
    {
        EnsureInitialized();
        isFinishing = true;
        this.fadeDuration = Mathf.Max(0.01f, fadeDuration);
        fadeEndTime = Time.time + this.fadeDuration;
    }

    private void Update()
    {
        EnsureInitialized();
        if (material != null)
        {
            material.mainTextureOffset = new Vector2(-Time.time * 3.8f, 0f);
        }
        if (glowMaterial != null)
        {
            glowMaterial.mainTextureOffset = new Vector2(-Time.time * 1.6f, 0f);
        }
        if (!isFinishing)
        {
            return;
        }
        float remaining = Mathf.Clamp01((fadeEndTime - Time.time) / fadeDuration);
        SetColor(remaining);
        if (Time.time >= fadeEndTime)
        {
            Destroy(gameObject);
        }
    }

    private void EnsureInitialized()
    {
        if (lineRenderer != null && glowRenderer != null && material != null && glowMaterial != null)
        {
            return;
        }

        if (lineRenderer == null || glowRenderer == null)
        {
            LineRenderer[] renderers = GetComponents<LineRenderer>();
            foreach (LineRenderer existingRenderer in renderers)
            {
                if (existingRenderer.sortingOrder == 2 && lineRenderer == null)
                {
                    lineRenderer = existingRenderer;
                }
                else if (existingRenderer.sortingOrder == 0 && glowRenderer == null)
                {
                    glowRenderer = existingRenderer;
                }
            }
        }

        if (glowRenderer == null)
        {
            glowRenderer = gameObject.AddComponent<LineRenderer>();
        }
        ConfigureLineRenderer(glowRenderer, 0);
        glowRenderer.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.9f),
            new Keyframe(0.2f, 1.65f),
            new Keyframe(1f, 0.65f));

        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }
        ConfigureLineRenderer(lineRenderer, 2);
        lineRenderer.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.2f, 1f),
            new Keyframe(1f, 0.25f));

        if (glowMaterial == null)
        {
            glowMaterial = glowRenderer.sharedMaterial;
            if (glowMaterial == null)
            {
                glowMaterial = SkillVisuals.CreateTintedParticleMaterial(new Color(1f, 0.08f, 0.01f, 0.42f));
                if (glowMaterial != null)
                {
                    glowRenderer.material = glowMaterial;
                }
            }
        }

        if (material == null)
        {
            material = lineRenderer.sharedMaterial;
            if (material == null)
            {
                material = SkillVisuals.CreateTintedParticleMaterial(new Color(1f, 0.26f, 0.02f, 0.9f));
                if (material != null)
                {
                    lineRenderer.material = material;
                }
            }
        }

        SetColor(1f);
    }

    private void SetColor(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.15f), 0f),
                new GradientColorKey(new Color(1f, 0.12f, 0.01f), 0.7f),
                new GradientColorKey(new Color(0.38f, 0.02f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(alpha, 0f),
                new GradientAlphaKey(alpha * 0.9f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        lineRenderer.colorGradient = gradient;
        Gradient glowGradient = new Gradient();
        glowGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.5f, 0.04f), 0f),
                new GradientColorKey(new Color(1f, 0.08f, 0.01f), 0.75f),
                new GradientColorKey(new Color(0.35f, 0.01f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(alpha * 0.3f, 0f),
                new GradientAlphaKey(alpha * 0.18f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        glowRenderer.colorGradient = glowGradient;
    }

    private static void ConfigureLineRenderer(LineRenderer renderer, int sortingOrder)
    {
        renderer.useWorldSpace = true;
        renderer.alignment = LineAlignment.View;
        renderer.textureMode = LineTextureMode.Tile;
        renderer.numCapVertices = 4;
        renderer.numCornerVertices = 4;
        renderer.sortingOrder = sortingOrder;
    }

    private void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
        if (glowMaterial != null)
        {
            Destroy(glowMaterial);
        }
    }
}

public sealed class TankMuzzleVfx : MonoBehaviour
{
    private readonly List<Material> materials = new List<Material>();
    private Transform barrel;
    private float endTime;

    public static void Spawn(Vector3 position, Vector3 direction)
    {
        GameObject muzzleObject = new GameObject("SKILL_TankMuzzle");
        muzzleObject.transform.position = position;
        muzzleObject.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        TankMuzzleVfx muzzle = muzzleObject.AddComponent<TankMuzzleVfx>();
        muzzle.Initialize();
    }

    private void Initialize()
    {
        endTime = Time.time + 0.46f;
        barrel = CreatePrimitive("DeployingBarrel", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.45f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.34f, 0.08f, 0.34f), new Color(0.12f, 0.13f, 0.16f, 1f));
        CreateMuzzleFlame();
        CreateMuzzleSmoke();
        SkillVisuals.CreatePointLight(transform, new Color(1f, 0.38f, 0.02f), 5f, 2.1f);
    }

    private void Update()
    {
        float progress = 1f - Mathf.Clamp01((endTime - Time.time) / 0.46f);
        float barrelLength = Mathf.Lerp(0.08f, 0.78f, Mathf.Sin(progress * Mathf.PI));
        barrel.localScale = new Vector3(0.34f, barrelLength, 0.34f);
        if (Time.time >= endTime)
        {
            Destroy(gameObject);
        }
    }

    private Transform CreatePrimitive(string name, PrimitiveType type, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Color color)
    {
        GameObject visualObject = GameObject.CreatePrimitive(type);
        visualObject.name = name;
        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localPosition = localPosition;
        visualObject.transform.localRotation = localRotation;
        visualObject.transform.localScale = localScale;
        Collider visualCollider = visualObject.GetComponent<Collider>();
        if (visualCollider != null)
        {
            Destroy(visualCollider);
        }
        Material material = visualObject.GetComponent<Renderer>().material;
        material.color = color;
        materials.Add(material);
        return visualObject.transform;
    }

    private void CreateMuzzleFlame()
    {
        ParticleSystem particles = SkillVisuals.CreateParticleSystem("MuzzleFlame", transform);
        particles.transform.localPosition = Vector3.forward * 1.05f;
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.16f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.55f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.22f, 0.01f), new Color(1f, 0.92f, 0.2f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 26;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)22) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.18f;
        shape.angle = 12f;
        SkillVisuals.ConfigureRenderer(particles, 7);
        particles.Play();
    }

    private void CreateMuzzleSmoke()
    {
        ParticleSystem particles = SkillVisuals.CreateParticleSystem("MuzzleSmoke", transform);
        particles.transform.localPosition = Vector3.forward * 0.38f;
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.35f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.58f);
        main.startColor = new Color(0.24f, 0.2f, 0.17f, 0.52f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 20;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)16) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.24f;
        shape.angle = 22f;
        SkillVisuals.ConfigureRenderer(particles, 4);
        particles.Play();
    }

    private void OnDestroy()
    {
        foreach (Material material in materials)
        {
            Destroy(material);
        }
    }
}
