using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class BoatEnemyChaser : MonoBehaviour
{
    [SerializeField, Min(0f)] private float fallbackMoveSpeed = 30.5f;
    [SerializeField, Min(0f)] private float rotationSpeed = 5f;
    [SerializeField, Min(0f)] private float maximumTurnRate = 145f;
    [SerializeField, Min(0f)] private float turnAcceleration = 420f;
    [SerializeField, Range(0f, 1f)] private float maximumPredictionTime = 0.3f;
    [SerializeField, Range(0f, 1f)] private float healthPackDropChance = 0.1f;

    private Transform player;
    private Rigidbody playerBody;
    private Rigidbody body;
    private BoatChaseDifficultyController difficultyController;
    private float currentTurnRate;
    private bool destroyed;

    public void Configure(
        Transform playerTarget,
        BoatChaseDifficultyController difficulty,
        float newMaximumTurnRate,
        float newTurnAcceleration,
        float newMaximumPredictionTime)
    {
        player = playerTarget;
        playerBody = player != null ? player.GetComponentInParent<Rigidbody>() : null;
        difficultyController = difficulty;
        maximumTurnRate = Mathf.Max(0f, newMaximumTurnRate);
        turnAcceleration = Mathf.Max(0f, newTurnAcceleration);
        maximumPredictionTime = Mathf.Clamp01(newMaximumPredictionTime);
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezePositionY
            | RigidbodyConstraints.FreezeRotationX
            | RigidbodyConstraints.FreezeRotationZ;
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void FixedUpdate()
    {
        if (destroyed)
        {
            return;
        }

        ResolveReferences();
        if (player == null)
        {
            currentTurnRate = 0f;
            body.velocity = Vector3.zero;
            return;
        }

        Vector3 targetPosition = GameModeSession.IsEndlessSea
            ? player.position
            : CalculatePredictedTargetPosition();
        Vector3 direction = targetPosition - body.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            currentTurnRate = 0f;
            body.velocity = Vector3.zero;
            return;
        }

        Quaternion nextRotation = GameModeSession.IsEndlessSea
            ? CalculateEndlessRotation(direction)
            : CalculateStoryTrackingRotation(direction);
        body.MoveRotation(nextRotation);

        float moveSpeed = difficultyController != null
            ? difficultyController.GetEnemyChaseSpeed()
            : fallbackMoveSpeed;
        body.velocity = nextRotation * Vector3.forward * moveSpeed;
    }

    private Quaternion CalculateStoryTrackingRotation(Vector3 direction)
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

        float turnDegrees = currentTurnRate * Time.fixedDeltaTime;
        if (Mathf.Sign(turnDegrees) == Mathf.Sign(angleToPlayer)
            && Mathf.Abs(turnDegrees) > Mathf.Abs(angleToPlayer))
        {
            turnDegrees = angleToPlayer;
            currentTurnRate = 0f;
        }

        return body.rotation * Quaternion.Euler(0f, turnDegrees, 0f);
    }

    private Vector3 CalculatePredictedTargetPosition()
    {
        Vector3 targetPosition = player.position;
        if (playerBody == null || maximumPredictionTime <= 0f)
        {
            return targetPosition;
        }

        Vector3 playerVelocity = Vector3.ProjectOnPlane(playerBody.velocity, Vector3.up);
        float distanceToPlayer = Vector3.Distance(body.position, player.position);
        float predictionStrength = Mathf.InverseLerp(12f, 60f, distanceToPlayer);
        return targetPosition + playerVelocity * (maximumPredictionTime * predictionStrength);
    }

    private Quaternion CalculateEndlessRotation(Vector3 direction)
    {
        currentTurnRate = 0f;
        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        return Quaternion.Slerp(
            body.rotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        ResolveContact(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        ResolveContact(other);
    }

    private void ResolveContact(Collider other)
    {
        if (destroyed || other == null)
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

        BoatEnemyChaser otherEnemy = other.GetComponentInParent<BoatEnemyChaser>();
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
        if (Random.value <= healthPackDropChance)
        {
            HealthPickup.SpawnAt(transform.position);
        }

        if (PlayerProgression.Instance != null)
        {
            PlayerProgression.Instance.RegisterEnemyDestroyed();
        }

        foreach (Collider enemyCollider in GetComponentsInChildren<Collider>())
        {
            enemyCollider.enabled = false;
        }

        foreach (Renderer enemyRenderer in GetComponentsInChildren<Renderer>())
        {
            enemyRenderer.enabled = false;
        }

        body.velocity = Vector3.zero;
        body.isKinematic = true;
        Destroy(gameObject, 0.05f);
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
        }
        if (playerBody == null && player != null)
        {
            playerBody = player.GetComponentInParent<Rigidbody>();
        }
        if (difficultyController == null)
        {
            difficultyController = FindObjectOfType<BoatChaseDifficultyController>();
        }
    }
}
