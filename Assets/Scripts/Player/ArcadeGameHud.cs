using UnityEngine;

public sealed class ArcadeGameHud : MonoBehaviour
{
    private Rigidbody playerBody;
    private SimplePlayerHealth health;
    private PlayerProgression progression;
    private PlayerSkillSystem skillSystem;
    private SurvivalGameController survivalController;
    private ArcadeHudLayoutSettings layoutSettings;

    [Header("HUD Layout (reference: 1920 x 1080)")]
    [SerializeField] private ArcadeHudElementLayout healthLayout = new ArcadeHudElementLayout { offset = Vector2.zero, size = new Vector2(455f, 94f) };
    [SerializeField] private ArcadeHudElementLayout timerLayout = new ArcadeHudElementLayout { offset = Vector2.zero, size = new Vector2(350f, 148f) };
    [SerializeField] private ArcadeHudElementLayout killLayout = new ArcadeHudElementLayout { offset = new Vector2(116f, 0f), size = new Vector2(290f, 80f) };
    [SerializeField] private ArcadeHudElementLayout pauseLayout = new ArcadeHudElementLayout { offset = Vector2.zero, size = new Vector2(116f, 72f) };
    [SerializeField] private ArcadeHudElementLayout experienceLayout = new ArcadeHudElementLayout { offset = new Vector2(0f, 10f), size = new Vector2(760f, 66f) };
    [SerializeField] private Vector2 skillCardsOffset = new Vector2(22f, 18f);
    [SerializeField] private Vector2 qSkillCardOffset = Vector2.zero;
    [SerializeField] private Vector2 qSkillCardSize = new Vector2(212f, 230f);
    [SerializeField] private Vector2 eSkillCardOffset = Vector2.zero;
    [SerializeField] private Vector2 eSkillCardSize = new Vector2(212f, 230f);
    [SerializeField, Min(0f)] private float skillCardSpacing = 12f;
    [SerializeField] private bool basicHudOnly;

    private Texture2D redPanelTexture;
    private Texture2D orangePanelTexture;
    private Texture2D bluePanelTexture;
    private Texture2D speedometerTexture;
    private Texture2D heartTexture;
    private Texture2D solidTexture;
    private Texture2D healthFrameTexture;
    private Texture2D timerFrameTexture;
    private Texture2D killFrameTexture;
    private Texture2D pauseButtonTexture;
    private Texture2D experienceFrameTexture;
    private GUIStyle largeNumberStyle;
    private GUIStyle headingStyle;
    private GUIStyle bodyStyle;
    private GUIStyle smallStyle;
    private GUIStyle italicNumberStyle;
    private GUIStyle italicHeadingStyle;
    private bool isPaused;

    public void ConfigureBasicHudOnly()
    {
        basicHudOnly = true;
        isPaused = false;
        Time.timeScale = 1f;
    }

    private void Awake()
    {
        playerBody = GetComponent<Rigidbody>();
        health = GetComponent<SimplePlayerHealth>();
        progression = GetComponent<PlayerProgression>();
        skillSystem = GetComponent<PlayerSkillSystem>();
        survivalController = FindObjectOfType<SurvivalGameController>();
        ApplySavedLayoutSettings();
        redPanelTexture = CreateAngularPanel(
            256,
            96,
            new Color(0.018f, 0.025f, 0.04f, 0.94f),
            new Color(0.95f, 0.14f, 0.04f, 1f));
        orangePanelTexture = CreateAngularPanel(
            256,
            96,
            new Color(0.018f, 0.025f, 0.04f, 0.95f),
            new Color(1f, 0.28f, 0.04f, 1f));
        bluePanelTexture = CreateAngularPanel(
            256,
            96,
            new Color(0.018f, 0.03f, 0.055f, 0.94f),
            new Color(0.05f, 0.55f, 1f, 1f));
        speedometerTexture = CreateSpeedometerTexture();
        heartTexture = LoadHudTexture("heart") ?? CreateHeartTexture();
        solidTexture = CreateSolidTexture(Color.white);
        healthFrameTexture = LoadHudTexture("health_frame") ?? redPanelTexture;
        timerFrameTexture = LoadHudTexture("timer_frame") ?? orangePanelTexture;
        killFrameTexture = LoadHudTexture("kill_frame") ?? redPanelTexture;
        pauseButtonTexture = LoadHudTexture("pause_button") ?? redPanelTexture;
        experienceFrameTexture = LoadHudTexture("xp_frame") ?? bluePanelTexture;
    }

    private void OnGUI()
    {
        if (skillSystem == null)
        {
            skillSystem = GetComponent<PlayerSkillSystem>();
        }
        if (health == null)
        {
            health = GetComponent<SimplePlayerHealth>();
        }
        if (progression == null)
        {
            progression = GetComponent<PlayerProgression>();
        }
        if (survivalController == null)
        {
            survivalController = FindObjectOfType<SurvivalGameController>();
        }
        if (progression == null || health == null || health.CurrentHealth <= 0
            || (survivalController != null && survivalController.IsFinished))
        {
            return;
        }
        if (!basicHudOnly && (skillSystem == null || !skillSystem.IsGameplayActive))
        {
            return;
        }

        float scale = Mathf.Clamp(Screen.height / 1080f, 0.62f, 1.22f);
        EnsureStyles();
        ApplyStyleScale(scale);
        GUI.depth = -900;
        DrawHealthPanel(scale);
        DrawTimerPanel(scale);
        DrawKillPanel(scale);
        DrawPauseButton(scale);
        if (!basicHudOnly)
        {
            DrawExperienceBar(scale);
            DrawSkillCards(scale);
        }
        if (isPaused && !GameModeSession.IsEndless)
        {
            DrawStoryPauseMenu(scale);
        }
    }

    private void ApplySavedLayoutSettings()
    {
        layoutSettings = Resources.Load<ArcadeHudLayoutSettings>("UI/HudLayoutSettings");
        if (layoutSettings == null) return;

        healthLayout = layoutSettings.healthLayout ?? healthLayout;
        timerLayout = layoutSettings.timerLayout ?? timerLayout;
        killLayout = layoutSettings.killLayout ?? killLayout;
        pauseLayout = layoutSettings.pauseLayout ?? pauseLayout;
        experienceLayout = layoutSettings.experienceLayout ?? experienceLayout;
        skillCardsOffset = layoutSettings.skillCardsOffset;
        qSkillCardOffset = layoutSettings.qSkillCardOffset;
        qSkillCardSize = layoutSettings.qSkillCardSize;
        eSkillCardOffset = layoutSettings.eSkillCardOffset;
        eSkillCardSize = layoutSettings.eSkillCardSize;
        skillCardSpacing = layoutSettings.skillCardSpacing;
    }

    private void DrawHealthPanel(float scale)
    {
        Rect panel = new Rect(
            healthLayout.offset.x * scale,
            healthLayout.offset.y * scale,
            healthLayout.size.x * scale,
            healthLayout.size.y * scale);
        GUI.DrawTexture(panel, healthFrameTexture, ScaleMode.StretchToFill, true);
        float heartSize = 36f * scale;
        float heartY = 27f * scale;
        for (int heartIndex = 0; heartIndex < health.MaxHealth; heartIndex++)
        {
            Rect heartRect = new Rect(panel.x + (45f + heartIndex * 45f) * scale, panel.y + heartY, heartSize, heartSize);
            GUI.color = heartIndex < health.CurrentHealth
                ? Color.white
                : new Color(0.18f, 0.2f, 0.24f, 0.8f);
            GUI.DrawTexture(heartRect, heartTexture, ScaleMode.StretchToFill, true);
        }
        GUI.color = Color.white;
        GUIStyle healthValueStyle = new GUIStyle(italicNumberStyle)
        {
            fontSize = Mathf.RoundToInt(42f * scale)
        };
        GUI.Label(
            new Rect(panel.x + 205f * scale, panel.y + 20f * scale, 120f * scale, 46f * scale),
            $"{health.CurrentHealth}/{health.MaxHealth}",
            healthValueStyle);
    }

    private void DrawTimerPanel(float scale)
    {
        float panelWidth = timerLayout.size.x * scale;
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f + timerLayout.offset.x * scale,
            timerLayout.offset.y * scale,
            panelWidth,
            timerLayout.size.y * scale);
        GUI.DrawTexture(panel, timerFrameTexture, ScaleMode.StretchToFill, true);
        bool endlessMode = GameModeSession.IsEndless && EndlessModeController.Instance != null;
        int displaySeconds = endlessMode
            ? Mathf.FloorToInt(EndlessModeController.Instance.ElapsedTime)
            : survivalController != null
                ? Mathf.CeilToInt(survivalController.RemainingTime)
                : 0;
        GUIStyle timerTitleStyle = new GUIStyle(italicHeadingStyle)
        {
            fontSize = Mathf.RoundToInt(20f * scale)
        };
        GUIStyle timerValueStyle = new GUIStyle(italicNumberStyle)
        {
            fontSize = Mathf.RoundToInt(44f * scale)
        };
        GUI.Label(
            new Rect(panel.x, panel.y + 32f * scale, panel.width, 24f * scale),
            "存活时间",
            timerTitleStyle);
        GUI.Label(
            new Rect(panel.x, panel.y + 62f * scale, panel.width, 52f * scale),
            $"{displaySeconds / 60:00}:{displaySeconds % 60:00}",
            timerValueStyle);
    }

    private void DrawKillPanel(float scale)
    {
        float panelWidth = killLayout.size.x * scale;
        Rect panel = new Rect(
            Screen.width - (killLayout.offset.x + killLayout.size.x) * scale,
            killLayout.offset.y * scale,
            panelWidth,
            killLayout.size.y * scale);
        GUI.DrawTexture(panel, killFrameTexture, ScaleMode.StretchToFill, true);
        GUIStyle killStyle = new GUIStyle(italicHeadingStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(30f * scale)
        };
        killStyle.normal.textColor = new Color(1f, 0.2f, 0.08f);
        GUI.Label(
            new Rect(panel.x + 16f * scale, panel.y + 10f * scale, panel.width - 32f * scale, 56f * scale),
            $"击毁  {progression.DestroyedEnemies}",
            killStyle);
    }

    private void DrawPauseButton(float scale)
    {
        float pauseWidth = pauseLayout.size.x * scale;
        Rect pauseRect = new Rect(
            Screen.width - (pauseLayout.offset.x + pauseLayout.size.x) * scale,
            pauseLayout.offset.y * scale,
            pauseWidth,
            pauseLayout.size.y * scale);
        GUI.DrawTexture(pauseRect, pauseButtonTexture, ScaleMode.StretchToFill, true);
        if (GUI.Button(pauseRect, GUIContent.none, GUIStyle.none))
        {
            if (GameModeSession.IsEndless && EndlessModeController.Instance != null)
            {
                EndlessModeController.Instance.TogglePause();
                return;
            }
            isPaused = !isPaused;
            Time.timeScale = isPaused ? 0f : 1f;
            AudioListener.pause = isPaused;
        }
    }

    private void DrawStoryPauseMenu(float scale)
    {
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), solidTexture, ScaleMode.StretchToFill);
        GUI.color = previousColor;

        float panelWidth = Mathf.Min(Screen.width - 40f, 520f * scale);
        float panelHeight = Mathf.Min(Screen.height - 40f, 330f * scale);
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);
        GUI.Box(panel, GUIContent.none);

        GUIStyle pauseTitleStyle = new GUIStyle(headingStyle)
        {
            fontSize = Mathf.RoundToInt(34f * scale)
        };
        GUIStyle pauseMenuButtonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(22f * scale),
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(panel.x + 30f, panel.y + 28f * scale, panel.width - 60f, 55f * scale),
            "游戏暂停", pauseTitleStyle);

        float buttonWidth = panel.width - 170f * scale;
        float buttonHeight = 58f * scale;
        float buttonX = panel.x + (panel.width - buttonWidth) * 0.5f;
        if (GUI.Button(new Rect(buttonX, panel.y + 112f * scale, buttonWidth, buttonHeight),
            "继续游戏", pauseMenuButtonStyle))
        {
            isPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
        if (GUI.Button(new Rect(buttonX, panel.y + 200f * scale, buttonWidth, buttonHeight),
            "返回主页面", pauseMenuButtonStyle))
        {
            isPaused = false;
            GameModeSession.ReturnToMainMenu();
        }
    }

    private void DrawSpeedometer(float scale)
    {
        float width = 300f * scale;
        float height = 188f * scale;
        Rect panel = new Rect(14f * scale, Screen.height - height - 8f * scale, width, height);
        GUI.DrawTexture(panel, speedometerTexture, ScaleMode.StretchToFill, true);

        float planarSpeed = playerBody != null
            ? new Vector2(playerBody.velocity.x, playerBody.velocity.z).magnitude
            : 0f;
        int kilometresPerHour = Mathf.RoundToInt(planarSpeed * 3.6f);
        GUI.Label(
            new Rect(panel.x, panel.y + 72f * scale, panel.width, 32f * scale),
            "速度",
            headingStyle);
        GUI.Label(
            new Rect(panel.x, panel.y + 94f * scale, panel.width, 78f * scale),
            kilometresPerHour.ToString("00"),
            largeNumberStyle);
    }

    private void DrawExperienceBar(float scale)
    {
        float panelWidth = Mathf.Min(experienceLayout.size.x * scale, Screen.width * 0.9f);
        float panelHeight = experienceLayout.size.y * scale;
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f + experienceLayout.offset.x * scale,
            Screen.height - panelHeight - experienceLayout.offset.y * scale,
            panelWidth,
            panelHeight);
        GUI.DrawTexture(panel, experienceFrameTexture, ScaleMode.StretchToFill, true);

        GUI.Label(
            new Rect(panel.x + 20f * scale, panel.y + 10f * scale, 92f * scale, 38f * scale),
            $"Lv. {progression.Level}",
            headingStyle);
        float barX = panel.x + 122f * scale;
        float barY = panel.y + 24f * scale;
        float barWidth = panel.width - 288f * scale;
        float barHeight = 16f * scale;
        DrawSolidRect(new Rect(barX, barY, barWidth, barHeight), new Color(0.015f, 0.025f, 0.045f, 0.95f));

        float pulse = progression.PickupPulse;
        Color fillColor = Color.Lerp(
            new Color(0.02f, 0.6f, 1f),
            new Color(0.25f, 1f, 1f),
            pulse);
        DrawSolidRect(
            new Rect(barX + 3f * scale, barY + 3f * scale,
                Mathf.Max(0f, (barWidth - 6f * scale) * progression.ExperienceProgress),
                barHeight - 6f * scale),
            fillColor);
        GUI.Label(
            new Rect(panel.x + panel.width - 150f * scale, panel.y + 12f * scale, 130f * scale, 34f * scale),
            $"{progression.CurrentExperience} / {progression.ExperienceToNextLevel}",
            smallStyle);
    }

    private void DrawSkillCards(float scale)
    {
        float spacing = skillCardSpacing * scale;
        Vector2 qSize = Vector2.Max(Vector2.one, qSkillCardSize) * scale;
        Vector2 eSize = Vector2.Max(Vector2.one, eSkillCardSize) * scale;
        Vector2 qOffset = qSkillCardOffset * scale;
        Vector2 eOffset = eSkillCardOffset * scale;
        float eBaseX = Screen.width - (skillCardsOffset.x * scale + eSize.x);
        float qBaseX = eBaseX - spacing - qSize.x;
        Rect eRect = new Rect(
            eBaseX + eOffset.x,
            Screen.height - (skillCardsOffset.y * scale + eSize.y) + eOffset.y,
            eSize.x,
            eSize.y);
        Rect qRect = new Rect(
            qBaseX + qOffset.x,
            Screen.height - (skillCardsOffset.y * scale + qSize.y) + qOffset.y,
            qSize.x,
            qSize.y);
        DrawSkillCard(
            qRect,
            skillSystem.QSkillTexture,
            skillSystem.QCooldownRemaining,
            skillSystem.QCooldownDuration,
            new Color(1f, 0.2f, 0.06f),
            scale);
        DrawSkillCard(
            eRect,
            skillSystem.ESkillTexture,
            skillSystem.ECooldownRemaining,
            skillSystem.ECooldownDuration,
            new Color(0.05f, 0.65f, 1f),
            scale);
    }

    private void DrawSkillCard(
        Rect rect,
        Texture2D skillTexture,
        float cooldownRemaining,
        float cooldownDuration,
        Color accentColor,
        float scale)
    {
        if (skillTexture != null)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(rect, skillTexture, ScaleMode.ScaleToFit, true);
            GUI.color = Color.white;
        }

        float readyProgress = cooldownDuration > 0f
            ? 1f - Mathf.Clamp01(cooldownRemaining / cooldownDuration)
            : 1f;
        float cooldownBarHeight = 6f * scale;
        float cooldownBarWidth = rect.width * 0.72f;
        float cooldownBarX = rect.center.x - cooldownBarWidth * 0.5f;
        float cooldownBarY = rect.yMax - 20f * scale;
        DrawSolidRect(new Rect(cooldownBarX, cooldownBarY, cooldownBarWidth, cooldownBarHeight),
            new Color(0.015f, 0.02f, 0.035f, 0.96f));
        DrawSolidRect(new Rect(cooldownBarX, cooldownBarY, cooldownBarWidth * readyProgress, cooldownBarHeight), accentColor);
        if (cooldownRemaining > 0f)
        {
            DrawCooldownLabel(rect, $"{cooldownRemaining:0.0}s", scale);
        }
    }

    private void DrawCooldownLabel(Rect rect, string text, float scale)
    {
        GUIStyle countdownStyle = new GUIStyle(largeNumberStyle)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(34f * scale, 18f, 36f))
        };
        GUIStyle outlineStyle = new GUIStyle(countdownStyle);
        outlineStyle.normal.textColor = new Color(0.02f, 0.02f, 0.03f, 0.9f);
        countdownStyle.normal.textColor = Color.white;

        float outline = Mathf.Max(1f, 1.5f * scale);
        GUI.Label(new Rect(rect.x - outline, rect.y, rect.width, rect.height), text, outlineStyle);
        GUI.Label(new Rect(rect.x + outline, rect.y, rect.width, rect.height), text, outlineStyle);
        GUI.Label(new Rect(rect.x, rect.y - outline, rect.width, rect.height), text, outlineStyle);
        GUI.Label(new Rect(rect.x, rect.y + outline, rect.width, rect.height), text, outlineStyle);
        GUI.Label(rect, text, countdownStyle);
    }

    private void DrawSolidRect(Rect rect, Color color)
    {
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return;
        }
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, solidTexture, ScaleMode.StretchToFill, true);
        GUI.color = previousColor;
    }

    private void EnsureStyles()
    {
        if (largeNumberStyle != null)
        {
            return;
        }

        largeNumberStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 54
        };
        largeNumberStyle.normal.textColor = Color.white;

        italicNumberStyle = new GUIStyle(largeNumberStyle)
        {
            fontStyle = FontStyle.BoldAndItalic
        };

        headingStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 25
        };
        headingStyle.normal.textColor = Color.white;

        italicHeadingStyle = new GUIStyle(headingStyle)
        {
            fontStyle = FontStyle.BoldAndItalic
        };

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Bold,
            fontSize = 22
        };
        bodyStyle.normal.textColor = Color.white;

        smallStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 18
        };
        smallStyle.normal.textColor = Color.white;
    }

    private void ApplyStyleScale(float scale)
    {
        largeNumberStyle.fontSize = Mathf.RoundToInt(54f * scale);
        headingStyle.fontSize = Mathf.RoundToInt(25f * scale);
        bodyStyle.fontSize = Mathf.RoundToInt(22f * scale);
        smallStyle.fontSize = Mathf.RoundToInt(18f * scale);
        italicNumberStyle.fontSize = Mathf.RoundToInt(54f * scale);
        italicHeadingStyle.fontSize = Mathf.RoundToInt(25f * scale);
    }

    private static Texture2D CreateAngularPanel(int width, int height, Color fill, Color border)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "HUD_AngularPanel",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[width * height];
        const int cut = 18;
        const int borderWidth = 4;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside = x + y >= cut
                    && (width - 1 - x) + y >= cut
                    && x + (height - 1 - y) >= cut
                    && (width - 1 - x) + (height - 1 - y) >= cut;
                if (!inside)
                {
                    pixels[y * width + x] = Color.clear;
                    continue;
                }

                bool inner = x >= borderWidth && x < width - borderWidth
                    && y >= borderWidth && y < height - borderWidth
                    && x + y >= cut + borderWidth
                    && (width - 1 - x) + y >= cut + borderWidth
                    && x + (height - 1 - y) >= cut + borderWidth
                    && (width - 1 - x) + (height - 1 - y) >= cut + borderWidth;
                if (!inner)
                {
                    pixels[y * width + x] = border;
                    continue;
                }

                float carbonPattern = ((x / 8 + y / 8) & 1) == 0 ? 0.02f : -0.01f;
                pixels[y * width + x] = new Color(
                    Mathf.Clamp01(fill.r + carbonPattern),
                    Mathf.Clamp01(fill.g + carbonPattern),
                    Mathf.Clamp01(fill.b + carbonPattern),
                    fill.a);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateSpeedometerTexture()
    {
        const int width = 512;
        const int height = 320;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "HUD_Speedometer",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[width * height];
        Vector2 center = new Vector2(width * 0.5f, 34f);
        const float outerRadius = 235f;
        const float innerArcRadius = 194f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 offset = new Vector2(x, y) - center;
                float radius = offset.magnitude;
                if (offset.y < 0f || radius > outerRadius)
                {
                    pixels[y * width + x] = Color.clear;
                    continue;
                }

                float angle = Mathf.Atan2(offset.y, offset.x);
                float progress = Mathf.Clamp01(angle / Mathf.PI);
                Color color = ((x / 9 + y / 9) & 1) == 0
                    ? new Color(0.025f, 0.035f, 0.055f, 0.97f)
                    : new Color(0.014f, 0.022f, 0.04f, 0.97f);
                if (radius >= outerRadius - 5f)
                {
                    color = Color.Lerp(
                        new Color(1f, 0.2f, 0.03f),
                        new Color(0.05f, 0.75f, 1f),
                        progress);
                }
                else if (radius >= innerArcRadius && radius <= innerArcRadius + 22f)
                {
                    color = Color.Lerp(
                        new Color(1f, 0.2f, 0.03f),
                        new Color(0.05f, 0.75f, 1f),
                        progress);
                }
                pixels[y * width + x] = color;
            }
        }

        texture.SetPixels(pixels);
        for (int tickIndex = 0; tickIndex <= 12; tickIndex++)
        {
            float angle = tickIndex * Mathf.PI / 12f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            DrawTextureLine(
                texture,
                center + direction * 166f,
                center + direction * (tickIndex % 3 == 0 ? 190f : 181f),
                Color.white,
                3);
        }
        texture.Apply();
        return texture;
    }

    private static void DrawTextureLine(
        Texture2D texture,
        Vector2 start,
        Vector2 end,
        Color color,
        int thickness)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(start, end));
        for (int step = 0; step <= steps; step++)
        {
            Vector2 point = Vector2.Lerp(start, end, steps > 0 ? (float)step / steps : 0f);
            int centerX = Mathf.RoundToInt(point.x);
            int centerY = Mathf.RoundToInt(point.y);
            for (int offsetY = -thickness; offsetY <= thickness; offsetY++)
            {
                for (int offsetX = -thickness; offsetX <= thickness; offsetX++)
                {
                    int pixelX = centerX + offsetX;
                    int pixelY = centerY + offsetY;
                    if (pixelX >= 0 && pixelX < texture.width && pixelY >= 0 && pixelY < texture.height)
                    {
                        texture.SetPixel(pixelX, pixelY, color);
                    }
                }
            }
        }
    }

    private static Texture2D CreateHeartTexture()
    {
        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "HUD_Heart",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalizedX = (x / (size - 1f)) * 2f - 1f;
                float normalizedY = (y / (size - 1f)) * 2f - 0.9f;
                float expression = Mathf.Pow(normalizedX * normalizedX + normalizedY * normalizedY - 1f, 3f)
                    - normalizedX * normalizedX * normalizedY * normalizedY * normalizedY;
                if (expression > 0f)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }

                float highlight = Mathf.Clamp01((normalizedY + 1f) * 0.35f);
                pixels[y * size + x] = Color.Lerp(
                    new Color(0.82f, 0.015f, 0.03f, 1f),
                    new Color(1f, 0.22f, 0.18f, 1f),
                    highlight);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "HUD_Solid",
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private static Texture2D LoadHudTexture(string assetName)
    {
        return Resources.Load<Texture2D>("UI/HUD/" + assetName);
    }

    private void OnDestroy()
    {
        Destroy(redPanelTexture);
        Destroy(orangePanelTexture);
        Destroy(bluePanelTexture);
        Destroy(speedometerTexture);
        Destroy(solidTexture);
    }
}
