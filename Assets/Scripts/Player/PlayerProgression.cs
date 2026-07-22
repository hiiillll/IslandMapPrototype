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

    public static PlayerProgression Instance { get; private set; }

    public int Level => level;
    public int CurrentExperience => currentExperience;
    public int ExperienceToNextLevel => experienceToNextLevel;
    public int DestroyedEnemies => destroyedEnemies;
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
    }

    public void ResetForNewLevel()
    {
        level = Mathf.Max(1, startingLevel);
        currentExperience = 0;
        experienceToNextLevel = CalculateExperienceRequirement(level);
        destroyedEnemies = 0;
        pickupPulse = 0f;
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
