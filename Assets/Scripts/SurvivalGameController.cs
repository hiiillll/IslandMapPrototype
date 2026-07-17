using UnityEngine;
using UnityEngine.SceneManagement;

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

        SimpleAutoDriveController playerController = FindObjectOfType<SimpleAutoDriveController>();
        if (playerController != null)
        {
            playerController.enabled = false;
            Rigidbody playerBody = playerController.GetComponent<Rigidbody>();
            if (playerBody != null)
            {
                playerBody.velocity = Vector3.zero;
            }
        }

        Time.timeScale = 0f;
    }

    private void OnGUI()
    {
        if (!hasWon)
        {
            return;
        }

        const float panelWidth = 300f;
        const float panelHeight = 142f;
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);
        GUI.Box(panel, "通关！");
        GUI.Label(new Rect(panel.x + 52f, panel.y + 44f, 210f, 28f), "成功存活 3 分钟");
        if (GUI.Button(new Rect(panel.x + 52f, panel.y + 84f, 196f, 36f), "再玩一次"))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
