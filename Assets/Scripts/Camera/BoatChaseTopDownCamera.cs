using UnityEngine;

[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(AudioListener))]
public sealed class BoatChaseTopDownCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float height = 62f;
    [SerializeField, Min(0.01f)] private float orthographicSize = 36f;
    [SerializeField, Min(0f)] private float followSmoothTime = 0.14f;

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
        height = cameraHeight;
        orthographicSize = cameraOrthographicSize;
        cameraComponent = GetComponent<Camera>();
        ApplyCameraSettings();
    }

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        height = Mathf.Max(height, 62f);
        orthographicSize = Mathf.Max(orthographicSize, 36f);
        ApplyCameraSettings();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = target.position + Vector3.up * height;
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
            followSmoothTime) + shakeOffset;
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void ApplyCameraSettings()
    {
        if (cameraComponent == null)
        {
            return;
        }

        cameraComponent.orthographic = true;
        cameraComponent.orthographicSize = orthographicSize;
        cameraComponent.nearClipPlane = 0.1f;
        cameraComponent.farClipPlane = 220f;
    }
}
