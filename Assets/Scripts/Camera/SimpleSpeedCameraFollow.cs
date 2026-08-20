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

    private Camera cameraComponent;
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
    }

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        if (FindObjectOfType<AudioListener>() == null)
        {
            gameObject.AddComponent<AudioListener>();
        }
        ApplyProjection();
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
