using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class WarshipEnemySpawner : MonoBehaviour
{
    [Header("Story Mode")]
    [SerializeField, Min(1f)] private float storySurvivalDuration = 120f;
    [SerializeField, Range(0f, 1f)] private float storyStartingDifficultyProgress = 1f / 3f;

    [SerializeField] private Transform player;
    [SerializeField] private GameObject enemyVisualPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float spawnInterval = 4.3f;
    [SerializeField] private int maxEnemies = 6;
    [SerializeField, Range(0.1f, 1.5f)] private float playerSpeedRatio = 0.9f;
    [SerializeField] private float warshipDoorSpawnOffset = 2.5f;
    [SerializeField] private float enemySizeMultiplier = 1f;
    [Header("Difficulty Over Time")]
    [SerializeField] private float finalSpawnInterval = 1.2f;
    [SerializeField] private int finalMaxEnemies = 16;
    [SerializeField, Range(0.1f, 1.5f)] private float finalPlayerSpeedRatio = 1.05f;

    private float nextSpawnTime;
    private int nextSpawnPointIndex;
    private float initialSpawnInterval;
    private int initialMaxEnemies;
    private float initialPlayerSpeedRatio;
    private bool spawningEnabled = true;
    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private PhysicMaterial drivingMaterial;
    private SurvivalGameController survivalController;

    private void Awake()
    {
        playerSpeedRatio = 0.9f;
        finalPlayerSpeedRatio = 1.05f;
        initialSpawnInterval = spawnInterval;
        initialMaxEnemies = maxEnemies;
        initialPlayerSpeedRatio = playerSpeedRatio;

        if (GetComponent<EnvironmentCollisionRefiner>() == null)
        {
            gameObject.AddComponent<EnvironmentCollisionRefiner>();
        }

        survivalController = GetComponent<SurvivalGameController>();
        if (survivalController == null)
        {
            survivalController = gameObject.AddComponent<SurvivalGameController>();
        }
        if (!GameModeSession.IsEndless)
        {
            survivalController.Configure(storySurvivalDuration, true);
        }

        drivingMaterial = new PhysicMaterial("EnemyCar_Frictionless")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicMaterialCombine.Minimum,
            bounceCombine = PhysicMaterialCombine.Minimum
        };
    }

    public void Configure(Transform playerTarget, GameObject visualPrefab, Transform[] points)
    {
        player = playerTarget;
        enemyVisualPrefab = visualPrefab;
        spawnPoints = points;
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
        }
        AlignSpawnPointsToWarshipDoors();
        nextSpawnTime = Time.time + 1f;
    }

    private void Update()
    {
        activeEnemies.RemoveAll(enemy => enemy == null);
        if (!spawningEnabled || (survivalController != null && survivalController.IsFinished))
        {
            return;
        }

        if (survivalController != null)
        {
            ApplyDifficulty(survivalController.DifficultyProgress);
        }

        if (Time.time < nextSpawnTime || activeEnemies.Count >= maxEnemies || player == null
            || enemyVisualPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            return;
        }

        SpawnEnemy();
        nextSpawnTime = Time.time + spawnInterval;
    }

    public void StopSpawningAndClearEnemies()
    {
        spawningEnabled = false;
        foreach (GameObject enemy in activeEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }
        activeEnemies.Clear();
    }

    private void ApplyDifficulty(float progress)
    {
        float effectiveProgress = survivalController != null && !survivalController.IsEndless
            ? Mathf.Lerp(storyStartingDifficultyProgress, 1f, progress)
            : progress;
        spawnInterval = Mathf.Lerp(initialSpawnInterval, finalSpawnInterval, effectiveProgress);
        float enemyLimit = Mathf.Lerp(initialMaxEnemies, finalMaxEnemies, effectiveProgress);
        maxEnemies = survivalController != null && survivalController.IsEndless
            ? effectiveProgress >= 1f ? finalMaxEnemies : Mathf.FloorToInt(enemyLimit)
            : Mathf.RoundToInt(enemyLimit);
        playerSpeedRatio = Mathf.Lerp(
            initialPlayerSpeedRatio,
            finalPlayerSpeedRatio,
            effectiveProgress);

        foreach (GameObject enemy in activeEnemies)
        {
            if (enemy == null)
            {
                continue;
            }

            NavMeshEnemyCarChaser chaser = enemy.GetComponent<NavMeshEnemyCarChaser>();
            if (chaser != null)
            {
                chaser.SetSpeedRatio(playerSpeedRatio);
            }
        }
    }

    private void SpawnEnemy()
    {
        Transform spawnPoint = spawnPoints[nextSpawnPointIndex % spawnPoints.Length];
        nextSpawnPointIndex++;
        if (!NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit hit, 20f, NavMesh.AllAreas))
        {
            Debug.LogWarning($"Enemy spawn point '{spawnPoint.name}' has no nearby baked NavMesh position.", this);
            return;
        }
        Vector3 position = hit.position;

        GameObject enemy = new GameObject($"ENEMY_Car_{activeEnemies.Count + 1:00}");
        enemy.transform.position = position;
        enemy.transform.rotation = spawnPoint.rotation;

        GameObject visual = Instantiate(enemyVisualPrefab, enemy.transform);
        visual.name = "Visual_Model14";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        ResetImportedRootOffset(visual.transform);
        FitVisualToPlayer(enemy, visual);

        BoxCollider collider = enemy.AddComponent<BoxCollider>();
        Bounds localBounds = CalculateLocalBounds(enemy);
        collider.center = localBounds.center;
        collider.size = localBounds.size;
        collider.material = drivingMaterial;

        enemy.AddComponent<Rigidbody>();
        NavMeshEnemyCarChaser chaser = enemy.AddComponent<NavMeshEnemyCarChaser>();
        chaser.Configure(player, playerSpeedRatio);
        activeEnemies.Add(enemy);
    }

    private void FitVisualToPlayer(GameObject enemy, GameObject visual)
    {
        Bounds visualBounds = CalculateWorldBounds(visual);
        CapsuleCollider playerCollider = player.GetComponent<CapsuleCollider>();
        float targetLength = playerCollider != null ? playerCollider.height * enemySizeMultiplier : 5f;
        float horizontalSize = Mathf.Max(visualBounds.size.x, visualBounds.size.z);
        visual.transform.localScale = Vector3.one * (targetLength / Mathf.Max(horizontalSize, 0.01f));

        Bounds localBounds = CalculateLocalBounds(enemy);
        visual.transform.localPosition -= new Vector3(localBounds.center.x, localBounds.min.y, localBounds.center.z);
    }

    private static void ResetImportedRootOffset(Transform visual)
    {
        foreach (Transform child in visual)
        {
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
        }
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

    private void OnDestroy()
    {
        if (drivingMaterial != null)
        {
            Destroy(drivingMaterial);
        }
    }

    private void AlignSpawnPointsToWarshipDoors()
    {
        GameObject beachObject = GameObject.Find("COL_Beach");
        GameObject warshipRoot = GameObject.Find("ENV_Warships");
        if (warshipRoot == null || spawnPoints == null)
        {
            return;
        }

        BoxCollider beachCollider = beachObject != null ? beachObject.GetComponent<BoxCollider>() : null;
        float spawnHeight = beachCollider != null ? beachCollider.bounds.max.y + 0.2f : 0.2f;

        List<Transform> warships = new List<Transform>();
        foreach (Transform child in warshipRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.StartsWith("ENV_Warship_"))
            {
                warships.Add(child);
            }
        }

        List<Transform> markers = new List<Transform>(spawnPoints);
        warships.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        markers.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        int pairCount = Mathf.Min(warships.Count, markers.Count);
        for (int index = 0; index < pairCount; index++)
        {
            Transform warship = warships[index];
            Transform marker = markers[index];
            Vector3 exitDirection = warship.forward;
            exitDirection.y = 0f;
            if (exitDirection.sqrMagnitude < 0.01f)
            {
                exitDirection = Vector3.forward;
            }
            exitDirection.Normalize();

            Vector3 exitPosition = GetWarshipForwardEdge(warship, exitDirection);
            marker.position = new Vector3(
                exitPosition.x + exitDirection.x * warshipDoorSpawnOffset,
                spawnHeight,
                exitPosition.z + exitDirection.z * warshipDoorSpawnOffset);
            marker.rotation = Quaternion.LookRotation(exitDirection, Vector3.up);
        }

        Debug.Log($"Aligned {pairCount} enemy spawn points to the warship doors.", this);
    }

    private static Vector3 GetWarshipForwardEdge(Transform warship, Vector3 forward)
    {
        Renderer[] renderers = warship.GetComponentsInChildren<Renderer>(true);
        float furthestProjection = float.NegativeInfinity;
        foreach (Renderer renderer in renderers)
        {
            Bounds bounds = renderer.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z)
            };
            foreach (Vector3 corner in corners)
            {
                furthestProjection = Mathf.Max(furthestProjection, Vector3.Dot(corner, forward));
            }
        }

        if (float.IsNegativeInfinity(furthestProjection))
        {
            return warship.position;
        }

        float centerProjection = Vector3.Dot(warship.position, forward);
        return warship.position + forward * (furthestProjection - centerProjection);
    }

    private void OnDrawGizmos()
    {
        if (spawnPoints == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        foreach (Transform spawnPoint in spawnPoints)
        {
            if (spawnPoint == null)
            {
                continue;
            }

            Gizmos.DrawSphere(spawnPoint.position, 1.2f);
            Gizmos.DrawLine(spawnPoint.position, spawnPoint.position + spawnPoint.forward * 6f);
        }
    }
}
