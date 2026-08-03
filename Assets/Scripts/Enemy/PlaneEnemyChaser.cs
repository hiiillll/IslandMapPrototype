using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class PlaneEnemyChaser : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Level04GameController gameController;
    [SerializeField] private Transform bankPivot;
    [SerializeField, Min(0f)] private float maximumTurnRate = 145f;
    [SerializeField, Min(0f)] private float turnAcceleration = 420f;
    [SerializeField, Range(0f, 45f)] private float maximumBankAngle = 28f;
    [SerializeField, Min(0f)] private float maximumVerticalSpeed = 14f;
    [SerializeField, Min(0f)] private float verticalAcceleration = 30f;
    [SerializeField, Min(0f)] private float verticalTrackingResponse = 2.2f;
    [SerializeField, Range(0f, 45f)] private float maximumPitchAngle = 20f;

    private Rigidbody body;
    private float currentTurnRate;
    private float currentVerticalSpeed;
    private bool destroyed;

    public void Configure(
        Transform playerTarget,
        Level04GameController controller,
        Transform visualBankPivot)
    {
        target = playerTarget;
        gameController = controller;
        bankPivot = visualBankPivot;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotationX
            | RigidbodyConstraints.FreezeRotationZ;
    }

    private void FixedUpdate()
    {
        if (destroyed || target == null || gameController == null || gameController.IsFinished)
        {
            body.velocity = Vector3.zero;
            return;
        }

        Vector3 targetDelta = target.position - body.position;
        float verticalError = targetDelta.y;
        Vector3 direction = Vector3.ProjectOnPlane(targetDelta, Vector3.up);
        Quaternion nextRotation = body.rotation;
        float turnDegrees = 0f;
        if (direction.sqrMagnitude >= 0.001f)
        {
            Vector3 currentForward = body.rotation * Vector3.forward;
            float angleToPlayer = Vector3.SignedAngle(
                currentForward,
                direction.normalized,
                Vector3.up);
            float stoppingTurnRate = Mathf.Sqrt(
                2f * turnAcceleration * Mathf.Abs(angleToPlayer));
            float targetTurnRate = Mathf.Sign(angleToPlayer)
                * Mathf.Min(maximumTurnRate, stoppingTurnRate);
            currentTurnRate = Mathf.MoveTowards(
                currentTurnRate,
                targetTurnRate,
                turnAcceleration * Time.fixedDeltaTime);
            turnDegrees = currentTurnRate * Time.fixedDeltaTime;
            if (Mathf.Sign(turnDegrees) == Mathf.Sign(angleToPlayer)
                && Mathf.Abs(turnDegrees) > Mathf.Abs(angleToPlayer))
            {
                turnDegrees = angleToPlayer;
                currentTurnRate = 0f;
            }

            nextRotation = body.rotation * Quaternion.Euler(0f, turnDegrees, 0f);
            body.MoveRotation(nextRotation);
        }

        float targetVerticalSpeed = Mathf.Clamp(
            verticalError * verticalTrackingResponse,
            -maximumVerticalSpeed,
            maximumVerticalSpeed);
        currentVerticalSpeed = Mathf.MoveTowards(
            currentVerticalSpeed,
            targetVerticalSpeed,
            verticalAcceleration * Time.fixedDeltaTime);
        body.velocity = nextRotation * Vector3.forward * gameController.GetEnemyForwardSpeed()
            + Vector3.up * currentVerticalSpeed;

        if (bankPivot != null)
        {
            float bank = Mathf.Clamp(
                turnDegrees / Mathf.Max(0.01f, maximumTurnRate * Time.fixedDeltaTime),
                -1f,
                1f) * -maximumBankAngle;
            float pitch = maximumVerticalSpeed > 0f
                ? -Mathf.Clamp(currentVerticalSpeed / maximumVerticalSpeed, -1f, 1f)
                    * maximumPitchAngle
                : 0f;
            bankPivot.localRotation = Quaternion.Slerp(
                bankPivot.localRotation,
                Quaternion.Euler(pitch, 0f, bank),
                1f - Mathf.Exp(-8f * Time.fixedDeltaTime));
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (destroyed)
        {
            return;
        }

        SimplePlayerHealth playerHealth = other.GetComponentInParent<SimplePlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(1);
            Explode();
            return;
        }

        PlaneEnemyChaser otherEnemy = other.GetComponentInParent<PlaneEnemyChaser>();
        if (otherEnemy != null && otherEnemy != this)
        {
            otherEnemy.Explode();
            Explode();
        }
    }

    public void RemoveWithoutEffect()
    {
        if (destroyed)
        {
            return;
        }

        destroyed = true;
        Destroy(gameObject);
    }

    private void Explode()
    {
        if (destroyed)
        {
            return;
        }

        destroyed = true;
        EnemyExplosionEffect.Spawn(transform.position);
        if (PlayerProgression.Instance != null)
        {
            PlayerProgression.Instance.RegisterEnemyDestroyed();
        }
        Destroy(gameObject);
    }
}
