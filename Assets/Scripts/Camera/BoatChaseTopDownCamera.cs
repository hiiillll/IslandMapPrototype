using UnityEngine;

[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(AudioListener))]
public sealed class BoatChaseTopDownCamera : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Header("Follow")]
    [SerializeField, Min(0f)] private float followSmoothTime = 0.14f;
    [SerializeField, Min(0f)] private float maximumFollowSpeed = 100f;

    [SerializeField, Min(0f)] private float viewBlendSpeed = 8f;

    [Header("Third Person")]
    [SerializeField] private Vector3 thirdPersonCameraOffset = new Vector3(0f, 10.5f, -24.5f);
    [SerializeField] private Vector3 thirdPersonLookOffset = new Vector3(0f, 1.7f, -6.5f);
    [SerializeField, Range(30f, 100f)] private float thirdPersonFieldOfView = 66f;

    private Camera cameraComponent;
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

    public void Configure(Transform followTarget, float cameraHeight, float cameraOrthographicSize)
    {
        target = followTarget;
        cameraComponent = GetComponent<Camera>();
        ApplyCameraSettings();
    }

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        thirdPersonCameraOffset = new Vector3(0f, 10.5f, -24.5f);
        thirdPersonLookOffset = new Vector3(0f, 1.7f, -6.5f);
        thirdPersonFieldOfView = 66f;
        ApplyCameraSettings();
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            target = player != null ? player.transform : null;
        }

        ApplyCameraSettings();
        if (target != null)
        {
            GetThirdPersonPose(out Vector3 position, out Quaternion rotation);
            transform.SetPositionAndRotation(position, rotation);
            followVelocity = Vector3.zero;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        GetThirdPersonPose(out Vector3 targetPosition, out Quaternion targetRotation);

        Vector3 shakeOffset = Vector3.zero;
        if (shakeRemaining > 0f)
        {
            float strength = shakeMagnitude * (shakeRemaining / shakeDuration);
            Vector2 planarShake = Random.insideUnitCircle * strength;
            shakeOffset = new Vector3(planarShake.x, 0f, planarShake.y);
            shakeRemaining -= Time.unscaledDeltaTime;
            if (shakeRemaining <= 0f)
            {
                shakeDuration = 0f;
                shakeMagnitude = 0f;
            }
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref followVelocity,
            followSmoothTime,
            maximumFollowSpeed) + shakeOffset;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            viewBlendSpeed * Time.unscaledDeltaTime);
    }

    private void ApplyCameraSettings()
    {
        if (cameraComponent == null)
        {
            return;
        }

        cameraComponent.clearFlags = CameraClearFlags.Skybox;
        cameraComponent.orthographic = false;
        cameraComponent.fieldOfView = thirdPersonFieldOfView;
        cameraComponent.nearClipPlane = 0.1f;
        cameraComponent.farClipPlane = 1600f;
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
