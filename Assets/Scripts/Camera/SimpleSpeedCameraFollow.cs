using UnityEngine;

[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(AudioListener))]
public class SimpleSpeedCameraFollow : MonoBehaviour
{
    private enum CameraViewMode
    {
        TopDown,
        ThirdPerson
    }

    [SerializeField] public Transform target;

    [Header("Follow")]
    [SerializeField] private float smoothTime = 0.12f;
    [SerializeField] private float maxSpeed = 60f;

    [Header("Top Down")]
    [SerializeField] private float topDownHeight = 45f;
    [SerializeField] private float orthographicSize = 22f;

    [Header("View Toggle")]
    [SerializeField] private KeyCode toggleViewKey = KeyCode.C;
    [SerializeField] private CameraViewMode startViewMode = CameraViewMode.TopDown;
    [SerializeField] private float viewBlendSpeed = 8f;

    [Header("Third Person")]
    [SerializeField] private Vector3 thirdPersonCameraOffset = new Vector3(0f, 8f, -18f);
    [SerializeField] private Vector3 thirdPersonLookOffset = new Vector3(0f, 1.5f, -2f);
    [SerializeField] private float thirdPersonFieldOfView = 68f;

    private Camera cameraComponent;
    private CameraViewMode viewMode;
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

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        if (FindObjectOfType<AudioListener>() == null)
        {
            gameObject.AddComponent<AudioListener>();
        }
        topDownHeight = 45f;
        orthographicSize = 22f;
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

        viewMode = startViewMode;
        ApplyProjection();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleViewKey))
        {
            viewMode = viewMode == CameraViewMode.TopDown
                ? CameraViewMode.ThirdPerson
                : CameraViewMode.TopDown;
            ApplyProjection();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition;
        Quaternion desiredRotation;
        if (viewMode == CameraViewMode.TopDown)
        {
            desiredPosition = target.position + Vector3.up * topDownHeight;
            desiredRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        else
        {
            GetThirdPersonPose(out desiredPosition, out desiredRotation);
        }

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
        bool topDown = viewMode == CameraViewMode.TopDown;
        cameraComponent.orthographic = topDown;
        if (topDown)
        {
            cameraComponent.orthographicSize = orthographicSize;
        }
        else
        {
            cameraComponent.fieldOfView = thirdPersonFieldOfView;
        }
    }

    private void GetThirdPersonPose(out Vector3 position, out Quaternion rotation)
    {
        Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 cameraOffset = right * thirdPersonCameraOffset.x
            + Vector3.up * thirdPersonCameraOffset.y
            + forward * thirdPersonCameraOffset.z;
        Vector3 lookOffset = right * thirdPersonLookOffset.x
            + Vector3.up * thirdPersonLookOffset.y
            + forward * thirdPersonLookOffset.z;
        Vector3 lookTarget = target.position + lookOffset;
        position = target.position + cameraOffset;
        rotation = Quaternion.LookRotation(lookTarget - position, Vector3.up);
    }
}
