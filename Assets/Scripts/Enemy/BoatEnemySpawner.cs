using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class BoatEnemySpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Camera spawnCamera;
    [SerializeField] private GameObject enemyVisualPrefab;
    [SerializeField] private BoatChaseDifficultyController difficultyController;

    [Header("Off-screen Spawning")]
    [SerializeField, Min(0f)] private float openingSpawnDelay = 3f;
    [SerializeField, Min(0f)] private float offscreenSpawnDistance = 60f;
    [SerializeField, Min(0f)] private float minimumSpawnDistance = 75f;
    [SerializeField, Min(1)] private int spawnSearchAttempts = 30;
    [SerializeField, Min(0f)] private float spawnCollisionGrace = 0.15f;
    [SerializeField, Min(1)] private int maximumActiveEnemies = 12;
    [SerializeField, Range(0f, 180f)] private float forwardSpawnExclusionHalfAngle = 20f;
    [SerializeField, Range(0.1f, 1f)] private float horizontalColliderScale = 0.9f;

    [Header("Story Enemy Tracking")]
    [SerializeField, Min(0f)] private float enemyMaximumTurnRate = 145f;
    [SerializeField, Min(0f)] private float enemyTurnAcceleration = 420f;
    [SerializeField, Range(0f, 1f)] private float enemyMaximumPredictionTime = 0.3f;

    private float spawnTimer;
    private bool spawningEnabled = true;
    private readonly List<GameObject> activeEnemies = new List<GameObject>();

    public void Configure(
        Transform playerTarget,
        Camera cameraTarget,
        GameObject visualPrefab,
        BoatChaseDifficultyController difficulty)
    {
        player = playerTarget;
        spawnCamera = cameraTarget;
        enemyVisualPrefab = visualPrefab;
        difficultyController = difficulty;
    }

    private void Start()
    {
        ResolveReferences();
        spawnTimer = openingSpawnDelay;
    }

    private void Update()
    {
        activeEnemies.RemoveAll(enemy => enemy == null);
        ResolveReferences();

        SurvivalGameController survivalController = difficultyController != null
            ? difficultyController.GetComponent<SurvivalGameController>()
            : null;
        if (!spawningEnabled || Time.timeScale == 0f
            || (survivalController != null && survivalController.IsFinished))
        {
            return;
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f)
        {
            return;
        }

        int activeLimit = GameModeSession.IsEndlessSea && difficultyController != null
            ? difficultyController.GetMaximumActiveEnemies()
            : maximumActiveEnemies;
        if (activeEnemies.Count >= activeLimit)
        {
            return;
        }

        if (player != null && spawnCamera != null && enemyVisualPrefab != null)
        {
            SpawnEnemy();
        }

        spawnTimer = difficultyController != null
            ? difficultyController.GetSpawnInterval()
            : 2.5f;
    }

    public void StopSpawningAndClearEnemies()
    {
        spawningEnabled = false;
        foreach (GameObject enemy in activeEnemies)
        {
            if (enemy == null)
            {
                continue;
            }

            BoatEnemyChaser chaser = enemy.GetComponent<BoatEnemyChaser>();
            if (chaser != null)
            {
                chaser.RemoveWithoutEffect();
            }
            else
            {
                Destroy(enemy);
            }
        }

        activeEnemies.Clear();
    }

    private void SpawnEnemy()
    {
        if (!TryFindSpawnPosition(out Vector3 spawnPosition))
        {
            Debug.LogWarning("Unable to find an off-screen boat enemy spawn position.", this);
            return;
        }

        GameObject enemy = new GameObject($"ENEMY_Boat_{activeEnemies.Count + 1:00}");
        enemy.transform.position = spawnPosition;
        Vector3 initialDirection = Vector3.ProjectOnPlane(
            player.position - spawnPosition,
            Vector3.up);
        enemy.transform.rotation = initialDirection.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(initialDirection.normalized, Vector3.up)
            : Quaternion.identity;

        GameObject visual = Instantiate(enemyVisualPrefab, enemy.transform);
        visual.name = "Visual_Model17";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        visual.transform.localScale = Vector3.one;
        FitVisualToPlayer(enemy, visual);

        Bounds localBounds = CalculateLocalBounds(enemy);
        BoxCollider enemyCollider = enemy.AddComponent<BoxCollider>();
        enemyCollider.center = localBounds.center;
        Vector3 colliderSize = localBounds.size;
        float horizontalScale = Mathf.Clamp(horizontalColliderScale, 0.1f, 1f);
        colliderSize.x *= horizontalScale;
        colliderSize.z *= horizontalScale;
        enemyCollider.size = colliderSize;
        enemyCollider.isTrigger = true;

        Rigidbody enemyBody = enemy.AddComponent<Rigidbody>();
        enemyBody.mass = 800f;

        BoatEnemyChaser chaser = enemy.AddComponent<BoatEnemyChaser>();
        chaser.Configure(
            player,
            difficultyController,
            enemyMaximumTurnRate,
            enemyTurnAcceleration,
            enemyMaximumPredictionTime);
        enemy.AddComponent<BoatWakeTrail>();
        activeEnemies.Add(enemy);
        StartCoroutine(TemporarilyDisableCollision(enemyCollider));
    }

    private bool TryFindSpawnPosition(out Vector3 spawnPosition)
    {
        float cameraHeight = spawnCamera.orthographicSize * 2f;
        float cameraWidth = cameraHeight * spawnCamera.aspect;
        Vector3 cameraPosition = spawnCamera.transform.position;

        float left = cameraPosition.x - cameraWidth * 0.5f;
        float right = cameraPosition.x + cameraWidth * 0.5f;
        float bottom = cameraPosition.z - cameraHeight * 0.5f;
        float top = cameraPosition.z + cameraHeight * 0.5f;
        float spawnHeight = player.position.y;
        float exclusionHalfAngle = Mathf.Clamp(forwardSpawnExclusionHalfAngle, 0f, 180f);
        float allowedArc = 360f - exclusionHalfAngle * 2f;
        Vector3 playerForward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        if (allowedArc <= 0f || playerForward.sqrMagnitude < 0.001f)
        {
            spawnPosition = default;
            return false;
        }

        for (int attempt = 0; attempt < spawnSearchAttempts; attempt++)
        {
            float yawOffset = exclusionHalfAngle + Random.value * allowedArc;
            Vector3 spawnDirection = Quaternion.AngleAxis(yawOffset, Vector3.up) * playerForward;
            float distanceToCameraEdge = GetDistanceToCameraEdge(
                player.position,
                spawnDirection,
                left,
                right,
                bottom,
                top);
            float extraDistance = Random.Range(1f, Mathf.Max(1f, offscreenSpawnDistance));
            float spawnDistance = Mathf.Max(
                minimumSpawnDistance,
                distanceToCameraEdge + extraDistance);
            Vector3 candidatePosition = player.position
                + spawnDirection * spawnDistance;
            candidatePosition.y = spawnHeight;

            if (GameModeSession.IsEndlessSea)
            {
                candidatePosition.x = Mathf.Clamp(candidatePosition.x, -1980f, 1980f);
                candidatePosition.z = Mathf.Clamp(candidatePosition.z, -1980f, 1980f);
            }

            bool insideCamera = candidatePosition.x >= left && candidatePosition.x <= right
                && candidatePosition.z >= bottom && candidatePosition.z <= top;
            Vector3 finalSpawnOffset = Vector3.ProjectOnPlane(
                candidatePosition - player.position,
                Vector3.up);
            bool isTooClose = finalSpawnOffset.sqrMagnitude
                < minimumSpawnDistance * minimumSpawnDistance;
            if (insideCamera || isTooClose || IsInsideForwardSpawnExclusion(candidatePosition))
            {
                continue;
            }

            spawnPosition = candidatePosition;
            return true;
        }

        spawnPosition = default;
        return false;
    }

    private static float GetDistanceToCameraEdge(
        Vector3 origin,
        Vector3 direction,
        float left,
        float right,
        float bottom,
        float top)
    {
        bool originInsideCamera = origin.x >= left && origin.x <= right
            && origin.z >= bottom && origin.z <= top;
        if (!originInsideCamera)
        {
            return 0f;
        }

        float distanceToVerticalEdge = float.PositiveInfinity;
        if (direction.x > 0.001f)
        {
            distanceToVerticalEdge = (right - origin.x) / direction.x;
        }
        else if (direction.x < -0.001f)
        {
            distanceToVerticalEdge = (left - origin.x) / direction.x;
        }

        float distanceToHorizontalEdge = float.PositiveInfinity;
        if (direction.z > 0.001f)
        {
            distanceToHorizontalEdge = (top - origin.z) / direction.z;
        }
        else if (direction.z < -0.001f)
        {
            distanceToHorizontalEdge = (bottom - origin.z) / direction.z;
        }

        return Mathf.Max(0f, Mathf.Min(distanceToVerticalEdge, distanceToHorizontalEdge));
    }

    private bool IsInsideForwardSpawnExclusion(Vector3 candidatePosition)
    {
        float exclusionHalfAngle = Mathf.Clamp(forwardSpawnExclusionHalfAngle, 0f, 180f);
        if (exclusionHalfAngle <= 0f || player == null)
        {
            return false;
        }

        Vector3 playerForward = Vector3.ProjectOnPlane(player.forward, Vector3.up);
        Vector3 directionToCandidate = Vector3.ProjectOnPlane(
            candidatePosition - player.position,
            Vector3.up);
        if (directionToCandidate.sqrMagnitude < 0.001f)
        {
            return true;
        }
        if (playerForward.sqrMagnitude < 0.001f)
        {
            return false;
        }

        return Vector3.Angle(playerForward, directionToCandidate) <= exclusionHalfAngle;
    }

    private IEnumerator TemporarilyDisableCollision(Collider enemyCollider)
    {
        if (enemyCollider == null)
        {
            yield break;
        }

        enemyCollider.enabled = false;
        yield return new WaitForSeconds(spawnCollisionGrace);
        if (enemyCollider != null)
        {
            enemyCollider.enabled = true;
        }
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
        }

        if (spawnCamera == null)
        {
            spawnCamera = Camera.main;
        }

        if (difficultyController == null)
        {
            difficultyController = FindObjectOfType<BoatChaseDifficultyController>();
        }
    }

    private void FitVisualToPlayer(GameObject enemy, GameObject visual)
    {
        Bounds visualBounds = CalculateWorldBounds(visual);
        Collider playerCollider = player != null ? player.GetComponent<Collider>() : null;
        float targetSize = playerCollider != null
            ? Mathf.Max(playerCollider.bounds.size.x, playerCollider.bounds.size.z)
            : 5f;
        float visualSize = Mathf.Max(visualBounds.size.x, visualBounds.size.z);
        visual.transform.localScale = Vector3.one * (targetSize / Mathf.Max(visualSize, 0.01f));

        Bounds fittedBounds = CalculateLocalBounds(enemy);
        visual.transform.localPosition -= new Vector3(
            fittedBounds.center.x,
            fittedBounds.min.y,
            fittedBounds.center.z);
    }

    private static Bounds CalculateWorldBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds;
    }

    private static Bounds CalculateLocalBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool initialized = false;
        Bounds bounds = new Bounds();
        foreach (Renderer renderer in renderers)
        {
            Vector3 min = renderer.bounds.min;
            Vector3 max = renderer.bounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z)
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 localPoint = root.transform.InverseTransformPoint(corner);
                if (!initialized)
                {
                    bounds = new Bounds(localPoint, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(localPoint);
                }
            }
        }

        return bounds;
    }
}
