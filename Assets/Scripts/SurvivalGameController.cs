using UnityEngine;

public sealed class SurvivalGameController : MonoBehaviour
{
    [SerializeField] private float survivalDuration = 180f;

    private float elapsedTime;
    private bool hasWon;

    public float DifficultyProgress => Mathf.Clamp01(elapsedTime / survivalDuration);
    public bool IsFinished => hasWon;
    public float RemainingTime => Mathf.Max(0f, survivalDuration - elapsedTime);

    private void Awake()
    {
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (hasWon)
        {
            return;
        }

        elapsedTime = Mathf.Min(elapsedTime + Time.deltaTime, survivalDuration);
        if (elapsedTime >= survivalDuration)
        {
            CompleteLevel();
        }
    }

    private void CompleteLevel()
    {
        hasWon = true;

        WarshipEnemySpawner spawner = GetComponent<WarshipEnemySpawner>();
        if (spawner == null)
        {
            spawner = FindObjectOfType<WarshipEnemySpawner>();
        }
        if (spawner != null)
        {
            spawner.StopSpawningAndClearEnemies();
        }

        DockLevelTransition dockTransition = FindObjectOfType<DockLevelTransition>();
        if (dockTransition == null)
        {
            GameObject transitionObject = new GameObject("SYS_DockLevelTransition");
            dockTransition = transitionObject.AddComponent<DockLevelTransition>();
        }

        dockTransition.BeginDockObjective();
    }
}
