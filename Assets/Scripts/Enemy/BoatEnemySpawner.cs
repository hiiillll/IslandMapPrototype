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
    [SerializeField, Min(1)] private int spawnSearchAttempts = 30;
    [SerializeField, Min(0f)] private float spawnCollisionGrace = 0.15f;
    [SerializeField, Min(1)] private int maximumActiveEnemies = 12;

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
        enemy.transform.rotation = Quaternion.identity;

        GameObject visual = Instantiate(enemyVisualPrefab, enemy.transform);
        visual.name = "Visual_Model17";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        visual.transform.localScale = Vector3.one;
        FitVisualToPlayer(enemy, visual);

        Bounds localBounds = CalculateLocalBounds(enemy);
        BoxCollider enemyCollider = enemy.AddComponent<BoxCollider>();
        enemyCollider.center = localBounds.center;
        enemyCollider.size = localBounds.size;
        enemyCollider.isTrigger = true;

        Rigidbody enemyBody = enemy.AddComponent<Rigidbody>();
        enemyBody.mass = 800f;

        BoatEnemyChaser chaser = enemy.AddComponent<BoatEnemyChaser>();
        chaser.Configure(player, difficultyController);
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

        for (int attempt = 0; attempt < spawnSearchAttempts; attempt++)
        {
            float randomX = Random.Range(left - offscreenSpawnDistance, right + offscreenSpawnDistance);
            float randomZ = Random.Range(bottom - offscreenSpawnDistance, top + offscreenSpawnDistance);
            bool insideCamera = randomX >= left && randomX <= right
                && randomZ >= bottom && randomZ <= top;
            if (insideCamera)
            {
                continue;
            }

            if (GameModeSession.IsEndlessSea)
            {
                randomX = Mathf.Clamp(randomX, -1980f, 1980f);
                randomZ = Mathf.Clamp(randomZ, -1980f, 1980f);
            }

            spawnPosition = new Vector3(randomX, spawnHeight, randomZ);
            return true;
        }

        spawnPosition = default;
        return false;
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
