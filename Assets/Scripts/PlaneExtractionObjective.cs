using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(SphereCollider))]
public sealed class PlaneExtractionObjective : MonoBehaviour
{
    private const float OceanHalfSize = 2000f;

    [Header("Plane Objective")]
    [SerializeField] private string planeResourcePath = "Level02/PlaneObjective/a20e928798d90a32b7b6c4b41a481066";
    [SerializeField, Min(0f)] private float spawnDistance = 42f;
    [SerializeField, Min(0f)] private float altitude = 8f;
    [SerializeField, Min(1f)] private float planeWorldSize = 18f;
    [SerializeField, Min(1f)] private float interactionRadius = 14f;

    [Header("Cinematic")]
    [SerializeField] private string cinematicResourcePath = "Cinematics/Level02CompletionCG";

    private SphereCollider extractionZone;
    private Transform player;
    private GameObject planeVisual;
    private VideoPlayer videoPlayer;
    private AudioSource videoAudio;
    private RenderTexture cinematicTexture;
    private bool objectiveActive;
    private bool playerInZone;
    private bool cinematicActive;
    private bool cinematicFinished;
    private GUIStyle headingStyle;
    private GUIStyle bodyStyle;
    private GUIStyle arrowStyle;

    private void Awake()
    {
        extractionZone = GetComponent<SphereCollider>();
        extractionZone.isTrigger = true;
        extractionZone.radius = interactionRadius;
        extractionZone.enabled = false;
    }

    public void BeginObjective()
    {
        if (objectiveActive || cinematicActive || cinematicFinished)
        {
            return;
        }

        ResolvePlayer();
        if (player == null)
        {
            Debug.LogError("Unable to start the plane objective because the player was not found.", this);
            return;
        }

        Vector2 direction2D = Random.insideUnitCircle.normalized;
        if (direction2D.sqrMagnitude < 0.01f)
        {
            direction2D = Vector2.up;
        }

        Vector3 direction = new Vector3(direction2D.x, 0f, direction2D.y);
        Vector3 objectivePosition = player.position + direction * spawnDistance;
        float safeCoordinate = OceanHalfSize - interactionRadius - planeWorldSize * 0.5f - 8f;
        objectivePosition.x = Mathf.Clamp(objectivePosition.x, -safeCoordinate, safeCoordinate);
        objectivePosition.z = Mathf.Clamp(objectivePosition.z, -safeCoordinate, safeCoordinate);
        objectivePosition.y = player.position.y + altitude;
        transform.position = objectivePosition;
        SpawnPlane(direction);
        extractionZone.radius = interactionRadius;
        extractionZone.enabled = true;
        objectiveActive = true;
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        BoatChaseController controller = FindObjectOfType<BoatChaseController>();
        if (controller != null)
        {
            player = controller.transform;
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;
    }

    private void SpawnPlane(Vector3 arrivalDirection)
    {
        GameObject planePrefab = Resources.Load<GameObject>(planeResourcePath);
        if (planePrefab == null)
        {
            Debug.LogError($"Plane model was not found at Resources/{planeResourcePath}.", this);
            return;
        }

        planeVisual = Instantiate(planePrefab, transform);
        planeVisual.name = "VIS_PlaneExtraction";
        planeVisual.transform.localPosition = Vector3.zero;
        planeVisual.transform.localRotation = Quaternion.LookRotation(arrivalDirection, Vector3.up)
            * Quaternion.Euler(0f, 90f, 0f);

        Renderer[] renderers = planeVisual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = GetRendererBounds(renderers);
        float largestHorizontalSize = Mathf.Max(bounds.size.x, bounds.size.z);
        if (largestHorizontalSize > 0.001f)
        {
            planeVisual.transform.localScale *= planeWorldSize / largestHorizontalSize;
        }

        bounds = GetRendererBounds(renderers);
        planeVisual.transform.position += transform.position - bounds.center;
    }

    private static Bounds GetRendererBounds(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }
        return bounds;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (objectiveActive && other.GetComponentInParent<BoatChaseController>() != null)
        {
            playerInZone = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<BoatChaseController>() != null)
        {
            playerInZone = false;
        }
    }

    private void Update()
    {
        if (objectiveActive && playerInZone && Input.GetKeyDown(KeyCode.F))
        {
            StartCinematic();
        }
    }

    private void StartCinematic()
    {
        objectiveActive = false;
        playerInZone = false;
        extractionZone.enabled = false;
        cinematicActive = true;

        BoatChaseController playerController = player != null
            ? player.GetComponent<BoatChaseController>()
            : null;
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        Rigidbody playerBody = player != null ? player.GetComponent<Rigidbody>() : null;
        if (playerBody != null)
        {
            playerBody.velocity = Vector3.zero;
            playerBody.angularVelocity = Vector3.zero;
        }

        VideoClip cinematicClip = Resources.Load<VideoClip>(cinematicResourcePath);
        if (cinematicClip == null)
        {
            Debug.LogError($"Completion cinematic was not found at Resources/{cinematicResourcePath}.", this);
            FinishCinematic(null);
            return;
        }

        cinematicTexture = new RenderTexture(
            Mathf.Max(1280, Screen.width),
            Mathf.Max(720, Screen.height),
            0,
            RenderTextureFormat.ARGB32);
        cinematicTexture.Create();

        videoAudio = gameObject.AddComponent<AudioSource>();
        videoAudio.playOnAwake = false;
        videoAudio.spatialBlend = 0f;
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
        videoPlayer.loopPointReached += FinishCinematic;
        videoPlayer.Play();

        Time.timeScale = 0f;
    }

    private void FinishCinematic(VideoPlayer source)
    {
        cinematicActive = false;
        cinematicFinished = true;
        Time.timeScale = 0f;
    }

    private void OnGUI()
    {
        if (!objectiveActive && !cinematicActive && !cinematicFinished)
        {
            return;
        }

        EnsureStyles();
        GUI.depth = -1100;

        if (cinematicActive || cinematicFinished)
        {
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
                GUI.Label(
                    new Rect(0f, Screen.height - 130f, Screen.width, 72f),
                    "ESCAPE COMPLETE",
                    headingStyle);
            }
            return;
        }

        DrawDirectionArrow();

        const float panelWidth = 580f;
        const float panelHeight = 118f;
        Rect panel = new Rect((Screen.width - panelWidth) * 0.5f, 46f, panelWidth, panelHeight);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(
            new Rect(panel.x + 24f, panel.y + 17f, panel.width - 48f, 38f),
            "SURVIVAL COMPLETE",
            headingStyle);
        string instruction = playerInZone
            ? "Press [F] to board the extraction plane."
            : "All enemies cleared. Follow the arrow to the plane.";
        GUI.Label(
            new Rect(panel.x + 24f, panel.y + 58f, panel.width - 48f, 34f),
            instruction,
            bodyStyle);
    }

    private void DrawDirectionArrow()
    {
        Camera objectiveCamera = Camera.main;
        if (objectiveCamera == null)
        {
            return;
        }

        Vector3 screenPoint = objectiveCamera.WorldToScreenPoint(transform.position);
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 targetPoint = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
        Vector2 direction = targetPoint - screenCenter;
        if (screenPoint.z < 0f)
        {
            direction = -direction;
        }
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Vector2.up;
        }

        direction.Normalize();
        float radius = Mathf.Min(Screen.width, Screen.height) * 0.36f;
        Vector2 arrowCenter = screenCenter + direction * radius;
        float arrowAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(arrowAngle, arrowCenter);
        GUI.Label(
            new Rect(arrowCenter.x - 40f, arrowCenter.y - 40f, 80f, 80f),
            "▲",
            arrowStyle);
        GUI.matrix = previousMatrix;
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
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.78f, 0.12f) }
        };
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 19,
            normal = { textColor = Color.white }
        };
        arrowStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 58,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.78f, 0.12f) }
        };
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= FinishCinematic;
        }
        if (cinematicTexture != null)
        {
            cinematicTexture.Release();
            Destroy(cinematicTexture);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.78f, 0.12f, 0.25f);
        Gizmos.DrawSphere(transform.position, interactionRadius);
        Gizmos.color = new Color(1f, 0.78f, 0.12f, 1f);
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
