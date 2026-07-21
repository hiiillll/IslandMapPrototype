using UnityEngine;
using UnityEngine.SceneManagement;

public class SimplePlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float damageCooldown = 0.25f;
    [SerializeField] private bool defeatBelowWorldHeight;
    [SerializeField] private float defeatHeight = -10f;

    private int currentHealth;
    private float nextDamageTime;
    private bool isRestarting;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
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

        const float buttonWidth = 220f;
        const float buttonHeight = 56f;
        Rect restartButton = new Rect(
            (Screen.width - buttonWidth) * 0.5f,
            (Screen.height - buttonHeight) * 0.5f,
            buttonWidth,
            buttonHeight);

        if (!isRestarting && GUI.Button(restartButton, "重新开始"))
        {
            RestartLevel();
        }
    }

    private void RestartLevel()
    {
        isRestarting = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
