using UnityEngine;

/// <summary>
/// Animates the named wheel pivots exported with the Porsche 911 visual.
/// This is visual-only: the existing arcade rigidbody remains authoritative.
/// </summary>
public sealed class CarWheelVisualAnimator : MonoBehaviour
{
    [SerializeField, Range(0f, 45f)] private float maximumSteerAngle = 28f;
    [SerializeField, Min(0f)] private float steeringResponse = 220f;

    private Transform frontLeftSteer;
    private Transform frontRightSteer;
    private Transform frontLeftSpin;
    private Transform frontRightSpin;
    private Transform rearLeftSpin;
    private Transform rearRightSpin;
    private Quaternion frontLeftSteerBase;
    private Quaternion frontRightSteerBase;
    private Quaternion frontLeftSpinBase;
    private Quaternion frontRightSpinBase;
    private Quaternion rearLeftSpinBase;
    private Quaternion rearRightSpinBase;
    private Vector3 frontLeftSteerAxis;
    private Vector3 frontRightSteerAxis;
    private SimpleAutoDriveController driveController;
    private Rigidbody vehicleBody;
    private Transform vehicleRoot;
    private float currentSteerAngle;
    private float spinAngle;
    private float wheelRadius = 0.32f;
    private bool initialized;

    public void Configure(Transform hostVehicle)
    {
        vehicleRoot = hostVehicle;
        driveController = hostVehicle != null
            ? hostVehicle.GetComponent<SimpleAutoDriveController>()
            : null;
        vehicleBody = hostVehicle != null ? hostVehicle.GetComponent<Rigidbody>() : null;

        frontLeftSteer = FindDescendant(transform, "Wheel_FL_Steer");
        frontRightSteer = FindDescendant(transform, "Wheel_FR_Steer");
        frontLeftSpin = FindDescendant(transform, "Wheel_FL_Spin");
        frontRightSpin = FindDescendant(transform, "Wheel_FR_Spin");
        rearLeftSpin = FindDescendant(transform, "Wheel_RL_Spin");
        rearRightSpin = FindDescendant(transform, "Wheel_RR_Spin");

        initialized = frontLeftSteer != null
            && frontRightSteer != null
            && frontLeftSpin != null
            && frontRightSpin != null
            && rearLeftSpin != null
            && rearRightSpin != null;
        if (!initialized)
        {
            Debug.LogWarning("Porsche 911 wheel hierarchy is incomplete; wheel animation is disabled.", this);
            enabled = false;
            return;
        }

        frontLeftSteerBase = frontLeftSteer.localRotation;
        frontRightSteerBase = frontRightSteer.localRotation;
        frontLeftSpinBase = frontLeftSpin.localRotation;
        frontRightSpinBase = frontRightSpin.localRotation;
        rearLeftSpinBase = rearLeftSpin.localRotation;
        rearRightSpinBase = rearRightSpin.localRotation;
        Vector3 vehicleUp = vehicleRoot != null ? vehicleRoot.up : transform.up;
        frontLeftSteerAxis = ToParentLocalDirection(frontLeftSteer, vehicleUp);
        frontRightSteerAxis = ToParentLocalDirection(frontRightSteer, vehicleUp);
        wheelRadius = CalculateWorldWheelRadius(frontLeftSpin);
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        float steeringInput = driveController != null ? driveController.SignedSteeringInput : 0f;
        float targetSteerAngle = steeringInput * maximumSteerAngle;
        currentSteerAngle = Mathf.MoveTowardsAngle(
            currentSteerAngle,
            targetSteerAngle,
            steeringResponse * Time.deltaTime);

        Quaternion leftSteeringRotation = Quaternion.AngleAxis(currentSteerAngle, frontLeftSteerAxis);
        Quaternion rightSteeringRotation = Quaternion.AngleAxis(currentSteerAngle, frontRightSteerAxis);
        frontLeftSteer.localRotation = leftSteeringRotation * frontLeftSteerBase;
        frontRightSteer.localRotation = rightSteeringRotation * frontRightSteerBase;

        float signedSpeed = 0f;
        if (vehicleBody != null && vehicleRoot != null)
        {
            signedSpeed = Vector3.Dot(vehicleBody.velocity, vehicleRoot.forward);
        }
        spinAngle = Mathf.Repeat(
            spinAngle - signedSpeed / Mathf.Max(wheelRadius, 0.01f) * Mathf.Rad2Deg * Time.deltaTime,
            360f);
        Quaternion spinRotation = Quaternion.AngleAxis(spinAngle, Vector3.right);
        frontLeftSpin.localRotation = frontLeftSpinBase * spinRotation;
        frontRightSpin.localRotation = frontRightSpinBase * spinRotation;
        rearLeftSpin.localRotation = rearLeftSpinBase * spinRotation;
        rearRightSpin.localRotation = rearRightSpinBase * spinRotation;
    }

    private static float CalculateWorldWheelRadius(Transform wheel)
    {
        MeshFilter filter = null;
        foreach (MeshFilter candidate in wheel.GetComponentsInChildren<MeshFilter>(true))
        {
            if (candidate.transform.name.EndsWith("_Tire", System.StringComparison.Ordinal))
            {
                filter = candidate;
                break;
            }
        }
        if (filter == null)
        {
            filter = wheel.GetComponent<MeshFilter>();
        }
        if (filter == null || filter.sharedMesh == null)
        {
            return 0.32f;
        }

        Vector3 extents = filter.sharedMesh.bounds.extents;
        float radiusY = filter.transform.TransformVector(Vector3.up * extents.y).magnitude;
        float radiusZ = filter.transform.TransformVector(Vector3.forward * extents.z).magnitude;
        return Mathf.Max(radiusY, radiusZ, 0.01f);
    }

    private static Vector3 ToParentLocalDirection(Transform target, Vector3 worldDirection)
    {
        if (target.parent == null)
        {
            return worldDirection.normalized;
        }

        return target.parent.InverseTransformDirection(worldDirection).normalized;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root.name == objectName)
        {
            return root;
        }
        foreach (Transform child in root)
        {
            Transform match = FindDescendant(child, objectName);
            if (match != null)
            {
                return match;
            }
        }
        return null;
    }
}
