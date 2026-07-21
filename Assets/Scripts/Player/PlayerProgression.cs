using UnityEngine;

public sealed class PlayerProgression : MonoBehaviour
{
    [SerializeField] private int startingLevel = 1;
    [SerializeField] private int baseExperienceToLevel = 100;
    [SerializeField] private float experienceGrowth = 1.25f;

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
        float scaledRequirement = baseExperienceToLevel
            * Mathf.Pow(Mathf.Max(1f, experienceGrowth), Mathf.Max(0, targetLevel - 1));
        int requirement = GameModeSession.IsEndless
            ? Mathf.CeilToInt(scaledRequirement)
            : Mathf.RoundToInt(scaledRequirement);
        return Mathf.Max(1, requirement);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
