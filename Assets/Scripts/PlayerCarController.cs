using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class PlayerCarController : MonoBehaviour
{
    [SerializeField] private float acceleration = 50f;
    [SerializeField] private float braking = 30f;
    [SerializeField] private float steering = 125f;
    [SerializeField] private float maxSpeed = 29f;
    [SerializeField] private float lateralGrip = 0.9f;
    [SerializeField] private RaceSettings settings;

    private Rigidbody body;
    private float accelerationMultiplier = 1f;
    private float steeringMultiplier = 1f;
    private float maxSpeedMultiplier = 1f;
    private float gripMultiplier = 1f;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.centerOfMass = new Vector3(0f, -0.5f, 0.15f);
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public void Configure(RaceSettings raceSettings)
    {
        settings = raceSettings;
    }

    private void FixedUpdate()
    {
        ApplyDriveMode();

        var throttle = GetThrottle();
        var turn = GetSteering();
        var forwardSpeed = Vector3.Dot(body.velocity, transform.forward);

        if (throttle > 0f && forwardSpeed < maxSpeed * maxSpeedMultiplier)
        {
            body.AddForce(transform.forward * (throttle * acceleration * accelerationMultiplier), ForceMode.Acceleration);
        }
        else if (throttle < 0f)
        {
            body.AddForce(transform.forward * (throttle * braking), ForceMode.Acceleration);
        }

        var sideways = Vector3.Dot(body.velocity, transform.right);
        body.AddForce(-transform.right * (sideways * lateralGrip * gripMultiplier), ForceMode.VelocityChange);

        var speedFactor = Mathf.Clamp01(body.velocity.magnitude / 3f);
        var yaw = turn * steering * steeringMultiplier * speedFactor * Time.fixedDeltaTime;
        body.MoveRotation(body.rotation * Quaternion.Euler(0f, yaw, 0f));
    }

    private void ApplyDriveMode()
    {
        var mode = settings != null ? settings.driveMode : 0;
        switch (mode)
        {
            case 1:
                accelerationMultiplier = 0.78f;
                steeringMultiplier = 0.72f;
                maxSpeedMultiplier = 0.78f;
                gripMultiplier = 1.25f;
                break;
            case 2:
                accelerationMultiplier = 0.9f;
                steeringMultiplier = 0.58f;
                maxSpeedMultiplier = 0.88f;
                gripMultiplier = 1.45f;
                break;
            default:
                accelerationMultiplier = 1.12f;
                steeringMultiplier = 1f;
                maxSpeedMultiplier = 1.08f;
                gripMultiplier = 1f;
                break;
        }
    }

    private static float GetThrottle()
    {
        var throttle = 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            throttle += 1f;
        }

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            throttle -= 1f;
        }

        return throttle;
    }

    private static float GetSteering()
    {
        var steeringInput = 0f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            steeringInput += 1f;
        }

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            steeringInput -= 1f;
        }

        return steeringInput;
    }
}
