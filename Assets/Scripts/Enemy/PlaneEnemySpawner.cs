using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlaneEnemySpawner : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Camera spawnCamera;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Level04GameController gameController;
    [SerializeField, Min(0f)] private float openingSpawnDelay = 3f;
    [SerializeField, Min(0f)] private float offscreenSpawnDistance = 60f;
    [SerializeField, Min(0f)] private float minimumSpawnDistance = 75f;
    [SerializeField, Min(1)] private int spawnSearchAttempts = 30;
    [SerializeField, Min(0f)] private float spawnCollisionGrace = 0.15f;
    [SerializeField, Min(1)] private int maximumActiveEnemies = 12;
    [SerializeField, Range(0f, 180f)] private float forwardSpawnExclusionHalfAngle = 20f;

    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private float spawnTimer;
    private bool spawningEnabled = true;

    public void Configure(
        Transform playerTarget,
        Camera cameraTarget,
        GameObject configuredEnemyPrefab,
        Level04GameController controller)
    {
        player = playerTarget;
        spawnCamera = cameraTarget;
        enemyPrefab = configuredEnemyPrefab;
        gameController = controller;
    }

    private void Start()
    {
        spawnTimer = openingSpawnDelay;
    }

    private void Update()
    {
        activeEnemies.RemoveAll(enemy => enemy == null);
        if (!spawningEnabled || Time.timeScale == 0f || player == null || spawnCamera == null ||
            enemyPrefab == null || gameController == null || gameController.IsFinished)
        {
            return;
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f || activeEnemies.Count >= maximumActiveEnemies)
        {
            return;
        }

        SpawnEnemy();
        spawnTimer = gameController.GetEnemySpawnInterval();
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

            PlaneEnemyChaser chaser = enemy.GetComponent<PlaneEnemyChaser>();
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
        if (!TryFindSpawnPosition(out Vector3 position))
        {
            return;
        }

        GameObject enemy = Instantiate(enemyPrefab, position, Quaternion.identity);
        enemy.name = $"ENEMY_Plane_{activeEnemies.Count + 1:00}";
        enemy.SetActive(true);
        PlaneEnemyChaser chaser = enemy.GetComponent<PlaneEnemyChaser>();
        if (chaser != null)
        {
            Transform bankPivot = enemy.transform.Find("BankPivot");
            chaser.Configure(player, gameController, bankPivot);
        }

        activeEnemies.Add(enemy);
        Collider enemyCollider = enemy.GetComponent<Collider>();
        if (enemyCollider != null)
        {
            StartCoroutine(TemporarilyDisableCollision(enemyCollider));
        }
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
        Vector3 playerForward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        float exclusionHalfAngle = Mathf.Clamp(forwardSpawnExclusionHalfAngle, 0f, 180f);
        float allowedArc = 360f - exclusionHalfAngle * 2f;
        if (allowedArc <= 0f || playerForward.sqrMagnitude < 0.001f)
        {
            spawnPosition = default;
            return false;
        }

        for (int attempt = 0; attempt < spawnSearchAttempts; attempt++)
        {
            float yawOffset = exclusionHalfAngle + Random.value * allowedArc;
            Vector3 direction = Quaternion.AngleAxis(yawOffset, Vector3.up) * playerForward;
            float distanceToEdge = GetDistanceToCameraEdge(
                player.position,
                direction,
                left,
                right,
                bottom,
                top);
            float extraDistance = Random.Range(1f, Mathf.Max(1f, offscreenSpawnDistance));
            float spawnDistance = Mathf.Max(minimumSpawnDistance, distanceToEdge + extraDistance);
            Vector3 candidate = player.position + direction * spawnDistance;
            candidate.y = player.position.y;
            bool insideCamera = candidate.x >= left && candidate.x <= right
                && candidate.z >= bottom && candidate.z <= top;
            bool tooClose = Vector3.ProjectOnPlane(
                candidate - player.position,
                Vector3.up).sqrMagnitude < minimumSpawnDistance * minimumSpawnDistance;
            if (insideCamera || tooClose ||
                Vector3.Angle(playerForward, direction) <= exclusionHalfAngle)
            {
                continue;
            }

            spawnPosition = candidate;
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

        float verticalDistance = float.PositiveInfinity;
        if (direction.x > 0.001f)
        {
            verticalDistance = (right - origin.x) / direction.x;
        }
        else if (direction.x < -0.001f)
        {
            verticalDistance = (left - origin.x) / direction.x;
        }

        float horizontalDistance = float.PositiveInfinity;
        if (direction.z > 0.001f)
        {
            horizontalDistance = (top - origin.z) / direction.z;
        }
        else if (direction.z < -0.001f)
        {
            horizontalDistance = (bottom - origin.z) / direction.z;
        }

        return Mathf.Max(0f, Mathf.Min(verticalDistance, horizontalDistance));
    }

    private IEnumerator TemporarilyDisableCollision(Collider enemyCollider)
    {
        enemyCollider.enabled = false;
        yield return new WaitForSeconds(spawnCollisionGrace);
        if (enemyCollider != null)
        {
            enemyCollider.enabled = true;
        }
    }
}
