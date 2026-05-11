using TMPro;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.UI;

public sealed class RaceGameManager : MonoBehaviour
{
    [SerializeField] private RaceSettings settings;
    [SerializeField] private TMP_Text hudLabel;
    [SerializeField] private GameObject[] opponents;
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private PlayerCarController playerController;
    [SerializeField] private LapTracker lapTracker;
    [SerializeField] private RaceProgressTracker playerProgress;
    [SerializeField] private RaceProgressTracker[] participants;
    [SerializeField] private Button restartButton;

    private float raceTime;
    private bool raceStarted;
    private bool playerFinished;
    private int finalPlayerPlace;
    private readonly List<RaceProgressTracker> finishOrder = new List<RaceProgressTracker>();
    private readonly Dictionary<RaceProgressTracker, Pose> startingPoses = new Dictionary<RaceProgressTracker, Pose>();

    private void Start()
    {
        CacheStartingPoses();
        ApplyOpponentVisibility(false);
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(ShowMenuAndReset);
            restartButton.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!raceStarted && Input.GetKeyDown(KeyCode.Space))
        {
            StartRace();
        }

        if (!raceStarted)
        {
            return;
        }

        UpdateFinishOrder();
        if (!playerFinished)
        {
            raceTime += Time.deltaTime;
        }

        if (playerProgress != null && playerProgress.Finished && !playerFinished)
        {
            playerFinished = true;
            finalPlayerPlace = GetPlayerPlace();
        }

        if (hudLabel != null && settings != null)
        {
            var place = playerFinished ? finalPlayerPlace : GetPlayerPlace();
            var lap = playerProgress != null ? playerProgress.CurrentLap : 1;
            var finishText = playerFinished ? " | Finished" : string.Empty;
            hudLabel.text = $"Lap: {lap}/{settings.laps} | Place: {place}/{settings.opponents + 1} | Time: {raceTime:0.0}{finishText}";
        }
    }

    public void StartRace()
    {
        if (settings == null)
        {
            raceStarted = true;
            return;
        }

        ApplyOpponentVisibility(true);
        if (playerController != null)
        {
            playerController.Configure(settings);
        }

        if (lapTracker != null)
        {
            lapTracker.ResetRace();
        }

        foreach (var participant in participants)
        {
            if (participant != null)
            {
                participant.ResetRace();
            }
        }

        finishOrder.Clear();
        playerFinished = false;
        finalPlayerPlace = 0;
        raceStarted = true;
        raceTime = 0f;

        if (menuRoot != null)
        {
            menuRoot.SetActive(false);
        }

        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(true);
        }
    }

    public void ShowMenuAndReset()
    {
        raceStarted = false;
        playerFinished = false;
        finalPlayerPlace = 0;
        raceTime = 0f;
        finishOrder.Clear();

        foreach (var participant in participants)
        {
            if (participant == null)
            {
                continue;
            }

            participant.ResetRace();
            if (startingPoses.TryGetValue(participant, out var pose))
            {
                var participantTransform = participant.transform;
                participantTransform.SetPositionAndRotation(pose.position, pose.rotation);
                var body = participant.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                var driver = participant.GetComponent<RacingAIDriver>();
                if (driver != null)
                {
                    driver.ResetDriver();
                }
            }
        }

        ApplyOpponentVisibility(false);
        if (menuRoot != null)
        {
            menuRoot.SetActive(true);
        }

        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(false);
        }

        if (hudLabel != null)
        {
            hudLabel.text = "Press Start Race";
        }
    }

    private int GetPlayerPlace()
    {
        if (playerProgress == null || participants == null || participants.Length == 0)
        {
            return 1;
        }

        var activeParticipants = participants
            .Where(participant => participant != null && participant.gameObject.activeInHierarchy)
            .ToArray();

        var finishedBeforePlayer = finishOrder.Count(participant => participant != playerProgress);
        if (finishOrder.Contains(playerProgress))
        {
            return finishOrder.IndexOf(playerProgress) + 1;
        }

        var unfinishedRank = activeParticipants
            .Where(participant => participant != null && !participant.Finished)
            .OrderByDescending(participant => participant.TotalProgress)
            .ToArray();

        for (var i = 0; i < unfinishedRank.Length; i++)
        {
            if (unfinishedRank[i] == playerProgress)
            {
                return finishedBeforePlayer + i + 1;
            }
        }

        return 1;
    }

    private void UpdateFinishOrder()
    {
        if (participants == null)
        {
            return;
        }

        foreach (var participant in participants)
        {
            if (participant != null && participant.gameObject.activeInHierarchy && participant.Finished && !finishOrder.Contains(participant))
            {
                finishOrder.Add(participant);
            }
        }
    }

    private void CacheStartingPoses()
    {
        if (participants == null)
        {
            return;
        }

        foreach (var participant in participants)
        {
            if (participant != null && !startingPoses.ContainsKey(participant))
            {
                startingPoses.Add(participant, new Pose(participant.transform.position, participant.transform.rotation));
            }
        }
    }

    private void ApplyOpponentVisibility(bool raceActive)
    {
        for (var i = 0; i < opponents.Length; i++)
        {
            if (opponents[i] != null)
            {
                opponents[i].SetActive(raceActive && settings != null && i < settings.opponents);
            }
        }
    }
}
