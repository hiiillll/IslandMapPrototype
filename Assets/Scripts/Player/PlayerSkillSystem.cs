using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerSkillSystem : MonoBehaviour
{
    private enum StartMenuPage
    {
        Main,
        StoryChapters,
        EndlessModes
    }

    private enum SkillId
    {
        None,
        HornBlast,
        GravityTrap,
        Blink,
        FlameTrail,
        TankShells
    }

    private const float HornBlastCooldown = 3f;
    private const float GravityTrapCooldown = 8f;
    private const float BlinkCooldown = 5f;
    private const float FlameTrailCooldown = 9f;
    private const float TankShellCooldown = 7f;
    private const float UnupgradedHornBlastCooldown = 4.5f;
    private const float UnupgradedGravityTrapCooldown = 11f;
    private const float UnupgradedBlinkCooldown = 7f;
    private const float UnupgradedFlameTrailCooldown = 12f;
    private const float UnupgradedTankShellCooldown = 10f;
    private const float HealthPackDropChanceUpgrade = 0.1f;
    private const string StartBrandXKey = "Menu.Layout.BrandX";
    private const string StartBrandYKey = "Menu.Layout.BrandY";
    private const string StartBrandScaleKey = "Menu.Layout.BrandScale";
    private const string StartButtonsXKey = "Menu.Layout.ButtonsX";
    private const string StartButtonsYKey = "Menu.Layout.ButtonsY";
    private const float DefaultStartBrandX = 0.14f;
    private const float DefaultStartBrandY = 0.05f;
    private const float DefaultStartBrandScale = 1f;
    private const float DefaultStartButtonsX = 0.13f;
    private const float DefaultStartButtonsY = 0.72f;

    [Header("Start Menu Layout")]
    [InspectorName("标题位置 X")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float startBrandX = DefaultStartBrandX;
    [InspectorName("标题位置 Y")]
    [Range(0.02f, 0.5f)]
    [SerializeField] private float startBrandY = DefaultStartBrandY;
    [InspectorName("标题大小")]
    [Range(0.7f, 1.8f)]
    [SerializeField] private float startBrandScale = DefaultStartBrandScale;
    [InspectorName("菜单位置 X")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float startButtonsX = DefaultStartButtonsX;
    [InspectorName("菜单位置 Y")]
    [Range(0.2f, 0.8f)]
    [SerializeField] private float startButtonsY = DefaultStartButtonsY;

    private SkillId qSkill;
    private SkillId eSkill;
    private float qReadyTime;
    private float eReadyTime;
    private bool isShowingStartScreen = true;
    private bool isChoosingSkills = true;
    private bool qSkillUpgraded;
    private bool eSkillUpgraded;
    private bool isChoosingUpgrade;
    private int pendingUpgradeChoices;
    private bool isChoosingLevelFourReward;
    private int pendingLevelFourRewardChoices;
    private int lastObservedLevel;
    private Rigidbody body;
    private SimplePlayerHealth health;
    private PlayerProgression progression;
    private Texture2D coverTexture;
    private Texture2D logoTexture;
    private Texture2D startButtonNormalTexture;
    private Texture2D startButtonHoverTexture;
    private Texture2D startButtonPrimaryTexture;
    private Texture2D startButtonActiveTexture;
    private Texture2D startAccentCyanTexture;
    private Texture2D startAccentOrangeTexture;
    private Texture2D startDividerTexture;
    private Texture2D startPanelTexture;
    private GUIStyle startButtonStyle;
    private GUIStyle startMenuTextButtonStyle;
    private GUIStyle startPrimaryButtonStyle;
    private GUIStyle startCompactButtonStyle;
    private GUIStyle startCompactPrimaryButtonStyle;
    private GUIStyle startTitleStyle;
    private GUIStyle startKickerStyle;
    private GUIStyle startMetaStyle;
    private GUIStyle startBackButtonStyle;
    private GUIStyle startBrandTitleStyle;
    private GUIStyle startBrandSubtitleStyle;
    private GUIStyle startLevelNumberStyle;
    private GUIStyle startLevelTitleStyle;
    private GUIStyle startLevelMetaStyle;
    private GUIStyle startLevelButtonStyle;
    private Texture2D hornBlastCard;
    private Texture2D gravityTrapCard;
    private Texture2D blinkCard;
    private Texture2D flameTrailCard;
    private Texture2D tankShellsCard;
    private Texture2D startMenuTextTexture;
    private Texture2D qHornBlastIcon;
    private Texture2D qGravityTrapIcon;
    private Texture2D qBlinkIcon;
    private Texture2D qFlameTrailIcon;
    private Texture2D qTankShellsIcon;
    private Texture2D eHornBlastIcon;
    private Texture2D eGravityTrapIcon;
    private Texture2D eBlinkIcon;
    private Texture2D eFlameTrailIcon;
    private Texture2D eTankShellsIcon;
    private AudioSource skillAudioSource;
    private AudioClip hornBlastAudio;
    private AudioClip gravityTrapAudio;
    private AudioClip blinkAudio;
    private AudioClip flameTrailAudio;
    private AudioClip tankShellsAudio;
    private StartMenuPage startMenuPage;
    private bool showStartLayoutEditor;
    private string startLayoutStatus;
    private float startLayoutStatusUntil;

    public bool IsGameplayActive => !isShowingStartScreen
        && !isChoosingSkills
        && !isChoosingUpgrade
        && !isChoosingLevelFourReward;
    public string QSkillName => GetSkillName(qSkill);
    public string ESkillName => GetSkillName(eSkill);
    public Texture2D QSkillTexture => GetSkillIconTexture(qSkill, true);
    public Texture2D ESkillTexture => GetSkillIconTexture(eSkill, false);
    public float QCooldownRemaining => Mathf.Max(0f, qReadyTime - Time.time);
    public float ECooldownRemaining => Mathf.Max(0f, eReadyTime - Time.time);
    public float QCooldownDuration => GetCooldown(qSkill);
    public float ECooldownDuration => GetCooldown(eSkill);

    private void Awake()
    {
        LoadStartMenuLayout();
        body = GetComponent<Rigidbody>();
        health = GetComponent<SimplePlayerHealth>();
        progression = GetComponent<PlayerProgression>();
        coverTexture = Resources.Load<Texture2D>("UI/SpeedEscape_MenuCover_V1");
        if (coverTexture == null)
        {
            coverTexture = Resources.Load<Texture2D>("UI/GameCover_MenuV2");
        }
        if (coverTexture == null)
        {
            coverTexture = Resources.Load<Texture2D>("UI/GameCover_FloridaDay");
        }
        logoTexture = Resources.Load<Texture2D>("UI/SpeedEscape_Logo");
        hornBlastCard = Resources.Load<Texture2D>("SkillCards/CARD-00_HornBlast");
        gravityTrapCard = Resources.Load<Texture2D>("SkillCards/CARD-07_GravityTrap");
        blinkCard = Resources.Load<Texture2D>("SkillCards/CARD-08_Blink");
        flameTrailCard = Resources.Load<Texture2D>("SkillCards/CARD-09_FlameTrail");
        tankShellsCard = Resources.Load<Texture2D>("SkillCards/CARD-10_TankShells");
        qHornBlastIcon = Resources.Load<Texture2D>("UI/SkillIconsLine/Q/SkillIcon_HornBlast_Q");
        qGravityTrapIcon = Resources.Load<Texture2D>("UI/SkillIconsLine/Q/SkillIcon_GravityTrap_Q");
        qBlinkIcon = Resources.Load<Texture2D>("UI/SkillIconsLine/Q/SkillIcon_Blink_Q");
        qFlameTrailIcon = Resources.Load<Texture2D>("UI/SkillIconsLine/Q/SkillIcon_FlameTrail_Q");
        qTankShellsIcon = Resources.Load<Texture2D>("UI/SkillIconsLine/Q/SkillIcon_TankShell_Q");
        eHornBlastIcon = Resources.Load<Texture2D>("UI/SkillIconsLine/E/SkillIcon_HornBlast_E");
        eGravityTrapIcon = Resources.Load<Texture2D>("UI/SkillIconsLine/E/SkillIcon_GravityTrap_E");
        eBlinkIcon = Resources.Load<Texture2D>("UI/SkillIconsLine/E/SkillIcon_Blink_E");
        eFlameTrailIcon = Resources.Load<Texture2D>("UI/SkillIconsLine/E/SkillIcon_FlameTrail_E");
        eTankShellsIcon = Resources.Load<Texture2D>("UI/SkillIconsLine/E/SkillIcon_TankShell_E");
        InitializeSkillAudio();
        bool openEndlessSelection = GameModeSession.ConsumeOpenEndlessSelection();
        bool openStorySelection = GameModeSession.ConsumeOpenStorySelection();
        startMenuPage = openEndlessSelection
            ? StartMenuPage.EndlessModes
            : openStorySelection
                ? StartMenuPage.StoryChapters
                : StartMenuPage.Main;
        isShowingStartScreen = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            == GameModeSession.IslandSceneName
            && (!GameModeSession.IsStoryRunActive || openEndlessSelection || openStorySelection);
        if (GameModeSession.IsEndlessLand)
        {
            isShowingStartScreen = false;
        }

        if (!openEndlessSelection && GameModeSession.TryGetStorySkills(
            out int savedQSkill,
            out int savedESkill,
            out bool savedQSkillUpgraded,
            out bool savedESkillUpgraded))
        {
            qSkill = (SkillId)savedQSkill;
            eSkill = (SkillId)savedESkill;
            qSkillUpgraded = savedQSkillUpgraded;
            eSkillUpgraded = savedESkillUpgraded;
            isChoosingSkills = false;
        }
        else if (!openEndlessSelection && GameModeSession.ShouldSkipStorySkillSelection)
        {
            isChoosingSkills = false;
        }
    }

    private void Start()
    {
        lastObservedLevel = progression != null ? progression.Level : 1;
        Time.timeScale = IsGameplayActive ? 1f : 0f;
    }

    private void Update()
    {
        QueueSkillUpgradesForLevelChanges();
        if (isShowingStartScreen || isChoosingSkills || isChoosingUpgrade || isChoosingLevelFourReward
            || (health != null && health.CurrentHealth <= 0))
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            TryActivateSkill(qSkill, true);
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryActivateSkill(eSkill, false);
        }
    }

    private void TryActivateSkill(SkillId skill, bool isQSlot)
    {
        float readyTime = isQSlot ? qReadyTime : eReadyTime;
        if (skill == SkillId.None || Time.time < readyTime)
        {
            return;
        }

        ActivateSkill(skill);
        float nextReadyTime = Time.time + GetCooldown(skill);
        if (isQSlot)
        {
            qReadyTime = nextReadyTime;
        }
        else
        {
            eReadyTime = nextReadyTime;
        }
    }

    private void ActivateSkill(SkillId skill)
    {
        PlaySkillAudio(skill);

        switch (skill)
        {
            case SkillId.HornBlast:
                HornBlastEffect.Spawn(
                    transform,
                    GetSkillValue(skill, 12f, 16f),
                    GetSkillValue(skill, 9f, 14f),
                    GetSkillValue(skill, 17f, 26f),
                    IsSkillUpgraded(skill) ? 3 : 2);
                break;
            case SkillId.GravityTrap:
                GravityTrap.Spawn(
                    transform.position - transform.forward * 4f,
                    GetSkillValue(skill, 3f, 5f),
                    GetSkillValue(skill, 5.5f, 9f),
                    GetSkillValue(skill, 8f, 15f));
                break;
            case SkillId.Blink:
                BlinkForward(GetSkillValue(skill, 7f, 13f), GetSkillValue(skill, 12f, 16f));
                break;
            case SkillId.FlameTrail:
                FlameTrailEmitter.Spawn(
                    transform,
                    GetSkillValue(skill, 1.5f, 3f),
                    GetSkillValue(skill, 0.14f, 0.08f),
                    GetSkillValue(skill, 0.85f, 1.45f),
                    GetSkillValue(skill, 2.2f, 4f));
                break;
            case SkillId.TankShells:
                FireTankShells(
                    GetSkillValue(skill, 22f, 32f),
                    GetSkillValue(skill, 3.5f, 10f),
                    GetSkillValue(skill, 1.7f, 3f));
                break;
        }
    }

    private void InitializeSkillAudio()
    {
        skillAudioSource = gameObject.AddComponent<AudioSource>();
        skillAudioSource.playOnAwake = false;
        skillAudioSource.loop = false;
        skillAudioSource.spatialBlend = 0f;
        skillAudioSource.dopplerLevel = 0f;
        skillAudioSource.volume = 1f;

        hornBlastAudio = Resources.Load<AudioClip>("Audio/Skills/SFX_Skill_HornBlast");
        gravityTrapAudio = Resources.Load<AudioClip>("Audio/Skills/SFX_Skill_GravityTrap");
        blinkAudio = Resources.Load<AudioClip>("Audio/Skills/SFX_Skill_Blink");
        flameTrailAudio = Resources.Load<AudioClip>("Audio/Skills/SFX_Skill_FlameTrail");
        tankShellsAudio = Resources.Load<AudioClip>("Audio/Skills/SFX_Skill_TankShells");
    }

    private void PlaySkillAudio(SkillId skill)
    {
        AudioClip clip = null;
        float volumeScale = 0.5f;
        switch (skill)
        {
            case SkillId.HornBlast:
                clip = hornBlastAudio;
                volumeScale = 0.24f;
                break;
            case SkillId.GravityTrap:
                clip = gravityTrapAudio;
                volumeScale = 0.45f;
                break;
            case SkillId.Blink:
                clip = blinkAudio;
                volumeScale = 0.5f;
                break;
            case SkillId.FlameTrail:
                clip = flameTrailAudio;
                volumeScale = 0.35f;
                break;
            case SkillId.TankShells:
                clip = tankShellsAudio;
                volumeScale = 0.6f;
                break;
        }

        if (skillAudioSource != null && clip != null)
        {
            skillAudioSource.PlayOneShot(clip, volumeScale);
        }
    }

    private void BlinkForward(float distance, float minimumSpeed)
    {
        Vector3 destination = transform.position + transform.forward * distance;
        if (body != null)
        {
            body.position = destination;
            body.velocity = transform.forward * Mathf.Max(body.velocity.magnitude, minimumSpeed);
        }
        else
        {
            transform.position = destination;
        }
        LegacySkillVfx.FlashPlayer(transform, 0.1f);
    }

    private void FireTankShells(float speed, float lifetime, float explosionRadius)
    {
        Vector3 spawnPosition = transform.position - transform.forward * 2.8f + Vector3.up * 0.9f;
        TankMuzzleVfx.Spawn(spawnPosition, -transform.forward);
        TankShellProjectile.Spawn(spawnPosition, -transform.forward, transform, speed, lifetime, explosionRadius);
    }

    private float GetCooldown(SkillId skill)
    {
        switch (skill)
        {
            case SkillId.HornBlast:
                return GetSkillValue(skill, UnupgradedHornBlastCooldown, HornBlastCooldown);
            case SkillId.GravityTrap:
                return GetSkillValue(skill, UnupgradedGravityTrapCooldown, GravityTrapCooldown);
            case SkillId.Blink:
                return GetSkillValue(skill, UnupgradedBlinkCooldown, BlinkCooldown);
            case SkillId.FlameTrail:
                return GetSkillValue(skill, UnupgradedFlameTrailCooldown, FlameTrailCooldown);
            case SkillId.TankShells:
                return GetSkillValue(skill, UnupgradedTankShellCooldown, TankShellCooldown);
            default:
                return 0f;
        }
    }

    private float GetSkillValue(SkillId skill, float unupgradedValue, float upgradedValue)
    {
        return IsSkillUpgraded(skill) ? upgradedValue : unupgradedValue;
    }

    private bool IsSkillUpgraded(SkillId skill)
    {
        return (skill == qSkill && qSkillUpgraded) || (skill == eSkill && eSkillUpgraded);
    }

    private bool CanUpgradeSkill(SkillId skill)
    {
        return skill != SkillId.None && (skill == qSkill || skill == eSkill) && !IsSkillUpgraded(skill);
    }

    private void QueueSkillUpgradesForLevelChanges()
    {
        if (progression == null)
        {
            progression = GetComponent<PlayerProgression>();
            if (progression != null)
            {
                lastObservedLevel = progression.Level;
            }
            return;
        }

        if (progression.Level <= lastObservedLevel)
        {
            return;
        }

        int previousLevel = lastObservedLevel;
        int levelsGained = progression.Level - previousLevel;
        lastObservedLevel = progression.Level;
        if (!IsGameplayActive)
        {
            return;
        }

        QueueLevelRewards(previousLevel, levelsGained);

        if (!isChoosingUpgrade && !isChoosingLevelFourReward)
        {
            ShowNextUpgradeChoice();
        }
    }

    private void QueueLevelRewards(int previousLevel, int levelsGained)
    {
        int skillUpgradeLevels = 0;
        int healthUpgradeLevels = 0;
        for (int gainedIndex = 1; gainedIndex <= levelsGained; gainedIndex++)
        {
            int reachedLevel = previousLevel + gainedIndex;
            if (reachedLevel <= 3)
            {
                skillUpgradeLevels++;
            }
            else if (reachedLevel == 4)
            {
                pendingLevelFourRewardChoices++;
            }
            else
            {
                healthUpgradeLevels++;
            }
        }

        pendingUpgradeChoices += Mathf.Min(skillUpgradeLevels, 2);
        if (healthUpgradeLevels > 0)
        {
            GrantMaxHealthReward(healthUpgradeLevels);
        }
    }

    private void GrantMaxHealthReward(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (progression != null)
        {
            progression.IncreaseMaxHealthBonus(amount);
        }
        if (health != null)
        {
            health.IncreaseMaxHealth(amount, amount);
        }
        if (EndlessModeController.Instance != null)
        {
            EndlessModeController.Instance.ShowMaxHealthUpgrade();
        }
    }

    private bool HasUpgradableSkill()
    {
        return CanUpgradeSkill(qSkill) || CanUpgradeSkill(eSkill);
    }

    private void ShowNextUpgradeChoice()
    {
        if (pendingUpgradeChoices > 0 && HasUpgradableSkill())
        {
            isChoosingUpgrade = true;
            isChoosingLevelFourReward = false;
            Time.timeScale = 0f;
            return;
        }

        pendingUpgradeChoices = 0;
        isChoosingUpgrade = false;
        if (pendingLevelFourRewardChoices > 0)
        {
            isChoosingLevelFourReward = true;
            Time.timeScale = 0f;
            return;
        }

        isChoosingLevelFourReward = false;
    }

    private void UpgradeSkill(SkillId skill)
    {
        if (!isChoosingUpgrade || !CanUpgradeSkill(skill))
        {
            return;
        }

        if (skill == qSkill)
        {
            qSkillUpgraded = true;
        }
        if (skill == eSkill)
        {
            eSkillUpgraded = true;
        }

        SaveStorySkillState();

        pendingUpgradeChoices--;
        isChoosingUpgrade = false;
        if ((pendingUpgradeChoices > 0 && HasUpgradableSkill())
            || pendingLevelFourRewardChoices > 0)
        {
            ShowNextUpgradeChoice();
            return;
        }

        pendingUpgradeChoices = 0;
        Time.timeScale = 1f;
    }

    private void SelectLevelFourReward(bool increaseMaxHealth)
    {
        if (!isChoosingLevelFourReward || pendingLevelFourRewardChoices <= 0)
        {
            return;
        }

        if (increaseMaxHealth)
        {
            GrantMaxHealthReward(1);
        }
        else
        {
            if (progression != null)
            {
                progression.IncreaseHealthPackDropChance(HealthPackDropChanceUpgrade);
            }
            if (EndlessModeController.Instance != null)
            {
                EndlessModeController.Instance.ShowHealthPackDropUpgrade();
            }
        }

        pendingLevelFourRewardChoices--;
        isChoosingLevelFourReward = false;
        if ((pendingUpgradeChoices > 0 && HasUpgradableSkill())
            || pendingLevelFourRewardChoices > 0)
        {
            ShowNextUpgradeChoice();
            return;
        }

        Time.timeScale = 1f;
    }

    private void SelectSkillCard(SkillId skill)
    {
        if (qSkill == skill)
        {
            qSkill = SkillId.None;
            return;
        }
        if (eSkill == skill)
        {
            eSkill = SkillId.None;
            return;
        }
        if (qSkill == SkillId.None)
        {
            qSkill = skill;
            return;
        }
        if (eSkill == SkillId.None)
        {
            eSkill = skill;
            return;
        }

        eSkill = skill;
    }

    private void StartGame()
    {
        if (qSkill == SkillId.None || eSkill == SkillId.None)
        {
            return;
        }

        isChoosingSkills = false;
        SaveStorySkillState();
        if (GameModeSession.IsStoryRunActive && progression != null && progression.Level > 1)
        {
            pendingUpgradeChoices += Mathf.Min(progression.Level - 1, 2);
            lastObservedLevel = progression.Level;
            ShowNextUpgradeChoice();
            if (isChoosingUpgrade || isChoosingLevelFourReward)
            {
                return;
            }
        }
        Time.timeScale = 1f;
    }

    private void SaveStorySkillState()
    {
        GameModeSession.RecordStorySkills(
            (int)qSkill,
            (int)eSkill,
            qSkillUpgraded,
            eSkillUpgraded);
    }

    private void OnGUI()
    {
        if (VehicleGarageSystem.Instance != null && VehicleGarageSystem.Instance.IsOpen)
        {
            return;
        }

        GUI.depth = -1000;
        if (isShowingStartScreen)
        {
            DrawStartScreen();
        }
        else if (isChoosingSkills)
        {
            DrawSkillSelection();
        }
        else if (isChoosingUpgrade)
        {
            DrawSkillUpgradeSelection();
        }
        else if (isChoosingLevelFourReward)
        {
            DrawLevelFourRewardSelection();
        }
    }

    private void DrawStartScreen()
    {
        Rect screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
        GUI.DrawTexture(screenRect, Texture2D.blackTexture, ScaleMode.StretchToFill);
        if (coverTexture != null)
        {
            GUI.DrawTexture(screenRect, coverTexture, ScaleMode.ScaleAndCrop, true);
        }

        EnsureStartScreenStyles();
        DrawStartBrand();

        if (startMenuPage == StartMenuPage.Main)
        {
            float menuWidth = Mathf.Clamp(Screen.width * 0.27f, 330f, 500f);
            float rowHeight = Mathf.Clamp(Screen.height * 0.06f, 52f, 70f);
            float rowSpacing = Mathf.Clamp(Screen.height * 0.01f, 8f, 14f);
            float panelHeight = rowHeight * 3f + rowSpacing * 2f;
            float menuX = Mathf.Clamp(
                Screen.width * startButtonsX - menuWidth * 0.5f,
                20f,
                Screen.width - menuWidth - 20f);
            float menuY = Mathf.Clamp(
                Screen.height * startButtonsY,
                80f,
                Screen.height - panelHeight - 36f);

            Rect storyButton = new Rect(menuX, menuY, menuWidth, rowHeight);
            Rect endlessButton = new Rect(storyButton.x, storyButton.yMax + rowSpacing, storyButton.width, rowHeight);
            Rect garageButton = new Rect(endlessButton.x, endlessButton.yMax + rowSpacing, endlessButton.width, rowHeight);
            if (DrawStartMenuTextButton(storyButton, "故事模式"))
            {
                GameModeSession.SelectStory();
                startMenuPage = StartMenuPage.StoryChapters;
            }
            if (DrawStartMenuTextButton(endlessButton, "无尽模式"))
            {
                startMenuPage = StartMenuPage.EndlessModes;
            }
            if (DrawStartMenuTextButton(garageButton, "车库"))
            {
                VehicleGarageSystem garage = VehicleGarageSystem.Instance;
                if (garage == null)
                {
                    garage = GetComponent<VehicleGarageSystem>();
                }
                if (garage != null)
                {
                    garage.OpenGarage();
                }
            }
            DrawStartMenuLayoutEditor();
            return;
        }

        if (startMenuPage == StartMenuPage.StoryChapters)
        {
            DrawStoryLevelSelection();
            return;
        }

        DrawEndlessModeTextMenu();
        return;
    }

    private void DrawEndlessModeTextMenu()
    {
        float menuWidth = Mathf.Clamp(Screen.width * 0.27f, 330f, 500f);
        float rowHeight = Mathf.Clamp(Screen.height * 0.06f, 52f, 70f);
        float rowSpacing = Mathf.Clamp(Screen.height * 0.01f, 8f, 14f);
        float menuHeight = rowHeight * 4f + rowSpacing * 3f;
        float menuX = Mathf.Clamp(
            Screen.width * startButtonsX - menuWidth * 0.5f,
            20f,
            Screen.width - menuWidth - 20f);
        float menuY = Mathf.Clamp(
            Screen.height - menuHeight - 42f,
            160f,
            Screen.height - menuHeight - 24f);

        Rect land = new Rect(menuX, menuY, menuWidth, rowHeight);
        Rect sea = new Rect(menuX, land.yMax + rowSpacing, menuWidth, rowHeight);
        Rect sky = new Rect(menuX, sea.yMax + rowSpacing, menuWidth, rowHeight);
        Rect back = new Rect(menuX, sky.yMax + rowSpacing, menuWidth, rowHeight);

        if (DrawStartMenuTextButton(land, "陆地追逐"))
        {
            GameModeSession.StartEndlessLand();
            isShowingStartScreen = false;
        }
        if (DrawStartMenuTextButton(sea, "海上逃生"))
        {
            GameModeSession.StartEndlessSea();
        }
        if (DrawStartMenuTextButton(sky, "空中追击"))
        {
            GameModeSession.StartEndlessSky();
        }
        if (DrawStartMenuTextButton(back, "返回"))
        {
            startMenuPage = StartMenuPage.Main;
        }
    }

    private void EnsureStartScreenStyles()
    {
        if (startButtonNormalTexture == null)
        {
            startButtonNormalTexture = CreateSolidTexture(new Color(0.025f, 0.03f, 0.032f, 0.62f));
        }
        if (startButtonHoverTexture == null)
        {
            startButtonHoverTexture = CreateSolidTexture(new Color(0.34f, 0.12f, 0.035f, 0.84f));
        }
        if (startButtonPrimaryTexture == null)
        {
            startButtonPrimaryTexture = CreateSolidTexture(new Color(0.56f, 0.2f, 0.045f, 0.94f));
        }
        if (startButtonActiveTexture == null)
        {
            startButtonActiveTexture = CreateSolidTexture(new Color(0.5f, 0.2f, 0.055f, 1f));
        }
        if (startMenuTextTexture == null)
        {
            startMenuTextTexture = CreateSolidTexture(Color.clear);
        }
        if (startAccentCyanTexture == null)
        {
            startAccentCyanTexture = CreateSolidTexture(new Color(0.2f, 0.78f, 0.86f, 1f));
        }
        if (startAccentOrangeTexture == null)
        {
            startAccentOrangeTexture = CreateSolidTexture(new Color(1f, 0.34f, 0.07f, 1f));
        }
        if (startDividerTexture == null)
        {
            startDividerTexture = CreateSolidTexture(new Color(0.82f, 0.86f, 0.86f, 0.32f));
        }
        if (startPanelTexture == null)
        {
            startPanelTexture = CreateSolidTexture(new Color(0.015f, 0.018f, 0.02f, 0.58f));
        }
        if (startButtonStyle == null)
        {
            startButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(18, 18, 0, 0)
            };
            startButtonStyle.normal.background = startButtonNormalTexture;
            startButtonStyle.hover.background = startButtonHoverTexture;
            startButtonStyle.active.background = startButtonActiveTexture;
            startButtonStyle.focused.background = startButtonNormalTexture;
            startButtonStyle.normal.textColor = new Color(0.9f, 0.94f, 0.96f, 1f);
            startButtonStyle.hover.textColor = Color.white;
            startButtonStyle.active.textColor = Color.white;
            startButtonStyle.focused.textColor = new Color(0.9f, 0.94f, 0.96f, 1f);

            startMenuTextButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            startMenuTextButtonStyle.normal.background = startMenuTextTexture;
            startMenuTextButtonStyle.hover.background = startMenuTextTexture;
            startMenuTextButtonStyle.active.background = startMenuTextTexture;
            startMenuTextButtonStyle.focused.background = startMenuTextTexture;
            startMenuTextButtonStyle.normal.textColor = new Color(1f, 1f, 1f, 0.94f);
            startMenuTextButtonStyle.hover.textColor = Color.white;
            startMenuTextButtonStyle.active.textColor = Color.white;
            startMenuTextButtonStyle.focused.textColor = Color.white;

            startPrimaryButtonStyle = new GUIStyle(startButtonStyle);
            startPrimaryButtonStyle.normal.background = startButtonPrimaryTexture;

            startCompactButtonStyle = new GUIStyle(startButtonStyle);
            startCompactButtonStyle.alignment = TextAnchor.MiddleLeft;
            startCompactButtonStyle.padding = new RectOffset(24, 180, 0, 0);
            startCompactPrimaryButtonStyle = new GUIStyle(startPrimaryButtonStyle);
            startCompactPrimaryButtonStyle.alignment = TextAnchor.MiddleLeft;
            startCompactPrimaryButtonStyle.padding = new RectOffset(24, 180, 0, 0);

            startTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            startTitleStyle.normal.textColor = new Color(0.94f, 0.97f, 0.98f, 1f);

            startKickerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            startKickerStyle.normal.textColor = new Color(0.12f, 0.78f, 0.9f, 1f);

            startMetaStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip
            };
            startMetaStyle.normal.textColor = new Color(0.56f, 0.82f, 0.86f, 1f);

            startBackButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            startBackButtonStyle.normal.background = startButtonNormalTexture;
            startBackButtonStyle.hover.background = startButtonHoverTexture;
            startBackButtonStyle.active.background = startButtonActiveTexture;
            startBackButtonStyle.normal.textColor = new Color(0.78f, 0.84f, 0.86f, 1f);
            startBackButtonStyle.hover.textColor = Color.white;
            startBackButtonStyle.active.textColor = Color.white;

            startBrandTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            startBrandTitleStyle.normal.textColor = new Color(1f, 0.98f, 0.94f, 1f);

            startBrandSubtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            startBrandSubtitleStyle.normal.textColor = new Color(0.84f, 0.91f, 0.92f, 0.96f);

            startLevelButtonStyle = new GUIStyle(GUI.skin.button);
            startLevelButtonStyle.normal.background = startButtonNormalTexture;
            startLevelButtonStyle.hover.background = startButtonHoverTexture;
            startLevelButtonStyle.active.background = startButtonActiveTexture;
            startLevelButtonStyle.normal.textColor = Color.clear;
            startLevelButtonStyle.hover.textColor = Color.clear;
            startLevelButtonStyle.active.textColor = Color.clear;

            startLevelNumberStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontStyle = FontStyle.Bold
            };
            startLevelNumberStyle.normal.textColor = new Color(1f, 0.42f, 0.09f, 1f);

            startLevelTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };
            startLevelTitleStyle.normal.textColor = new Color(0.95f, 0.97f, 0.96f, 1f);

            startLevelMetaStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerLeft,
                fontStyle = FontStyle.Normal,
                clipping = TextClipping.Clip
            };
            startLevelMetaStyle.normal.textColor = new Color(0.68f, 0.75f, 0.76f, 1f);
        }

        int buttonFontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.023f, 19f, 25f));
        startButtonStyle.fontSize = buttonFontSize;
        startMenuTextButtonStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.029f, 24f, 36f));
        startPrimaryButtonStyle.fontSize = buttonFontSize;
        startCompactButtonStyle.fontSize = Mathf.Max(18, buttonFontSize - 3);
        startCompactPrimaryButtonStyle.fontSize = startCompactButtonStyle.fontSize;
        startTitleStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.036f, 26f, 38f));
        startKickerStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.014f, 12f, 16f));
        startMetaStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.016f, 12f, 17f));
        startBackButtonStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.026f, 20f, 28f));
        startBrandTitleStyle.fontSize = Mathf.RoundToInt(
            Mathf.Clamp(Screen.height * 0.078f, 56f, 88f) * startBrandScale);
        startBrandSubtitleStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.0165f, 14f, 19f));
        startLevelNumberStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.02f, 16f, 22f));
        startLevelTitleStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.03f, 22f, 32f));
        startLevelMetaStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.015f, 12f, 16f));
    }

    private void DrawStartBrand()
    {
        if (logoTexture != null)
        {
            float logoWidth = Mathf.Clamp(
                Screen.width * 0.25f * Mathf.Max(0.8f, startBrandScale),
                360f,
                Mathf.Min(Screen.width - 40f, 900f));
            float logoX = Mathf.Clamp(
                Screen.width * startBrandX - logoWidth * 0.5f,
                20f,
                Screen.width - logoWidth - 20f);
            float logoY = Mathf.Clamp(Screen.height * startBrandY, 20f, Screen.height - 150f);
            float logoHeight = logoWidth * logoTexture.height / (float)Mathf.Max(1, logoTexture.width);
            GUI.DrawTexture(new Rect(logoX, logoY, logoWidth, logoHeight), logoTexture, ScaleMode.ScaleToFit, true);
            return;
        }

        float titleWidth = Mathf.Clamp(
            Screen.width * 0.52f * Mathf.Max(1f, startBrandScale),
            420f,
            Mathf.Min(Screen.width - 40f, 1200f));
        float titleX = Mathf.Clamp(
            Screen.width * startBrandX - titleWidth * 0.5f,
            20f,
            Screen.width - titleWidth - 20f);
        float titleY = Mathf.Clamp(
            Screen.height * startBrandY,
            20f,
            Screen.height - 150f);
        float titleHeight = Mathf.Clamp(
            Mathf.Clamp(Screen.height * 0.12f, 86f, 128f) * startBrandScale,
            86f,
            180f);
        Rect titleRect = new Rect(titleX, titleY, titleWidth, titleHeight);
        Color titleColor = startBrandTitleStyle.normal.textColor;
        startBrandTitleStyle.normal.textColor = new Color(0f, 0f, 0f, 0.5f);
        GUI.Label(new Rect(titleRect.x + 3f, titleRect.y + 4f, titleRect.width, titleRect.height), "佛罗里达的一天", startBrandTitleStyle);
        startBrandTitleStyle.normal.textColor = titleColor;
        GUI.Label(titleRect, "佛罗里达的一天", startBrandTitleStyle);
        GUI.DrawTexture(
            new Rect(titleRect.center.x - 45f, titleRect.yMax - 6f, 90f, 3f),
            startAccentOrangeTexture);
        GUI.Label(
            new Rect(titleRect.x, titleRect.yMax + 2f, titleRect.width, 26f),
            "A  D A Y   I N   F L O R I D A",
            startBrandSubtitleStyle);
    }

    private bool DrawStartMenuTextButton(Rect rect, string label)
    {
        bool hovered = rect.Contains(Event.current.mousePosition);
        string displayLabel = hovered ? ">  " + label : label;
        return GUI.Button(rect, displayLabel, startMenuTextButtonStyle);
    }

    public void SetStartMenuLayout(float brandX, float brandY, float buttonsX, float buttonsY)
    {
        SetStartMenuLayout(brandX, brandY, buttonsX, buttonsY, startBrandScale);
    }

    public void SetStartMenuLayout(
        float brandX,
        float brandY,
        float buttonsX,
        float buttonsY,
        float brandScale)
    {
        startBrandX = Mathf.Clamp(brandX, 0.1f, 0.9f);
        startBrandY = Mathf.Clamp(brandY, 0.02f, 0.5f);
        startButtonsX = Mathf.Clamp(buttonsX, 0.1f, 0.9f);
        startButtonsY = Mathf.Clamp(buttonsY, 0.2f, 0.8f);
        startBrandScale = Mathf.Clamp(brandScale, 0.7f, 1.8f);
    }

    private void LoadStartMenuLayout()
    {
        SetStartMenuLayout(
            PlayerPrefs.GetFloat(StartBrandXKey, DefaultStartBrandX),
            PlayerPrefs.GetFloat(StartBrandYKey, DefaultStartBrandY),
            PlayerPrefs.GetFloat(StartButtonsXKey, DefaultStartButtonsX),
            PlayerPrefs.GetFloat(StartButtonsYKey, DefaultStartButtonsY),
            PlayerPrefs.GetFloat(StartBrandScaleKey, DefaultStartBrandScale));
    }

    private void SaveStartMenuLayout()
    {
        PlayerPrefs.SetFloat(StartBrandXKey, startBrandX);
        PlayerPrefs.SetFloat(StartBrandYKey, startBrandY);
        PlayerPrefs.SetFloat(StartBrandScaleKey, startBrandScale);
        PlayerPrefs.SetFloat(StartButtonsXKey, startButtonsX);
        PlayerPrefs.SetFloat(StartButtonsYKey, startButtonsY);
        PlayerPrefs.Save();
        startLayoutStatus = "位置参数已保存";
        startLayoutStatusUntil = Time.unscaledTime + 2f;
    }

    private void ResetStartMenuLayout()
    {
        SetStartMenuLayout(
            DefaultStartBrandX,
            DefaultStartBrandY,
            DefaultStartButtonsX,
            DefaultStartButtonsY,
            DefaultStartBrandScale);
        PlayerPrefs.DeleteKey(StartBrandXKey);
        PlayerPrefs.DeleteKey(StartBrandYKey);
        PlayerPrefs.DeleteKey(StartBrandScaleKey);
        PlayerPrefs.DeleteKey(StartButtonsXKey);
        PlayerPrefs.DeleteKey(StartButtonsYKey);
        PlayerPrefs.Save();
        startLayoutStatus = "已恢复默认位置";
        startLayoutStatusUntil = Time.unscaledTime + 2f;
    }

    private void DrawStartMenuLayoutEditor()
    {
        float scale = Mathf.Clamp(Screen.height / 1080f, 0.67f, 1.2f);
        Rect toggleRect = new Rect(
            Screen.width - 118f * scale,
            28f * scale,
            90f * scale,
            42f * scale);
        if (GUI.Button(toggleRect, "调整", startBackButtonStyle))
        {
            showStartLayoutEditor = !showStartLayoutEditor;
        }
        if (!showStartLayoutEditor)
        {
            return;
        }

        Rect panelRect = new Rect(
            Screen.width - 340f * scale,
            84f * scale,
            312f * scale,
            338f * scale);
        GUI.DrawTexture(panelRect, startPanelTexture);
        GUI.DrawTexture(
            new Rect(panelRect.x, panelRect.y, panelRect.width, 2f * scale),
            startAccentOrangeTexture);
        GUI.Label(
            new Rect(panelRect.x + 16f * scale, panelRect.y + 10f * scale,
                panelRect.width - 32f * scale, 30f * scale),
            "开始菜单位置",
            startTitleStyle);

        float rowX = panelRect.x + 18f * scale;
        float rowWidth = panelRect.width - 36f * scale;
        float rowY = panelRect.y + 50f * scale;
        float nextBrandX = DrawStartLayoutSlider(
            rowX, rowY, rowWidth, "标题 X", startBrandX, 0.1f, 0.9f, scale);
        rowY += 46f * scale;
        float nextBrandY = DrawStartLayoutSlider(
            rowX, rowY, rowWidth, "标题 Y", startBrandY, 0.02f, 0.5f, scale);
        rowY += 46f * scale;
        float nextBrandScale = DrawStartLayoutSlider(
            rowX, rowY, rowWidth, "标题大小", startBrandScale, 0.7f, 1.8f, scale);
        rowY += 46f * scale;
        float nextButtonsX = DrawStartLayoutSlider(
            rowX, rowY, rowWidth, "菜单 X", startButtonsX, 0.1f, 0.9f, scale);
        rowY += 46f * scale;
        float nextButtonsY = DrawStartLayoutSlider(
            rowX, rowY, rowWidth, "菜单 Y", startButtonsY, 0.2f, 0.8f, scale);

        if (!Mathf.Approximately(nextBrandX, startBrandX)
            || !Mathf.Approximately(nextBrandY, startBrandY)
            || !Mathf.Approximately(nextBrandScale, startBrandScale)
            || !Mathf.Approximately(nextButtonsX, startButtonsX)
            || !Mathf.Approximately(nextButtonsY, startButtonsY))
        {
            SetStartMenuLayout(
                nextBrandX,
                nextBrandY,
                nextButtonsX,
                nextButtonsY,
                nextBrandScale);
        }

        float buttonY = panelRect.yMax - 46f * scale;
        float buttonGap = 10f * scale;
        float buttonWidth = (rowWidth - buttonGap) * 0.5f;
        if (GUI.Button(
            new Rect(rowX, buttonY, buttonWidth, 32f * scale),
            "保存",
            startPrimaryButtonStyle))
        {
            SaveStartMenuLayout();
        }
        if (GUI.Button(
            new Rect(rowX + buttonWidth + buttonGap, buttonY, buttonWidth, 32f * scale),
            "重置",
            startButtonStyle))
        {
            ResetStartMenuLayout();
        }
        if (!string.IsNullOrEmpty(startLayoutStatus)
            && Time.unscaledTime < startLayoutStatusUntil)
        {
            GUI.Label(
                new Rect(panelRect.x, panelRect.yMax + 4f * scale, panelRect.width, 22f * scale),
                startLayoutStatus,
                startKickerStyle);
        }
    }

    private float DrawStartLayoutSlider(
        float x,
        float y,
        float width,
        string label,
        float value,
        float minimum,
        float maximum,
        float scale)
    {
        GUI.Label(
            new Rect(x, y, width, 20f * scale),
            $"{label}    {value:0.00}",
            startMetaStyle);
        return GUI.HorizontalSlider(
            new Rect(x, y + 24f * scale, width, 14f * scale),
            value,
            minimum,
            maximum);
    }

    private void DrawStartMenuHeader(Rect contentRect, string kicker, string title)
    {
        GUI.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, 34f), title, startTitleStyle);
        GUI.Label(new Rect(contentRect.x, contentRect.y + 31f, contentRect.width, 22f), kicker, startKickerStyle);
    }

    private bool DrawStartMenuButton(Rect rect, string label, bool primary, bool compact)
    {
        GUIStyle style = compact
            ? primary ? startCompactPrimaryButtonStyle : startCompactButtonStyle
            : primary ? startPrimaryButtonStyle : startButtonStyle;
        bool wasClicked = GUI.Button(rect, label, style);
        bool isHovered = rect.Contains(Event.current.mousePosition) && GUI.enabled;
        GUI.DrawTexture(
            new Rect(rect.x, rect.yMax - 1f, rect.width, 1f),
            isHovered ? startAccentOrangeTexture : startDividerTexture);
        if (primary || isHovered)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, 4f, rect.height), startAccentOrangeTexture);
        }
        return wasClicked;
    }

    private void DrawStartMenuMeta(Rect rect, string text)
    {
        GUI.Label(new Rect(rect.x + rect.width * 0.42f, rect.y, rect.width * 0.45f, rect.height), text, startMetaStyle);
    }

    private void DrawStartMenuBackButton(float menuX, float menuY, float menuWidth)
    {
        Rect backRect = new Rect(
            menuX + 8f,
            menuY,
            Mathf.Clamp(menuWidth * 0.12f, 44f, 52f),
            Mathf.Clamp(Screen.height * 0.042f, 38f, 46f));
        if (GUI.Button(backRect, new GUIContent("←", "返回"), startBackButtonStyle))
        {
            startMenuPage = StartMenuPage.Main;
        }
    }

    private bool DrawStartLevelCard(
        Rect rect,
        int levelNumber,
        string chapter,
        string title,
        string description)
    {
        bool isHovered = rect.Contains(Event.current.mousePosition);
        bool wasClicked = GUI.Button(rect, GUIContent.none, startLevelButtonStyle);
        GUI.DrawTexture(
            new Rect(rect.x, rect.y, isHovered ? 6f : 3f, rect.height),
            isHovered ? startAccentOrangeTexture : startDividerTexture);
        GUI.Label(
            new Rect(rect.x + 22f, rect.y + 14f, 54f, 30f),
            levelNumber.ToString("00"),
            startLevelNumberStyle);
        GUI.Label(
            new Rect(rect.xMax - 116f, rect.y + 14f, 92f, 26f),
            chapter,
            startKickerStyle);
        GUI.Label(
            new Rect(rect.x + 82f, rect.y + 28f, rect.width - 112f, 48f),
            title,
            startLevelTitleStyle);
        GUI.Label(
            new Rect(rect.x + 24f, rect.yMax - 42f, rect.width - 48f, 26f),
            description,
            startLevelMetaStyle);
        return wasClicked;
    }

    private static Texture2D CreateSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private void OnDestroy()
    {
        if (startButtonNormalTexture != null)
        {
            Destroy(startButtonNormalTexture);
        }
        if (startButtonHoverTexture != null)
        {
            Destroy(startButtonHoverTexture);
        }
        if (startButtonPrimaryTexture != null)
        {
            Destroy(startButtonPrimaryTexture);
        }
        if (startButtonActiveTexture != null)
        {
            Destroy(startButtonActiveTexture);
        }
        if (startAccentCyanTexture != null)
        {
            Destroy(startAccentCyanTexture);
        }
        if (startAccentOrangeTexture != null)
        {
            Destroy(startAccentOrangeTexture);
        }
        if (startDividerTexture != null)
        {
            Destroy(startDividerTexture);
        }
        if (startPanelTexture != null)
        {
            Destroy(startPanelTexture);
        }
        if (startMenuTextTexture != null)
        {
            Destroy(startMenuTextTexture);
        }
    }

    private void DrawSkillSelection()
    {
        const float spacing = 14f;
        const float cardAspectRatio = 1024f / 1536f;
        float maximumCardHeight = Mathf.Min(452f, Screen.height - 170f);
        float cardWidth = Mathf.Min((Screen.width - spacing * 6f) / 5f, maximumCardHeight * cardAspectRatio);
        float cardHeight = cardWidth / cardAspectRatio;
        float startX = (Screen.width - (cardWidth * 5f + spacing * 4f)) * 0.5f;
        float startY = Mathf.Max(74f, (Screen.height - cardHeight - 120f) * 0.5f);

        GUI.Box(new Rect(0f, 0f, Screen.width, 62f), "开局选择两个主动技能");
        GUI.Label(new Rect(24f, 28f, 360f, 28f), "直接点击卡牌：第一次选 Q，第二次选 E，再点已选卡可取消");
        DrawSkillCard(new Rect(startX, startY, cardWidth, cardHeight), SkillId.HornBlast);
        DrawSkillCard(new Rect(startX + (cardWidth + spacing), startY, cardWidth, cardHeight), SkillId.GravityTrap);
        DrawSkillCard(new Rect(startX + (cardWidth + spacing) * 2f, startY, cardWidth, cardHeight), SkillId.Blink);
        DrawSkillCard(new Rect(startX + (cardWidth + spacing) * 3f, startY, cardWidth, cardHeight), SkillId.FlameTrail);
        DrawSkillCard(new Rect(startX + (cardWidth + spacing) * 4f, startY, cardWidth, cardHeight), SkillId.TankShells);

        string qName = GetSkillName(qSkill);
        string eName = GetSkillName(eSkill);
        GUI.Label(new Rect((Screen.width - 420f) * 0.5f, startY + cardHeight + 20f, 420f, 28f), $"Q 槽：{qName}    E 槽：{eName}");
        if (qSkill != SkillId.None && eSkill != SkillId.None)
        {
            if (GUI.Button(new Rect((Screen.width - 220f) * 0.5f, startY + cardHeight + 52f, 220f, 42f), "开始生存"))
            {
                StartGame();
            }
        }
    }

    private void DrawSkillUpgradeSelection()
    {
        Rect screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.76f);
        GUI.DrawTexture(screenRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = previousColor;

        List<SkillId> choices = new List<SkillId>(2);
        if (CanUpgradeSkill(qSkill))
        {
            choices.Add(qSkill);
        }
        if (CanUpgradeSkill(eSkill) && eSkill != qSkill)
        {
            choices.Add(eSkill);
        }

        float panelWidth = Mathf.Min(Screen.width - 48f, choices.Count == 1 ? 440f : 920f);
        float panelHeight = 300f;
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);
        GUI.Box(panel, GUIContent.none);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 30,
            fontStyle = FontStyle.Bold
        };
        GUIStyle descriptionStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 17,
            wordWrap = true
        };
        GUIStyle choiceStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };

        GUI.Label(new Rect(panel.x + 20f, panel.y + 24f, panel.width - 40f, 44f), "技能强化", titleStyle);
        GUI.Label(
            new Rect(panel.x + 30f, panel.y + 72f, panel.width - 60f, 40f),
            "选择一项已装备技能，恢复为当前版本的完整强度", descriptionStyle);

        float spacing = 18f;
        float choiceWidth = (panel.width - 56f - spacing * Mathf.Max(0, choices.Count - 1)) / choices.Count;
        for (int index = 0; index < choices.Count; index++)
        {
            SkillId skill = choices[index];
            Rect choiceRect = new Rect(
                panel.x + 28f + index * (choiceWidth + spacing),
                panel.y + 132f,
                choiceWidth,
                132f);
            string choiceText = $"{GetSkillName(skill)}\n\n{GetUpgradeSummary(skill)}";
            if (GUI.Button(choiceRect, choiceText, choiceStyle))
            {
                UpgradeSkill(skill);
            }
        }
    }

    private void DrawLevelFourRewardSelection()
    {
        Rect screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.76f);
        GUI.DrawTexture(screenRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = previousColor;

        float panelWidth = Mathf.Min(Screen.width - 48f, 920f);
        const float panelHeight = 320f;
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);
        GUI.Box(panel, GUIContent.none);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 30,
            fontStyle = FontStyle.Bold
        };
        GUIStyle descriptionStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 17,
            wordWrap = true
        };
        GUIStyle choiceStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };

        GUI.Label(new Rect(panel.x + 20f, panel.y + 22f, panel.width - 40f, 44f), "等级 4 奖励", titleStyle);
        GUI.Label(
            new Rect(panel.x + 30f, panel.y + 68f, panel.width - 60f, 42f),
            "选择一项本局永久强化",
            descriptionStyle);

        const float spacing = 18f;
        float choiceWidth = (panel.width - 56f - spacing) * 0.5f;
        Rect healthChoice = new Rect(panel.x + 28f, panel.y + 130f, choiceWidth, 150f);
        Rect dropChoice = new Rect(healthChoice.xMax + spacing, healthChoice.y, choiceWidth, healthChoice.height);
        if (GUI.Button(healthChoice, "提高生命上限\n\n最大生命 +1\n并恢复 1 点生命", choiceStyle))
        {
            SelectLevelFourReward(true);
        }
        if (GUI.Button(dropChoice, "提高血包掉落率\n\n敌人掉落概率 +10%\n本局持续生效", choiceStyle))
        {
            SelectLevelFourReward(false);
        }
    }

    private void DrawStoryLevelSelection()
    {
        DrawStoryLevelSelectionTextMenu();
    }

    private void DrawStoryLevelSelectionCards()
    {
        float gridWidth = Mathf.Min(Screen.width - 64f, Mathf.Clamp(Screen.width * 0.6f, 720f, 980f));
        float gap = Mathf.Clamp(Screen.width * 0.012f, 12f, 18f);
        float cardWidth = (gridWidth - gap) * 0.5f;
        float cardHeight = Mathf.Clamp(Screen.height * 0.135f, 112f, 146f);
        float headerHeight = 70f;
        float panelWidth = gridWidth + 44f;
        float panelHeight = headerHeight + cardHeight * 2f + gap + 68f;
        float panelX = (Screen.width - panelWidth) * 0.5f;
        float panelY = Mathf.Clamp(Screen.height * 0.29f, 205f, Screen.height - panelHeight - 24f);
        Rect panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);
        GUI.DrawTexture(panelRect, startPanelTexture);
        DrawStartMenuHeader(
            new Rect(panelRect.x + 22f, panelRect.y + 12f, panelRect.width - 44f, 54f),
            "直接选择并开始",
            "选择关卡");

        float gridX = panelRect.x + 22f;
        float gridY = panelRect.y + headerHeight;
        Rect levelOne = new Rect(gridX, gridY, cardWidth, cardHeight);
        Rect levelTwo = new Rect(levelOne.xMax + gap, gridY, cardWidth, cardHeight);
        Rect levelThree = new Rect(gridX, levelOne.yMax + gap, cardWidth, cardHeight);
        Rect levelFour = new Rect(levelThree.xMax + gap, levelThree.y, cardWidth, cardHeight);
        if (DrawStartLevelCard(
            levelOne,
            1,
            "第一章",
            "岛屿追逐",
            "从海岛公路开始逃亡 · 重新选择技能"))
        {
            GameModeSession.StartStoryLevel(1);
        }
        if (DrawStartLevelCard(
            levelTwo,
            2,
            "第一章",
            "海上逃生",
            "驾驶快艇穿越海面追击 · 继承技能"))
        {
            GameModeSession.StartStoryLevel(2);
        }
        if (DrawStartLevelCard(
            levelThree,
            3,
            "第二章",
            "宝箱撤离",
            "夺取宝箱并冲向机场 · 继承第一关技能"))
        {
            GameModeSession.StartStoryLevel(3);
        }
        if (DrawStartLevelCard(
            levelFour,
            4,
            "第二章",
            "空中追击",
            "驾驶飞机穿越云海 · 无技能选择"))
        {
            GameModeSession.StartStoryLevel(4);
        }
        DrawStartMenuBackButton(
            panelRect.x + 18f,
            levelThree.yMax + gap + 8f,
            panelRect.width);
    }

    private void DrawStoryLevelSelectionTextMenu()
    {
        float menuWidth = Mathf.Clamp(Screen.width * 0.27f, 330f, 500f);
        float rowHeight = Mathf.Clamp(Screen.height * 0.06f, 52f, 70f);
        float rowSpacing = Mathf.Clamp(Screen.height * 0.01f, 8f, 14f);
        float menuHeight = rowHeight * 5f + rowSpacing * 4f;
        float menuX = Mathf.Clamp(
            Screen.width * startButtonsX - menuWidth * 0.5f,
            20f,
            Screen.width - menuWidth - 20f);
        float menuY = Mathf.Clamp(
            Screen.height - menuHeight - 42f,
            120f,
            Screen.height - menuHeight - 24f);

        Rect levelOne = new Rect(menuX, menuY, menuWidth, rowHeight);
        Rect levelTwo = new Rect(menuX, levelOne.yMax + rowSpacing, menuWidth, rowHeight);
        Rect levelThree = new Rect(menuX, levelTwo.yMax + rowSpacing, menuWidth, rowHeight);
        Rect levelFour = new Rect(menuX, levelThree.yMax + rowSpacing, menuWidth, rowHeight);
        Rect back = new Rect(menuX, levelFour.yMax + rowSpacing, menuWidth, rowHeight);

        if (DrawStartMenuTextButton(levelOne, "第一关  岛屿追逐"))
        {
            GameModeSession.StartStoryLevel(1);
        }
        if (DrawStartMenuTextButton(levelTwo, "第二关  海上逃生"))
        {
            GameModeSession.StartStoryLevel(2);
        }
        if (DrawStartMenuTextButton(levelThree, "第三关  宝箱撤离"))
        {
            GameModeSession.StartStoryLevel(3);
        }
        if (DrawStartMenuTextButton(levelFour, "第四关  空中追击"))
        {
            GameModeSession.StartStoryLevel(4);
        }
        if (DrawStartMenuTextButton(back, "返回"))
        {
            startMenuPage = StartMenuPage.Main;
        }
    }

    private static string GetUpgradeSummary(SkillId skill)
    {
        switch (skill)
        {
            case SkillId.HornBlast:
                return "冷却 4.5秒 → 3秒\n范围 12 → 16，击退与脉冲增强";
            case SkillId.GravityTrap:
                return "冷却 11秒 → 8秒\n范围、持续与拉力恢复";
            case SkillId.Blink:
                return "冷却 7秒 → 5秒\n位移距离与出场速度恢复";
            case SkillId.FlameTrail:
                return "冷却 12秒 → 9秒\n喷火时间、宽度与残留恢复";
            case SkillId.TankShells:
                return "冷却 10秒 → 7秒\n射程、速度与爆炸范围恢复";
            default:
                return "恢复完整技能强度";
        }
    }

    private void DrawSkillCard(Rect rect, SkillId skill)
    {
        bool isHovered = rect.Contains(Event.current.mousePosition);
        bool isSelected = qSkill == skill || eSkill == skill;
        float scale = isHovered ? 1.045f : isSelected ? 1.02f : 1f;
        Rect drawRect = new Rect(
            rect.center.x - rect.width * scale * 0.5f,
            rect.center.y - rect.height * scale * 0.5f,
            rect.width * scale,
            rect.height * scale);
        Texture2D cardTexture = GetSkillCardTexture(skill);

        Color borderColor = isSelected
            ? new Color(1f, 0.52f, 0.08f, 1f)
            : isHovered
                ? new Color(0.1f, 0.78f, 1f, 1f)
                : new Color(0.48f, 0.53f, 0.6f, 1f);
        DrawTintedRect(drawRect, new Color(0.018f, 0.024f, 0.034f, 1f));
        DrawCardBorder(drawRect, borderColor, Mathf.Max(2f, drawRect.width * 0.018f));

        Rect headerRect = new Rect(
            drawRect.x + drawRect.width * 0.075f,
            drawRect.y + drawRect.height * 0.045f,
            drawRect.width * 0.85f,
            drawRect.height * 0.145f);
        Rect artworkRect = new Rect(
            drawRect.x + drawRect.width * 0.075f,
            drawRect.y + drawRect.height * 0.215f,
            drawRect.width * 0.85f,
            drawRect.height * 0.64f);
        Rect footerRect = new Rect(
            drawRect.x + drawRect.width * 0.075f,
            drawRect.y + drawRect.height * 0.88f,
            drawRect.width * 0.85f,
            drawRect.height * 0.075f);

        DrawTintedRect(headerRect, new Color(0.025f, 0.035f, 0.05f, 0.98f));
        DrawTintedRect(artworkRect, Color.black);
        if (cardTexture != null)
        {
            Rect artworkUv = new Rect(0.12f, 0.18f, 0.76f, 0.57f);
            GUI.DrawTextureWithTexCoords(artworkRect, cardTexture, artworkUv, true);
        }
        else
        {
            GUI.Box(artworkRect, GetSkillName(skill));
        }
        DrawCardBorder(artworkRect, new Color(0.32f, 0.38f, 0.46f, 1f), 2f);
        DrawTintedRect(footerRect, new Color(0.025f, 0.035f, 0.05f, 0.98f));

        float accentHeight = Mathf.Max(2f, drawRect.height * 0.008f);
        Rect leftAccent = new Rect(
            headerRect.x,
            headerRect.yMax - accentHeight,
            headerRect.width * 0.48f,
            accentHeight);
        Rect rightAccent = new Rect(
            headerRect.x + headerRect.width * 0.52f,
            headerRect.yMax - accentHeight,
            headerRect.width * 0.48f,
            accentHeight);
        DrawTintedRect(leftAccent, new Color(1f, 0.18f, 0.04f, 1f));
        DrawTintedRect(rightAccent, new Color(0.04f, 0.68f, 1f, 1f));

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = Mathf.RoundToInt(Mathf.Clamp(drawRect.width * 0.1f, 18f, 34f))
        };
        Rect titleRect = new Rect(headerRect.x, headerRect.y, headerRect.width, headerRect.height - accentHeight);
        titleStyle.normal.textColor = new Color(0f, 0f, 0f, 0.86f);
        GUI.Label(new Rect(titleRect.x + 2f, titleRect.y + 2f, titleRect.width, titleRect.height), GetSkillName(skill), titleStyle);
        titleStyle.normal.textColor = Color.white;
        GUI.Label(titleRect, GetSkillName(skill), titleStyle);

        string selectedSlot = GetSelectedSlotText(skill);
        GUIStyle footerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(Mathf.Clamp(drawRect.width * 0.065f, 13f, 21f)),
            fontStyle = FontStyle.Bold
        };
        footerStyle.normal.textColor = isSelected ? borderColor : new Color(0.72f, 0.78f, 0.86f, 1f);
        string footerText = isSelected ? selectedSlot : $"冷却 {GetCooldown(skill):0.#}秒";
        GUI.Label(footerRect, footerText, footerStyle);

        if (Event.current.type == EventType.MouseUp && rect.Contains(Event.current.mousePosition))
        {
            SelectSkillCard(skill);
            Event.current.Use();
        }
    }

    private static void DrawTintedRect(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = previousColor;
    }

    private static void DrawCardBorder(Rect rect, Color color, float thickness)
    {
        DrawTintedRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        DrawTintedRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        DrawTintedRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        DrawTintedRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private Texture2D GetSkillCardTexture(SkillId skill)
    {
        switch (skill)
        {
            case SkillId.HornBlast:
                return hornBlastCard;
            case SkillId.GravityTrap:
                return gravityTrapCard;
            case SkillId.Blink:
                return blinkCard;
            case SkillId.FlameTrail:
                return flameTrailCard;
            case SkillId.TankShells:
                return tankShellsCard;
            default:
                return null;
        }
    }

    private Texture2D GetSkillIconTexture(SkillId skill, bool isQSlot)
    {
        switch (skill)
        {
            case SkillId.HornBlast:
                return isQSlot ? qHornBlastIcon : eHornBlastIcon;
            case SkillId.GravityTrap:
                return isQSlot ? qGravityTrapIcon : eGravityTrapIcon;
            case SkillId.Blink:
                return isQSlot ? qBlinkIcon : eBlinkIcon;
            case SkillId.FlameTrail:
                return isQSlot ? qFlameTrailIcon : eFlameTrailIcon;
            case SkillId.TankShells:
                return isQSlot ? qTankShellsIcon : eTankShellsIcon;
            default:
                return null;
        }
    }

    private string GetSelectedSlotText(SkillId skill)
    {
        if (qSkill == skill)
        {
            return "已装备到 Q";
        }
        if (eSkill == skill)
        {
            return "已装备到 E";
        }
        return "未装备";
    }

    private static string GetSkillName(SkillId skill)
    {
        switch (skill)
        {
            case SkillId.HornBlast:
                return "小喇叭";
            case SkillId.GravityTrap:
                return "重力陷阱";
            case SkillId.Blink:
                return "空间移动";
            case SkillId.FlameTrail:
                return "烈焰尾迹";
            case SkillId.TankShells:
                return "汽车？坦克！";
            default:
                return "未选择";
        }
    }
}

public sealed class GravityTrap : MonoBehaviour
{
    private readonly List<Transform> rings = new List<Transform>();
    private readonly List<Vector3> ringBaseScales = new List<Vector3>();
    private readonly List<Transform> orbitingShards = new List<Transform>();
    private readonly List<float> shardOrbitOffsets = new List<float>();
    private float lifetime;
    private float radius;
    private float pullSpeed;
    private float endTime;

    public static void Spawn(Vector3 position, float lifetime, float radius, float pullSpeed)
    {
        GameObject trapObject = new GameObject("SKILL_GravityTrap");
        trapObject.transform.position = position + Vector3.up * 0.06f;
        GravityTrap trap = trapObject.AddComponent<GravityTrap>();
        trap.lifetime = lifetime;
        trap.radius = radius;
        trap.pullSpeed = pullSpeed;
    }

    private void Start()
    {
        endTime = Time.time + lifetime;
        LegacySkillVfx.CreateGravityVortex(transform, radius, lifetime);
    }

    private void FixedUpdate()
    {
        if (Time.time >= endTime)
        {
            Destroy(gameObject);
            return;
        }

        NavMeshEnemyCarChaser[] enemies = FindObjectsOfType<NavMeshEnemyCarChaser>();
        foreach (NavMeshEnemyCarChaser enemy in enemies)
        {
            Rigidbody enemyBody = enemy.GetComponent<Rigidbody>();
            if (enemyBody == null)
            {
                continue;
            }

            Vector3 toTrap = transform.position - enemyBody.position;
            toTrap.y = 0f;
            if (toTrap.sqrMagnitude > radius * radius)
            {
                continue;
            }

            enemy.SlowFor(0.55f, 0.12f);
            enemy.MarkPlayerCredit(2.5f);
            enemyBody.MovePosition(Vector3.MoveTowards(
                enemyBody.position,
                transform.position,
                pullSpeed * Time.fixedDeltaTime));
        }

    }

    private void CreateRing(float diameter, float height)
    {
        GameObject ringObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ringObject.name = "GravityRing";
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = Vector3.up * (height * 0.5f);
        ringObject.transform.localScale = new Vector3(diameter, height, diameter);
        Collider ringCollider = ringObject.GetComponent<Collider>();
        if (ringCollider != null)
        {
            Destroy(ringCollider);
        }
        Renderer ringRenderer = ringObject.GetComponent<Renderer>();
        ringRenderer.material.color = new Color(0.5f, 0.08f, 1f, 0.75f);
        rings.Add(ringObject.transform);
        ringBaseScales.Add(ringObject.transform.localScale);
    }

    private void CreateGravityParticles()
    {
        ParticleSystem particles = SkillVisuals.CreateParticleSystem("GravityWisps", transform);
        ParticleSystem.MainModule main = particles.main;
        main.duration = lifetime;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.42f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.32f, 0.04f, 0.9f), new Color(0.88f, 0.28f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 90;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 34f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 4.8f;
        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.55f;
        noise.frequency = 0.7f;
        SkillVisuals.ConfigureRenderer(particles, 2);
        particles.Play();
    }

    private void CreateOrbitingShards()
    {
        for (int index = 0; index < 12; index++)
        {
            GameObject shardObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shardObject.name = "GravityDebris";
            shardObject.transform.SetParent(transform, false);
            shardObject.transform.localScale = new Vector3(0.12f, 0.32f, 0.12f);
            Collider shardCollider = shardObject.GetComponent<Collider>();
            if (shardCollider != null)
            {
                Destroy(shardCollider);
            }
            shardObject.GetComponent<Renderer>().material.color = new Color(0.62f, 0.12f, 1f, 1f);
            orbitingShards.Add(shardObject.transform);
            shardOrbitOffsets.Add(index * Mathf.PI * 2f / 12f);
        }
    }
}

public sealed class FlameTrailEmitter : MonoBehaviour
{
    private Transform owner;
    private float duration;
    private float spawnInterval;
    private float segmentRadius;
    private float segmentLifetime;
    private float endTime;
    private float nextSpawnTime;

    public static void Spawn(
        Transform playerTransform,
        float duration,
        float spawnInterval,
        float segmentRadius,
        float segmentLifetime)
    {
        if (playerTransform == null)
        {
            return;
        }

        GameObject emitterObject = new GameObject("SKILL_FlameTrailEmitter");
        FlameTrailEmitter emitter = emitterObject.AddComponent<FlameTrailEmitter>();
        emitter.owner = playerTransform;
        emitter.duration = duration;
        emitter.spawnInterval = spawnInterval;
        emitter.segmentRadius = segmentRadius;
        emitter.segmentLifetime = segmentLifetime;
    }

    private void Start()
    {
        if (!EnsureOwner())
        {
            Destroy(gameObject);
            return;
        }

        endTime = Time.time + duration;
        nextSpawnTime = Time.time;
    }

    private void Update()
    {
        if (!EnsureOwner() || Time.time >= endTime)
        {
            Destroy(gameObject);
            return;
        }
        Vector3 trailPosition = owner.position - owner.forward * 2.2f;
        if (Time.time < nextSpawnTime)
        {
            return;
        }

        FlameTrailSegment.Spawn(trailPosition, segmentRadius, segmentLifetime);
        nextSpawnTime = Time.time + spawnInterval;
    }

    private bool EnsureOwner()
    {
        if (owner != null)
        {
            return true;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        owner = player != null ? player.transform : null;
        if (owner != null && endTime <= Time.time)
        {
            endTime = Time.time + duration;
            nextSpawnTime = Time.time;
        }
        return owner != null;
    }
}

public sealed class FlameTrailSegment : MonoBehaviour
{
    private readonly HashSet<NavMeshEnemyCarChaser> triggeredEnemies = new HashSet<NavMeshEnemyCarChaser>();
    private float radius;
    private float lifetime;
    private float endTime;

    public static void Spawn(Vector3 position, float radius, float lifetime)
    {
        GameObject flameObject = new GameObject("SKILL_FlameTrail");
        flameObject.transform.position = position + Vector3.up * 0.18f;
        SphereCollider trigger = flameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = radius;
        FlameTrailSegment segment = flameObject.AddComponent<FlameTrailSegment>();
        segment.radius = radius;
        segment.lifetime = lifetime;
    }

    private void Start()
    {
        endTime = Time.time + lifetime;
        LegacySkillVfx.CreateFlameNodeVisual(transform, radius, lifetime);
    }

    private void Update()
    {
        if (Time.time >= endTime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        NavMeshEnemyCarChaser enemy = other.GetComponentInParent<NavMeshEnemyCarChaser>();
        if (enemy == null || !triggeredEnemies.Add(enemy))
        {
            return;
        }

        enemy.Explode(true);
    }

    private void CreateFlameDisc(float diameter, Color color)
    {
        GameObject discObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        discObject.name = "FlameTrailVisual";
        discObject.transform.SetParent(transform, false);
        discObject.transform.localPosition = Vector3.up * 0.035f;
        discObject.transform.localScale = new Vector3(diameter, 0.07f, diameter);
        Collider discCollider = discObject.GetComponent<Collider>();
        if (discCollider != null)
        {
            Destroy(discCollider);
        }
        Renderer discRenderer = discObject.GetComponent<Renderer>();
        discRenderer.material.color = color;
    }

    private void CreateFlameParticles()
    {
        ParticleSystem particles = SkillVisuals.CreateParticleSystem("FlameSparks", transform);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 4f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.42f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.16f, 0.01f), new Color(1f, 0.86f, 0.15f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 42f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.1f;
        SkillVisuals.ConfigureRenderer(particles, 3);
        particles.Play();
    }

    private void CreateSmokeParticles()
    {
        ParticleSystem particles = SkillVisuals.CreateParticleSystem("FlameSmoke", transform);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 4f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startColor = new Color(0.17f, 0.12f, 0.1f, 0.42f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 35;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 14f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1f;
        SkillVisuals.ConfigureRenderer(particles, 1);
        particles.Play();
    }
}

public sealed class TankShellProjectile : MonoBehaviour
{
    private Vector3 direction;
    private Transform owner;
    private float speed;
    private float lifetime;
    private float explosionRadius;
    private float endTime;
    private Material speedTrailMaterial;
    private bool exploded;

    public static void Spawn(
        Vector3 position,
        Vector3 direction,
        Transform owner,
        float speed,
        float lifetime,
        float explosionRadius)
    {
        GameObject shellObject = new GameObject("SKILL_RearMissile");
        shellObject.transform.position = position;
        shellObject.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        SphereCollider shellCollider = shellObject.AddComponent<SphereCollider>();
        shellCollider.isTrigger = true;
        shellCollider.radius = 0.5f;
        Rigidbody shellBody = shellObject.AddComponent<Rigidbody>();
        shellBody.useGravity = false;
        shellBody.isKinematic = true;
        CreateShellModel(shellObject.transform);
        SkillVisuals.CreateShellTrail(shellObject.transform);
        SkillVisuals.CreateShellSmokeTrail(shellObject.transform);
        SkillVisuals.CreatePointLight(shellObject.transform, new Color(1f, 0.32f, 0.02f), 4.5f, 1.8f);

        TankShellProjectile projectile = shellObject.AddComponent<TankShellProjectile>();
        projectile.direction = direction.normalized;
        projectile.owner = owner;
        projectile.speed = speed;
        projectile.lifetime = lifetime;
        projectile.explosionRadius = explosionRadius;
        projectile.CreateSpeedTrail();

        if (owner != null)
        {
            foreach (Collider ownerCollider in owner.GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(shellCollider, ownerCollider, true);
            }
        }
    }

    private static void CreateShellModel(Transform root)
    {
        GameObject bodyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bodyObject.name = "MissileBody";
        bodyObject.transform.SetParent(root, false);
        bodyObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        bodyObject.transform.localScale = new Vector3(0.46f, 0.95f, 0.46f);
        RemoveVisualCollider(bodyObject);
        Renderer bodyRenderer = bodyObject.GetComponent<Renderer>();
        bodyRenderer.material.color = new Color(0.18f, 0.2f, 0.23f, 1f);

        GameObject tipObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tipObject.name = "GlowingWarhead";
        tipObject.transform.SetParent(root, false);
        tipObject.transform.localPosition = Vector3.forward * 1.05f;
        tipObject.transform.localScale = new Vector3(0.48f, 0.48f, 0.72f);
        RemoveVisualCollider(tipObject);
        Renderer tipRenderer = tipObject.GetComponent<Renderer>();
        Material tipMaterial = tipRenderer.material;
        tipMaterial.color = new Color(1f, 0.28f, 0.02f, 1f);
        if (tipMaterial.HasProperty("_EmissionColor"))
        {
            tipMaterial.EnableKeyword("_EMISSION");
            tipMaterial.SetColor("_EmissionColor", new Color(1f, 0.12f, 0.01f) * 2.8f);
        }

        GameObject bandObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bandObject.name = "MissileBand";
        bandObject.transform.SetParent(root, false);
        bandObject.transform.localPosition = Vector3.forward * 0.15f;
        bandObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        bandObject.transform.localScale = new Vector3(0.52f, 0.12f, 0.52f);
        RemoveVisualCollider(bandObject);
        bandObject.GetComponent<Renderer>().material.color = new Color(0.82f, 0.56f, 0.08f, 1f);

        CreateFin(root, new Vector3(0.48f, 0f, -0.72f), new Vector3(0.18f, 0.58f, 0.5f));
        CreateFin(root, new Vector3(-0.48f, 0f, -0.72f), new Vector3(0.18f, 0.58f, 0.5f));
        CreateFin(root, new Vector3(0f, 0.48f, -0.72f), new Vector3(0.58f, 0.18f, 0.5f));
        CreateFin(root, new Vector3(0f, -0.48f, -0.72f), new Vector3(0.58f, 0.18f, 0.5f));

        GameObject nozzleObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        nozzleObject.name = "MissileNozzle";
        nozzleObject.transform.SetParent(root, false);
        nozzleObject.transform.localPosition = Vector3.back * 1.02f;
        nozzleObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        nozzleObject.transform.localScale = new Vector3(0.32f, 0.2f, 0.32f);
        RemoveVisualCollider(nozzleObject);
        nozzleObject.GetComponent<Renderer>().material.color = new Color(0.92f, 0.18f, 0.02f, 1f);
    }

    private static void CreateFin(Transform root, Vector3 localPosition, Vector3 localScale)
    {
        GameObject finObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        finObject.name = "MissileFin";
        finObject.transform.SetParent(root, false);
        finObject.transform.localPosition = localPosition;
        finObject.transform.localScale = localScale;
        RemoveVisualCollider(finObject);
        Renderer finRenderer = finObject.GetComponent<Renderer>();
        finRenderer.material.color = new Color(0.22f, 0.23f, 0.27f, 1f);
    }

    private static void RemoveVisualCollider(GameObject visualObject)
    {
        Collider visualCollider = visualObject.GetComponent<Collider>();
        if (visualCollider != null)
        {
            Destroy(visualCollider);
        }
    }

    private void Start()
    {
        endTime = Time.time + lifetime;
    }

    private void CreateSpeedTrail()
    {
        TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.45f;
        trail.minVertexDistance = 0.04f;
        trail.startWidth = 0.42f;
        trail.endWidth = 0.04f;
        trail.startColor = new Color(1f, 0.52f, 0.04f, 0.9f);
        trail.endColor = new Color(1f, 0.08f, 0.01f, 0f);
        speedTrailMaterial = SkillVisuals.CreateTintedParticleMaterial(new Color(1f, 0.35f, 0.02f, 0.82f));
        if (speedTrailMaterial != null)
        {
            trail.material = speedTrailMaterial;
        }
    }

    private void Update()
    {
        if (Time.time >= endTime)
        {
            ExplodeAtCurrentPosition();
            return;
        }

        float moveDistance = speed * Time.deltaTime;
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            0.42f,
            direction,
            moveDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        Collider closestCollider = null;
        float closestDistance = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            if (IsIgnoredCollider(hit.collider) || hit.distance >= closestDistance)
            {
                continue;
            }
            closestCollider = hit.collider;
            closestDistance = hit.distance;
        }
        if (closestCollider != null)
        {
            ExplodeAtCurrentPosition();
            return;
        }
        transform.position += direction * moveDistance;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsIgnoredCollider(other))
        {
            ExplodeAtCurrentPosition();
        }
    }

    private bool IsIgnoredCollider(Collider other)
    {
        if (other == null || other.isTrigger || other.transform.IsChildOf(transform))
        {
            return true;
        }
        if (owner == null)
        {
            return false;
        }
        return other.transform == owner
            || other.transform.IsChildOf(owner)
            || owner.IsChildOf(other.transform);
    }

    private void ExplodeAtCurrentPosition()
    {
        if (exploded)
        {
            return;
        }

        exploded = true;
        Vector3 explosionPosition = transform.position;
        LegacySkillVfx.SpawnCannonImpact(explosionPosition);
        SkillVisuals.SpawnBurst(
            explosionPosition + Vector3.up * 0.55f,
            new Color(1f, 0.15f, 0.01f),
            new Color(1f, 0.9f, 0.15f),
            48,
            2f,
            8f,
            0.2f,
            0.85f,
            0.75f);
        SkillShockwave.Spawn(explosionPosition, new Color(1f, 0.38f, 0.02f, 0.9f), 7f, 0.45f);
        EnemyExplosionEffect.Spawn(explosionPosition);
        SkillVisuals.ShakeCamera(0.14f, 0.18f);

        HashSet<NavMeshEnemyCarChaser> affectedEnemies = new HashSet<NavMeshEnemyCarChaser>();
        foreach (Collider hit in Physics.OverlapSphere(explosionPosition, explosionRadius))
        {
            NavMeshEnemyCarChaser enemy = hit.GetComponentInParent<NavMeshEnemyCarChaser>();
            if (enemy != null && affectedEnemies.Add(enemy))
            {
                enemy.Explode(true);
            }
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (speedTrailMaterial != null)
        {
            Destroy(speedTrailMaterial);
        }
    }
}

public static class SkillVisuals
{
    private const string ParticleMaterialResourcePath = "Effects/ExplosionParticle";

    private static Material particleMaterial;
    private static Texture2D radialTexture;

    public static ParticleSystem CreateParticleSystem(string name, Transform parent)
    {
        GameObject particleObject = new GameObject(name);
        particleObject.transform.SetParent(parent, false);
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    public static void ConfigureRenderer(ParticleSystem particles, int sortingOrder)
    {
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        Material material = GetParticleMaterial();
        if (material != null)
        {
            renderer.material = material;
        }
        renderer.sortingOrder = sortingOrder;
    }

    public static void SpawnBurst(Vector3 position, Color firstColor, Color secondColor, int particleCount, float minSpeed, float maxSpeed, float minSize, float maxSize, float lifetime)
    {
        GameObject effectObject = new GameObject("SKILL_BurstVfx");
        effectObject.transform.position = position;
        ParticleSystem particles = CreateParticleSystem("Burst", effectObject.transform);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.5f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new ParticleSystem.MinMaxGradient(firstColor, secondColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = particleCount + 6;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)particleCount) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.45f;
        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(firstColor, 0f),
                new GradientColorKey(secondColor, 0.5f),
                new GradientColorKey(secondColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.75f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;
        ConfigureRenderer(particles, 5);
        particles.Play();
        Object.Destroy(effectObject, lifetime + 0.2f);
    }

    public static void SpawnFlamePillar(Vector3 position)
    {
        GameObject effectObject = new GameObject("SKILL_FlameImpactPillar");
        effectObject.transform.position = position + Vector3.up * 0.15f;
        effectObject.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        ParticleSystem particles = CreateParticleSystem("FlamePillar", effectObject.transform);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.32f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.8f, 5.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.68f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.12f, 0.01f), new Color(1f, 0.9f, 0.15f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 42;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)36) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.8f;
        shape.angle = 18f;
        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.35f), 0f),
                new GradientColorKey(new Color(1f, 0.15f, 0.01f), 0.55f),
                new GradientColorKey(new Color(0.28f, 0.02f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.75f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;
        ConfigureRenderer(particles, 7);
        particles.Play();
        Object.Destroy(effectObject, 1f);
    }

    public static void SpawnDebrisBurst(Vector3 position, int particleCount)
    {
        GameObject effectObject = new GameObject("SKILL_ImpactDebris");
        effectObject.transform.position = position + Vector3.up * 0.55f;
        ParticleSystem particles = CreateParticleSystem("MetalDebris", effectObject.transform);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.25f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.24f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.32f, 0.34f, 0.38f), new Color(1f, 0.46f, 0.04f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = particleCount + 5;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)particleCount) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;
        ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
        ConfigureRenderer(particles, 8);
        particles.Play();
        Object.Destroy(effectObject, 1.1f);
    }

    public static void CreatePointLight(Transform parent, Color color, float range, float intensity)
    {
        GameObject lightObject = new GameObject("SkillLight");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = Vector3.up * 0.7f;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
    }

    public static void CreateShellTrail(Transform parent)
    {
        ParticleSystem particles = CreateParticleSystem("ShellTrail", parent);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 2.2f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.22f, 0.02f), new Color(1f, 0.86f, 0.12f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 48f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;
        ConfigureRenderer(particles, 4);
        particles.Play();
    }

    public static void CreateShellSmokeTrail(Transform parent)
    {
        ParticleSystem particles = CreateParticleSystem("ShellSmokeTrail", parent);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 2.2f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.38f);
        main.startColor = new Color(0.28f, 0.22f, 0.18f, 0.48f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 32;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 26f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;
        ConfigureRenderer(particles, 2);
        particles.Play();
    }

    public static Material GetParticleMaterial()
    {
        if (particleMaterial != null)
        {
            return particleMaterial;
        }

        Material template = Resources.Load<Material>(ParticleMaterialResourcePath);
        if (template == null)
        {
            return null;
        }

        particleMaterial = new Material(template);
        particleMaterial.mainTexture = GetRadialTexture();
        return particleMaterial;
    }

    public static Material CreateTintedParticleMaterial(Color color)
    {
        Material sourceMaterial = GetParticleMaterial();
        if (sourceMaterial == null)
        {
            return null;
        }

        Material material = new Material(sourceMaterial);
        material.color = color;
        return material;
    }

    public static void ShakeCamera(float duration, float magnitude)
    {
        SimpleSpeedCameraFollow cameraFollow = Camera.main != null
            ? Camera.main.GetComponent<SimpleSpeedCameraFollow>()
            : Object.FindObjectOfType<SimpleSpeedCameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.Shake(duration, magnitude);
        }
    }

    private static Texture2D GetRadialTexture()
    {
        if (radialTexture != null)
        {
            return radialTexture;
        }

        radialTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        radialTexture.wrapMode = TextureWrapMode.Clamp;
        radialTexture.filterMode = FilterMode.Bilinear;
        for (int yIndex = 0; yIndex < 64; yIndex++)
        {
            for (int xIndex = 0; xIndex < 64; xIndex++)
            {
                float distance = Vector2.Distance(new Vector2(xIndex, yIndex), new Vector2(31.5f, 31.5f)) / 31.5f;
                radialTexture.SetPixel(xIndex, yIndex, new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, distance)));
            }
        }
        radialTexture.Apply();
        return radialTexture;
    }
}
