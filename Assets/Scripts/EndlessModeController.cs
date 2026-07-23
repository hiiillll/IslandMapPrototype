using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class EndlessModeController : MonoBehaviour
{
    private const float DifficultyDuration = 180f;
    private const float OceanHalfSize = 2000f;
    private const float BoundaryWarningDistance = 150f;
    private const float BoundaryWarningClearDistance = 175f;

    private SimplePlayerHealth health;
    private PlayerProgression progression;
    private PlayerSkillSystem skillSystem;
    private Transform player;
    private float elapsedTime;
    private float messageUntil;
    private string statusMessage;
    private bool paused;
    private bool showingResults;
    private bool boundaryWarning;
    private bool reachedOneMinute;
    private bool reachedTwoMinutes;
    private bool reachedMaximumDifficulty;
    private int resultTimeMilliseconds;
    private int resultKills;
    private int bestTimeMilliseconds;
    private int bestKills;
    private bool newTimeRecord;
    private bool newKillRecord;
    private GUIStyle titleStyle;
    private GUIStyle headingStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;

    public static EndlessModeController Instance { get; private set; }

    public float ElapsedTime => elapsedTime;
    public float DifficultyProgress => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsedTime / DifficultyDuration));
    public bool IsPaused => paused;
    public bool IsShowingResults => showingResults;
    public int CurrentKills => progression != null ? progression.DestroyedEnemies : 0;
    public bool IsRunActive => GameModeSession.IsEndlessSea
        || (skillSystem != null && skillSystem.IsGameplayActive);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoader()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForActiveScene();
    }

    public static void EnsureForActiveScene()
    {
        if (!GameModeSession.IsEndless || Instance != null)
        {
            return;
        }

        string expectedScene = GameModeSession.IsEndlessSea ? "Level02" : "IslandMap";
        if (SceneManager.GetActiveScene().name != expectedScene)
        {
            return;
        }

        GameObject controllerObject = new GameObject("SYS_EndlessModeController");
        controllerObject.AddComponent<EndlessModeController>();
    }

    public static int GetBestTimeMilliseconds(GameModeKind mode)
    {
        return PlayerPrefs.GetInt(GetRecordKey(mode, "BestTimeMs"), 0);
    }

    public static int GetBestKills(GameModeKind mode)
    {
        return PlayerPrefs.GetInt(GetRecordKey(mode, "BestKills"), 0);
    }

    public static string FormatResultTime(int milliseconds)
    {
        int tenths = Mathf.Max(0, milliseconds) / 100;
        int minutes = tenths / 600;
        int seconds = tenths / 10 % 60;
        return $"{minutes:00}:{seconds:00}.{tenths % 10}";
    }

    private static string GetRecordKey(GameModeKind mode, string suffix)
    {
        string modeName = mode == GameModeKind.EndlessSea ? "Sea" : "Land";
        return $"EndlessMode.{modeName}.{suffix}";
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
        ConfigureMode();
    }

    private void ResolveReferences()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            return;
        }

        player = playerObject.transform;
        health = playerObject.GetComponent<SimplePlayerHealth>();
        progression = playerObject.GetComponent<PlayerProgression>();
        skillSystem = playerObject.GetComponent<PlayerSkillSystem>();
    }

    private void ConfigureMode()
    {
        SurvivalGameController survival = FindObjectOfType<SurvivalGameController>();
        if (survival != null)
        {
            survival.ConfigureEndless(DifficultyDuration);
        }

        if (health != null)
        {
            health.ResetToFullHealth(3);
            health.ConfigureFallDefeat(GameModeSession.IsEndlessLand, -10f);
        }

        if (progression != null)
        {
            progression.ResetForNewLevel();
        }

        if (GameModeSession.IsEndlessSea)
        {
            BoatChaseDifficultyController difficulty = FindObjectOfType<BoatChaseDifficultyController>();
            if (difficulty != null)
            {
                difficulty.ConfigureEndless(30f, DifficultyDuration, 0.5f, 2f, 2.5f, 0.65f);
            }

        }

        Time.timeScale = GameModeSession.IsEndlessSea ? 1f : Time.timeScale;
        AudioListener.pause = false;
    }

    private void Update()
    {
        if (!GameModeSession.IsEndless)
        {
            Destroy(gameObject);
            return;
        }

        if (health == null || progression == null || player == null)
        {
            ResolveReferences();
        }

        if (showingResults)
        {
            return;
        }

        if (health != null && health.CurrentHealth <= 0)
        {
            FinishRun();
            return;
        }

        if (IsRunActive && Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }

        if (paused || !IsRunActive)
        {
            return;
        }

        elapsedTime += Time.deltaTime;
        UpdateMilestones();
        if (GameModeSession.IsEndlessSea)
        {
            UpdateBoundaryWarning();
        }
    }

    public void TogglePause()
    {
        if (showingResults || !IsRunActive)
        {
            return;
        }

        paused = !paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
    }

    public void ShowMaxHealthUpgrade()
    {
        ShowStatusMessage("MAX HP +1", 2f);
        PlayFeedbackTone(660f, 0.12f);
    }

    private void UpdateMilestones()
    {
        if (!reachedOneMinute && elapsedTime >= 60f)
        {
            reachedOneMinute = true;
            ShowStatusMessage("已生存 1 分钟", 2.5f);
        }
        if (!reachedTwoMinutes && elapsedTime >= 120f)
        {
            reachedTwoMinutes = true;
            ShowStatusMessage("已生存 2 分钟", 2.5f);
        }
        if (!reachedMaximumDifficulty && elapsedTime >= DifficultyDuration)
        {
            reachedMaximumDifficulty = true;
            ShowStatusMessage("最高难度已锁定", 3f);
            TriggerMilestoneShake();
            PlayFeedbackTone(760f, 0.18f);
        }
    }

    private void ShowStatusMessage(string message, float duration)
    {
        statusMessage = message;
        messageUntil = Time.unscaledTime + duration;
    }

    private static void TriggerMilestoneShake()
    {
        SimpleSpeedCameraFollow landCamera = FindObjectOfType<SimpleSpeedCameraFollow>();
        if (landCamera != null)
        {
            landCamera.Shake(0.22f, 0.2f);
        }

        BoatChaseTopDownCamera seaCamera = FindObjectOfType<BoatChaseTopDownCamera>();
        if (seaCamera != null)
        {
            seaCamera.Shake(0.25f, 0.3f);
        }
    }

    private void UpdateBoundaryWarning()
    {
        if (player == null)
        {
            return;
        }

        float distance = OceanHalfSize - Mathf.Max(Mathf.Abs(player.position.x), Mathf.Abs(player.position.z));
        if (!boundaryWarning && distance <= BoundaryWarningDistance)
        {
            boundaryWarning = true;
        }
        else if (boundaryWarning && distance >= BoundaryWarningClearDistance)
        {
            boundaryWarning = false;
        }
    }

    private void FinishRun()
    {
        showingResults = true;
        paused = false;
        resultTimeMilliseconds = Mathf.Max(0, Mathf.RoundToInt(elapsedTime * 1000f));
        resultKills = CurrentKills;
        GameModeKind mode = GameModeSession.CurrentMode;
        bestTimeMilliseconds = GetBestTimeMilliseconds(mode);
        bestKills = GetBestKills(mode);
        newTimeRecord = resultTimeMilliseconds > bestTimeMilliseconds;
        newKillRecord = resultKills > bestKills;

        if (newTimeRecord)
        {
            bestTimeMilliseconds = resultTimeMilliseconds;
            PlayerPrefs.SetInt(GetRecordKey(mode, "BestTimeMs"), bestTimeMilliseconds);
        }
        if (newKillRecord)
        {
            bestKills = resultKills;
            PlayerPrefs.SetInt(GetRecordKey(mode, "BestKills"), bestKills);
        }
        if (newTimeRecord || newKillRecord)
        {
            PlayerPrefs.Save();
            PlayFeedbackTone(880f, 0.2f);
        }

        StopAndClearEnemies();
        Time.timeScale = 0f;
        AudioListener.pause = false;
    }

    private static void StopAndClearEnemies()
    {
        WarshipEnemySpawner landSpawner = FindObjectOfType<WarshipEnemySpawner>();
        if (landSpawner != null)
        {
            landSpawner.StopSpawningAndClearEnemies();
        }

        BoatEnemySpawner seaSpawner = FindObjectOfType<BoatEnemySpawner>();
        if (seaSpawner != null)
        {
            seaSpawner.StopSpawningAndClearEnemies();
        }
    }

    private void PlayFeedbackTone(float frequency, float duration)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
        float[] samples = new float[sampleCount];
        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float progress = (float)sampleIndex / sampleCount;
            float envelope = Mathf.Sin(progress * Mathf.PI);
            samples[sampleIndex] = Mathf.Sin(2f * Mathf.PI * frequency * sampleIndex / sampleRate) * envelope * 0.18f;
        }

        AudioClip clip = AudioClip.Create("EndlessMode_Feedback", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        AudioSource source = GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0f;
        }
        source.PlayOneShot(clip);
        Destroy(clip, duration + 0.2f);
    }

    private void OnGUI()
    {
        if (!GameModeSession.IsEndless || !IsRunActive)
        {
            return;
        }

        EnsureStyles();
        GUI.depth = -1100;
        if (showingResults)
        {
            DrawResults();
            return;
        }
        if (paused)
        {
            DrawPauseMenu();
            return;
        }

        string difficultyText = elapsedTime < DifficultyDuration ? "难度提升中" : "最高难度";
        GUI.Label(new Rect((Screen.width - 260f) * 0.5f, 126f, 260f, 34f), difficultyText, bodyStyle);
        if (Time.unscaledTime < messageUntil)
        {
            GUI.Label(new Rect((Screen.width - 520f) * 0.5f, Screen.height * 0.25f, 520f, 64f), statusMessage, headingStyle);
        }
        if (boundaryWarning)
        {
            string direction = GetReturnDirection();
            GUI.Label(new Rect((Screen.width - 720f) * 0.5f, Screen.height * 0.18f, 720f, 70f),
                $"警告：即将离开海域  ·  向 {direction} 返回中心", titleStyle);
        }
    }

    private string GetReturnDirection()
    {
        if (player == null)
        {
            return "中心";
        }

        Vector3 direction = -player.position;
        string horizontal = direction.x > 100f ? "东" : direction.x < -100f ? "西" : string.Empty;
        string vertical = direction.z > 100f ? "北" : direction.z < -100f ? "南" : string.Empty;
        return string.IsNullOrEmpty(vertical + horizontal) ? "中心" : vertical + horizontal;
    }

    private void DrawPauseMenu()
    {
        DrawScreenDim();
        Rect panel = CenteredPanel(520f, 330f);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(new Rect(panel.x + 30f, panel.y + 34f, panel.width - 60f, 64f), "游戏暂停", titleStyle);
        if (GUI.Button(new Rect(panel.x + 95f, panel.y + 126f, panel.width - 190f, 58f), "继续游戏", buttonStyle))
        {
            TogglePause();
        }
        if (GUI.Button(new Rect(panel.x + 95f, panel.y + 210f, panel.width - 190f, 58f), "返回主页面", buttonStyle))
        {
            GameModeSession.ReturnToMainMenu();
        }
    }

    private void DrawResults()
    {
        DrawScreenDim();
        Rect panel = CenteredPanel(650f, 570f);
        GUI.Box(panel, GUIContent.none);
        string modeName = GameModeSession.IsEndlessSea ? "海上逃生" : "陆地追逐";
        string killName = GameModeSession.IsEndlessSea ? "击沉船只" : "击毁车辆";
        GUI.Label(new Rect(panel.x + 30f, panel.y + 28f, panel.width - 60f, 64f), $"{modeName} · 本局结束", titleStyle);
        GUI.Label(new Rect(panel.x + 65f, panel.y + 112f, panel.width - 130f, 46f),
            $"生存时间：{FormatResultTime(resultTimeMilliseconds)}{(newTimeRecord ? "  新纪录！" : string.Empty)}", headingStyle);
        GUI.Label(new Rect(panel.x + 65f, panel.y + 166f, panel.width - 130f, 46f),
            $"{killName}：{resultKills}{(newKillRecord ? "  新纪录！" : string.Empty)}", headingStyle);
        GUI.Label(new Rect(panel.x + 65f, panel.y + 245f, panel.width - 130f, 38f),
            $"历史最长：{FormatResultTime(bestTimeMilliseconds)}", bodyStyle);
        GUI.Label(new Rect(panel.x + 65f, panel.y + 287f, panel.width - 130f, 38f),
            $"历史最高击杀：{bestKills}", bodyStyle);
        if (GUI.Button(new Rect(panel.x + 90f, panel.y + 380f, 210f, 62f), "重新挑战", buttonStyle))
        {
            GameModeSession.RetryCurrentMode();
        }
        if (GUI.Button(new Rect(panel.x + panel.width - 300f, panel.y + 380f, 210f, 62f), "返回模式选择", buttonStyle))
        {
            GameModeSession.ReturnToEndlessSelection();
        }
    }

    private static void DrawScreenDim()
    {
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static Rect CenteredPanel(float width, float height)
    {
        width = Mathf.Min(width, Screen.width - 40f);
        height = Mathf.Min(height, Screen.height - 40f);
        return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        titleStyle.normal.textColor = new Color(1f, 0.3f, 0.12f);
        headingStyle = new GUIStyle(titleStyle) { fontSize = 25 };
        headingStyle.normal.textColor = Color.white;
        bodyStyle = new GUIStyle(titleStyle) { fontSize = 20 };
        bodyStyle.normal.textColor = new Color(0.88f, 0.93f, 1f);
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            AudioListener.pause = false;
        }
    }
}
