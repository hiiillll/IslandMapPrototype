using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class BoatChaseController : MonoBehaviour
{
    private const float OceanHalfSize = 2000f;

    [Header("Automatic Propulsion")]
    [SerializeField, Min(0f)] private float forwardSpeed = 24f;
    [SerializeField, Min(0f)] private float forwardAcceleration = 7.5f;
    [SerializeField, Min(0f)] private float forwardDeceleration = 10f;
    [SerializeField, Min(0f)] private float speedErrorResponse = 2.2f;

    [Header("Water Handling")]
    [SerializeField, Min(0f)] private float lateralWaterResistance = 3f;
    [SerializeField, Min(0f)] private float maximumTurnRate = 300f;
    [SerializeField, Min(0f)] private float turnAcceleration = 420f;
    [SerializeField, Min(0f)] private float turnResponse = 7f;
    [SerializeField, Min(0f)] private float minimumSteeringSpeed = 1.5f;
    [SerializeField] private bool immediateSteering = true;

    private Rigidbody body;
    private SimplePlayerHealth health;
    private BoatChaseDifficultyController difficultyController;
    private float steeringInput;

    public float CurrentForwardSpeed { get; private set; }
    public float MaximumTurnRate => maximumTurnRate;
    public float TurnAcceleration => turnAcceleration;
    public float TurnResponse => turnResponse;
    public float MinimumSteeringSpeed => minimumSteeringSpeed;
    public bool ImmediateSteering => immediateSteering;

    public void ConfigureSteering(
        float newMaximumTurnRate,
        float newTurnAcceleration,
        float newTurnResponse,
        float newMinimumSteeringSpeed = 1.5f,
        bool useImmediateSteering = true)
    {
        maximumTurnRate = Mathf.Max(0f, newMaximumTurnRate);
        turnAcceleration = Mathf.Max(0f, newTurnAcceleration);
        turnResponse = Mathf.Max(0f, newTurnResponse);
        minimumSteeringSpeed = Mathf.Max(0f, newMinimumSteeringSpeed);
        immediateSteering = useImmediateSteering;
        ApplyAngularVelocityLimit();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        health = GetComponent<SimplePlayerHealth>();
        difficultyController = FindObjectOfType<BoatChaseDifficultyController>();
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezePositionY
            | RigidbodyConstraints.FreezeRotationX
            | RigidbodyConstraints.FreezeRotationZ;
        body.drag = 0f;
        body.angularDrag = 0.2f;
        ApplyAngularVelocityLimit();
        EnsureOceanBoundaries();
    }

    private void Start()
    {
        CurrentForwardSpeed = GetTargetForwardSpeed();
        body.velocity = transform.forward * CurrentForwardSpeed;
    }

    private void Update()
    {
        steeringInput = Input.GetAxis("Horizontal");
    }

    private void FixedUpdate()
    {
        if (health != null && health.CurrentHealth <= 0)
        {
            CurrentForwardSpeed = 0f;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            return;
        }

        if (difficultyController == null)
        {
            difficultyController = FindObjectOfType<BoatChaseDifficultyController>();
        }

        float targetForwardSpeed = GetTargetForwardSpeed();
        Vector3 planarVelocity = new Vector3(body.velocity.x, 0f, body.velocity.z);
        float currentForwardSpeed = Vector3.Dot(planarVelocity, transform.forward);
        if (immediateSteering)
        {
            float turnDegrees = steeringInput * maximumTurnRate * Time.fixedDeltaTime;
            Quaternion nextRotation = body.rotation * Quaternion.Euler(0f, turnDegrees, 0f);
            body.MoveRotation(nextRotation);
            body.velocity = nextRotation * Vector3.forward * targetForwardSpeed;
            body.angularVelocity = Vector3.zero;
            CurrentForwardSpeed = targetForwardSpeed;
            return;
        }

        float speedError = targetForwardSpeed - currentForwardSpeed;
        float accelerationLimit = speedError >= 0f ? forwardAcceleration : forwardDeceleration;
        float propulsionAcceleration = Mathf.Clamp(
            speedError * speedErrorResponse,
            -forwardDeceleration,
            accelerationLimit);
        body.AddForce(transform.forward * propulsionAcceleration, ForceMode.Acceleration);

        // Water resists sideways motion without deleting it instantly. This lets the stern
        // slide outward during a turn and then settle back behind the bow.
        float lateralSpeed = Vector3.Dot(planarVelocity, transform.right);
        body.AddForce(
            -transform.right * lateralSpeed * lateralWaterResistance,
            ForceMode.Acceleration);

        float steeringStrength = Mathf.InverseLerp(
            minimumSteeringSpeed,
            Mathf.Max(minimumSteeringSpeed + 0.01f, targetForwardSpeed * 0.7f),
            Mathf.Abs(currentForwardSpeed));
        float targetYawRate = steeringInput * maximumTurnRate * steeringStrength;
        float currentYawRate = body.angularVelocity.y * Mathf.Rad2Deg;
        float yawAcceleration = Mathf.Clamp(
            (targetYawRate - currentYawRate) * turnResponse,
            -turnAcceleration,
            turnAcceleration);
        body.AddTorque(Vector3.up * yawAcceleration * Mathf.Deg2Rad, ForceMode.Acceleration);

        CurrentForwardSpeed = Mathf.Max(0f, currentForwardSpeed);
    }

    private float GetTargetForwardSpeed()
    {
        return difficultyController != null
            ? difficultyController.GetPlayerForwardSpeed()
            : forwardSpeed;
    }

    private void ApplyAngularVelocityLimit()
    {
        if (body != null)
        {
            body.maxAngularVelocity = Mathf.Max(2.5f, maximumTurnRate * Mathf.Deg2Rad);
        }
    }

    private static void EnsureOceanBoundaries()
    {
        if (GameObject.Find("COL_OceanBoundary") != null)
        {
            return;
        }

        GameObject root = new GameObject("COL_OceanBoundary");
        CreateBoundaryWall(root.transform, "North", new Vector3(0f, 0f, OceanHalfSize + 5f), new Vector3(4020f, 100f, 10f));
        CreateBoundaryWall(root.transform, "South", new Vector3(0f, 0f, -OceanHalfSize - 5f), new Vector3(4020f, 100f, 10f));
        CreateBoundaryWall(root.transform, "East", new Vector3(OceanHalfSize + 5f, 0f, 0f), new Vector3(10f, 100f, 4020f));
        CreateBoundaryWall(root.transform, "West", new Vector3(-OceanHalfSize - 5f, 0f, 0f), new Vector3(10f, 100f, 4020f));
    }

    private static void CreateBoundaryWall(Transform parent, string wallName, Vector3 position, Vector3 size)
    {
        GameObject wall = new GameObject(wallName);
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        BoxCollider collider = wall.AddComponent<BoxCollider>();
        collider.size = size;
    }
}
