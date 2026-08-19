using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class Level02WhirlpoolSpawner : MonoBehaviour
{
    private const string LevelName = "Level02";
    private const float OceanLimit = 1880f;

    [SerializeField, Min(0f)] private float initialDelay = 4.5f;
    [SerializeField, Min(1f)] private float spawnInterval = 8.5f;
    [SerializeField, Min(1)] private int maximumActiveWhirlpools = 4;
    [SerializeField, Min(4f)] private float whirlpoolLifetime = 22f;
    [SerializeField] private Vector2 forwardSpawnRange = new Vector2(76f, 98f);

    private readonly List<Level02Whirlpool> activeWhirlpools =
        new List<Level02Whirlpool>();
    private Transform player;
    private SimplePlayerHealth playerHealth;
    private SurvivalGameController survivalController;
    private float spawnTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != LevelName || FindObjectOfType<Level02WhirlpoolSpawner>() != null)
        {
            return;
        }

        GameObject spawnerObject = new GameObject("SYS_Level02WhirlpoolSpawner");
        SceneManager.MoveGameObjectToScene(spawnerObject, scene);
        spawnerObject.AddComponent<Level02WhirlpoolSpawner>();
    }

    private void Awake()
    {
        ResolveReferences();
        spawnTimer = initialDelay;
    }

    private void Update()
    {
        ResolveReferences();
        RemoveExpiredWhirlpools();
        if (player == null || playerHealth == null || playerHealth.CurrentHealth <= 0)
        {
            return;
        }
        if (survivalController != null && survivalController.IsFinished)
        {
            ClearWhirlpools();
            return;
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f || activeWhirlpools.Count >= maximumActiveWhirlpools)
        {
            return;
        }

        SpawnWhirlpool();
        spawnTimer = spawnInterval * Random.Range(0.88f, 1.12f);
    }

    private void SpawnWhirlpool()
    {
        Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }
        float forwardDistance = Random.Range(forwardSpawnRange.x, forwardSpawnRange.y);

        Vector3 position = player.position + forward * forwardDistance;
        position.x = Mathf.Clamp(position.x, -OceanLimit, OceanLimit);
        position.y = 0.02f;
        position.z = Mathf.Clamp(position.z, -OceanLimit, OceanLimit);

        GameObject whirlpoolObject = new GameObject("HAZ_Level02Whirlpool");
        whirlpoolObject.transform.position = position;
        Level02Whirlpool whirlpool = whirlpoolObject.AddComponent<Level02Whirlpool>();
        whirlpool.Initialize(whirlpoolLifetime);
        activeWhirlpools.Add(whirlpool);
    }

    private void RemoveExpiredWhirlpools()
    {
        for (int index = activeWhirlpools.Count - 1; index >= 0; index--)
        {
            Level02Whirlpool whirlpool = activeWhirlpools[index];
            if (whirlpool == null)
            {
                activeWhirlpools.RemoveAt(index);
                continue;
            }

            Vector3 toWhirlpool = whirlpool.transform.position - player.position;
            toWhirlpool.y = 0f;
            bool farBehind = Vector3.Dot(player.forward, toWhirlpool) < -90f;
            if (farBehind || toWhirlpool.sqrMagnitude > 220f * 220f)
            {
                Destroy(whirlpool.gameObject);
                activeWhirlpools.RemoveAt(index);
            }
        }
    }

    private void ClearWhirlpools()
    {
        foreach (Level02Whirlpool whirlpool in activeWhirlpools)
        {
            if (whirlpool != null)
            {
                Destroy(whirlpool.gameObject);
            }
        }
        activeWhirlpools.Clear();
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
        }
        if (playerHealth == null && player != null)
        {
            playerHealth = player.GetComponent<SimplePlayerHealth>();
        }
        if (survivalController == null)
        {
            survivalController = FindObjectOfType<SurvivalGameController>();
        }
    }
}
