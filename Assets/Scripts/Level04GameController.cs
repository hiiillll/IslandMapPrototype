using System.Collections;
using UnityEngine;

public sealed class Level04GameController : MonoBehaviour
{
    [SerializeField, Min(1)] private int targetFrameRate = 100;
    [SerializeField, Min(1f)] private float difficultyRampDuration = 50f;
    [SerializeField, Min(0f)] private float playerStartSpeed = 36f;
    [SerializeField, Min(0f)] private float playerSpeedIncreasePerSecond = 0.12f;
    [SerializeField, Min(0f)] private float enemySpeedAdvantage = 1.5f;
    [SerializeField, Min(0.05f)] private float initialSpawnInterval = 2.5f;
    [SerializeField, Min(0f)] private float spawnIntervalShortenPerSecond = 0.04f;
    [SerializeField, Min(0.05f)] private float minimumSpawnInterval = 0.55f;
    [SerializeField, Min(0f)] private float chapterCompletionDelay = 2.25f;
    [SerializeField] private SimplePlayerHealth playerHealth;
    [SerializeField] private PlaneEnemySpawner enemySpawner;
    [SerializeField] private SurvivalGameController survivalController;

    private bool hasFailed;
    private bool completionCleanupHandled;
    private bool completionTransitionStarted;
    private int previousTargetFrameRate;
    private int previousVSyncCount;
    private bool frameRateOverrideApplied;
    private GUIStyle completionHeadingStyle;
    private GUIStyle completionBodyStyle;

    public bool IsFinished => hasFailed ||
        (survivalController != null && survivalController.IsFinished);

    public void Configure(
        SimplePlayerHealth health,
        PlaneEnemySpawner spawner,
        SurvivalGameController survival)
    {
        playerHealth = health;
        enemySpawner = spawner;
        survivalController = survival;
    }

    private void Awake()
    {
        previousTargetFrameRate = Application.targetFrameRate;
        previousVSyncCount = QualitySettings.vSyncCount;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = Mathf.Max(1, targetFrameRate);
        frameRateOverrideApplied = true;

        Time.timeScale = 1f;
        hasFailed = false;
        completionCleanupHandled = false;
        completionTransitionStarted = false;
    }

    private void OnDestroy()
    {
        if (!frameRateOverrideApplied)
        {
            return;
        }

        Application.targetFrameRate = previousTargetFrameRate;
        QualitySettings.vSyncCount = previousVSyncCount;
        frameRateOverrideApplied = false;
    }

    private void Update()
    {
        if (hasFailed)
        {
            return;
        }

        if (playerHealth != null && playerHealth.CurrentHealth <= 0)
        {
            hasFailed = true;
            if (enemySpawner != null)
            {
                enemySpawner.StopSpawningAndClearEnemies();
            }
            return;
        }

        if (!completionCleanupHandled && survivalController != null && survivalController.IsFinished)
        {
            completionCleanupHandled = true;
            if (enemySpawner != null)
            {
                enemySpawner.StopSpawningAndClearEnemies();
            }

            StartCoroutine(CompleteChapterAfterDelay());
        }
    }

    private IEnumerator CompleteChapterAfterDelay()
    {
        if (completionTransitionStarted)
        {
            yield break;
        }

        completionTransitionStarted = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        if (chapterCompletionDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(chapterCompletionDelay);
        }

        GameModeSession.CompleteChapterTwoAndReturnToMenu();
    }

    private void OnGUI()
    {
        if (!completionTransitionStarted)
        {
            return;
        }

        EnsureCompletionStyles();
        GUI.depth = -1200;
        const float width = 520f;
        const float height = 150f;
        Rect panel = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(
            new Rect(panel.x + 20f, panel.y + 20f, panel.width - 40f, 58f),
            "\u7a7a\u4e2d\u8ffd\u51fb\u5b8c\u6210",
            completionHeadingStyle);
        GUI.Label(
            new Rect(panel.x + 20f, panel.y + 82f, panel.width - 40f, 42f),
            "\u7b2c\u4e8c\u7ae0\u5df2\u901a\u5173",
            completionBodyStyle);
    }

    private void EnsureCompletionStyles()
    {
        if (completionHeadingStyle != null)
        {
            return;
        }

        completionHeadingStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.78f, 0.12f) }
        };
        completionBodyStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
    }

    public float GetPlayerForwardSpeed()
    {
        return playerStartSpeed + playerSpeedIncreasePerSecond * GetDifficultyTime();
    }

    public float GetEnemyForwardSpeed()
    {
        return GetPlayerForwardSpeed() + enemySpeedAdvantage;
    }

    public float GetEnemySpawnInterval()
    {
        return Mathf.Max(
            minimumSpawnInterval,
            initialSpawnInterval - spawnIntervalShortenPerSecond * GetDifficultyTime());
    }

    private float GetDifficultyTime()
    {
        float elapsed = survivalController != null ? survivalController.ElapsedTime : Time.timeSinceLevelLoad;
        return Mathf.Min(elapsed, difficultyRampDuration);
    }
}
