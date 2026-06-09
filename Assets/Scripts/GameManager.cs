using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        WaitingForPlayers,
        Lobby,
        Countdown,
        InProgress,
        ShowingResults
    }

    public static bool IsMatchInProgress =>
        Instance != null && Instance.CurrentState.Value == GameState.InProgress;

    public static bool CanPlayerMove =>
        Instance != null && Instance.CurrentState.Value is GameState.Lobby
            or GameState.Countdown or GameState.InProgress;

    public static bool CanDriveVehicle =>
        Instance != null && Instance.CurrentState.Value == GameState.InProgress;

    [SerializeField] private int requiredPlayers = 2;
    [SerializeField] private float countdownSeconds = 3f;
    [SerializeField] private float finishDelaySeconds = 3f;
    [SerializeField] private float resultsScreenSeconds = 5f;

    public int RequiredPlayersForUi => requiredPlayers;

    public readonly SyncVar<GameState> CurrentState = new(GameState.WaitingForPlayers);
    public readonly SyncVar<int> ConnectedPlayers = new(0);
    public readonly SyncVar<float> CountdownRemaining = new(0f);
    public readonly SyncVar<float> RaceElapsedTime = new(0f);
    public readonly SyncVar<float> FinishCountdownRemaining = new(0f);
    public readonly SyncVar<string> ResultsText = new("");
    public readonly SyncVar<string> StatusMessage = new("");

    private readonly Dictionary<NetworkVehicle, float> _raceStartTimes = new();
    private readonly List<FinishData> _finishers = new();
    private readonly HashSet<NetworkVehicle> _finishedVehicles = new();

    private Coroutine _countdownCoroutine;
    private Coroutine _finishDelayCoroutine;
    private Coroutine _resetCoroutine;
    private float _raceStartServerTime;

    private struct FinishData
    {
        public string Name;
        public float TimeTaken;
    }

    public override void OnStartNetwork() => Instance = this;

    public override void OnStartServer()
    {
        ServerManager.OnRemoteConnectionState += (_, _) => { Recount(); UpdateLobbyState(); };
        NetworkManager.ClientManager.OnClientConnectionState += (_) => { Recount(); UpdateLobbyState(); };
        Recount();
        UpdateLobbyState();
    }

    private void Update()
    {
        if (!IsServerStarted) return;
        if (CurrentState.Value != GameState.InProgress) return;

        RaceElapsedTime.Value = Time.time - _raceStartServerTime;
    }

    private void Recount() =>
        ConnectedPlayers.Value = ServerManager.Clients.Count + (NetworkManager.IsHostStarted ? 1 : 0);

    private void UpdateLobbyState()
    {
        if (CurrentState.Value is GameState.ShowingResults or GameState.Countdown or GameState.InProgress)
            return;

        if (ConnectedPlayers.Value >= requiredPlayers)
        {
            if (CurrentState.Value != GameState.Lobby)
            {
                CurrentState.Value = GameState.Lobby;
                StatusMessage.Value = "\u0421\u0430\u0434\u0438\u0442\u0435\u0441\u044c \u0432 \u043c\u0430\u0448\u0438\u043d\u044b (E)";
            }
        }
        else
        {
            CurrentState.Value = GameState.WaitingForPlayers;
            StatusMessage.Value = $"\u041e\u0436\u0438\u0434\u0430\u043d\u0438\u0435 \u0438\u0433\u0440\u043e\u043a\u043e\u0432: {ConnectedPlayers.Value}/{requiredPlayers}";
        }
    }

    [Server]
    public void OnVehicleDriverChanged()
    {
        if (CurrentState.Value == GameState.Lobby)
            TryBeginCountdown();
        else if (CurrentState.Value == GameState.Countdown && !AllRequiredPlayersInCars())
            CancelCountdown();
    }

    [Server]
    private void TryBeginCountdown()
    {
        if (!AllRequiredPlayersInCars()) return;
        if (_countdownCoroutine != null) return;

        _countdownCoroutine = StartCoroutine(CountdownRoutine());
    }

    [Server]
    private void CancelCountdown()
    {
        if (_countdownCoroutine == null) return;

        StopCoroutine(_countdownCoroutine);
        _countdownCoroutine = null;
        CountdownRemaining.Value = 0f;
        CurrentState.Value = GameState.Lobby;
        StatusMessage.Value = "\u0421\u0430\u0434\u0438\u0442\u0435\u0441\u044c \u0432 \u043c\u0430\u0448\u0438\u043d\u044b (E)";
    }

    [Server]
    private IEnumerator CountdownRoutine()
    {
        CurrentState.Value = GameState.Countdown;
        float remaining = countdownSeconds;

        while (remaining > 0f)
        {
            if (!AllRequiredPlayersInCars())
            {
                _countdownCoroutine = null;
                CancelCountdown();
                yield break;
            }

            CountdownRemaining.Value = remaining;
            StatusMessage.Value = $"\u0421\u0442\u0430\u0440\u0442 \u0447\u0435\u0440\u0435\u0437 {Mathf.CeilToInt(remaining)}...";
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        CountdownRemaining.Value = 0f;
        _countdownCoroutine = null;
        StartRace();
    }

    [Server]
    private void StartRace()
    {
        _raceStartTimes.Clear();
        _finishers.Clear();
        _finishedVehicles.Clear();
        FinishCountdownRemaining.Value = 0f;
        _raceStartServerTime = Time.time;
        RaceElapsedTime.Value = 0f;
        CurrentState.Value = GameState.InProgress;
        StatusMessage.Value = "\u0413\u043e\u043d\u043a\u0430!";

        foreach (NetworkVehicle vehicle in FindObjectsByType<NetworkVehicle>(FindObjectsSortMode.None))
        {
            if (!vehicle.HasDriver) continue;
            _raceStartTimes[vehicle] = _raceStartServerTime;
        }
    }

    [Server]
    public void RegisterFinish(NetworkVehicle vehicle)
    {
        if (CurrentState.Value != GameState.InProgress) return;
        if (!vehicle || !vehicle.HasDriver) return;
        if (_finishedVehicles.Contains(vehicle)) return;
        if (!_raceStartTimes.TryGetValue(vehicle, out float startTime)) return;

        _finishedVehicles.Add(vehicle);
        _finishers.Add(new FinishData
        {
            Name = GetDriverName(vehicle),
            TimeTaken = Time.time - startTime
        });

        if (_finishDelayCoroutine == null)
            _finishDelayCoroutine = StartCoroutine(FinishDelayRoutine());
    }

    [Server]
    private IEnumerator FinishDelayRoutine()
    {
        float remaining = finishDelaySeconds;
        while (remaining > 0f)
        {
            FinishCountdownRemaining.Value = remaining;
            StatusMessage.Value = $"\u0418\u0442\u043e\u0433\u0438 \u0447\u0435\u0440\u0435\u0437 {Mathf.CeilToInt(remaining)}...";
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        FinishCountdownRemaining.Value = 0f;
        _finishDelayCoroutine = null;
        EndMatch();
    }

    [Server]
    private void EndMatch()
    {
        CurrentState.Value = GameState.ShowingResults;

        var sorted = _finishers.OrderBy(f => f.TimeTaken).ToList();
        StringBuilder sb = new StringBuilder("\u0418\u0422\u041e\u0413\u0418 \u0413\u041e\u041d\u041A\u0418:\n\n");
        for (int i = 0; i < sorted.Count; i++)
            sb.AppendLine($"{i + 1}. {sorted[i].Name} - {sorted[i].TimeTaken:F2} \u0441\u0435\u043a");

        if (sorted.Count == 0)
            sb.AppendLine("\u041d\u0438\u043a\u0442\u043e \u043d\u0435 \u0444\u0438\u043d\u0438\u0448\u0438\u0440\u043e\u0432\u0430\u043b");

        ResultsText.Value = sb.ToString();
        StatusMessage.Value = "\u0413\u043e\u043d\u043a\u0430 \u0437\u0430\u0432\u0435\u0440\u0448\u0435\u043d\u0430";

        if (_resetCoroutine != null)
            StopCoroutine(_resetCoroutine);
        _resetCoroutine = StartCoroutine(ResetAfterResultsRoutine());
    }

    [Server]
    private IEnumerator ResetAfterResultsRoutine()
    {
        yield return new WaitForSeconds(resultsScreenSeconds);

        RaceResetManager resetManager = RaceResetManager.Instance;
        if (resetManager)
            resetManager.ResetRace();

        _raceStartTimes.Clear();
        _finishers.Clear();
        _finishedVehicles.Clear();
        ResultsText.Value = "";
        RaceElapsedTime.Value = 0f;
        FinishCountdownRemaining.Value = 0f;
        CountdownRemaining.Value = 0f;
        _resetCoroutine = null;

        CurrentState.Value = ConnectedPlayers.Value >= requiredPlayers
            ? GameState.Lobby
            : GameState.WaitingForPlayers;
        UpdateLobbyState();
        if (CurrentState.Value == GameState.Lobby)
            StatusMessage.Value = "\u0421\u0430\u0434\u0438\u0442\u0435\u0441\u044c \u0432 \u043c\u0430\u0448\u0438\u043d\u044b (E)";
    }

    [Server]
    private bool AllRequiredPlayersInCars()
    {
        NetworkVehicle[] vehicles = FindObjectsByType<NetworkVehicle>(FindObjectsSortMode.None);
        int drivers = vehicles.Count(v => v.HasDriver);
        return drivers >= requiredPlayers;
    }

    [Server]
    private string GetDriverName(NetworkVehicle vehicle)
    {
        if (!vehicle.HasDriver)
            return "\u041d\u0435\u0438\u0437\u0432\u0435\u0441\u0442\u043d\u043e";

        foreach (PlayerNetwork player in FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None))
        {
            if (player.OwnerId == vehicle.DriverOwnerId.Value)
                return player.Nickname.Value;
        }

        return $"\u0418\u0433\u0440\u043e\u043a {vehicle.DriverOwnerId.Value}";
    }

    public static string FormatRaceTime(float time) =>
        string.Format("{0:00}:{1:00.00}", (int)time / 60, time % 60);
}
