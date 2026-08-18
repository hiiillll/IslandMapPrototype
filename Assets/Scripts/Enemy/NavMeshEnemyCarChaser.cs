using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
public class NavMeshEnemyCarChaser : MonoBehaviour
{
    private const float MinimumKnockbackSpeed = 18f;
    private const float KnockbackSpeedMultiplier = 1.25f;
    private const float MaximumKnockbackSpeed = 34f;

    [Header("Navigation Driving")]
    [SerializeField] private float fallbackPlayerSpeed = 24f;
    [SerializeField] private float acceleration = 60f;
    [SerializeField] private float turnSpeedDegrees = 400f;
    [SerializeField] private float turnAccelerationDegrees = 1200f;
    [SerializeField] private float directionResponse = 12f;
    [SerializeField] private float obstacleTurnSpeedDegrees = 520f;
    [SerializeField] private float obstacleTurnAccelerationDegrees = 1800f;
    [SerializeField] private float obstacleDirectionResponse = 18f;
    [SerializeField] private float obstacleCheckDistance = 8f;
    [SerializeField] private float obstacleCheckRadius = 0.7f;
    [SerializeField] private float lookAheadDistance = 9f;
    [SerializeField] private float minimumVisibleLookAhead = 2.5f;
    [SerializeField] private float visibilityStep = 1.5f;
    [SerializeField] private float cornerReachDistance = 2f;
    [SerializeField] private float targetMoveRepathDistance = 4.5f;
    [SerializeField] private float minimumRepathInterval = 0.35f;
    [SerializeField] private float failedPathRetryInterval = 0.25f;
    [SerializeField] private float selfSampleRadius = 6f;
    [SerializeField] private float targetSampleRadius = 60f;

    [Header("Collision Explosion")]
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private int explosionDamage = 1;
    [SerializeField, Range(0f, 1f)] private float healthPackDropChance = 0.1f;

    private Rigidbody body;
    private Transform player;
    private SimpleAutoDriveController playerDrive;
    private Rigidbody playerBody;
    private NavMeshPath navigationPath;
    private NavMeshQueryFilter navigationFilter;
    private Vector3[] pathCorners = Array.Empty<Vector3>();
    private readonly RaycastHit[] obstacleHits = new RaycastHit[8];
    private int cornerIndex;
    private float speedRatio = 0.95f;
    private float nextPathRefreshTime;
    private Vector3 lastPathTarget;
    private bool forcePathRefresh = true;
    private float slowMultiplier = 1f;
    private float slowUntil;
    private float knockbackUntil;
    private float playerCreditUntil;
    private float currentTurnSpeedDegrees;
    private Vector3 smoothedDriveDirection;
    private bool exploded;

    public void Configure(Transform playerTarget, float newSpeedRatio)
    {
        player = playerTarget;
        playerDrive = player != null ? player.GetComponent<SimpleAutoDriveController>() : null;
        playerBody = player != null ? player.GetComponent<Rigidbody>() : null;
        SetSpeedRatio(newSpeedRatio);
        forcePathRefresh = true;
        nextPathRefreshTime = 0f;
        RefreshPath();
    }

    public void SetSpeedRatio(float newSpeedRatio)
    {
        speedRatio = Mathf.Clamp(newSpeedRatio, 0.1f, 1.5f);
    }

    public void SlowFor(float newSpeedMultiplier, float duration)
    {
        if (exploded)
        {
            return;
        }

        float clampedMultiplier = Mathf.Clamp(newSpeedMultiplier, 0.05f, 1f);
        slowMultiplier = Time.time < slowUntil
            ? Mathf.Min(slowMultiplier, clampedMultiplier)
            : clampedMultiplier;
        slowUntil = Mathf.Max(slowUntil, Time.time + Mathf.Max(0f, duration));
        MarkPlayerCredit(duration + 2f);
    }

    public void ApplyKnockback(Vector3 direction, float force, float duration = 0.55f)
    {
        if (exploded || body == null)
        {
            return;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = -transform.forward;
        }

        Vector3 knockbackDirection = direction.normalized;
        Vector3 planarVelocity = new Vector3(body.velocity.x, 0f, body.velocity.z);
        float existingOutwardSpeed = Mathf.Max(0f, Vector3.Dot(planarVelocity, knockbackDirection));
        float requestedKnockbackSpeed = Mathf.Clamp(
            Mathf.Max(0f, force) * KnockbackSpeedMultiplier,
            MinimumKnockbackSpeed,
            MaximumKnockbackSpeed);
        float knockbackSpeed = Mathf.Max(existingOutwardSpeed, requestedKnockbackSpeed);

        body.WakeUp();
        body.velocity = new Vector3(
            knockbackDirection.x * knockbackSpeed,
            body.velocity.y,
            knockbackDirection.z * knockbackSpeed);
        body.angularVelocity = Vector3.zero;
        knockbackUntil = Mathf.Max(knockbackUntil, Time.time + Mathf.Max(0f, duration));
        MarkPlayerCredit(duration + 2f);
        forcePathRefresh = true;
        nextPathRefreshTime = Mathf.Max(nextPathRefreshTime, knockbackUntil);
    }

    public void MarkPlayerCredit(float duration = 2f)
    {
        if (!GameModeSession.IsEndlessLand || exploded)
        {
            return;
        }

        playerCreditUntil = Mathf.Max(playerCreditUntil, Time.time + Mathf.Max(0f, duration));
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        body.drag = 0f;
        body.angularDrag = 0f;
        body.maxAngularVelocity = 12f;
        smoothedDriveDirection = body.rotation * Vector3.forward;
        navigationPath = new NavMeshPath();
        navigationFilter = new NavMeshQueryFilter
        {
            agentTypeID = 0,
            areaMask = GetWalkableAreaMask()
        };
    }

    private void Start()
    {
        ResolvePlayer();
        forcePathRefresh = true;
        nextPathRefreshTime = 0f;
    }

    private void Update()
    {
        if (exploded)
        {
            return;
        }

        if (player == null)
        {
            ResolvePlayer();
            if (player == null)
            {
                return;
            }
        }

        if (Time.time < knockbackUntil || Time.time < nextPathRefreshTime)
        {
            return;
        }

        bool targetMoved = PlanarSqrDistance(player.position, lastPathTarget)
            >= targetMoveRepathDistance * targetMoveRepathDistance;
        bool reachedPathEnd = HasUsablePath()
            && PlanarSqrDistance(body.position, pathCorners[pathCorners.Length - 1])
            <= cornerReachDistance * cornerReachDistance;
        if (forcePathRefresh || !HasUsablePath() || targetMoved || reachedPathEnd)
        {
            RefreshPath();
        }
    }

    private void FixedUpdate()
    {
        if (exploded || body.isKinematic || player == null)
        {
            return;
        }

        body.WakeUp();
        if (Time.time < knockbackUntil)
        {
            return;
        }

        if (Time.time >= slowUntil)
        {
            slowMultiplier = 1f;
        }

        Vector3 driveDirection = GetDriveDirection();
        bool obstacleAhead = IsStaticObstacleAhead();
        float activeDirectionResponse = obstacleAhead ? obstacleDirectionResponse : directionResponse;
        float activeTurnSpeed = obstacleAhead ? obstacleTurnSpeedDegrees : turnSpeedDegrees;
        float activeTurnAcceleration = obstacleAhead
            ? obstacleTurnAccelerationDegrees
            : turnAccelerationDegrees;
        float directionBlend = 1f - Mathf.Exp(-activeDirectionResponse * Time.fixedDeltaTime);
        smoothedDriveDirection = Vector3.Slerp(
            smoothedDriveDirection,
            driveDirection,
            directionBlend).normalized;
        Quaternion desiredRotation = Quaternion.LookRotation(smoothedDriveDirection, Vector3.up);
        float remainingAngle = Quaternion.Angle(body.rotation, desiredRotation);
        float targetTurnSpeed = Mathf.Min(activeTurnSpeed, remainingAngle * activeDirectionResponse);
        currentTurnSpeedDegrees = Mathf.MoveTowards(
            currentTurnSpeedDegrees,
            targetTurnSpeed,
            activeTurnAcceleration * Time.fixedDeltaTime);
        Quaternion nextRotation = Quaternion.RotateTowards(
            body.rotation,
            desiredRotation,
            currentTurnSpeedDegrees * Time.fixedDeltaTime);
        body.MoveRotation(nextRotation);
        body.angularVelocity = Vector3.zero;

        Vector3 planarVelocity = new Vector3(body.velocity.x, 0f, body.velocity.z);
        float currentSpeed = planarVelocity.magnitude;
        float targetSpeed = GetTargetSpeed();
        float nextSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);
        Vector3 nextForward = nextRotation * Vector3.forward;
        body.velocity = new Vector3(
            nextForward.x * nextSpeed,
            body.velocity.y,
            nextForward.z * nextSpeed);
    }

    private void ResolvePlayer()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
        }

        if (player != null && playerDrive == null)
        {
            playerDrive = player.GetComponent<SimpleAutoDriveController>();
        }
        if (player != null && playerBody == null)
        {
            playerBody = player.GetComponent<Rigidbody>();
        }
    }

    private void RefreshPath()
    {
        if (player == null || navigationPath == null)
        {
            return;
        }

        int areaMask = navigationFilter.areaMask;
        Vector3 predictedTarget = player.position;
        if (playerBody != null)
        {
            Vector3 playerVelocity = playerBody.velocity;
            playerVelocity.y = 0f;
            predictedTarget += playerVelocity * 0.25f;
        }
        bool sampledStart = NavMesh.SamplePosition(body.position, out NavMeshHit startHit, selfSampleRadius, areaMask);
        bool sampledTarget = NavMesh.SamplePosition(predictedTarget, out NavMeshHit targetHit, targetSampleRadius, areaMask);
        bool calculated = sampledStart && sampledTarget
            && NavMesh.CalculatePath(startHit.position, targetHit.position, navigationFilter, navigationPath)
            && navigationPath.status != NavMeshPathStatus.PathInvalid
            && navigationPath.corners.Length >= 2;

        if (!calculated)
        {
            forcePathRefresh = true;
            nextPathRefreshTime = Time.time + failedPathRetryInterval;
            return;
        }

        pathCorners = navigationPath.corners;
        cornerIndex = 1;
        lastPathTarget = player.position;
        forcePathRefresh = false;
        nextPathRefreshTime = Time.time + minimumRepathInterval;
    }

    private Vector3 GetDriveDirection()
    {
        Vector3 currentPosition = body.position;
        if (!HasUsablePath())
        {
            forcePathRefresh = true;
            Vector3 toPlayer = player.position - currentPosition;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.001f)
            {
                return toPlayer.normalized;
            }

            return body.rotation * Vector3.forward;
        }

        Vector3 lookAheadPoint = GetVisibleLookAheadPoint(currentPosition);
        Vector3 direction = lookAheadPoint - currentPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = body.rotation * Vector3.forward;
            direction.y = 0f;
        }
        return direction.normalized;
    }

    private Vector3 GetVisibleLookAheadPoint(Vector3 currentPosition)
    {
        int areaMask = navigationFilter.areaMask;
        if (!NavMesh.SamplePosition(currentPosition, out NavMeshHit startHit, selfSampleRadius, areaMask))
        {
            return GetLookAheadPoint(currentPosition, minimumVisibleLookAhead);
        }

        float candidateDistance = lookAheadDistance;
        while (candidateDistance >= minimumVisibleLookAhead)
        {
            Vector3 candidate = GetLookAheadPoint(currentPosition, candidateDistance);
            if (!NavMesh.Raycast(startHit.position, candidate, out _, navigationFilter))
            {
                return candidate;
            }

            candidateDistance -= visibilityStep;
        }

        return pathCorners[Mathf.Clamp(cornerIndex, 1, pathCorners.Length - 1)];
    }

    private Vector3 GetLookAheadPoint(Vector3 currentPosition, float requestedDistance)
    {
        int firstSegment = Mathf.Clamp(cornerIndex - 1, 0, pathCorners.Length - 2);
        int closestSegment = firstSegment;
        Vector3 closestPoint = pathCorners[firstSegment];
        float closestDistance = float.PositiveInfinity;
        for (int index = firstSegment; index < pathCorners.Length - 1; index++)
        {
            Vector3 pathStart = pathCorners[index];
            Vector3 pathEnd = pathCorners[index + 1];
            Vector3 segment = pathEnd - pathStart;
            segment.y = 0f;
            float segmentLengthSquared = segment.sqrMagnitude;
            float projection = segmentLengthSquared > 0.001f
                ? Mathf.Clamp01(Vector3.Dot(currentPosition - pathStart, segment) / segmentLengthSquared)
                : 0f;
            Vector3 projectedPoint = Vector3.Lerp(pathStart, pathEnd, projection);
            float distance = PlanarSqrDistance(currentPosition, projectedPoint);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = projectedPoint;
                closestSegment = index;
            }
        }

        cornerIndex = closestSegment + 1;
        float remainingDistance = requestedDistance;
        Vector3 segmentStart = closestPoint;
        for (int index = cornerIndex; index < pathCorners.Length; index++)
        {
            Vector3 segmentEnd = pathCorners[index];
            Vector3 segment = segmentEnd - segmentStart;
            segment.y = 0f;
            float segmentLength = segment.magnitude;
            if (segmentLength >= remainingDistance && segmentLength > 0.001f)
            {
                return segmentStart + segment / segmentLength * remainingDistance;
            }

            remainingDistance -= segmentLength;
            segmentStart = segmentEnd;
        }

        return pathCorners[pathCorners.Length - 1];
    }

    private float GetTargetSpeed()
    {
        float playerSpeed = playerDrive != null
            ? playerDrive.ForwardSpeed
            : GetFallbackPlayerSpeed();
        return playerSpeed * speedRatio * slowMultiplier;
    }

    private float GetFallbackPlayerSpeed()
    {
        return fallbackPlayerSpeed
            * (GameModeSession.IsEndless ? 1f : SimpleAutoDriveController.StoryForwardSpeedMultiplier);
    }

    private bool HasUsablePath()
    {
        return pathCorners != null && pathCorners.Length >= 2
            && cornerIndex > 0 && cornerIndex < pathCorners.Length;
    }

    private static int GetWalkableAreaMask()
    {
        int walkableArea = NavMesh.GetAreaFromName("Walkable");
        return walkableArea >= 0 ? 1 << walkableArea : NavMesh.AllAreas;
    }


    private static float PlanarSqrDistance(Vector3 first, Vector3 second)
    {
        float deltaX = first.x - second.x;
        float deltaZ = first.z - second.z;
        return deltaX * deltaX + deltaZ * deltaZ;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (exploded || collision.collider == null)
        {
            return;
        }

        Collider other = collision.collider;
        if (other.GetComponentInParent<Level03KnockableGuardrail>() != null)
        {
            return;
        }

        if (other.GetComponentInParent<SimplePlayerHealth>() != null)
        {
            Explode(true, true);
            return;
        }

        if (other.GetComponentInParent<NavMeshEnemyCarChaser>() != null || IsStaticObstacle(other))
        {
            Explode(false);
        }
    }

    private static bool IsStaticObstacle(Collider collider)
    {
        if (collider == null || collider.isTrigger || collider.attachedRigidbody != null)
        {
            return false;
        }

        string objectName = collider.gameObject.name;
        return !IsDrivingSurface(collider) && !objectName.StartsWith("SPAWN_");
    }

    public static bool IsDrivingSurface(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (collider is TerrainCollider)
        {
            return true;
        }

        for (Transform current = collider.transform; current != null; current = current.parent)
        {
            string objectName = current.name;
            if (objectName == "COL_DriveSurface" ||
                objectName == "COL_Grass" ||
                objectName == "COL_Beach" ||
                objectName.StartsWith("COL_Road") ||
                objectName.StartsWith("ENV_Ground_Grass") ||
                objectName == "ENV_Ground_Beach" ||
                objectName.StartsWith("ENV_Road_") ||
                objectName.StartsWith("MB_Coastal_Sidewalk_") ||
                objectName.StartsWith("MB_Sidewalk_") ||
                objectName.StartsWith("MB_Bike_Path_") ||
                objectName == "MB_Promenade" ||
                objectName.StartsWith("MB_Road_") && !objectName.StartsWith("MB_Road_Barrier_") ||
                objectName == "ENV_Level03_RoadNetwork_FromReference" ||
                objectName.StartsWith("Terrain_Level03_"))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsStaticObstacleAhead()
    {
        Vector3 forward = body.rotation * Vector3.forward;
        Vector3 origin = body.position + Vector3.up * 0.6f;
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            obstacleCheckRadius,
            forward,
            obstacleHits,
            obstacleCheckDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hitCollider = obstacleHits[hitIndex].collider;
            if (hitCollider != null && !hitCollider.transform.IsChildOf(transform)
                && IsStaticObstacle(hitCollider))
            {
                return true;
            }
        }
        return false;
    }

    public void Explode(bool playerCredit, bool shakeCamera = false)
    {
        if (exploded)
        {
            return;
        }

        exploded = true;
        if (shakeCamera)
        {
            SimpleSpeedCameraFollow cameraFollow = Camera.main != null
                ? Camera.main.GetComponent<SimpleSpeedCameraFollow>()
                : null;
            if (cameraFollow != null)
            {
                cameraFollow.Shake(0.16f, 0.22f);
            }
        }

        try
        {
            EnemyExplosionEffect.Spawn(transform.position);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        bool creditedToPlayer = playerCredit
            || (GameModeSession.IsEndlessLand && Time.time <= playerCreditUntil);
        bool grantsPlayerRewards = !GameModeSession.IsEndless || creditedToPlayer;
        PlayerProgression progression = PlayerProgression.Instance;
        if (grantsPlayerRewards && progression != null)
        {
            progression.RegisterEnemyDestroyed();
        }

        GearPickup.SpawnAt(transform.position);
        float activeHealthPackDropChance = Mathf.Clamp01(
            healthPackDropChance
            + (progression != null ? progression.HealthPackDropChanceBonus : 0f));
        if (grantsPlayerRewards && UnityEngine.Random.value <= activeHealthPackDropChance)
        {
            HealthPickup.SpawnAt(transform.position);
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider hit in hits)
        {
            SimplePlayerHealth playerHealth = hit.GetComponentInParent<SimplePlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(explosionDamage);
            }

            NavMeshEnemyCarChaser enemy = hit.GetComponentInParent<NavMeshEnemyCarChaser>();
            if (enemy != null && enemy != this)
            {
                enemy.Explode(creditedToPlayer);
            }
        }

        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = false;
        }

        body.velocity = Vector3.zero;
        body.isKinematic = true;
        Destroy(gameObject, 0.05f);
    }

    private void OnDrawGizmosSelected()
    {
        if (pathCorners == null || pathCorners.Length < 2)
        {
            return;
        }

        Gizmos.color = Color.green;
        for (int index = 1; index < pathCorners.Length; index++)
        {
            Gizmos.DrawLine(pathCorners[index - 1], pathCorners[index]);
        }

        if (body != null && HasUsablePath())
        {
            Vector3 lookAheadPoint = GetVisibleLookAheadPoint(body.position);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(lookAheadPoint, 0.45f);
            Gizmos.DrawLine(body.position, lookAheadPoint);
        }
    }
}
