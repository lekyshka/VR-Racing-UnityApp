using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class RacingAIDriver : MonoBehaviour
{
    [SerializeField] private AStarGrid grid;
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private float waypointReachDistance = 5.5f;
    [SerializeField] private float laneOffset;
    [SerializeField] private Transform parkingSpot;

    private Rigidbody body;
    private RaceProgressTracker progressTracker;
    private int targetNodeIndex;
    private bool parked;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        progressTracker = GetComponent<RaceProgressTracker>();
        body.centerOfMass = new Vector3(0f, -0.45f, 0.1f);
        body.isKinematic = true;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void Start()
    {
        SnapTargetToNextNode();
    }

    private void FixedUpdate()
    {
        if (progressTracker != null && progressTracker.Finished)
        {
            ParkAfterFinish();
            return;
        }

        if (grid == null || grid.Nodes.Count == 0)
        {
            return;
        }

        var target = GetLanePosition(targetNodeIndex);
        var flatTarget = new Vector3(target.x, transform.position.y, target.z);
        var toTarget = flatTarget - transform.position;

        if (toTarget.magnitude <= waypointReachDistance)
        {
            targetNodeIndex = (targetNodeIndex + 1) % grid.Nodes.Count;
            target = GetLanePosition(targetNodeIndex);
            flatTarget = new Vector3(target.x, transform.position.y, target.z);
            toTarget = flatTarget - transform.position;
        }

        if (toTarget.sqrMagnitude < 0.01f)
        {
            return;
        }

        var desiredRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        var nextRotation = Quaternion.Slerp(body.rotation, desiredRotation, turnSpeed * Time.fixedDeltaTime);
        var nextPosition = Vector3.MoveTowards(body.position, flatTarget, maxSpeed * Time.fixedDeltaTime);
        body.MoveRotation(nextRotation);
        body.MovePosition(nextPosition);
    }

    public void Configure(AStarGrid newGrid, int startOffset, float newLaneOffset, float newMaxSpeed, Transform finishParkingSpot)
    {
        grid = newGrid;
        targetNodeIndex = Mathf.Max(0, startOffset);
        laneOffset = newLaneOffset;
        maxSpeed = newMaxSpeed;
        parkingSpot = finishParkingSpot;
        parked = false;
    }

    public void ResetDriver()
    {
        parked = false;
        SnapTargetToNextNode();
    }

    private Vector3 GetLanePosition(int nodeIndex)
    {
        if (grid == null || grid.Nodes.Count == 0)
        {
            return transform.position;
        }

        var count = grid.Nodes.Count;
        var current = grid.Nodes[(nodeIndex + count) % count].position;
        var previous = grid.Nodes[(nodeIndex - 1 + count) % count].position;
        var next = grid.Nodes[(nodeIndex + 1) % count].position;
        var direction = (next - previous).normalized;

        if (direction.sqrMagnitude < 0.01f)
        {
            direction = transform.forward;
        }

        var right = Vector3.Cross(Vector3.up, direction).normalized;
        return current + right * laneOffset;
    }

    private void SnapTargetToNextNode()
    {
        if (grid == null || grid.Nodes.Count == 0)
        {
            return;
        }

        var closest = grid.FindClosestNode(transform.position);
        var closestIndex = Mathf.Max(0, grid.IndexOf(closest));
        targetNodeIndex = (closestIndex + 1) % grid.Nodes.Count;

        // The AI route is still generated from the A* graph; movement follows the next clockwise node
        // so cars do not all pick the same shortest reverse route and pile up.
        grid.FindPath(closest, grid.Nodes[targetNodeIndex]);
    }

    private void ParkAfterFinish()
    {
        if (parkingSpot == null || parked)
        {
            return;
        }

        var target = parkingSpot.position;
        var toTarget = target - transform.position;
        if (toTarget.magnitude <= 0.25f)
        {
            body.MovePosition(target);
            body.MoveRotation(parkingSpot.rotation);
            parked = true;
            return;
        }

        var desiredRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        body.MoveRotation(Quaternion.Slerp(body.rotation, desiredRotation, turnSpeed * Time.fixedDeltaTime));
        body.MovePosition(Vector3.MoveTowards(body.position, target, maxSpeed * Time.fixedDeltaTime));
    }
}
