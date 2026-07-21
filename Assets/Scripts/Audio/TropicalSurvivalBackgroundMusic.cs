using UnityEngine;

/// <summary>
/// Plays the selected background-music loop and fades it with the gameplay state.
/// </summary>
public sealed class TropicalSurvivalBackgroundMusic : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float gameplayVolume = 0.24f;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.8f;

    private AudioSource source;
    private PlayerSkillSystem skillSystem;
    private BoatChaseController boatController;
    private SimplePlayerHealth health;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateBackgroundMusic()
    {
        if (FindObjectOfType<TropicalSurvivalBackgroundMusic>() != null)
        {
            return;
        }

        AudioClip clip = Resources.Load<AudioClip>("Audio/BGM_DownloadMusic");
        if (clip == null)
        {
            Debug.LogWarning("Background music clip was not found at Resources/Audio/BGM_DownloadMusic.");
            return;
        }

        GameObject musicObject = new GameObject("BGM_DownloadMusic");
        DontDestroyOnLoad(musicObject);
        TropicalSurvivalBackgroundMusic music = musicObject.AddComponent<TropicalSurvivalBackgroundMusic>();
        music.Initialize(clip);
    }

    private void Initialize(AudioClip clip)
    {
        source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.volume = 0f;
        source.spatialBlend = 0f;
        source.playOnAwake = false;
    }

    private void Update()
    {
        RefreshGameplayReferences();

        bool gameplayActive = (skillSystem != null && skillSystem.IsGameplayActive)
            || boatController != null;
        bool shouldPlay = gameplayActive
            && health != null && health.CurrentHealth > 0 && Time.timeScale > 0.001f;
        float targetVolume = shouldPlay ? gameplayVolume : 0f;
        float volumeStep = gameplayVolume / fadeDuration * Time.unscaledDeltaTime;

        if (shouldPlay && !source.isPlaying)
        {
            source.Play();
        }

        source.volume = Mathf.MoveTowards(source.volume, targetVolume, volumeStep);
        if (!shouldPlay && source.isPlaying && source.volume <= 0.0001f)
        {
            source.Pause();
        }
    }

    private void RefreshGameplayReferences()
    {
        if (skillSystem == null)
        {
            skillSystem = FindObjectOfType<PlayerSkillSystem>();
        }
        if (boatController == null)
        {
            boatController = FindObjectOfType<BoatChaseController>();
        }
        if (health == null)
        {
            health = FindObjectOfType<SimplePlayerHealth>();
        }
    }
}
