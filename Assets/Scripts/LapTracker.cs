using TMPro;
using UnityEngine;

public sealed class LapTracker : MonoBehaviour
{
    [SerializeField] private AStarGrid grid;
    [SerializeField] private RaceSettings settings;
    [SerializeField] private TMP_Text hudLabel;

    private int completedLaps;
    private bool reachedHalfTrack;
    private bool finished;
    private int lastNodeIndex;

    public int CurrentLap => Mathf.Clamp(completedLaps + 1, 1, settings != null ? settings.laps : 1);
    public bool Finished => finished;

    public void Configure(AStarGrid newGrid, RaceSettings raceSettings, TMP_Text hud)
    {
        grid = newGrid;
        settings = raceSettings;
        hudLabel = hud;
    }

    public void ResetRace()
    {
        completedLaps = 0;
        reachedHalfTrack = false;
        finished = false;
        lastNodeIndex = 0;
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

        var halfIndex = grid.Nodes.Count / 2;
        if (index >= halfIndex)
        {
            reachedHalfTrack = true;
        }

        var crossedStart = reachedHalfTrack && lastNodeIndex >= grid.Nodes.Count - 2 && index <= 1;
        if (crossedStart)
        {
            completedLaps++;
            reachedHalfTrack = false;
            if (completedLaps >= settings.laps)
            {
                finished = true;
            }
        }

        lastNodeIndex = index;

        if (finished && hudLabel != null)
        {
            hudLabel.text = $"Finished | Laps: {settings.laps}/{settings.laps}";
        }
    }
}
