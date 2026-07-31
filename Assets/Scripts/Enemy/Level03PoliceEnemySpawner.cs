using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public sealed class Level03PoliceEnemySpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private GameObject policeCarVisualPrefab;
    [SerializeField] private Transform[] spawnPoints = new Transform[0];

    [Header("Initial Rhythm")]
    [SerializeField, Min(0f)] private float initialDelay = 1f;
    [SerializeField, Min(0.1f)] private float initialSpawnInterval = 4.3f;
    [SerializeField, Min(1)] private int initialMaximumEnemies = 6;
    [SerializeField, Range(0.1f, 1.5f)] private float initialPlayerSpeedRatio = 0.9f;

    [Header("Difficulty Over Time")]
    [SerializeField, Min(1f)] private float difficultyDuration = 120f;
    [SerializeField, Range(0f, 1f)] private float startingDifficultyProgress = 1f / 3f;
    [SerializeField, Min(0.1f)] private float finalSpawnInterval = 1.2f;
    [SerializeField, Min(1)] private int finalMaximumEnemies = 16;
    [SerializeField, Range(0.1f, 1.5f)] private float finalPlayerSpeedRatio = 1.05f;

    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private float nextSpawnTime;
    private float elapsedTime;
    private float currentSpawnInterval;
    private int currentMaximumEnemies;
    private float currentPlayerSpeedRatio;
    private bool spawningEnabled = true;
    private PhysicMaterial drivingMaterial;
    private int nextSpawnPointIndex;

    public Transform Player => player;
    public GameObject PoliceCarVisualPrefab => policeCarVisualPrefab;
    public Transform SpawnPoint => spawnPoints != null && spawnPoints.Length > 0
        ? spawnPoints[0]
        : null;
    public IReadOnlyList<Transform> SpawnPoints => spawnPoints;
    public float InitialSpawnInterval => initialSpawnInterval;
    public int InitialMaximumEnemies => initialMaximumEnemies;
    public float FinalSpawnInterval => finalSpawnInterval;
    public int FinalMaximumEnemies => finalMaximumEnemies;
    public int ActiveEnemyCount => activeEnemies.Count;

    private void Awake()
    {
        currentSpawnInterval = initialSpawnInterval;
        currentMaximumEnemies = initialMaximumEnemies;
        currentPlayerSpeedRatio = initialPlayerSpeedRatio;
        drivingMaterial = new PhysicMaterial("Level03PoliceCar_Frictionless")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicMaterialCombine.Minimum,
            bounceCombine = PhysicMaterialCombine.Minimum
        };
    }

    private void Start()
    {
        ResolvePlayer();
        nextSpawnTime = Time.time + initialDelay;
    }

    private void Update()
    {
        activeEnemies.RemoveAll(enemy => enemy == null);
        if (!spawningEnabled)
        {
            return;
        }

        ResolvePlayer();
        elapsedTime += Time.deltaTime;
        ApplyDifficulty(Mathf.Clamp01(elapsedTime / difficultyDuration));
        if (Time.time < nextSpawnTime ||
            activeEnemies.Count >= currentMaximumEnemies ||
            player == null ||
            policeCarVisualPrefab == null ||
            !HasSpawnPoint())
        {
            return;
        }

        SpawnEnemy();
        nextSpawnTime = Time.time + currentSpawnInterval;
    }

    public void Configure(
        Transform playerTarget,
        GameObject policeCarPrefab,
        params Transform[] configuredSpawnPoints)
    {
        player = playerTarget;
        policeCarVisualPrefab = policeCarPrefab;
        spawnPoints = configuredSpawnPoints ?? new Transform[0];
        nextSpawnPointIndex = 0;
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

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;
    }

    private void ApplyDifficulty(float progress)
    {
        float effectiveProgress = Mathf.Lerp(startingDifficultyProgress, 1f, progress);
        currentSpawnInterval = Mathf.Lerp(
            initialSpawnInterval,
            finalSpawnInterval,
            effectiveProgress);
        currentMaximumEnemies = Mathf.RoundToInt(Mathf.Lerp(
            initialMaximumEnemies,
            finalMaximumEnemies,
            effectiveProgress));
        currentPlayerSpeedRatio = Mathf.Lerp(
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
                chaser.SetSpeedRatio(currentPlayerSpeedRatio);
            }
        }
    }

    private void SpawnEnemy()
    {
        Transform spawnPoint = GetNextSpawnPoint();
        if (spawnPoint == null)
        {
            return;
        }

        int walkableArea = NavMesh.GetAreaFromName("Walkable");
        int areaMask = walkableArea >= 0 ? 1 << walkableArea : NavMesh.AllAreas;
        if (!NavMesh.SamplePosition(
                spawnPoint.position,
                out NavMeshHit spawnHit,
                24f,
                areaMask))
        {
            Debug.LogWarning(
                $"Police spawn point '{spawnPoint.name}' has no nearby baked NavMesh.",
                this);
            return;
        }

        GameObject enemy = new GameObject($"ENEMY_Level03_PoliceCar_{activeEnemies.Count + 1:00}");
        enemy.transform.SetParent(transform, true);
        enemy.transform.SetPositionAndRotation(spawnHit.position, spawnPoint.rotation);

        GameObject visual = Instantiate(policeCarVisualPrefab, enemy.transform);
        visual.name = "Visual_PoliceCar";
        visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        ResetImportedRootOffset(visual.transform);
        if (!FitVisualToPlayer(enemy, visual))
        {
            Destroy(enemy);
            return;
        }

        CapsuleCollider collider = enemy.AddComponent<CapsuleCollider>();
        CopyPlayerCollider(collider);
        collider.material = drivingMaterial;

        Rigidbody enemyBody = enemy.AddComponent<Rigidbody>();
        Rigidbody sourceBody = player != null ? player.GetComponent<Rigidbody>() : null;
        if (sourceBody != null)
        {
            enemyBody.mass = sourceBody.mass;
        }
        NavMeshEnemyCarChaser chaser = enemy.AddComponent<NavMeshEnemyCarChaser>();
        chaser.Configure(player, currentPlayerSpeedRatio);
        activeEnemies.Add(enemy);
    }

    private bool HasSpawnPoint()
    {
        if (spawnPoints == null)
        {
            return false;
        }

        foreach (Transform candidate in spawnPoints)
        {
            if (candidate != null)
            {
                return true;
            }
        }
        return false;
    }

    private Transform GetNextSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return null;
        }

        for (int offset = 0; offset < spawnPoints.Length; offset++)
        {
            int index = (nextSpawnPointIndex + offset) % spawnPoints.Length;
            Transform candidate = spawnPoints[index];
            if (candidate == null)
            {
                continue;
            }

            nextSpawnPointIndex = (index + 1) % spawnPoints.Length;
            return candidate;
        }
        return null;
    }

    private void CopyPlayerCollider(CapsuleCollider target)
    {
        CapsuleCollider source = player != null
            ? player.GetComponent<CapsuleCollider>()
            : null;
        if (source != null)
        {
            target.center = source.center;
            target.radius = source.radius;
            target.height = source.height;
            target.direction = source.direction;
            return;
        }

        target.center = new Vector3(0f, 0.9f, 0f);
        target.radius = 0.82f;
        target.height = 5.5f;
        target.direction = 2;
    }

    private bool FitVisualToPlayer(GameObject enemy, GameObject visual)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogError("The Level03 police car model has no renderers.", this);
            return false;
        }

        Bounds visualBounds = CalculateWorldBounds(renderers);
        CapsuleCollider playerCollider = player != null
            ? player.GetComponent<CapsuleCollider>()
            : null;
        float targetLength = playerCollider != null ? playerCollider.height : 5f;
        float horizontalSize = Mathf.Max(visualBounds.size.x, visualBounds.size.z);
        visual.transform.localScale =
            Vector3.one * (targetLength / Mathf.Max(horizontalSize, 0.01f));

        Bounds localBounds = CalculateLocalBounds(enemy);
        visual.transform.localPosition -= new Vector3(
            localBounds.center.x,
            localBounds.min.y,
            localBounds.center.z);
        return true;
    }

    private static void ResetImportedRootOffset(Transform visual)
    {
        foreach (Transform child in visual)
        {
            child.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
    }

    private static Bounds CalculateWorldBounds(Renderer[] renderers)
    {
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
        Bounds bounds = new Bounds();
        bool initialized = false;
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

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        foreach (Transform spawnPoint in spawnPoints)
        {
            if (spawnPoint == null)
            {
                continue;
            }

            Gizmos.DrawSphere(spawnPoint.position, 1.2f);
            Gizmos.DrawLine(
                spawnPoint.position,
                spawnPoint.position + spawnPoint.forward * 6f);
        }
    }
}
