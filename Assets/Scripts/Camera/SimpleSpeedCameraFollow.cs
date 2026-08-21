using UnityEngine;

[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(AudioListener))]
public class SimpleSpeedCameraFollow : MonoBehaviour
{
    [SerializeField] public Transform target;

    [Header("Follow")]
    [SerializeField] private float smoothTime = 0.12f;
    [SerializeField] private float maxSpeed = 60f;

    [InspectorName("视角切换平滑速度")]
    [Tooltip("数值越大，按 C 切换视角时越快到位。")]
    [SerializeField] private float viewBlendSpeed = 8f;

    [Header("Third Person")]
    [SerializeField] private Vector3 thirdPersonCameraOffset = new Vector3(0f, 8f, -18f);
    [SerializeField] private Vector3 thirdPersonLookOffset = new Vector3(0f, 1.5f, -2f);
    [SerializeField] private float thirdPersonFieldOfView = 62f;

    [Header("近距离视角（第一关按 C 切换）")]
    [InspectorName("启用近距离视角")]
    [SerializeField] private bool enableCloseChaseView;
    [InspectorName("切换按键")]
    [SerializeField] private KeyCode toggleViewKey = KeyCode.C;
    [InspectorName("摄像机位置偏移")]
    [Tooltip("X 是左右，Y 是高度，Z 是前后；Z 越小，摄像机离车越远。")]
    [SerializeField] private Vector3 closeChaseCameraOffset = new Vector3(0f, 3.2f, -8.5f);
    [InspectorName("镜头注视点偏移")]
    [Tooltip("X 是左右，Y 是注视高度，Z 是向车前方看的距离。")]
    [SerializeField] private Vector3 closeChaseLookOffset = new Vector3(0f, 1.35f, 6f);
    [InspectorName("近距离视野 FOV")]
    [Range(35f, 90f)]
    [Tooltip("数值越小越像长焦、速度感更稳；数值越大视野更广。")]
    [SerializeField] private float closeChaseFieldOfView = 60f;

    [Header("近距离视角后视镜")]
    [InspectorName("启用后视镜")]
    [SerializeField] private bool enableRearViewMirror = true;
    [InspectorName("后视摄像机位置偏移")]
    [Tooltip("相对于车辆的位置：X 左右、Y 高度、Z 前后。")]
    [SerializeField] private Vector3 rearViewCameraOffset = new Vector3(0f, 2.35f, -1.6f);
    [InspectorName("后视摄像机注视点偏移")]
    [Tooltip("默认朝车后方看。Z 越小，视线看得越靠后。")]
    [SerializeField] private Vector3 rearViewLookOffset = new Vector3(0f, 1.25f, -24f);
    [InspectorName("后视镜视野 FOV")]
    [Range(30f, 100f)]
    [SerializeField] private float rearViewFieldOfView = 58f;
    [InspectorName("后视镜屏幕位置")]
    [Tooltip("归一化屏幕坐标：X 是中心位置，Y 是顶部位置。")]
    [SerializeField] private Vector2 rearViewScreenPosition = new Vector2(0.5f, 0.035f);
    [InspectorName("后视镜屏幕大小")]
    [Tooltip("归一化屏幕尺寸。建议保持宽度约为高度的 2.8 至 3.4 倍。")]
    [SerializeField] private Vector2 rearViewScreenSize = new Vector2(0.36f, 0.135f);
    [InspectorName("后视镜渲染分辨率")]
    [Tooltip("提高会更清晰但更耗性能。")]
    [SerializeField] private Vector2Int rearViewRenderResolution = new Vector2Int(768, 256);

    private Camera cameraComponent;
    private Camera rearViewCamera;
    private RenderTexture rearViewTexture;
    private Material rearViewMaterial;
    private bool closeChaseViewActive;
    private Vector3 followVelocity;
    private float shakeRemaining;
    private float shakeDuration;
    private float shakeMagnitude;

    public void Shake(float duration, float magnitude)
    {
        shakeDuration = Mathf.Max(shakeDuration, duration);
        shakeRemaining = Mathf.Max(shakeRemaining, duration);
        shakeMagnitude = Mathf.Max(shakeMagnitude, magnitude);
    }

    public void ConfigureCloseChaseView(bool enabled)
    {
        enableCloseChaseView = enabled;
        if (!enabled)
        {
            closeChaseViewActive = false;
        }
        ApplyProjection();
        UpdateRearViewMirrorState();
    }

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        if (FindObjectOfType<AudioListener>() == null)
        {
            gameObject.AddComponent<AudioListener>();
        }
        ApplyProjection();
        EnsureRearViewMirror();
        UpdateRearViewMirrorState();
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }

        ApplyProjection();
        if (target != null)
        {
            GetThirdPersonPose(out Vector3 position, out Quaternion rotation);
            transform.SetPositionAndRotation(position, rotation);
            followVelocity = Vector3.zero;
        }
    }

    private void Update()
    {
        if (!enableCloseChaseView || !Input.GetKeyDown(toggleViewKey))
        {
            return;
        }

        closeChaseViewActive = !closeChaseViewActive;
        followVelocity = Vector3.zero;
        ApplyProjection();
        UpdateRearViewMirrorState();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        GetThirdPersonPose(out Vector3 desiredPosition, out Quaternion desiredRotation);

        Vector3 shakeOffset = Vector3.zero;
        if (shakeRemaining > 0f)
        {
            float strength = shakeMagnitude * (shakeRemaining / shakeDuration);
            shakeOffset = Random.insideUnitSphere * strength;
            shakeRemaining -= Time.unscaledDeltaTime;
            if (shakeRemaining <= 0f)
            {
                shakeDuration = 0f;
                shakeMagnitude = 0f;
            }
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            smoothTime,
            maxSpeed) + shakeOffset;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            viewBlendSpeed * Time.unscaledDeltaTime);

        UpdateRearViewCameraPose();
    }

    private void OnGUI()
    {
        if (!ShouldShowRearViewMirror() || rearViewTexture == null || Event.current.type != EventType.Repaint)
        {
            return;
        }

        float width = Mathf.Clamp(rearViewScreenSize.x, 0.12f, 0.8f) * Screen.width;
        float height = Mathf.Clamp(rearViewScreenSize.y, 0.05f, 0.4f) * Screen.height;
        float centerX = Mathf.Clamp01(rearViewScreenPosition.x) * Screen.width;
        float top = Mathf.Clamp01(rearViewScreenPosition.y) * Screen.height;
        Rect mirrorRect = new Rect(centerX - width * 0.5f, top, width, height);

        int previousDepth = GUI.depth;
        GUI.depth = -900;
        if (rearViewMaterial != null)
        {
            Graphics.DrawTexture(mirrorRect, rearViewTexture, rearViewMaterial);
        }
        else
        {
            GUI.DrawTexture(mirrorRect, rearViewTexture, ScaleMode.StretchToFill, false);
            GUI.Box(mirrorRect, GUIContent.none);
        }
        GUI.depth = previousDepth;
    }

    private void OnDestroy()
    {
        if (rearViewCamera != null)
        {
            Destroy(rearViewCamera.gameObject);
        }
        if (rearViewTexture != null)
        {
            rearViewTexture.Release();
            Destroy(rearViewTexture);
        }
        if (rearViewMaterial != null)
        {
            Destroy(rearViewMaterial);
        }
    }

    private void ApplyProjection()
    {
        if (cameraComponent == null)
        {
            return;
        }

        cameraComponent.orthographic = false;
        cameraComponent.fieldOfView = closeChaseViewActive
            ? closeChaseFieldOfView
            : thirdPersonFieldOfView;
    }

    private void EnsureRearViewMirror()
    {
        if (!enableCloseChaseView || !enableRearViewMirror || rearViewCamera != null || cameraComponent == null)
        {
            return;
        }

        int textureWidth = Mathf.Clamp(rearViewRenderResolution.x, 256, 1920);
        int textureHeight = Mathf.Clamp(rearViewRenderResolution.y, 96, 720);
        rearViewTexture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32)
        {
            name = "RT_Level01_RearViewMirror",
            antiAliasing = 1,
            filterMode = FilterMode.Bilinear,
            useMipMap = false
        };
        rearViewTexture.Create();

        GameObject rearCameraObject = new GameObject("SYS_RearViewMirrorCamera");
        rearCameraObject.transform.SetParent(transform, false);
        rearViewCamera = rearCameraObject.AddComponent<Camera>();
        rearViewCamera.CopyFrom(cameraComponent);
        rearViewCamera.name = "Rear View Mirror Camera";
        rearViewCamera.targetTexture = rearViewTexture;
        rearViewCamera.depth = cameraComponent.depth - 1f;
        rearViewCamera.allowMSAA = false;
        rearViewCamera.useOcclusionCulling = true;
        rearViewCamera.fieldOfView = rearViewFieldOfView;

        Shader mirrorShader = Shader.Find("Hidden/SpeedEscape/RearViewMirror");
        if (mirrorShader != null)
        {
            rearViewMaterial = new Material(mirrorShader)
            {
                name = "MAT_Runtime_RearViewMirror"
            };
        }
    }

    private bool ShouldShowRearViewMirror()
    {
        return enableCloseChaseView && enableRearViewMirror && closeChaseViewActive && target != null;
    }

    private void UpdateRearViewMirrorState()
    {
        EnsureRearViewMirror();
        if (rearViewCamera != null)
        {
            rearViewCamera.enabled = ShouldShowRearViewMirror();
        }
    }

    private void UpdateRearViewCameraPose()
    {
        if (rearViewCamera == null || !ShouldShowRearViewMirror())
        {
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 cameraPosition = target.position
            + right * rearViewCameraOffset.x
            + Vector3.up * rearViewCameraOffset.y
            + forward * rearViewCameraOffset.z;
        Vector3 lookTarget = target.position
            + right * rearViewLookOffset.x
            + Vector3.up * rearViewLookOffset.y
            + forward * rearViewLookOffset.z;
        rearViewCamera.transform.SetPositionAndRotation(
            cameraPosition,
            Quaternion.LookRotation(lookTarget - cameraPosition, Vector3.up));
        rearViewCamera.fieldOfView = rearViewFieldOfView;
    }

    private void GetThirdPersonPose(out Vector3 position, out Quaternion rotation)
    {
        Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 selectedCameraOffset = closeChaseViewActive
            ? closeChaseCameraOffset
            : thirdPersonCameraOffset;
        Vector3 selectedLookOffset = closeChaseViewActive
            ? closeChaseLookOffset
            : thirdPersonLookOffset;
        Vector3 cameraOffset = right * selectedCameraOffset.x
            + Vector3.up * selectedCameraOffset.y
            + forward * selectedCameraOffset.z;
        Vector3 lookOffset = right * selectedLookOffset.x
            + Vector3.up * selectedLookOffset.y
            + forward * selectedLookOffset.z;
        Vector3 lookTarget = target.position + lookOffset;
        position = target.position + cameraOffset;
        rotation = Quaternion.LookRotation(lookTarget - position, Vector3.up);
    }
}
