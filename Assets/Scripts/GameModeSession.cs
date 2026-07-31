using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameModeKind
{
    Story,
    EndlessLand,
    EndlessSea
}

public enum StoryChapter
{
    None,
    ChapterOne,
    ChapterTwo
}

public static class GameModeSession
{
    public const string IslandSceneName = "IslandMap";
    public const string SeaSceneName = "Level02";
    public const string ChapterTwoSceneName = "Level03";

    private const string ChapterTwoUnlockedKey = "Story.ChapterTwoUnlocked";
    private const string FirstLevelKeyPrefix = "Story.FirstLevel.";

    private static bool openEndlessSelection;
    private static bool openStorySelection;
    private static bool storyRunActive;
    private static StoryChapter activeChapter;
    private static string activeStoryScene = IslandSceneName;

    private static bool hasStorySkills;
    private static int qSkill;
    private static int eSkill;
    private static bool qSkillUpgraded;
    private static bool eSkillUpgraded;

    private static PlayerProgressState currentProgress = PlayerProgressState.LevelOne;
    private static PlayerProgressState chapterStartProgress = PlayerProgressState.LevelOne;
    private static PlayerProgressState firstLevelCompletionProgress = PlayerProgressState.LevelOne;
    private static bool hasFirstLevelCompletionProgress;
    private static bool chapterTwoUnlocked;

    public static GameModeKind CurrentMode { get; private set; } = GameModeKind.Story;
    public static bool IsEndless => CurrentMode != GameModeKind.Story;
    public static bool IsEndlessLand => CurrentMode == GameModeKind.EndlessLand;
    public static bool IsEndlessSea => CurrentMode == GameModeKind.EndlessSea;
    public static bool IsStoryRunActive => CurrentMode == GameModeKind.Story && storyRunActive;
    public static StoryChapter ActiveChapter => activeChapter;
    public static bool IsChapterTwoUnlocked => chapterTwoUnlocked;
    public static bool HasSelectedStorySkills => IsStoryRunActive && hasStorySkills;

    public static bool ShouldShowStartMenu
    {
        get
        {
            return SceneManager.GetActiveScene().name == IslandSceneName
                && CurrentMode == GameModeKind.Story
                && (!storyRunActive || openStorySelection);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        CurrentMode = GameModeKind.Story;
        openEndlessSelection = false;
        openStorySelection = false;
        storyRunActive = false;
        activeChapter = StoryChapter.None;
        activeStoryScene = IslandSceneName;
        ClearStorySkills();
        currentProgress = PlayerProgressState.LevelOne;
        chapterStartProgress = PlayerProgressState.LevelOne;
        LoadPersistentStoryProgress();
        ResumeTime();
    }

    public static void SelectStory()
    {
        CurrentMode = GameModeKind.Story;
        openEndlessSelection = false;
    }

    public static void StartStoryChapter(StoryChapter chapter)
    {
        if (chapter == StoryChapter.ChapterTwo && !chapterTwoUnlocked)
        {
            return;
        }

        CurrentMode = GameModeKind.Story;
        storyRunActive = true;
        activeChapter = chapter;
        activeStoryScene = GetChapterStartScene(chapter);
        chapterStartProgress = chapter == StoryChapter.ChapterTwo
            ? GetFirstLevelCompletionProgress()
            : PlayerProgressState.LevelOne;
        currentProgress = chapterStartProgress;
        openStorySelection = false;
        openEndlessSelection = false;
        ClearStorySkills();
        LoadScene(activeStoryScene);
    }

    public static void ContinueStoryRun()
    {
        if (!IsStoryRunActive)
        {
            return;
        }

        openStorySelection = false;
        LoadScene(activeStoryScene);
    }

    public static void RestartStoryRunWithNewSkills()
    {
        if (!IsStoryRunActive)
        {
            return;
        }

        currentProgress = chapterStartProgress;
        activeStoryScene = GetChapterStartScene(activeChapter);
        openStorySelection = false;
        ClearStorySkills();
        LoadScene(activeStoryScene);
    }

    public static void RetryCurrentStoryLevel()
    {
        if (!IsStoryRunActive)
        {
            LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        CaptureCurrentPlayerProgress();
        activeStoryScene = SceneManager.GetActiveScene().name;
        LoadScene(activeStoryScene);
    }

    public static void RecordStorySkills(
        int selectedQSkill,
        int selectedESkill,
        bool isQSkillUpgraded,
        bool isESkillUpgraded)
    {
        if (!IsStoryRunActive)
        {
            return;
        }

        qSkill = selectedQSkill;
        eSkill = selectedESkill;
        qSkillUpgraded = isQSkillUpgraded;
        eSkillUpgraded = isESkillUpgraded;
        hasStorySkills = qSkill > 0 && eSkill > 0;
    }

    public static bool TryGetStorySkills(
        out int selectedQSkill,
        out int selectedESkill,
        out bool isQSkillUpgraded,
        out bool isESkillUpgraded)
    {
        selectedQSkill = qSkill;
        selectedESkill = eSkill;
        isQSkillUpgraded = qSkillUpgraded;
        isESkillUpgraded = eSkillUpgraded;
        return IsStoryRunActive && hasStorySkills;
    }

    public static void ApplyStoryProgress(PlayerProgression progression)
    {
        if (progression != null && IsStoryRunActive)
        {
            progression.RestoreState(
                currentProgress.level,
                currentProgress.experience,
                currentProgress.destroyedEnemies,
                currentProgress.healthPackDropChanceBonus);
        }
    }

    public static void CompleteFirstLevelAndLoadSecondLevel(string fallbackSceneName = SeaSceneName)
    {
        if (!IsStoryRunActive)
        {
            activeChapter = StoryChapter.ChapterOne;
            storyRunActive = true;
            chapterStartProgress = PlayerProgressState.LevelOne;
        }

        CaptureCurrentPlayerProgress();
        firstLevelCompletionProgress = currentProgress.ForNewLevel();
        currentProgress = firstLevelCompletionProgress;
        hasFirstLevelCompletionProgress = true;
        SaveFirstLevelCompletionProgress();
        activeStoryScene = string.IsNullOrWhiteSpace(fallbackSceneName)
            ? SeaSceneName
            : fallbackSceneName;
        LoadScene(activeStoryScene);
    }

    public static void CompleteChapterOneAndStartChapterTwo()
    {
        CaptureCurrentPlayerProgress();
        chapterTwoUnlocked = true;
        PlayerPrefs.SetInt(ChapterTwoUnlockedKey, 1);
        PlayerPrefs.Save();

        CurrentMode = GameModeKind.Story;
        storyRunActive = true;
        activeChapter = StoryChapter.ChapterTwo;
        activeStoryScene = ChapterTwoSceneName;
        chapterStartProgress = GetFirstLevelCompletionProgress();
        currentProgress = chapterStartProgress;
        openStorySelection = false;
        ClearStorySkills();
        LoadScene(activeStoryScene);
    }

    public static void CompleteChapterTwoAndReturnToMenu()
    {
        CaptureCurrentPlayerProgress();
        storyRunActive = false;
        activeChapter = StoryChapter.None;
        activeStoryScene = IslandSceneName;
        ClearStorySkills();
        ReturnToStorySelection();
    }

    public static void StartEndlessLand()
    {
        CurrentMode = GameModeKind.EndlessLand;
        openEndlessSelection = false;
        openStorySelection = false;
        EndlessModeController.EnsureForActiveScene();
    }

    public static void StartEndlessSea()
    {
        CurrentMode = GameModeKind.EndlessSea;
        openEndlessSelection = false;
        openStorySelection = false;
        LoadScene(SeaSceneName);
    }

    public static void RetryCurrentMode()
    {
        string sceneName = IsEndlessSea ? SeaSceneName : IslandSceneName;
        LoadScene(sceneName);
    }

    public static void ReturnToEndlessSelection()
    {
        CurrentMode = GameModeKind.Story;
        openEndlessSelection = true;
        openStorySelection = false;
        LoadScene(IslandSceneName);
    }

    public static void ReturnToStorySelection()
    {
        if (IsStoryRunActive)
        {
            CaptureCurrentPlayerProgress();
            activeStoryScene = SceneManager.GetActiveScene().name;
        }

        CurrentMode = GameModeKind.Story;
        openStorySelection = true;
        openEndlessSelection = false;
        LoadScene(IslandSceneName);
    }

    public static void ReturnToMainMenu()
    {
        if (IsStoryRunActive)
        {
            CaptureCurrentPlayerProgress();
            activeStoryScene = SceneManager.GetActiveScene().name;
        }

        CurrentMode = GameModeKind.Story;
        openEndlessSelection = false;
        openStorySelection = storyRunActive;
        LoadScene(IslandSceneName);
    }

    public static bool ConsumeOpenEndlessSelection()
    {
        bool shouldOpen = openEndlessSelection;
        openEndlessSelection = false;
        return shouldOpen;
    }

    public static bool ConsumeOpenStorySelection()
    {
        bool shouldOpen = openStorySelection;
        openStorySelection = false;
        return shouldOpen;
    }

    private static void CaptureCurrentPlayerProgress()
    {
        PlayerProgression progression = PlayerProgression.Instance;
        if (progression == null)
        {
            return;
        }

        currentProgress = new PlayerProgressState(
            progression.Level,
            progression.CurrentExperience,
            progression.DestroyedEnemies,
            progression.HealthPackDropChanceBonus);
    }

    private static string GetChapterStartScene(StoryChapter chapter)
    {
        return chapter == StoryChapter.ChapterTwo ? ChapterTwoSceneName : IslandSceneName;
    }

    private static PlayerProgressState GetFirstLevelCompletionProgress()
    {
        return hasFirstLevelCompletionProgress
            ? firstLevelCompletionProgress
            : PlayerProgressState.LevelOne;
    }

    private static void ClearStorySkills()
    {
        hasStorySkills = false;
        qSkill = 0;
        eSkill = 0;
        qSkillUpgraded = false;
        eSkillUpgraded = false;
    }

    private static void LoadPersistentStoryProgress()
    {
        chapterTwoUnlocked = PlayerPrefs.GetInt(ChapterTwoUnlockedKey, 0) == 1;
        hasFirstLevelCompletionProgress = PlayerPrefs.HasKey(FirstLevelKeyPrefix + "Level");
        if (!hasFirstLevelCompletionProgress)
        {
            firstLevelCompletionProgress = PlayerProgressState.LevelOne;
            return;
        }

        firstLevelCompletionProgress = new PlayerProgressState(
            PlayerPrefs.GetInt(FirstLevelKeyPrefix + "Level", 1),
            PlayerPrefs.GetInt(FirstLevelKeyPrefix + "Experience", 0),
            0,
            0f);
    }

    private static void SaveFirstLevelCompletionProgress()
    {
        PlayerPrefs.SetInt(FirstLevelKeyPrefix + "Level", firstLevelCompletionProgress.level);
        PlayerPrefs.SetInt(FirstLevelKeyPrefix + "Experience", firstLevelCompletionProgress.experience);
        PlayerPrefs.Save();
    }

    private static void LoadScene(string sceneName)
    {
        ResumeTime();
        SceneManager.LoadScene(sceneName);
    }

    private static void ResumeTime()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private readonly struct PlayerProgressState
    {
        public static readonly PlayerProgressState LevelOne = new PlayerProgressState(1, 0, 0, 0f);

        public readonly int level;
        public readonly int experience;
        public readonly int destroyedEnemies;
        public readonly float healthPackDropChanceBonus;

        public PlayerProgressState(
            int playerLevel,
            int playerExperience,
            int enemiesDestroyed,
            float healthPackBonus)
        {
            level = Mathf.Max(1, playerLevel);
            experience = Mathf.Max(0, playerExperience);
            destroyedEnemies = Mathf.Max(0, enemiesDestroyed);
            healthPackDropChanceBonus = Mathf.Clamp01(healthPackBonus);
        }

        public PlayerProgressState ForNewLevel()
        {
            return new PlayerProgressState(level, experience, 0, healthPackDropChanceBonus);
        }
    }
}
