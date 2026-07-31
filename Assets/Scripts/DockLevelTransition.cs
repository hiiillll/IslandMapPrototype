using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(BoxCollider))]
public sealed class DockLevelTransition : MonoBehaviour
{
    [Header("Objective")]
    [SerializeField] private string nextSceneName = "Level02";
    [SerializeField] private Vector3 zonePadding = new Vector3(8f, 5f, 8f);

    [Header("Cinematic")]
    [SerializeField] private string cinematicResourcePath = "Cinematics/DockDepartureCG";

    private BoxCollider dockZone;
    private VideoPlayer videoPlayer;
    private AudioSource videoAudio;
    private RenderTexture cinematicTexture;
    private bool objectiveActive;
    private bool playerInDockZone;
    private bool cinematicActive;
    private bool loadingNextScene;
    private GUIStyle headingStyle;
    private GUIStyle bodyStyle;

    private void Awake()
    {
        EnsureDockZone();
    }

    public void BeginDockObjective()
    {
        if (objectiveActive || cinematicActive || loadingNextScene)
        {
            return;
        }

        EnsureDockZone();
        objectiveActive = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!objectiveActive || cinematicActive || loadingNextScene
            || other.GetComponentInParent<SimpleAutoDriveController>() == null)
        {
            return;
        }

        playerInDockZone = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<SimpleAutoDriveController>() != null)
        {
            playerInDockZone = false;
        }
    }

    private void Update()
    {
        if (objectiveActive && playerInDockZone && Input.GetKeyDown(KeyCode.F))
        {
            StartCinematic();
        }
    }

    private void StartCinematic()
    {
        objectiveActive = false;
        playerInDockZone = false;
        cinematicActive = true;

        SimpleAutoDriveController playerController = FindObjectOfType<SimpleAutoDriveController>();
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

        VideoClip cinematicClip = Resources.Load<VideoClip>(cinematicResourcePath);
        if (cinematicClip == null)
        {
            Debug.LogError($"Dock cinematic was not found at Resources/{cinematicResourcePath}.", this);
            LoadNextScene();
            return;
        }

        cinematicTexture = new RenderTexture(
            Mathf.Max(1280, Screen.width),
            Mathf.Max(720, Screen.height),
            0,
            RenderTextureFormat.ARGB32);
        cinematicTexture.Create();

        videoAudio = gameObject.AddComponent<AudioSource>();
        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = cinematicClip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = cinematicTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.controlledAudioTrackCount = 1;
        videoPlayer.EnableAudioTrack(0, true);
        videoPlayer.SetTargetAudioSource(0, videoAudio);
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.loopPointReached += OnCinematicFinished;
        videoPlayer.Play();

        Time.timeScale = 0f;
    }

    private void OnCinematicFinished(VideoPlayer source)
    {
        LoadNextScene();
    }

    private void LoadNextScene()
    {
        if (loadingNextScene)
        {
            return;
        }

        loadingNextScene = true;
        Time.timeScale = 1f;
        GameModeSession.CompleteFirstLevelAndLoadSecondLevel(nextSceneName);
    }

    private void EnsureDockZone()
    {
        dockZone = GetComponent<BoxCollider>();
        dockZone.isTrigger = true;

        if (TryGetDockBounds(out Bounds bounds))
        {
            transform.position = bounds.center;
            dockZone.center = Vector3.zero;
            dockZone.size = bounds.size + zonePadding;
        }
        else if (dockZone.size.sqrMagnitude < 0.01f)
        {
            dockZone.size = new Vector3(16f, 6f, 16f);
        }
    }

    private static bool TryGetDockBounds(out Bounds bounds)
    {
        GameObject dockRoot = GameObject.Find("PROP_Docks");
        if (dockRoot == null)
        {
            bounds = default;
            return false;
        }

        Renderer[] renderers = dockRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }
        return true;
    }

    private void OnGUI()
    {
        if (!objectiveActive && !cinematicActive)
        {
            return;
        }

        EnsureStyles();
        GUI.depth = -1000;

        if (cinematicActive)
        {
            if (cinematicTexture != null)
            {
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), cinematicTexture, ScaleMode.ScaleAndCrop);
            }
            else
            {
                GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
            }
            return;
        }

        const float panelWidth = 530f;
        const float panelHeight = 118f;
        Rect panel = new Rect((Screen.width - panelWidth) * 0.5f, 46f, panelWidth, panelHeight);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(new Rect(panel.x + 24f, panel.y + 17f, panel.width - 48f, 38f), "SURVIVAL COMPLETE", headingStyle);
        string instruction = playerInDockZone
            ? "Press [F] to enter the dock."
            : "All enemies cleared. Drive to the dock.";
        GUI.Label(new Rect(panel.x + 24f, panel.y + 58f, panel.width - 48f, 34f), instruction, bodyStyle);
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
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.78f, 0.12f) }
        };
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 19,
            normal = { textColor = Color.white }
        };
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnCinematicFinished;
        }
        if (cinematicTexture != null)
        {
            cinematicTexture.Release();
            Destroy(cinematicTexture);
        }
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider zone = GetComponent<BoxCollider>();
        if (zone == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.78f, 0.12f, 0.32f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(zone.center, zone.size);
        Gizmos.color = new Color(1f, 0.78f, 0.12f, 1f);
        Gizmos.DrawWireCube(zone.center, zone.size);
    }
}
