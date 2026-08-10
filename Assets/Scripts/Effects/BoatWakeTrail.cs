using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class BoatWakeTrail : MonoBehaviour
{
    [Header("Water Surface")]
    [SerializeField] private float waterSurfaceY;
    [SerializeField, Min(0f)] private float surfaceOffset = 0.025f;

    [Header("Boat Stern")]
    [SerializeField, Min(0f)] private float sternOffset = 1.1f;
    [SerializeField, Min(0.01f)] private float sternHalfWidth = 0.5f;

    [Header("Wake Shape")]
    [SerializeField, Min(0.1f)] private float minimumSpeed = 1f;
    [SerializeField, Min(0.1f)] private float fullWakeSpeed = 14f;
    [SerializeField, Min(0.05f)] private float sampleSpacing = 0.22f;
    [SerializeField, Min(0.1f)] private float lifetime = 2.4f;
    [SerializeField, Min(0.01f)] private float ribbonWidth = 0.85f;
    [SerializeField, Min(0f)] private float maximumDivergence = 2.5f;
    [SerializeField] private Color foamColor = new Color(0.9f, 0.98f, 1f, 0.98f);

    [Header("Water Spray")]
    [SerializeField, Min(0f)] private float minimumSpraySpeed = 0.5f;
    [SerializeField, Min(0.1f)] private float fullSpraySpeed = 14f;
    [SerializeField, Min(0f)] private float maximumSprayRate = 110f;

    private readonly List<WakePoint> wakePoints = new List<WakePoint>();
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<Color> colors = new List<Color>();
    private readonly List<int> triangles = new List<int>();

    private Rigidbody body;
    private GameObject wakeObject;
    private Mesh wakeMesh;
    private Material wakeMaterial;
    private Texture2D wakeTexture;
    private Material sprayMaterial;
    private Texture2D sprayTexture;
    private ParticleSystem leftSpray;
    private ParticleSystem rightSpray;
    private ParticleSystem bowSpray;
    private ParticleSystem leftSurfaceFoam;
    private ParticleSystem rightSurfaceFoam;

    private struct WakePoint
    {
        public Vector3 Position;
        public Vector3 Right;
        public float SpawnTime;
        public float Speed;

        public WakePoint(Vector3 position, Vector3 right, float spawnTime, float speed)
        {
            Position = position;
            Right = right;
            SpawnTime = spawnTime;
            Speed = speed;
        }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        ApplyNaturalWakeTuning();
        CreateWakeRenderer();
        CreateWaterSpray();
    }

    private void ApplyNaturalWakeTuning()
    {
        sternHalfWidth = 0.52f;
        surfaceOffset = 0.085f;
        minimumSpeed = 2f;
        fullWakeSpeed = 18f;
        sampleSpacing = 0.3f;
        lifetime = 0.95f;
        ribbonWidth = 1.15f;
        maximumDivergence = 2.3f;
        foamColor = new Color(0.84f, 0.93f, 0.96f, 0.82f);
        minimumSpraySpeed = 4f;
        fullSpraySpeed = 20f;
        maximumSprayRate = 58f;
    }

    private void LateUpdate()
    {
        UpdateWaterSpray();
    }

    private void OnDisable()
    {
        wakePoints.Clear();
        if (wakeMesh != null)
        {
            wakeMesh.Clear();
        }
    }

    private void OnDestroy()
    {
        if (wakeObject != null)
        {
            Destroy(wakeObject);
        }
        if (wakeMesh != null)
        {
            Destroy(wakeMesh);
        }
        if (wakeMaterial != null)
        {
            Destroy(wakeMaterial);
        }
        if (wakeTexture != null)
        {
            Destroy(wakeTexture);
        }
        if (sprayMaterial != null)
        {
            Destroy(sprayMaterial);
        }
        if (sprayTexture != null)
        {
            Destroy(sprayTexture);
        }
    }

    private void CreateWakeRenderer()
    {
        Shader wakeShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (wakeShader == null)
        {
            wakeShader = Shader.Find("Particles/Standard Unlit");
        }
        if (wakeShader == null)
        {
            Debug.LogError("BoatWakeTrail requires a transparent particle shader.", this);
            enabled = false;
            return;
        }

        wakeObject = new GameObject("Boat Wake Trail");
        wakeObject.transform.position = Vector3.zero;
        wakeMesh = new Mesh
        {
            name = "BoatWakeTrailMesh"
        };
        wakeMesh.MarkDynamic();

        MeshFilter meshFilter = wakeObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = wakeMesh;
        MeshRenderer meshRenderer = wakeObject.AddComponent<MeshRenderer>();
        meshRenderer.sortingOrder = 1;
        meshRenderer.enabled = false;

        wakeTexture = CreateWakeTexture();
        wakeMaterial = new Material(wakeShader)
        {
            name = "BoatWakeTrailMaterial",
            mainTexture = wakeTexture,
            color = new Color(foamColor.r, foamColor.g, foamColor.b, 1f),
            renderQueue = 3000
        };
        meshRenderer.sharedMaterial = wakeMaterial;

        sprayTexture = CreateSprayTexture();
        sprayMaterial = new Material(wakeShader)
        {
            name = "BoatWaterSprayMaterial",
            mainTexture = sprayTexture,
            color = new Color(0.78f, 0.9f, 0.93f, 0.68f),
            renderQueue = 3000
        };
    }

    private void CreateWaterSpray()
    {
        if (wakeMaterial == null)
        {
            return;
        }

        leftSpray = CreateSprayEmitter(
            "VFX_WaterSpray_Left",
            new Vector3(-0.52f, 0.1f, -1f),
            new Vector3(-18f, 195f, -12f),
            false);
        rightSpray = CreateSprayEmitter(
            "VFX_WaterSpray_Right",
            new Vector3(0.52f, 0.1f, -1f),
            new Vector3(-18f, 165f, 12f),
            false);
        bowSpray = CreateSprayEmitter(
            "VFX_BowSplash",
            new Vector3(0f, 0.12f, 1.15f),
            new Vector3(-24f, 0f, 0f),
            true);
        leftSurfaceFoam = CreateSurfaceFoamEmitter(
            "VFX_SurfaceFoam_Left",
            new Vector3(-0.46f, surfaceOffset + 0.015f, -1.05f),
            new Vector3(-0.9f, 0f, -1f));
        rightSurfaceFoam = CreateSurfaceFoamEmitter(
            "VFX_SurfaceFoam_Right",
            new Vector3(0.46f, surfaceOffset + 0.015f, -1.05f),
            new Vector3(0.9f, 0f, -1f));
    }

    private ParticleSystem CreateSurfaceFoamEmitter(
        string name,
        Vector3 localPosition,
        Vector3 localDirection)
    {
        GameObject foamObject = new GameObject(name);
        foamObject.transform.SetParent(transform, false);
        foamObject.transform.localPosition = localPosition;
        foamObject.transform.localRotation = Quaternion.LookRotation(localDirection, Vector3.up);

        ParticleSystem foam = foamObject.AddComponent<ParticleSystem>();
        foam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = foam.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.52f, 1.08f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.72f, 0.86f, 0.9f, 0.55f),
            new Color(0.92f, 0.97f, 1f, 0.88f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 180;

        ParticleSystem.EmissionModule emission = foam.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = foam.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.24f;

        ParticleSystem.ColorOverLifetimeModule color = foam.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.9f, 0.96f, 1f), 0f),
                new GradientColorKey(new Color(0.62f, 0.78f, 0.84f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.88f, 0.12f),
                new GradientAlphaKey(0.46f, 0.62f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;

        ParticleSystem.SizeOverLifetimeModule size = foam.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.38f),
                new Keyframe(0.28f, 1f),
                new Keyframe(1f, 1.28f)));

        ParticleSystem.NoiseModule noise = foam.noise;
        noise.enabled = true;
        noise.strength = 0.18f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.1f;

        ParticleSystemRenderer renderer = foam.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        renderer.sharedMaterial = sprayMaterial;
        renderer.sortingOrder = 1;

        foam.Play();
        return foam;
    }

    private ParticleSystem CreateSprayEmitter(
        string name,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        bool isBowSplash)
    {
        GameObject sprayObject = new GameObject(name);
        sprayObject.transform.SetParent(transform, false);
        sprayObject.transform.localPosition = localPosition;
        sprayObject.transform.localRotation = Quaternion.Euler(localEulerAngles);

        ParticleSystem spray = sprayObject.AddComponent<ParticleSystem>();
        spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = spray.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = isBowSplash
            ? new ParticleSystem.MinMaxCurve(0.32f, 0.58f)
            : new ParticleSystem.MinMaxCurve(0.42f, 0.8f);
        main.startSpeed = isBowSplash
            ? new ParticleSystem.MinMaxCurve(1.4f, 3.2f)
            : new ParticleSystem.MinMaxCurve(1.8f, 4.2f);
        main.startSize = isBowSplash
            ? new ParticleSystem.MinMaxCurve(0.22f, 0.58f)
            : new ParticleSystem.MinMaxCurve(0.2f, 0.56f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.62f, 0.8f, 0.86f, 0.42f),
            new Color(0.9f, 0.96f, 1f, 0.72f));
        main.gravityModifier = 0.48f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = isBowSplash ? 180 : 260;

        ParticleSystem.EmissionModule emission = spray.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = spray.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = isBowSplash ? 42f : 31f;
        shape.radius = isBowSplash ? 0.2f : 0.15f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = spray.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.88f, 0.95f, 1f), 0f),
                new GradientColorKey(new Color(0.58f, 0.76f, 0.82f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.62f, 0.12f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = colorGradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = spray.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.2f),
                new Keyframe(0.3f, 1f),
                new Keyframe(1f, 0.35f)));

        ParticleSystemRenderer sprayRenderer = spray.GetComponent<ParticleSystemRenderer>();
        sprayRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        sprayRenderer.sharedMaterial = sprayMaterial;
        sprayRenderer.sortingOrder = 2;

        spray.Play();
        return spray;
    }

    private void UpdateWaterSpray()
    {
        if (leftSpray == null
            || rightSpray == null
            || bowSpray == null
            || leftSurfaceFoam == null
            || rightSurfaceFoam == null)
        {
            return;
        }

        Vector3 planarVelocity = new Vector3(body.velocity.x, 0f, body.velocity.z);
        float sprayStrength = Mathf.InverseLerp(minimumSpraySpeed, fullSpraySpeed, planarVelocity.magnitude);
        float emissionRate = maximumSprayRate * sprayStrength;
        SetSprayRate(leftSpray, emissionRate);
        SetSprayRate(rightSpray, emissionRate);
        SetSprayRate(bowSpray, emissionRate * 0.4f);
        SetSprayRate(leftSurfaceFoam, emissionRate * 0.9f);
        SetSprayRate(rightSurfaceFoam, emissionRate * 0.9f);
    }

    private static void SetSprayRate(ParticleSystem spray, float emissionRate)
    {
        ParticleSystem.EmissionModule emission = spray.emission;
        emission.rateOverTime = emissionRate;
    }

    private void UpdateWakePoints()
    {
        float currentTime = Time.time;
        while (wakePoints.Count > 0 && currentTime - wakePoints[0].SpawnTime > lifetime)
        {
            wakePoints.RemoveAt(0);
        }

        Vector3 planarVelocity = new Vector3(body.velocity.x, 0f, body.velocity.z);
        float currentSpeed = planarVelocity.magnitude;
        if (currentSpeed < minimumSpeed)
        {
            return;
        }

        Vector3 sternPosition = transform.TransformPoint(new Vector3(0f, 0f, -sternOffset));
        sternPosition.y = waterSurfaceY + surfaceOffset;
        if (wakePoints.Count > 0
            && Vector3.Distance(wakePoints[wakePoints.Count - 1].Position, sternPosition) < sampleSpacing)
        {
            return;
        }

        wakePoints.Add(new WakePoint(sternPosition, transform.right, currentTime, currentSpeed));
    }

    private void BuildWakeMesh()
    {
        if (wakeMesh == null)
        {
            return;
        }

        wakeMesh.Clear();
        if (wakePoints.Count < 2)
        {
            return;
        }

        vertices.Clear();
        uvs.Clear();
        colors.Clear();
        triangles.Clear();

        for (int pointIndex = 0; pointIndex < wakePoints.Count; pointIndex++)
        {
            WakePoint wakePoint = wakePoints[pointIndex];
            float ageProgress = Mathf.Clamp01((Time.time - wakePoint.SpawnTime) / lifetime);
            float speedProgress = Mathf.InverseLerp(minimumSpeed, fullWakeSpeed, wakePoint.Speed);
            float divergence = Mathf.Lerp(0f, maximumDivergence, ageProgress) * speedProgress;
            float halfRibbonWidth = Mathf.Lerp(ribbonWidth * 0.3f, ribbonWidth, ageProgress) * 0.5f;
            float opacity = Mathf.InverseLerp(0f, 0.12f, ageProgress)
                * (1f - ageProgress)
                * Mathf.Lerp(0.4f, 1f, speedProgress);
            Color vertexColor = new Color(foamColor.r, foamColor.g, foamColor.b, opacity * foamColor.a);

            float leftVariation = Mathf.Lerp(
                0.78f,
                1.16f,
                Mathf.PerlinNoise(wakePoint.Position.x * 0.11f, wakePoint.Position.z * 0.11f));
            float rightVariation = Mathf.Lerp(
                0.8f,
                1.14f,
                Mathf.PerlinNoise(wakePoint.Position.z * 0.13f + 8f, wakePoint.Position.x * 0.13f));

            Vector3 leftCenter = wakePoint.Position - wakePoint.Right * (sternHalfWidth + divergence);
            Vector3 rightCenter = wakePoint.Position + wakePoint.Right * (sternHalfWidth + divergence);

            vertices.Add(leftCenter + wakePoint.Right * halfRibbonWidth * leftVariation);
            vertices.Add(leftCenter - wakePoint.Right * halfRibbonWidth * leftVariation);
            vertices.Add(rightCenter - wakePoint.Right * halfRibbonWidth * rightVariation);
            vertices.Add(rightCenter + wakePoint.Right * halfRibbonWidth * rightVariation);

            float textureV = wakePoint.SpawnTime * 1.7f;
            uvs.Add(new Vector2(0f, textureV));
            uvs.Add(new Vector2(1f, textureV));
            uvs.Add(new Vector2(0f, textureV));
            uvs.Add(new Vector2(1f, textureV));

            colors.Add(vertexColor);
            colors.Add(vertexColor);
            colors.Add(vertexColor);
            colors.Add(vertexColor);
        }

        for (int pointIndex = 0; pointIndex < wakePoints.Count - 1; pointIndex++)
        {
            int currentVertex = pointIndex * 4;
            int nextVertex = currentVertex + 4;

            triangles.Add(currentVertex);
            triangles.Add(nextVertex);
            triangles.Add(currentVertex + 1);
            triangles.Add(currentVertex + 1);
            triangles.Add(nextVertex);
            triangles.Add(nextVertex + 1);

            triangles.Add(currentVertex + 2);
            triangles.Add(currentVertex + 3);
            triangles.Add(nextVertex + 2);
            triangles.Add(currentVertex + 3);
            triangles.Add(nextVertex + 3);
            triangles.Add(nextVertex + 2);
        }

        wakeMesh.SetVertices(vertices);
        wakeMesh.SetUVs(0, uvs);
        wakeMesh.SetColors(colors);
        wakeMesh.SetTriangles(triangles, 0, true);
        wakeMesh.RecalculateBounds();
    }

    private static Texture2D CreateWakeTexture()
    {
        const int textureWidth = 32;
        const int textureHeight = 128;
        Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            name = "BoatWakeTrailTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat
        };

        for (int row = 0; row < textureHeight; row++)
        {
            for (int column = 0; column < textureWidth; column++)
            {
                float horizontalPosition = column / (textureWidth - 1f) * 2f - 1f;
                float edgeFade = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(horizontalPosition));
                float foamNoise = Mathf.PerlinNoise(column * 0.31f, row * 0.12f);
                float longitudinalBreakup = Mathf.PerlinNoise(row * 0.17f, column * 0.08f + 13f);
                float breakupMask = Mathf.SmoothStep(
                    0.38f,
                    0.7f,
                    longitudinalBreakup * 0.72f + foamNoise * 0.28f);
                float alpha = edgeFade
                    * Mathf.Lerp(0.45f, 1f, foamNoise)
                    * breakupMask
                    * 0.88f;
                texture.SetPixel(column, row, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateSprayTexture()
    {
        const int textureSize = 32;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "BoatWaterSprayTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int row = 0; row < textureSize; row++)
        {
            for (int column = 0; column < textureSize; column++)
            {
                Vector2 position = new Vector2(column, row) / (textureSize - 1f) * 2f - Vector2.one;
                float radialFade = Mathf.SmoothStep(1f, 0f, position.magnitude);
                float breakup = Mathf.PerlinNoise(column * 0.21f + 4f, row * 0.21f + 9f);
                float alpha = radialFade * radialFade * Mathf.Lerp(0.45f, 1f, breakup);
                texture.SetPixel(column, row, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        return texture;
    }
}
