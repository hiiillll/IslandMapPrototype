using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class PlaneChaseController : MonoBehaviour
{
    [SerializeField] private Level04GameController gameController;
    [SerializeField] private Transform bankPivot;
    [SerializeField, Min(0f)] private float fallbackForwardSpeed = 36f;
    [SerializeField, Min(0f)] private float maximumTurnRate = 300f;
    [SerializeField, Range(0f, 45f)] private float maximumBankAngle = 22f;
    [SerializeField, Min(0f)] private float bankResponse = 8f;

    [Header("Altitude Control")]
    [SerializeField, Min(0f)] private float maximumVerticalSpeed = 12f;
    [SerializeField, Min(0f)] private float verticalAcceleration = 24f;
    [SerializeField] private float minimumAltitude = 8f;
    [SerializeField] private float maximumAltitude = 56f;
    [SerializeField, Range(0f, 45f)] private float maximumPitchAngle = 18f;

    private Rigidbody body;
    private SimplePlayerHealth health;
    private float steeringInput;
    private float altitudeInput;

    public float CurrentForwardSpeed { get; private set; }
    public float CurrentVerticalSpeed { get; private set; }
    public float CurrentAltitude => body != null ? body.position.y : transform.position.y;

    public void Configure(Level04GameController controller, Transform visualBankPivot)
    {
        gameController = controller;
        bankPivot = visualBankPivot;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        health = GetComponent<SimplePlayerHealth>();
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotationX
            | RigidbodyConstraints.FreezeRotationZ;
        body.drag = 0f;
        body.angularDrag = 0.2f;
        body.maxAngularVelocity = Mathf.Max(2.5f, maximumTurnRate * Mathf.Deg2Rad);
    }

    private void Start()
    {
        if (gameController == null)
        {
            gameController = FindObjectOfType<Level04GameController>();
        }

        CurrentForwardSpeed = GetTargetSpeed();
        CurrentVerticalSpeed = 0f;
        body.velocity = transform.forward * CurrentForwardSpeed;
    }

    private void Update()
    {
        steeringInput = Input.GetAxis("Horizontal");
        altitudeInput = Input.GetAxis("Vertical");
        if (bankPivot == null)
        {
            return;
        }

        float pitchRatio = maximumVerticalSpeed > 0f
            ? Mathf.Clamp(CurrentVerticalSpeed / maximumVerticalSpeed, -1f, 1f)
            : 0f;
        Quaternion targetBank = Quaternion.Euler(
            -pitchRatio * maximumPitchAngle,
            0f,
            -steeringInput * maximumBankAngle);
        bankPivot.localRotation = Quaternion.Slerp(
            bankPivot.localRotation,
            targetBank,
            1f - Mathf.Exp(-bankResponse * Time.deltaTime));
    }

    private void FixedUpdate()
    {
        if ((health != null && health.CurrentHealth <= 0) ||
            (gameController != null && gameController.IsFinished))
        {
            CurrentForwardSpeed = 0f;
            CurrentVerticalSpeed = 0f;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            return;
        }

        float speed = GetTargetSpeed();
        float turnDegrees = steeringInput * maximumTurnRate * Time.fixedDeltaTime;
        Quaternion nextRotation = body.rotation * Quaternion.Euler(0f, turnDegrees, 0f);
        body.MoveRotation(nextRotation);

        float lowerAltitude = Mathf.Min(minimumAltitude, maximumAltitude);
        float upperAltitude = Mathf.Max(minimumAltitude, maximumAltitude);
        float targetVerticalSpeed = altitudeInput * maximumVerticalSpeed;
        if ((body.position.y <= lowerAltitude && targetVerticalSpeed < 0f) ||
            (body.position.y >= upperAltitude && targetVerticalSpeed > 0f))
        {
            targetVerticalSpeed = 0f;
        }
        CurrentVerticalSpeed = Mathf.MoveTowards(
            CurrentVerticalSpeed,
            targetVerticalSpeed,
            verticalAcceleration * Time.fixedDeltaTime);
        float targetAltitude = Mathf.Clamp(
            body.position.y + CurrentVerticalSpeed * Time.fixedDeltaTime,
            lowerAltitude,
            upperAltitude);
        CurrentVerticalSpeed = (targetAltitude - body.position.y) / Time.fixedDeltaTime;

        body.velocity = nextRotation * Vector3.forward * speed
            + Vector3.up * CurrentVerticalSpeed;
        body.angularVelocity = Vector3.zero;
        CurrentForwardSpeed = speed;
    }

    private float GetTargetSpeed()
    {
        return gameController != null
            ? gameController.GetPlayerForwardSpeed()
            : fallbackForwardSpeed;
    }
}
