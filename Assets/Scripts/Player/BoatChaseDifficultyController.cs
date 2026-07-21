using UnityEngine;

public sealed class BoatChaseDifficultyController : MonoBehaviour
{
    [Header("Player Speed")]
    [SerializeField, Min(0f)] private float playerStartSpeed = 30f;
    [SerializeField, Min(0f)] private float playerSpeedIncreasePerSecond = 0.12f;
    [SerializeField, Min(0f)] private float maxPlayerSpeed;
    [SerializeField, Min(0f)] private float difficultyRampDuration = 50f;

    [Header("Enemy Pressure")]
    [SerializeField, Min(0f)] private float enemyChaseSpeedAdvantage = 0.5f;
    [SerializeField, Min(0.01f)] private float initialEnemySpawnInterval = 2.5f;
    [SerializeField, Min(0f)] private float enemySpawnIntervalShortenPerSecond = 0.04f;
    [SerializeField, Min(0.01f)] private float minimumEnemySpawnInterval = 0.55f;

    private SurvivalGameController survivalController;
    private float elapsedDifficultyTime;

    public float ElapsedDifficultyTime => elapsedDifficultyTime;
    public float DifficultyRampDuration => difficultyRampDuration;

    public void Configure(
        float startSpeed,
        float speedIncreasePerSecond,
        float maximumSpeed,
        float enemySpeedAdvantage,
        float startingSpawnInterval,
        float spawnIntervalShortenPerSecond,
        float minimumSpawnInterval,
        float growthDuration = 50f)
    {
        playerStartSpeed = Mathf.Max(0f, startSpeed);
        playerSpeedIncreasePerSecond = Mathf.Max(0f, speedIncreasePerSecond);
        maxPlayerSpeed = Mathf.Max(0f, maximumSpeed);
        enemyChaseSpeedAdvantage = Mathf.Max(0f, enemySpeedAdvantage);
        initialEnemySpawnInterval = Mathf.Max(0.01f, startingSpawnInterval);
        enemySpawnIntervalShortenPerSecond = Mathf.Max(0f, spawnIntervalShortenPerSecond);
        minimumEnemySpawnInterval = Mathf.Max(0.01f, minimumSpawnInterval);
        difficultyRampDuration = Mathf.Max(0f, growthDuration);
    }

    private void Awake()
    {
        survivalController = GetComponent<SurvivalGameController>();
        if (survivalController == null)
        {
            survivalController = FindObjectOfType<SurvivalGameController>();
        }

        elapsedDifficultyTime = 0f;
    }

    private void Update()
    {
        if (survivalController != null && survivalController.IsFinished)
        {
            return;
        }

        elapsedDifficultyTime = Mathf.Min(
            elapsedDifficultyTime + Time.deltaTime,
            difficultyRampDuration);
    }

    public float GetPlayerForwardSpeed()
    {
        float speed = playerStartSpeed + playerSpeedIncreasePerSecond * elapsedDifficultyTime;
        return maxPlayerSpeed > 0f ? Mathf.Min(speed, maxPlayerSpeed) : speed;
    }

    public float GetEnemyChaseSpeed()
    {
        return GetPlayerForwardSpeed() + enemyChaseSpeedAdvantage;
    }

    public float GetSpawnInterval()
    {
        float interval = initialEnemySpawnInterval
            - enemySpawnIntervalShortenPerSecond * elapsedDifficultyTime;
        return Mathf.Max(minimumEnemySpawnInterval, interval);
    }
}
