using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public sealed class HealthPickup : MonoBehaviour
{
    [SerializeField] private float lifetime = 25f;
    [SerializeField] private float rotationSpeed = 48f;

    private Transform visualRoot;
    private Material caseMaterial;
    private Material crossMaterial;
    private float spawnTime;

    public static void SpawnAt(Vector3 position)
    {
        GameObject pickup = new GameObject("HealthPickup");
        pickup.transform.position = position + Vector3.up * 0.08f;

        SphereCollider trigger = pickup.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.45f;

        Rigidbody body = pickup.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;

        pickup.AddComponent<HealthPickup>();
    }

    private void Awake()
    {
        spawnTime = Time.time;
        CreateVisual();
    }

    private void Update()
    {
        if (Time.time - spawnTime >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float hover = 0.13f + Mathf.Sin(Time.time * 3.2f) * 0.1f;
        visualRoot.localPosition = Vector3.up * hover;
        visualRoot.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);
    }

    private void OnTriggerEnter(Collider other)
    {
        SimplePlayerHealth health = other.GetComponentInParent<SimplePlayerHealth>();
        if (health == null)
        {
            return;
        }

        health.Heal(1);
        Destroy(gameObject);
    }

    private void CreateVisual()
    {
        visualRoot = new GameObject("HealthPack_RedWhite").transform;
        visualRoot.SetParent(transform, false);

        caseMaterial = CreateMaterial(new Color(0.94f, 0.94f, 0.9f));
        crossMaterial = CreateMaterial(new Color(0.9f, 0.06f, 0.06f));

        CreateBox("Case", new Vector3(0f, 0.55f, 0f), new Vector3(1.3f, 0.8f, 0.9f), caseMaterial);
        CreateBox("RedBand", new Vector3(0f, 0.55f, 0f), new Vector3(1.33f, 0.16f, 0.93f), crossMaterial);
        CreateCross("FrontCross", 0.47f);
        CreateCross("BackCross", -0.47f);
        CreateBox("HandleTop", new Vector3(0f, 1.2f, 0f), new Vector3(0.58f, 0.12f, 0.22f), crossMaterial);
        CreateBox("HandleLeft", new Vector3(-0.24f, 1.1f, 0f), new Vector3(0.12f, 0.28f, 0.22f), crossMaterial);
        CreateBox("HandleRight", new Vector3(0.24f, 1.1f, 0f), new Vector3(0.12f, 0.28f, 0.22f), crossMaterial);
    }

    private void CreateCross(string namePrefix, float zPosition)
    {
        CreateBox(namePrefix + "Horizontal", new Vector3(0f, 0.55f, zPosition), new Vector3(0.6f, 0.16f, 0.06f), crossMaterial);
        CreateBox(namePrefix + "Vertical", new Vector3(0f, 0.55f, zPosition), new Vector3(0.16f, 0.6f, 0.06f), crossMaterial);
    }

    private void CreateBox(string objectName, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.SetParent(visualRoot, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = localScale;

        Collider boxCollider = box.GetComponent<Collider>();
        if (boxCollider != null)
        {
            Destroy(boxCollider);
        }

        Renderer boxRenderer = box.GetComponent<Renderer>();
        if (material != null)
        {
            boxRenderer.sharedMaterial = material;
        }
    }

    private static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    private void OnDestroy()
    {
        if (caseMaterial != null)
        {
            Destroy(caseMaterial);
        }
        if (crossMaterial != null)
        {
            Destroy(crossMaterial);
        }
    }
}
