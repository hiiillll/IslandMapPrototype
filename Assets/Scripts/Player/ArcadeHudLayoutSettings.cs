using UnityEngine;

[System.Serializable]
public sealed class ArcadeHudElementLayout
{
    [Tooltip("Offset from this element's screen anchor, in reference pixels.")]
    public Vector2 offset;

    [Tooltip("Element size in reference pixels.")]
    public Vector2 size;
}

[CreateAssetMenu(fileName = "HudLayoutSettings", menuName = "Island Map/Arcade HUD Layout")]
public sealed class ArcadeHudLayoutSettings : ScriptableObject
{
    [Header("Reference resolution: 1920 x 1080")]
    public ArcadeHudElementLayout healthLayout = new ArcadeHudElementLayout { offset = Vector2.zero, size = new Vector2(455f, 94f) };
    public ArcadeHudElementLayout timerLayout = new ArcadeHudElementLayout { offset = Vector2.zero, size = new Vector2(350f, 148f) };
    public ArcadeHudElementLayout killLayout = new ArcadeHudElementLayout { offset = new Vector2(116f, 0f), size = new Vector2(290f, 80f) };
    public ArcadeHudElementLayout pauseLayout = new ArcadeHudElementLayout { offset = Vector2.zero, size = new Vector2(116f, 72f) };
    public ArcadeHudElementLayout experienceLayout = new ArcadeHudElementLayout { offset = new Vector2(0f, 10f), size = new Vector2(760f, 66f) };
    public Vector2 skillCardsOffset = new Vector2(22f, 18f);
    public Vector2 qSkillCardOffset = Vector2.zero;
    public Vector2 qSkillCardSize = new Vector2(212f, 230f);
    public Vector2 eSkillCardOffset = Vector2.zero;
    public Vector2 eSkillCardSize = new Vector2(212f, 230f);
    [Min(0f)] public float skillCardSpacing = 12f;
}
