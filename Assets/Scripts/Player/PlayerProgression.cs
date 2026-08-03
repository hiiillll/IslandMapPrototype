using UnityEngine;

public sealed class PlayerProgression : MonoBehaviour
{
    private const int FirstExperienceRequirement = 80;
    private const int ExperienceIncreasePerLevel = 20;

    [SerializeField] private int startingLevel = 1;
    [SerializeField] private int baseExperienceToLevel = FirstExperienceRequirement;
    [SerializeField] private int experienceIncreasePerLevel = ExperienceIncreasePerLevel;

    private int level;
    private int currentExperience;
    private int experienceToNextLevel;
    private int destroyedEnemies;
    private float pickupPulse;
    private float healthPackDropChanceBonus;
    private int maxHealthBonus;

    public static PlayerProgression Instance { get; private set; }

    public int Level => level;
    public int CurrentExperience => currentExperience;
    public int ExperienceToNextLevel => experienceToNextLevel;
    public int DestroyedEnemies => destroyedEnemies;
    public float HealthPackDropChanceBonus => healthPackDropChanceBonus;
    public int MaxHealthBonus => maxHealthBonus;
    public float ExperienceProgress => experienceToNextLevel > 0
        ? Mathf.Clamp01((float)currentExperience / experienceToNextLevel)
        : 0f;
    public float PickupPulse => pickupPulse;

    private void Awake()
    {
        baseExperienceToLevel = FirstExperienceRequirement;
        experienceIncreasePerLevel = ExperienceIncreasePerLevel;
        Instance = this;
        ResetForNewLevel();
        GameModeSession.ApplyStoryProgress(this);
    }

    public void ResetForNewLevel()
    {
        level = Mathf.Max(1, startingLevel);
        currentExperience = 0;
        experienceToNextLevel = CalculateExperienceRequirement(level);
        destroyedEnemies = 0;
        pickupPulse = 0f;
        healthPackDropChanceBonus = 0f;
        maxHealthBonus = 0;
    }

    public void RestoreState(
        int restoredLevel,
        int restoredExperience,
        int restoredDestroyedEnemies = 0,
        float restoredHealthPackDropChanceBonus = 0f,
        int restoredMaxHealthBonus = 0)
    {
        level = Mathf.Max(1, restoredLevel);
        currentExperience = Mathf.Max(0, restoredExperience);
        experienceToNextLevel = CalculateExperienceRequirement(level);
        while (currentExperience >= experienceToNextLevel)
        {
            currentExperience -= experienceToNextLevel;
            level++;
            experienceToNextLevel = CalculateExperienceRequirement(level);
        }

        destroyedEnemies = Mathf.Max(0, restoredDestroyedEnemies);
        pickupPulse = 0f;
        healthPackDropChanceBonus = Mathf.Clamp01(restoredHealthPackDropChanceBonus);
        maxHealthBonus = Mathf.Max(0, restoredMaxHealthBonus);
    }

    private void Update()
    {
        pickupPulse = Mathf.MoveTowards(pickupPulse, 0f, Time.unscaledDeltaTime * 2.8f);
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentExperience += amount;
        pickupPulse = 1f;
        while (currentExperience >= experienceToNextLevel)
        {
            currentExperience -= experienceToNextLevel;
            level++;
            experienceToNextLevel = CalculateExperienceRequirement(level);
        }
    }

    public void RegisterEnemyDestroyed()
    {
        destroyedEnemies++;
    }

    public void IncreaseHealthPackDropChance(float amount)
    {
        healthPackDropChanceBonus = Mathf.Clamp01(
            healthPackDropChanceBonus + Mathf.Max(0f, amount));
    }

    public void IncreaseMaxHealthBonus(int amount)
    {
        maxHealthBonus += Mathf.Max(0, amount);
    }

    private int CalculateExperienceRequirement(int targetLevel)
    {
        int levelOffset = Mathf.Max(0, targetLevel - 1);
        return Mathf.Max(1, baseExperienceToLevel + experienceIncreasePerLevel * levelOffset);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
