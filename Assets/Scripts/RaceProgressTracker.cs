using UnityEngine;

public sealed class RaceProgressTracker : MonoBehaviour
{
    [SerializeField] private AStarGrid grid;
    [SerializeField] private RaceSettings settings;
    [SerializeField] private string racerName = "Racer";

    private int completedLaps;
    private int lastNodeIndex;
    private bool reachedHalfTrack;
    private bool finished;

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
        lastNodeIndex = 0;
        reachedHalfTrack = false;
        finished = false;
    }

    private void Update()
    {
        if (grid == null || settings == null || grid.Nodes.Count == 0 || finished)
        {
            return;
        }

        var closest = grid.FindClosestNode(transform.position);
        var index = grid.IndexOf(closest);
        if (index < 0)
        {
            return;
        }

        if (index >= grid.Nodes.Count / 2)
        {
            reachedHalfTrack = true;
        }

        if (reachedHalfTrack && lastNodeIndex >= grid.Nodes.Count - 2 && index <= 1)
        {
            completedLaps++;
            reachedHalfTrack = false;
            if (completedLaps >= settings.laps)
            {
                finished = true;
            }
        }

        lastNodeIndex = index;
    }
}
