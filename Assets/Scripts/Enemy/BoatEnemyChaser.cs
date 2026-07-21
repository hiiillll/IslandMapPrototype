using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class BoatEnemyChaser : MonoBehaviour
{
    [SerializeField, Min(0f)] private float fallbackMoveSpeed = 30.5f;
    [SerializeField, Min(0f)] private float rotationSpeed = 5f;

    private Transform player;
    private Rigidbody body;
    private BoatChaseDifficultyController difficultyController;
    private bool destroyed;

    public void Configure(Transform playerTarget, BoatChaseDifficultyController difficulty)
    {
        player = playerTarget;
        difficultyController = difficulty;
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
            body.velocity = Vector3.zero;
            return;
        }

        Vector3 direction = player.position - body.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            body.velocity = Vector3.zero;
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Quaternion nextRotation = Quaternion.Slerp(
            body.rotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime);
        body.MoveRotation(nextRotation);

        float moveSpeed = difficultyController != null
            ? difficultyController.GetEnemyChaseSpeed()
            : fallbackMoveSpeed;
        body.velocity = nextRotation * Vector3.forward * moveSpeed;
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

        if (difficultyController == null)
        {
            difficultyController = FindObjectOfType<BoatChaseDifficultyController>();
        }
    }
}
