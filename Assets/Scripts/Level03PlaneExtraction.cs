using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public sealed class Level03PlaneExtraction : MonoBehaviour
{
    [SerializeField] private Level03TreasureObjective treasureObjective;
    [SerializeField] private Transform planeRoot;
    [SerializeField, Min(1f)] private float interactionRadius = 32f;

    [Header("Cinematic")]
    [SerializeField] private string cinematicResourcePath = "Cinematics/Level03CompletionCG";
    [SerializeField, Min(0f)] private float returnToMenuDelay = 2.25f;

    private SimpleAutoDriveController playerController;
    private VideoPlayer videoPlayer;
    private AudioSource videoAudio;
    private RenderTexture cinematicTexture;
    private bool evacuationComplete;
    private bool cinematicActive;
    private bool cinematicFinished;
    private bool levelTransitionStarted;
    private GUIStyle headingStyle;
    private GUIStyle promptStyle;

    public event Action EvacuationCompleted;

    public Level03TreasureObjective TreasureObjective => treasureObjective;
    public Transform PlaneRoot => planeRoot;
    public float InteractionRadius => interactionRadius;
    public bool IsUnlocked => treasureObjective != null && treasureObjective.IsComplete;
    public bool IsEvacuationComplete => evacuationComplete;

    private void Awake()
    {
        if (treasureObjective == null)
        {
            treasureObjective = FindObjectOfType<Level03TreasureObjective>();
        }

        RefreshPositionFromPlane();
        ResolvePlayer();
    }

    private void Update()
    {
        if (evacuationComplete)
        {
            return;
        }

        ResolvePlayer();
        if (playerController != null &&
            CanEvacuateAt(playerController.transform.position) &&
            Input.GetKeyDown(KeyCode.F))
        {
            BeginEvacuation();
        }
    }

    public void Configure(
        Level03TreasureObjective objective,
        Transform configuredPlaneRoot,
        float configuredInteractionRadius = 32f)
    {
        treasureObjective = objective;
        planeRoot = configuredPlaneRoot;
        interactionRadius = Mathf.Max(1f, configuredInteractionRadius);
        RefreshPositionFromPlane();
    }

    public bool IsWithinInteractionRange(Vector3 worldPosition)
    {
        Vector2 playerPoint = new Vector2(worldPosition.x, worldPosition.z);
        Vector2 extractionPoint = new Vector2(transform.position.x, transform.position.z);
        return Vector2.SqrMagnitude(playerPoint - extractionPoint) <=
            interactionRadius * interactionRadius;
    }

    public bool CanEvacuateAt(Vector3 worldPosition)
    {
        return !evacuationComplete && IsUnlocked && IsWithinInteractionRange(worldPosition);
    }

    private void ResolvePlayer()
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<SimpleAutoDriveController>();
        }
    }

    private void RefreshPositionFromPlane()
    {
        if (planeRoot == null)
        {
            return;
        }

        Renderer[] renderers = planeRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            transform.position = planeRoot.position;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        transform.position = new Vector3(bounds.center.x, planeRoot.position.y, bounds.center.z);
    }

    private void BeginEvacuation()
    {
        evacuationComplete = true;
        if (playerController != null)
        {
            playerController.enabled = false;
            Rigidbody playerBody = playerController.GetComponent<Rigidbody>();
            if (playerBody != null)
            {
                playerBody.velocity = Vector3.zero;
                playerBody.angularVelocity = Vector3.zero;
            }
        }

        EvacuationCompleted?.Invoke();
        Time.timeScale = 0f;
        AudioListener.pause = true;
        StartCinematic();
    }

    private void StartCinematic()
    {
        cinematicActive = true;
        VideoClip cinematicClip = Resources.Load<VideoClip>(cinematicResourcePath);
        if (cinematicClip == null)
        {
            Debug.LogError(
                $"Level03 completion cinematic was not found at Resources/{cinematicResourcePath}.",
                this);
            FinishCinematic(null);
            return;
        }

        cinematicTexture = new RenderTexture(
            Mathf.Max(1280, Screen.width),
            Mathf.Max(720, Screen.height),
            0,
            RenderTextureFormat.ARGB32);
        cinematicTexture.Create();

        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = cinematicClip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = cinematicTexture;
        videoPlayer.isLooping = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        if (cinematicClip.audioTrackCount > 0)
        {
            videoAudio = gameObject.AddComponent<AudioSource>();
            videoAudio.playOnAwake = false;
            videoAudio.spatialBlend = 0f;
            videoAudio.ignoreListenerPause = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.controlledAudioTrackCount = 1;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, videoAudio);
        }
        else
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        }

        videoPlayer.loopPointReached += FinishCinematic;
        videoPlayer.errorReceived += OnCinematicError;
        videoPlayer.Play();
    }

    private void OnCinematicError(VideoPlayer source, string message)
    {
        Debug.LogError($"Level03 completion cinematic failed to play: {message}", this);
        FinishCinematic(source);
    }

    private void FinishCinematic(VideoPlayer source)
    {
        if (cinematicFinished)
        {
            return;
        }

        cinematicActive = false;
        cinematicFinished = true;
        if (!levelTransitionStarted)
        {
            levelTransitionStarted = true;
            StartCoroutine(StartSecondLevelAfterDelay());
        }
    }

    private IEnumerator StartSecondLevelAfterDelay()
    {
        if (returnToMenuDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(returnToMenuDelay);
        }

        GameModeSession.CompleteChapterTwoFirstLevelAndLoadSecondLevel();
    }

    private void OnGUI()
    {
        if (cinematicActive || cinematicFinished)
        {
            GUI.depth = -1200;
            if (cinematicTexture != null)
            {
                GUI.DrawTexture(
                    new Rect(0f, 0f, Screen.width, Screen.height),
                    cinematicTexture,
                    ScaleMode.ScaleAndCrop);
            }
            else
            {
                GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
            }

            if (cinematicFinished)
            {
                DrawCompletionPanel();
            }
            return;
        }

        if (evacuationComplete)
        {
            DrawCompletionPanel();
            return;
        }

        if (playerController == null ||
            !IsUnlocked ||
            !IsWithinInteractionRange(playerController.transform.position))
        {
            return;
        }

        EnsureStyles();
        GUI.depth = -1200;
        const float width = 420f;
        const float height = 74f;
        Rect panel = new Rect(
            (Screen.width - width) * 0.5f,
            Screen.height - height - 54f,
            width,
            height);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(
            panel,
            "\u6309 [F] \u64a4\u79bb",
            promptStyle);
    }

    private void DrawCompletionPanel()
    {
        EnsureStyles();
        GUI.depth = -1200;
        const float width = 520f;
        const float height = 150f;
        Rect panel = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(
            new Rect(panel.x + 20f, panel.y + 20f, panel.width - 40f, 58f),
            "\u64a4\u79bb\u6210\u529f",
            headingStyle);
        GUI.Label(
            new Rect(panel.x + 20f, panel.y + 82f, panel.width - 40f, 42f),
            "\u5373\u5c06\u8fdb\u5165\u7b2c\u4e8c\u7ae0\u7b2c\u4e8c\u5173",
            promptStyle);
    }

    private void EnsureStyles()
    {
        if (headingStyle != null)
        {
            return;
        }

        headingStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.78f, 0.12f) }
        };
        promptStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
    }

    private void OnDestroy()
    {
        if (!evacuationComplete)
        {
            return;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= FinishCinematic;
            videoPlayer.errorReceived -= OnCinematicError;
        }
        if (cinematicTexture != null)
        {
            cinematicTexture.Release();
            Destroy(cinematicTexture);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.78f, 0.12f, 0.18f);
        Gizmos.DrawSphere(transform.position, interactionRadius);
        Gizmos.color = new Color(1f, 0.78f, 0.12f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
