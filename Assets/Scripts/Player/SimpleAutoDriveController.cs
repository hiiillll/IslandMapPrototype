using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SimpleAutoDriveController : MonoBehaviour
{
    private const float LandPhysicsTimestep = 1f / 60f;

    [SerializeField] private float forwardSpeed = 24f;
    [SerializeField] private float turnSpeed = 3.6f;
    [SerializeField] private float inputSmoothing = 24f;
    [SerializeField] private float acceleration = 14f;
    [SerializeField] private float angularAcceleration = 14f;
    [SerializeField] private float lateralGrip = 6f;

    [Header("Arcade Handbrake Drift")]
    [SerializeField] private KeyCode driftKey = KeyCode.Space;
    [SerializeField, Range(0f, 1f)] private float minimumDriftSpeedRatio = 0.3f;
    [SerializeField, Range(0f, 89f)] private float maximumDriftAngle = 40f;
    [SerializeField, Min(0f)] private float driftAngleLimitResponse = 8f;
    [SerializeField, Range(0f, 1f)] private float driftGripMultiplier = 0.35f;
    [SerializeField, Min(1f)] private float driftTurnMultiplier = 1.28f;
    [SerializeField, Range(0f, 1f)] private float driftSpeedLossPerSecond = 0.045f;
    [SerializeField, Min(0f)] private float driftVelocitySteerRate = 85f;
    [SerializeField, Range(0f, 89f)] private float maximumControlledSlipAngle = 35f;
    [SerializeField, Range(0f, 89f)] private float counterSteerSlipAngle = 10f;
    [SerializeField, Min(0f)] private float driftSlipAngleResponse = 90f;
    [SerializeField, Min(0.01f)] private float gripRecoveryTime = 0.4f;
    [SerializeField, Min(1f)] private float counterSteerGripMultiplier = 1.5f;
    [SerializeField, Min(0f)] private float groundCheckDistance = 0.45f;
    [SerializeField] private LayerMask groundLayers = ~0;

    private Rigidbody body;
    private float turnInput;
    private float smoothedTurnInput;
    private float currentTurnRate;
    private bool driftHeld;
    private bool isDrifting;
    private bool isGrounded;
    private float driftDirection;
    private float driftTargetSpeed;
    private float gripRecoveryProgress = 1f;
    private float currentGripMultiplier = 1f;
    private float currentDriftAngle;
    private PhysicMaterial frictionlessMaterial;
    private float previousFixedDeltaTime;
    private Collider[] vehicleColliders;
    private readonly RaycastHit[] groundHits = new RaycastHit[8];

    public float ForwardSpeed => forwardSpeed;
    public float SteeringAmount => Mathf.Abs(smoothedTurnInput);
    public bool IsDrifting => isDrifting;
    public bool IsGrounded => isGrounded;
    public float CurrentDriftAngle => currentDriftAngle;

    private void Awake()
    {
        previousFixedDeltaTime = Time.fixedDeltaTime;
        Time.fixedDeltaTime = LandPhysicsTimestep;
        body = GetComponent<Rigidbody>();
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        body.drag = 0f;
        body.angularDrag = 0f;
        body.maxAngularVelocity = 10f;

        forwardSpeed = 24f;
        turnSpeed = 3.6f;
        inputSmoothing = 24f;
        acceleration = 14f;
        angularAcceleration = 14f;
        lateralGrip = 6f;
        ConfigureFrictionlessColliders();
        vehicleColliders = GetComponentsInChildren<Collider>(true);
        SimplePlayerHealth health = GetComponent<SimplePlayerHealth>();
        if (health == null)
        {
            health = gameObject.AddComponent<SimplePlayerHealth>();
        }
        health.ConfigureFallDefeat(true, -10f);
        if (GetComponent<VehicleGarageSystem>() == null)
        {
            gameObject.AddComponent<VehicleGarageSystem>();
        }
        if (GetComponent<PlayerSkillSystem>() == null)
        {
            gameObject.AddComponent<PlayerSkillSystem>();
        }
        if (GetComponent<PlayerProgression>() == null)
        {
            gameObject.AddComponent<PlayerProgression>();
        }
        if (GetComponent<ArcadeGameHud>() == null)
        {
            gameObject.AddComponent<ArcadeGameHud>();
        }
    }

    private void Start()
    {
        body.rotation = Quaternion.identity;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (VehicleGarageSystem.Instance != null && VehicleGarageSystem.Instance.IsOpen)
            {
                return;
            }
            if (GameModeSession.IsEndless)
            {
                return;
            }
            QuitGame();
            return;
        }

        bool steerLeft = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool steerRight = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        turnInput = steerLeft == steerRight ? 0f : steerLeft ? -1f : 1f;
        driftHeld = Input.GetKey(driftKey);
        smoothedTurnInput = Mathf.MoveTowards(
            smoothedTurnInput,
            turnInput,
            inputSmoothing * Time.deltaTime);
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void FixedUpdate()
    {
        body.WakeUp();
        Vector3 planarVelocity = new Vector3(body.velocity.x, 0f, body.velocity.z);
        isGrounded = CheckGrounded();
        UpdateDriftState(planarVelocity.magnitude);

        float turnMultiplier = isDrifting ? driftTurnMultiplier : 1f;
        float targetTurnRate = smoothedTurnInput * turnSpeed * turnMultiplier;
        currentTurnRate = Mathf.MoveTowards(
            currentTurnRate,
            targetTurnRate,
            angularAcceleration * turnMultiplier * Time.fixedDeltaTime);
        float turnDegrees = currentTurnRate * Mathf.Rad2Deg * Time.fixedDeltaTime;
        Vector3 currentForward = body.rotation * Vector3.forward;
        Vector3 currentRight = body.rotation * Vector3.right;
        Quaternion nextRotation = body.rotation * Quaternion.Euler(0f, turnDegrees, 0f);
        Vector3 nextForward = nextRotation * Vector3.forward;
        Vector3 nextRight = nextRotation * Vector3.right;

        if (isDrifting)
        {
            float currentSpeed = planarVelocity.magnitude;
            float nextSpeed = Mathf.MoveTowards(
                currentSpeed,
                driftTargetSpeed,
                acceleration * Time.fixedDeltaTime);
            Vector3 momentumDirection = currentSpeed > 0.01f
                ? planarVelocity / currentSpeed
                : nextForward;
            planarVelocity = momentumDirection * nextSpeed;
            planarVelocity = ApplyDriftTrajectoryAssist(planarVelocity);
        }
        else
        {
            Vector3 targetVelocity = currentForward * forwardSpeed;
            planarVelocity = Vector3.Lerp(
                planarVelocity,
                targetVelocity,
                acceleration * currentGripMultiplier * Time.fixedDeltaTime);
        }

        Vector3 gripRight = isDrifting ? nextRight : currentRight;
        float lateralSpeed = Vector3.Dot(planarVelocity, gripRight);
        planarVelocity -= gripRight
            * lateralSpeed
            * lateralGrip
            * currentGripMultiplier
            * Time.fixedDeltaTime;
        if (isDrifting)
        {
            planarVelocity = StabilizeDriftSlipAngle(planarVelocity, nextForward);
        }
        else
        {
            currentDriftAngle = 0f;
        }

        body.velocity = new Vector3(planarVelocity.x, body.velocity.y, planarVelocity.z);
        body.MoveRotation(nextRotation);
        body.angularVelocity = Vector3.zero;
    }

    private void UpdateDriftState(float planarSpeed)
    {
        float minimumDriftSpeed = forwardSpeed * minimumDriftSpeedRatio;
        bool shouldDrift = !GameModeSession.IsEndlessSea
            && isGrounded
            && driftHeld
            && Mathf.Abs(smoothedTurnInput) > 0.1f
            && planarSpeed >= minimumDriftSpeed;
        if (shouldDrift && !isDrifting)
        {
            isDrifting = true;
            driftDirection = Mathf.Sign(smoothedTurnInput);
            driftTargetSpeed = planarSpeed;
            gripRecoveryProgress = 0f;
        }
        else if (!shouldDrift && isDrifting)
        {
            isDrifting = false;
            gripRecoveryProgress = 0f;
        }

        if (isDrifting)
        {
            float retainedSpeedPerSecond = Mathf.Clamp01(1f - driftSpeedLossPerSecond);
            driftTargetSpeed *= Mathf.Pow(retainedSpeedPerSecond, Time.fixedDeltaTime);
            bool counterSteering = IsCounterSteering();
            currentGripMultiplier = driftGripMultiplier
                * (counterSteering ? counterSteerGripMultiplier : 1f);
            currentGripMultiplier = Mathf.Clamp01(currentGripMultiplier);
            return;
        }

        gripRecoveryProgress = Mathf.MoveTowards(
            gripRecoveryProgress,
            1f,
            Time.fixedDeltaTime / Mathf.Max(0.01f, gripRecoveryTime));
        currentGripMultiplier = Mathf.Lerp(
            driftGripMultiplier,
            1f,
            Mathf.SmoothStep(0f, 1f, gripRecoveryProgress));
    }

    private Vector3 ApplyDriftTrajectoryAssist(Vector3 planarVelocity)
    {
        float speed = planarVelocity.magnitude;
        if (speed < 0.01f)
        {
            return planarVelocity;
        }

        float steeringStrength = Mathf.Clamp01(Mathf.Abs(smoothedTurnInput));
        float assistedTurnDegrees = driftDirection
            * driftVelocitySteerRate
            * steeringStrength
            * Time.fixedDeltaTime;
        Vector3 assistedDirection = Quaternion.AngleAxis(
            assistedTurnDegrees,
            Vector3.up) * (planarVelocity / speed);
        return assistedDirection * speed;
    }

    private Vector3 StabilizeDriftSlipAngle(Vector3 planarVelocity, Vector3 forward)
    {
        float speed = planarVelocity.magnitude;
        if (speed < 0.01f)
        {
            currentDriftAngle = 0f;
            return planarVelocity;
        }

        float signedAngle = Vector3.SignedAngle(forward, planarVelocity / speed, Vector3.up);
        bool counterSteering = IsCounterSteering();
        float steeringStrength = Mathf.Clamp01(Mathf.Abs(smoothedTurnInput));
        float targetSlipMagnitude = counterSteering
            ? counterSteerSlipAngle
            : maximumControlledSlipAngle * steeringStrength;
        float targetSlipAngle = -driftDirection * targetSlipMagnitude;
        if (counterSteering || Mathf.Abs(signedAngle) > targetSlipMagnitude)
        {
            signedAngle = Mathf.MoveTowardsAngle(
                signedAngle,
                targetSlipAngle,
                driftSlipAngleResponse * Time.fixedDeltaTime);
        }

        currentDriftAngle = signedAngle;
        if (Mathf.Abs(signedAngle) > maximumDriftAngle)
        {
            float targetAngle = Mathf.Sign(signedAngle) * maximumDriftAngle;
            float correction = 1f - Mathf.Exp(
                -Mathf.Max(0f, driftAngleLimitResponse) * Time.fixedDeltaTime);
            currentDriftAngle = Mathf.Lerp(signedAngle, targetAngle, correction);
        }

        Vector3 limitedDirection = Quaternion.AngleAxis(currentDriftAngle, Vector3.up) * forward;
        return limitedDirection * speed;
    }

    private bool IsCounterSteering()
    {
        return Mathf.Abs(smoothedTurnInput) > 0.1f
            && Mathf.Sign(smoothedTurnInput) != driftDirection;
    }

    private bool CheckGrounded()
    {
        if (vehicleColliders == null || vehicleColliders.Length == 0)
        {
            return false;
        }

        bool hasBounds = false;
        Bounds vehicleBounds = new Bounds();
        foreach (Collider vehicleCollider in vehicleColliders)
        {
            if (vehicleCollider == null || !vehicleCollider.enabled || vehicleCollider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                vehicleBounds = vehicleCollider.bounds;
                hasBounds = true;
            }
            else
            {
                vehicleBounds.Encapsulate(vehicleCollider.bounds);
            }
        }
        if (!hasBounds)
        {
            return false;
        }

        float rayDistance = vehicleBounds.extents.y + groundCheckDistance;
        int hitCount = Physics.RaycastNonAlloc(
            vehicleBounds.center,
            Vector3.down,
            groundHits,
            rayDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);
        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit hit = groundHits[hitIndex];
            if (hit.collider != null
                && !hit.collider.transform.IsChildOf(transform)
                && Vector3.Dot(hit.normal, Vector3.up) >= 0.5f)
            {
                return true;
            }
        }

        return false;
    }

    private void ConfigureFrictionlessColliders()
    {
        frictionlessMaterial = new PhysicMaterial("PlayerCar_Frictionless")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicMaterialCombine.Minimum,
            bounceCombine = PhysicMaterialCombine.Minimum
        };

        foreach (Collider vehicleCollider in GetComponents<Collider>())
        {
            vehicleCollider.material = frictionlessMaterial;
        }
    }

    private void OnDestroy()
    {
        Time.fixedDeltaTime = previousFixedDeltaTime;
        if (frictionlessMaterial != null)
        {
            Destroy(frictionlessMaterial);
        }
    }
}
