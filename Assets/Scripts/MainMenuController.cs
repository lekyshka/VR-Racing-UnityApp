using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private RaceSettings settings;
    [SerializeField] private RaceGameManager gameManager;
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private Button lapsMinusButton;
    [SerializeField] private Button lapsPlusButton;
    [SerializeField] private Button opponentsMinusButton;
    [SerializeField] private Button opponentsPlusButton;
    [SerializeField] private Button modeButton;
    [SerializeField] private Button volumeMinusButton;
    [SerializeField] private Button volumePlusButton;
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_Text lapsLabel;
    [SerializeField] private TMP_Text opponentsLabel;
    [SerializeField] private TMP_Text modeLabel;
    [SerializeField] private TMP_Text volumeLabel;

    private static readonly string[] Modes = { "Arcade", "Comfort", "VR" };

    private void Start()
    {
        if (settings == null)
        {
            return;
        }

        settings.laps = Mathf.Clamp(settings.laps, 1, 5);
        settings.opponents = Mathf.Clamp(settings.opponents, 1, 5);
        settings.driveMode = Mathf.Clamp(settings.driveMode, 0, Modes.Length - 1);
        settings.audioVolume = Mathf.Clamp01(settings.audioVolume);

        lapsMinusButton.onClick.AddListener(() => ChangeLaps(-1));
        lapsPlusButton.onClick.AddListener(() => ChangeLaps(1));
        opponentsMinusButton.onClick.AddListener(() => ChangeOpponents(-1));
        opponentsPlusButton.onClick.AddListener(() => ChangeOpponents(1));
        modeButton.onClick.AddListener(CycleMode);
        volumeMinusButton.onClick.AddListener(() => ChangeVolume(-0.1f));
        volumePlusButton.onClick.AddListener(() => ChangeVolume(0.1f));
        if (startButton != null)
        {
            startButton.onClick.AddListener(StartRace);
        }
        ApplySettings();
    }

    public void ApplySettings()
    {
        if (settings == null)
        {
            return;
        }

        AudioListener.volume = settings.audioVolume;

        if (lapsLabel != null)
        {
            lapsLabel.text = $"Laps: {settings.laps}";
        }

        if (opponentsLabel != null)
        {
            opponentsLabel.text = $"Opponents: {settings.opponents}";
        }

        if (modeLabel != null)
        {
            modeLabel.text = $"Mode: {Modes[settings.driveMode]}";
        }

        if (volumeLabel != null)
        {
            volumeLabel.text = $"Engine Volume: {Mathf.RoundToInt(settings.audioVolume * 100f)}%";
        }
    }

    public void StartRace()
    {
        ApplySettings();
        if (menuRoot != null)
        {
            menuRoot.SetActive(false);
        }

        gameManager.StartRace();
    }

    private void ChangeLaps(int delta)
    {
        settings.laps = Mathf.Clamp(settings.laps + delta, 1, 5);
        ApplySettings();
    }

    private void ChangeOpponents(int delta)
    {
        settings.opponents = Mathf.Clamp(settings.opponents + delta, 1, 5);
        ApplySettings();
    }

    private void CycleMode()
    {
        settings.driveMode = (settings.driveMode + 1) % Modes.Length;
        ApplySettings();
    }

    private void ChangeVolume(float delta)
    {
        settings.audioVolume = Mathf.Clamp01(settings.audioVolume + delta);
        ApplySettings();
    }
}
