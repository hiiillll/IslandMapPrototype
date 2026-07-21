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
    private bool endlessMode;
    private float finalEnemyChaseSpeedAdvantage;
    private float finalEnemySpawnInterval;

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
        endlessMode = false;
    }

    public void ConfigureEndless(
        float fixedPlayerSpeed,
        float growthDuration,
        float startingEnemySpeedAdvantage,
        float endingEnemySpeedAdvantage,
        float startingSpawnInterval,
        float endingSpawnInterval)
    {
        playerStartSpeed = Mathf.Max(0f, fixedPlayerSpeed);
        playerSpeedIncreasePerSecond = 0f;
        maxPlayerSpeed = playerStartSpeed;
        difficultyRampDuration = Mathf.Max(1f, growthDuration);
        enemyChaseSpeedAdvantage = Mathf.Max(0f, startingEnemySpeedAdvantage);
        finalEnemyChaseSpeedAdvantage = Mathf.Max(0f, endingEnemySpeedAdvantage);
        initialEnemySpawnInterval = Mathf.Max(0.01f, startingSpawnInterval);
        finalEnemySpawnInterval = Mathf.Max(0.01f, endingSpawnInterval);
        elapsedDifficultyTime = 0f;
        endlessMode = true;
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
        float advantage = endlessMode
            ? Mathf.Lerp(enemyChaseSpeedAdvantage, finalEnemyChaseSpeedAdvantage, GetEndlessProgress())
            : enemyChaseSpeedAdvantage;
        return GetPlayerForwardSpeed() + advantage;
    }

    public float GetSpawnInterval()
    {
        if (endlessMode)
        {
            return Mathf.Lerp(initialEnemySpawnInterval, finalEnemySpawnInterval, GetEndlessProgress());
        }

        float interval = initialEnemySpawnInterval
            - enemySpawnIntervalShortenPerSecond * elapsedDifficultyTime;
        return Mathf.Max(minimumEnemySpawnInterval, interval);
    }

    public int GetMaximumActiveEnemies()
    {
        if (!endlessMode)
        {
            return 12;
        }

        float progress = GetEndlessProgress();
        return progress >= 1f ? 24 : Mathf.FloorToInt(Mathf.Lerp(12f, 24f, progress));
    }

    private float GetEndlessProgress()
    {
        float progress = difficultyRampDuration > 0f
            ? Mathf.Clamp01(elapsedDifficultyTime / difficultyRampDuration)
            : 1f;
        return Mathf.SmoothStep(0f, 1f, progress);
    }
}
