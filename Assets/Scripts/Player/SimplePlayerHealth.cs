using UnityEngine;

public class SimplePlayerHealth : MonoBehaviour
{
    private const float DamageInvincibilityDuration = 1f;

    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float damageCooldown = DamageInvincibilityDuration;
    [SerializeField] private bool defeatBelowWorldHeight;
    [SerializeField] private float defeatHeight = -10f;

    private int currentHealth;
    private int baseMaxHealth;
    private float nextDamageTime;
    private bool isRestarting;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        damageCooldown = DamageInvincibilityDuration;
        baseMaxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
    }

    private void Start()
    {
        PlayerProgression progression = GetComponent<PlayerProgression>();
        if (progression == null || progression.MaxHealthBonus <= 0)
        {
            return;
        }

        maxHealth = baseMaxHealth + progression.MaxHealthBonus;
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (defeatBelowWorldHeight && currentHealth > 0 && transform.position.y < defeatHeight)
        {
            DefeatImmediately();
        }
    }

    public void ConfigureFallDefeat(bool enabled, float worldHeight = -10f)
    {
        defeatBelowWorldHeight = enabled;
        defeatHeight = worldHeight;
    }

    public void ResetToFullHealth(int healthPoints = 3)
    {
        maxHealth = Mathf.Max(1, healthPoints);
        currentHealth = maxHealth;
        nextDamageTime = 0f;
        isRestarting = false;
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || currentHealth <= 0 || Time.time < nextDamageTime)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        nextDamageTime = Time.time + damageCooldown;
        TriggerDamageShake();
        if (currentHealth == 0)
        {
            StopPlayer();
        }
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || currentHealth <= 0)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    public void IncreaseMaxHealth(int amount, int healAmount = 1)
    {
        if (amount <= 0 || currentHealth <= 0)
        {
            return;
        }

        maxHealth += amount;
        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Max(0, healAmount));
    }

    private void OnCollisionEnter(Collision collision)
    {
        NavMeshEnemyCarChaser enemy = collision.collider.GetComponentInParent<NavMeshEnemyCarChaser>();
        if (enemy == null)
        {
            return;
        }

        TakeDamage(1);
        enemy.Explode(true, true);
    }

    private void TriggerDamageShake()
    {
        SimpleSpeedCameraFollow cameraFollow = Camera.main != null
            ? Camera.main.GetComponent<SimpleSpeedCameraFollow>()
            : null;
        if (cameraFollow == null)
        {
            cameraFollow = FindObjectOfType<SimpleSpeedCameraFollow>();
        }
        if (cameraFollow != null)
        {
            cameraFollow.Shake(0.18f, 0.3f);
            return;
        }

        BoatChaseTopDownCamera boatCamera = Camera.main != null
            ? Camera.main.GetComponent<BoatChaseTopDownCamera>()
            : null;
        if (boatCamera == null)
        {
            boatCamera = FindObjectOfType<BoatChaseTopDownCamera>();
        }
        if (boatCamera != null)
        {
            boatCamera.Shake(0.22f, 0.55f);
            return;
        }

        PlaneChaseTopDownCamera planeCamera = Camera.main != null
            ? Camera.main.GetComponent<PlaneChaseTopDownCamera>()
            : null;
        if (planeCamera == null)
        {
            planeCamera = FindObjectOfType<PlaneChaseTopDownCamera>();
        }
        if (planeCamera != null)
        {
            planeCamera.Shake(0.22f, 0.55f);
        }
    }

    private void DefeatImmediately()
    {
        currentHealth = 0;
        StopPlayer();
    }

    private void StopPlayer()
    {
        SimpleAutoDriveController controller = GetComponent<SimpleAutoDriveController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    private void OnGUI()
    {
        if (currentHealth > 0 || GameModeSession.IsEndless)
        {
            return;
        }

        const float buttonWidth = 300f;
        const float buttonHeight = 56f;
        const float spacing = 16f;
        float totalHeight = GameModeSession.IsStoryRunActive
            ? buttonHeight * 2f + spacing
            : buttonHeight;
        Rect retryButton = new Rect(
            (Screen.width - buttonWidth) * 0.5f,
            (Screen.height - totalHeight) * 0.5f,
            buttonWidth,
            buttonHeight);

        if (!isRestarting && GUI.Button(retryButton, "\u91cd\u8bd5\u672c\u5173\uff08\u4fdd\u7559\u6280\u80fd\uff09"))
        {
            RetryLevel();
        }

        if (!GameModeSession.IsStoryRunActive)
        {
            return;
        }

        Rect restartRunButton = new Rect(
            retryButton.x,
            retryButton.yMax + spacing,
            buttonWidth,
            buttonHeight);
        if (!isRestarting && GUI.Button(
            restartRunButton,
            "\u91cd\u5f00\u526f\u672c\uff08\u91cd\u9009\u6280\u80fd\uff09"))
        {
            RestartRun();
        }
    }

    private void RetryLevel()
    {
        isRestarting = true;
        GameModeSession.RetryCurrentStoryLevel();
    }

    private void RestartRun()
    {
        isRestarting = true;
        GameModeSession.RestartStoryRunWithNewSkills();
    }
}
