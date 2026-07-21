using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameModeKind
{
    Story,
    EndlessLand,
    EndlessSea
}

public static class GameModeSession
{
    private const string IslandSceneName = "IslandMap";
    private const string SeaSceneName = "Level02";

    private static bool openEndlessSelection;

    public static GameModeKind CurrentMode { get; private set; } = GameModeKind.Story;
    public static bool IsEndless => CurrentMode != GameModeKind.Story;
    public static bool IsEndlessLand => CurrentMode == GameModeKind.EndlessLand;
    public static bool IsEndlessSea => CurrentMode == GameModeKind.EndlessSea;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        CurrentMode = GameModeKind.Story;
        openEndlessSelection = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    public static void SelectStory()
    {
        CurrentMode = GameModeKind.Story;
        openEndlessSelection = false;
    }

    public static void StartEndlessLand()
    {
        CurrentMode = GameModeKind.EndlessLand;
        openEndlessSelection = false;
        EndlessModeController.EnsureForActiveScene();
    }

    public static void StartEndlessSea()
    {
        CurrentMode = GameModeKind.EndlessSea;
        openEndlessSelection = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SeaSceneName);
    }

    public static void RetryCurrentMode()
    {
        string sceneName = IsEndlessSea ? SeaSceneName : IslandSceneName;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(sceneName);
    }

    public static void ReturnToEndlessSelection()
    {
        CurrentMode = GameModeKind.Story;
        openEndlessSelection = true;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(IslandSceneName);
    }

    public static bool ConsumeOpenEndlessSelection()
    {
        bool shouldOpen = openEndlessSelection;
        openEndlessSelection = false;
        return shouldOpen;
    }
}
