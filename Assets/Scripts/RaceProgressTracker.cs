using UnityEngine;

public sealed class RaceProgressTracker : MonoBehaviour
{
    [SerializeField] private AStarGrid grid;
    [SerializeField] private RaceSettings settings;
    [SerializeField] private string racerName = "Racer";

    private int completedLaps;
    private int lastNodeIndex;
    private int stableNodeIndex;
    private bool reachedHalfTrack;
    private bool finished;
    private Vector3 previousPosition;
    private Vector3 startLinePoint;
    private Vector3 startLineDirection;
    private bool hasStartLine;
    private bool lapCountingStarted;

    public string RacerName => racerName;
    public int CurrentLap => Mathf.Clamp(completedLaps + 1, 1, settings != null ? settings.laps : 1);
    public int CompletedLaps => completedLaps;
    public bool Finished => finished;

    public float TotalProgress
    {
        get
        {
            if (grid == null || grid.Nodes.Count == 0)
            {
                return 0f;
            }

            var closest = grid.FindClosestNode(transform.position);
            var nodeIndex = Mathf.Max(0, grid.IndexOf(closest));
            return completedLaps * grid.Nodes.Count + nodeIndex;
        }
    }

    public void Configure(AStarGrid newGrid, RaceSettings raceSettings, string displayName)
    {
        grid = newGrid;
        settings = raceSettings;
        racerName = displayName;
    }

    public void ResetRace()
    {
        completedLaps = 0;
        lastNodeIndex = GetCurrentNodeIndex();
        stableNodeIndex = lastNodeIndex;
        reachedHalfTrack = false;
        finished = false;
        previousPosition = transform.position;
        CacheStartLine();
        lapCountingStarted = IsPastStartLine(transform.position);
    }

    private void Update()
    {
        if (grid == null || settings == null || grid.Nodes.Count == 0 || finished)
        {
            return;
        }

        var index = GetCurrentNodeIndex();
        if (index < 0)
        {
            return;
        }

        var count = grid.Nodes.Count;
        var forwardDelta = (index - stableNodeIndex + count) % count;
        var backwardDelta = (stableNodeIndex - index + count) % count;

        if (forwardDelta == 0)
        {
            lastNodeIndex = index;
            return;
        }

        // Ignore sudden nearest-node jumps caused by wide road/lane offsets.
        if (forwardDelta > 0 && forwardDelta <= 4)
        {
            stableNodeIndex = index;
        }
        else if (backwardDelta <= 2)
        {
            lastNodeIndex = index;
            return;
        }
        else
        {
            return;
        }

        if (stableNodeIndex >= count / 2)
        {
            reachedHalfTrack = true;
        }

        var crossedStartLine = CrossedStartLine();
        if (crossedStartLine && !lapCountingStarted)
        {
            lapCountingStarted = true;
            reachedHalfTrack = false;
            lastNodeIndex = stableNodeIndex;
            previousPosition = transform.position;
            return;
        }

        if (lapCountingStarted && reachedHalfTrack && crossedStartLine)
        {
            completedLaps++;
            reachedHalfTrack = false;
            if (completedLaps >= settings.laps)
            {
                finished = true;
            }
        }

        lastNodeIndex = stableNodeIndex;
        previousPosition = transform.position;
    }

    private void CacheStartLine()
    {
        hasStartLine = false;
        if (grid == null || grid.Nodes.Count < 2)
        {
            return;
        }

        startLinePoint = grid.Nodes[0].position;
        startLineDirection = (grid.Nodes[1].position - grid.Nodes[0].position).normalized;
        startLineDirection.y = 0f;
        if (startLineDirection.sqrMagnitude < 0.01f)
        {
            return;
        }

        hasStartLine = true;
    }

    private bool CrossedStartLine()
    {
        if (!hasStartLine)
        {
            return stableNodeIndex <= 1 && lastNodeIndex >= grid.Nodes.Count - 3;
        }

        var previous = previousPosition - startLinePoint;
        var current = transform.position - startLinePoint;
        previous.y = 0f;
        current.y = 0f;

        var previousSide = Vector3.Dot(previous, startLineDirection);
        var currentSide = Vector3.Dot(current, startLineDirection);
        return previousSide < 0f && currentSide >= 0f;
    }

    private bool IsPastStartLine(Vector3 position)
    {
        if (!hasStartLine)
        {
            return true;
        }

        var offset = position - startLinePoint;
        offset.y = 0f;
        return Vector3.Dot(offset, startLineDirection) >= 0f;
    }

    private int GetCurrentNodeIndex()
    {
        if (grid == null || grid.Nodes.Count == 0)
        {
            return 0;
        }

        var closest = grid.FindClosestNode(transform.position);
        return grid.IndexOf(closest);
    }
}
