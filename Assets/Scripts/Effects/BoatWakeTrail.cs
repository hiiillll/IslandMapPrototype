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
    private ParticleSystem leftSpray;
    private ParticleSystem rightSpray;
    private ParticleSystem bowSpray;

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
        ApplyVisibilityTuning();
        CreateWakeRenderer();
        CreateWaterSpray();
    }

    private void ApplyVisibilityTuning()
    {
        sternHalfWidth = Mathf.Max(sternHalfWidth, 0.5f);
        minimumSpeed = Mathf.Min(minimumSpeed, 1f);
        fullWakeSpeed = Mathf.Min(fullWakeSpeed, 14f);
        lifetime = Mathf.Max(lifetime, 2.4f);
        ribbonWidth = Mathf.Max(ribbonWidth, 0.85f);
        maximumDivergence = Mathf.Max(maximumDivergence, 2.5f);
        foamColor = new Color(
            Mathf.Max(foamColor.r, 0.9f),
            Mathf.Max(foamColor.g, 0.98f),
            Mathf.Max(foamColor.b, 1f),
            Mathf.Max(foamColor.a, 0.98f));
        minimumSpraySpeed = Mathf.Min(minimumSpraySpeed, 0.5f);
        fullSpraySpeed = Mathf.Min(fullSpraySpeed, 14f);
        maximumSprayRate = Mathf.Max(maximumSprayRate, 110f);
    }

    private void LateUpdate()
    {
        UpdateWakePoints();
        BuildWakeMesh();
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

        wakeTexture = CreateWakeTexture();
        wakeMaterial = new Material(wakeShader)
        {
            name = "BoatWakeTrailMaterial",
            mainTexture = wakeTexture,
            color = foamColor,
            renderQueue = 3000
        };
        meshRenderer.sharedMaterial = wakeMaterial;
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
            ? new ParticleSystem.MinMaxCurve(0.45f, 0.85f)
            : new ParticleSystem.MinMaxCurve(0.55f, 1.05f);
        main.startSpeed = isBowSplash
            ? new ParticleSystem.MinMaxCurve(2.2f, 4.8f)
            : new ParticleSystem.MinMaxCurve(2.5f, 5.8f);
        main.startSize = isBowSplash
            ? new ParticleSystem.MinMaxCurve(0.38f, 0.95f)
            : new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.72f, 0.94f, 1f, 0.8f),
            new Color(1f, 1f, 1f, 0.95f));
        main.gravityModifier = 0.35f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = isBowSplash ? 180 : 260;

        ParticleSystem.EmissionModule emission = spray.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = spray.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = isBowSplash ? 38f : 24f;
        shape.radius = isBowSplash ? 0.24f : 0.12f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = spray.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.62f, 0.9f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.9f, 0.08f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = colorGradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = spray.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.25f, 1f),
                new Keyframe(1f, 0.15f)));

        ParticleSystemRenderer sprayRenderer = spray.GetComponent<ParticleSystemRenderer>();
        sprayRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        sprayRenderer.sharedMaterial = wakeMaterial;
        sprayRenderer.sortingOrder = 2;

        spray.Play();
        return spray;
    }

    private void UpdateWaterSpray()
    {
        if (leftSpray == null || rightSpray == null || bowSpray == null)
        {
            return;
        }

        Vector3 planarVelocity = new Vector3(body.velocity.x, 0f, body.velocity.z);
        float sprayStrength = Mathf.InverseLerp(minimumSpraySpeed, fullSpraySpeed, planarVelocity.magnitude);
        float emissionRate = maximumSprayRate * sprayStrength;
        SetSprayRate(leftSpray, emissionRate);
        SetSprayRate(rightSpray, emissionRate);
        SetSprayRate(bowSpray, emissionRate * 0.65f);
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
            float opacity = Mathf.InverseLerp(0f, 0.1f, ageProgress)
                * (1f - ageProgress)
                * Mathf.Lerp(0.4f, 1f, speedProgress);
            Color vertexColor = new Color(foamColor.r, foamColor.g, foamColor.b, opacity * foamColor.a);

            Vector3 leftCenter = wakePoint.Position - wakePoint.Right * (sternHalfWidth + divergence);
            Vector3 rightCenter = wakePoint.Position + wakePoint.Right * (sternHalfWidth + divergence);

            vertices.Add(leftCenter + wakePoint.Right * halfRibbonWidth);
            vertices.Add(leftCenter - wakePoint.Right * halfRibbonWidth);
            vertices.Add(rightCenter - wakePoint.Right * halfRibbonWidth);
            vertices.Add(rightCenter + wakePoint.Right * halfRibbonWidth);

            uvs.Add(new Vector2(0f, ageProgress));
            uvs.Add(new Vector2(1f, ageProgress));
            uvs.Add(new Vector2(0f, ageProgress));
            uvs.Add(new Vector2(1f, ageProgress));

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
            wrapMode = TextureWrapMode.Clamp
        };

        for (int row = 0; row < textureHeight; row++)
        {
            for (int column = 0; column < textureWidth; column++)
            {
                float horizontalPosition = column / (textureWidth - 1f) * 2f - 1f;
                float edgeFade = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(horizontalPosition));
                float foamNoise = Mathf.PerlinNoise(column * 0.29f, row * 0.09f);
                float alpha = edgeFade * Mathf.Lerp(0.45f, 1f, foamNoise);
                texture.SetPixel(column, row, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        return texture;
    }
}
