using UnityEngine;

public sealed class SurvivalGameController : MonoBehaviour
{
    [SerializeField] private float survivalDuration = 180f;
    [SerializeField] private bool completeWithDockObjective = true;
    [SerializeField] private bool completeWithPlaneObjective;

    private float elapsedTime;
    private bool hasWon;

    public float DifficultyProgress => Mathf.Clamp01(elapsedTime / survivalDuration);
    public bool IsFinished => hasWon;
    public float RemainingTime => Mathf.Max(0f, survivalDuration - elapsedTime);

    public void Configure(
        float duration,
        bool useDockObjective,
        bool usePlaneObjective = false)
    {
        survivalDuration = Mathf.Max(1f, duration);
        completeWithDockObjective = useDockObjective;
        completeWithPlaneObjective = usePlaneObjective;
        elapsedTime = 0f;
        hasWon = false;
        Time.timeScale = 1f;
    }

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

        BoatEnemySpawner boatSpawner = FindObjectOfType<BoatEnemySpawner>();
        if (boatSpawner != null)
        {
            boatSpawner.StopSpawningAndClearEnemies();
        }

        if (completeWithPlaneObjective)
        {
            PlaneExtractionObjective planeObjective = FindObjectOfType<PlaneExtractionObjective>();
            if (planeObjective == null)
            {
                GameObject objectiveObject = new GameObject("SYS_PlaneExtractionObjective");
                planeObjective = objectiveObject.AddComponent<PlaneExtractionObjective>();
            }

            planeObjective.BeginObjective();
            return;
        }

        if (!completeWithDockObjective)
        {
            return;
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
