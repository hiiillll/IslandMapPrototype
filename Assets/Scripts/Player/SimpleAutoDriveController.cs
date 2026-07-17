using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SimpleAutoDriveController : MonoBehaviour
{
    [SerializeField] private float forwardSpeed = 24f;
    [SerializeField] private float turnSpeed = 3.6f;
    [SerializeField] private float inputSmoothing = 24f;
    [SerializeField] private float acceleration = 14f;
    [SerializeField] private float angularAcceleration = 14f;
    [SerializeField] private float lateralGrip = 6f;

    private Rigidbody body;
    private float turnInput;
    private float smoothedTurnInput;
    private float currentTurnRate;
    private PhysicMaterial frictionlessMaterial;

    public float ForwardSpeed => forwardSpeed;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        body.drag = 0f;
        body.angularDrag = 0f;
        body.maxAngularVelocity = 10f;

        forwardSpeed = 24f;
        turnSpeed = 3.6f;
        inputSmoothing = 24f;
        acceleration = 14f;
        angularAcceleration = 14f;
        lateralGrip = 6f;
        ConfigureFrictionlessColliders();
        if (GetComponent<SimplePlayerHealth>() == null)
        {
            gameObject.AddComponent<SimplePlayerHealth>();
        }
        if (GetComponent<PlayerSkillSystem>() == null)
        {
            gameObject.AddComponent<PlayerSkillSystem>();
        }
        if (GetComponent<PlayerProgression>() == null)
        {
            gameObject.AddComponent<PlayerProgression>();
        }
        if (GetComponent<ArcadeGameHud>() == null)
        {
            gameObject.AddComponent<ArcadeGameHud>();
        }
    }

    private void Start()
    {
        body.rotation = Quaternion.identity;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            QuitGame();
            return;
        }

        bool steerLeft = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool steerRight = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        turnInput = steerLeft == steerRight ? 0f : steerLeft ? -1f : 1f;
        smoothedTurnInput = Mathf.MoveTowards(
            smoothedTurnInput,
            turnInput,
            inputSmoothing * Time.deltaTime);
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void FixedUpdate()
    {
        body.WakeUp();
        Vector3 planarVelocity = new Vector3(body.velocity.x, 0f, body.velocity.z);
        Vector3 targetVelocity = transform.forward * forwardSpeed;
        planarVelocity = Vector3.Lerp(
            planarVelocity,
            targetVelocity,
            acceleration * Time.fixedDeltaTime);

        float lateralSpeed = Vector3.Dot(planarVelocity, transform.right);
        planarVelocity -= transform.right * lateralSpeed * lateralGrip * Time.fixedDeltaTime;
        body.velocity = new Vector3(planarVelocity.x, body.velocity.y, planarVelocity.z);

        float targetTurnRate = smoothedTurnInput * turnSpeed;
        currentTurnRate = Mathf.MoveTowards(
            currentTurnRate,
            targetTurnRate,
            angularAcceleration * Time.fixedDeltaTime);
        float turnDegrees = currentTurnRate * Mathf.Rad2Deg * Time.fixedDeltaTime;
        body.MoveRotation(body.rotation * Quaternion.Euler(0f, turnDegrees, 0f));
        body.angularVelocity = Vector3.zero;
    }

    private void ConfigureFrictionlessColliders()
    {
        frictionlessMaterial = new PhysicMaterial("PlayerCar_Frictionless")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicMaterialCombine.Minimum,
            bounceCombine = PhysicMaterialCombine.Minimum
        };

        foreach (Collider vehicleCollider in GetComponents<Collider>())
        {
            vehicleCollider.material = frictionlessMaterial;
        }
    }

    private void OnDestroy()
    {
        if (frictionlessMaterial != null)
        {
            Destroy(frictionlessMaterial);
        }
    }
}
