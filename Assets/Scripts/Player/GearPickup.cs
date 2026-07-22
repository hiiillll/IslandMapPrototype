using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class GearPickup : MonoBehaviour
{
    private const int ExperienceValue = 20;
    private const float Lifetime = 25f;
    private const float AutoPickupRadius = 3f;

    private static Material goldMaterial;
    private static Material darkMaterial;
    private static Material particleMaterial;
    private static PhysicMaterial bounceMaterial;

    private Transform visualRoot;
    private float spawnTime;
    private float baseVisualHeight;
    private bool collected;

    public static void SpawnAt(Vector3 position)
    {
        EnsureMaterials();
        GameObject pickupObject = new GameObject("PICKUP_ExperienceGear");
        pickupObject.transform.position = position + Vector3.up * 0.7f;

        Rigidbody pickupBody = pickupObject.AddComponent<Rigidbody>();
        pickupBody.mass = 0.28f;
        pickupBody.drag = 0.15f;
        pickupBody.angularDrag = 0.3f;
        pickupBody.interpolation = RigidbodyInterpolation.Interpolate;
        pickupBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        pickupBody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        Vector2 launchDirection = Random.insideUnitCircle.normalized;
        pickupBody.velocity = new Vector3(launchDirection.x * 2.2f, 4.5f, launchDirection.y * 2.2f);

        SphereCollider solidCollider = pickupObject.AddComponent<SphereCollider>();
        solidCollider.radius = 0.42f;
        solidCollider.material = bounceMaterial;

        GameObject triggerObject = new GameObject("PickupTrigger");
        triggerObject.transform.SetParent(pickupObject.transform, false);
        SphereCollider pickupTrigger = triggerObject.AddComponent<SphereCollider>();
        pickupTrigger.isTrigger = true;
        pickupTrigger.radius = AutoPickupRadius;

        pickupObject.AddComponent<GearPickup>();
    }

    private void Awake()
    {
        spawnTime = Time.time;
        EnsureMaterials();
        BuildVisual();
    }

    private void Update()
    {
        if (Time.time - spawnTime >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (visualRoot != null)
        {
            visualRoot.Rotate(0f, 0f, 150f * Time.deltaTime, Space.Self);
            Vector3 localPosition = visualRoot.localPosition;
            localPosition.y = baseVisualHeight + Mathf.Sin((Time.time - spawnTime) * 5f) * 0.06f;
            visualRoot.localPosition = localPosition;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
        {
            return;
        }

        PlayerProgression progression = other.GetComponentInParent<PlayerProgression>();
        if (progression == null)
        {
            return;
        }

        CollectExperience(progression);
    }

    private void CollectExperience(PlayerProgression progression)
    {
        if (collected || progression == null)
        {
            return;
        }

        collected = true;
        progression.AddExperience(ExperienceValue);
        SpawnCollectEffect(transform.position);
        Destroy(gameObject);
    }

    private void BuildVisual()
    {
        visualRoot = new GameObject("GearVisual").transform;
        visualRoot.SetParent(transform, false);
        visualRoot.localPosition = Vector3.up * 0.55f;
        baseVisualHeight = visualRoot.localPosition.y;

        GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hub.name = "GoldHub";
        hub.transform.SetParent(visualRoot, false);
        hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        hub.transform.localScale = new Vector3(0.9f, 0.12f, 0.9f);
        hub.GetComponent<Renderer>().sharedMaterial = goldMaterial;
        RemoveCollider(hub);

        const int toothCount = 12;
        const float toothRadius = 0.54f;
        for (int toothIndex = 0; toothIndex < toothCount; toothIndex++)
        {
            float angle = toothIndex * 360f / toothCount;
            float radians = angle * Mathf.Deg2Rad;
            GameObject tooth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tooth.name = $"GearTooth_{toothIndex:00}";
            tooth.transform.SetParent(visualRoot, false);
            tooth.transform.localPosition = new Vector3(
                Mathf.Cos(radians) * toothRadius,
                Mathf.Sin(radians) * toothRadius,
                0f);
            tooth.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            tooth.transform.localScale = new Vector3(0.3f, 0.2f, 0.24f);
            tooth.GetComponent<Renderer>().sharedMaterial = goldMaterial;
            RemoveCollider(tooth);
        }

        GameObject center = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        center.name = "DarkCenter";
        center.transform.SetParent(visualRoot, false);
        center.transform.localPosition = new Vector3(0f, 0f, -0.13f);
        center.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        center.transform.localScale = new Vector3(0.31f, 0.03f, 0.31f);
        center.GetComponent<Renderer>().sharedMaterial = darkMaterial;
        RemoveCollider(center);

        Light glow = visualRoot.gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.55f, 0.08f);
        glow.range = 3.2f;
        glow.intensity = 1.1f;
        glow.shadows = LightShadows.None;
    }

    private static void EnsureMaterials()
    {
        Shader shader = Shader.Find("Standard");
        if (goldMaterial == null)
        {
            goldMaterial = new Material(shader)
            {
                color = new Color(1f, 0.48f, 0.04f),
                name = "MAT_ExperienceGear_Gold"
            };
            goldMaterial.SetFloat("_Metallic", 0.72f);
            goldMaterial.SetFloat("_Glossiness", 0.82f);
            goldMaterial.EnableKeyword("_EMISSION");
            goldMaterial.SetColor("_EmissionColor", new Color(0.32f, 0.08f, 0.005f));
        }

        if (darkMaterial == null)
        {
            darkMaterial = new Material(shader)
            {
                color = new Color(0.06f, 0.07f, 0.08f),
                name = "MAT_ExperienceGear_Center"
            };
            darkMaterial.SetFloat("_Metallic", 0.45f);
            darkMaterial.SetFloat("_Glossiness", 0.55f);
        }

        if (particleMaterial == null)
        {
            Shader particleShader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (particleShader == null)
            {
                particleShader = shader;
            }
            particleMaterial = new Material(particleShader)
            {
                color = new Color(1f, 0.55f, 0.05f),
                name = "MAT_ExperienceGear_Particles"
            };
        }

        if (bounceMaterial == null)
        {
            bounceMaterial = new PhysicMaterial("GearPickup_Bounce")
            {
                bounciness = 0.25f,
                dynamicFriction = 0.35f,
                staticFriction = 0.45f,
                bounceCombine = PhysicMaterialCombine.Maximum
            };
        }
    }

    private static void SpawnCollectEffect(Vector3 position)
    {
        GameObject effectObject = new GameObject("VFX_GearCollected");
        effectObject.transform.position = position + Vector3.up * 0.5f;
        ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        main.duration = 0.45f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.35f, 0.02f),
            new Color(1f, 0.9f, 0.18f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = particleMaterial;
        particles.Play();
        Destroy(effectObject, 1.2f);
    }

    private static void RemoveCollider(GameObject visualObject)
    {
        Collider visualCollider = visualObject.GetComponent<Collider>();
        if (visualCollider != null)
        {
            Destroy(visualCollider);
        }
    }
}
