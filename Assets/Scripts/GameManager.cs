using System.Collections;
using System.Collections.Generic;
using System.Text;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public static bool IsMatchInProgress =>
        Instance && Instance.CurrentState.Value == GameState.InProgress;

    [SerializeField] private int requiredPlayers = 2;
    [SerializeField] private float countdownSeconds = 3f;
    [SerializeField] private float resultsDelaySeconds = 3f;

    public int RequiredPlayersForUi => requiredPlayers;

    public readonly SyncVar<GameState> CurrentState =
        new(GameState.WaitingForPlayers, new SyncTypeSettings(0.5f));

    public readonly SyncVar<int> ConnectedPlayers = new(0, new SyncTypeSettings(0.25f));

    public readonly SyncVar<float> CountdownTimer = new(0f, new SyncTypeSettings(0.1f));

    public readonly SyncVar<float> RaceElapsedTime = new(0f, new SyncTypeSettings(0.1f));

    public readonly SyncVar<string> ResultsText = new(string.Empty, new SyncTypeSettings(0.25f));

    private Coroutine _countdownCoroutine;
    private Coroutine _resultsCoroutine;
    private float _recountCooldown;
    private float _raceStartTime;
    private readonly HashSet<int> _crossedStartOwnerIds = new();
    private readonly HashSet<int> _finishedOwnerIds = new();
    private readonly List<FinishEntry> _finishOrder = new();

    private struct FinishEntry
    {
        public int OwnerId;
        public float TimeSeconds;
    }

    public enum GameState
    {
        WaitingForPlayers,
        WaitingForDrivers,
        Countdown,
        InProgress,
        Finished
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        Instance = this;
    }

    public override void OnStopNetwork()
    {
        if (Instance == this)
            Instance = null;

        base.OnStopNetwork();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        ServerManager.OnAuthenticationResult += OnAuthenticationResult;
        NetworkManager.ClientManager.OnClientConnectionState += OnLocalClientConnectionState;

        RecountConnectedPlayers();
        TryAdvanceFromLobby();
    }

    public override void OnStopServer()
    {
        ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        ServerManager.OnAuthenticationResult -= OnAuthenticationResult;
        NetworkManager.ClientManager.OnClientConnectionState -= OnLocalClientConnectionState;

        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }

        if (_resultsCoroutine != null)
        {
            StopCoroutine(_resultsCoroutine);
            _resultsCoroutine = null;
        }

        base.OnStopServer();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (!IsServerInitialized)
            return;

        RecountConnectedPlayers();
        TryAdvanceFromLobby();
        EvaluateDriverReadiness();
    }

    private void OnLocalClientConnectionState(ClientConnectionStateArgs args)
    {
        if (!IsServerInitialized)
            return;

        RecountConnectedPlayers();
        TryAdvanceFromLobby();
        EvaluateDriverReadiness();
    }

    private void OnAuthenticationResult(NetworkConnection conn, bool authenticated)
    {
        if (!IsServerInitialized)
            return;

        RecountConnectedPlayers();
        TryAdvanceFromLobby();
        EvaluateDriverReadiness();
    }

    private void Update()
    {
        if (!IsServerInitialized)
            return;

        if (CurrentState.Value == GameState.WaitingForPlayers)
        {
            _recountCooldown -= Time.unscaledDeltaTime;
            if (_recountCooldown <= 0f)
            {
                _recountCooldown = 0.25f;
                RecountConnectedPlayers();
            }
        }

        if (CurrentState.Value == GameState.InProgress)
            RaceElapsedTime.Value = Time.time - _raceStartTime;
    }

    public void NotifyDriverStateChanged()
    {
        if (!IsServerInitialized)
            return;

        EvaluateDriverReadiness();
    }

    public void RegisterStartCross(NetworkVehicle vehicle)
    {
        if (!IsServerInitialized || CurrentState.Value != GameState.InProgress)
            return;

        if (!vehicle || !vehicle.HasDriver)
            return;

        _crossedStartOwnerIds.Add(vehicle.DriverOwnerId.Value);
    }

    public void RegisterFinish(NetworkVehicle vehicle)
    {
        if (!IsServerInitialized || CurrentState.Value != GameState.InProgress)
            return;

        if (!vehicle || !vehicle.HasDriver)
            return;

        int ownerId = vehicle.DriverOwnerId.Value;
        if (!_crossedStartOwnerIds.Contains(ownerId))
            return;

        if (_finishedOwnerIds.Contains(ownerId))
            return;

        float finishTime = Time.time - _raceStartTime;
        _finishedOwnerIds.Add(ownerId);
        _finishOrder.Add(new FinishEntry
        {
            OwnerId = ownerId,
            TimeSeconds = finishTime
        });

        if (_finishOrder.Count == 1)
            _resultsCoroutine = StartCoroutine(ShowResultsAfterDelay());
    }

    private void RecountConnectedPlayers()
    {
        int count = ServerManager.Clients.Count;
        if (NetworkManager.IsHostStarted)
            count++;

        ConnectedPlayers.Value = count;
    }

    private void TryAdvanceFromLobby()
    {
        if (CurrentState.Value != GameState.WaitingForPlayers)
            return;

        if (ConnectedPlayers.Value < requiredPlayers)
            return;

        CurrentState.Value = GameState.WaitingForDrivers;
    }

    private void EvaluateDriverReadiness()
    {
        if (CurrentState.Value == GameState.InProgress || CurrentState.Value == GameState.Finished)
            return;

        if (ConnectedPlayers.Value < requiredPlayers)
        {
            CancelCountdown();
            CurrentState.Value = GameState.WaitingForPlayers;
            return;
        }

        if (CountPlayersInVehicles() < requiredPlayers)
        {
            CancelCountdown();
            if (CurrentState.Value == GameState.Countdown)
                CurrentState.Value = GameState.WaitingForDrivers;
            return;
        }

        if (CurrentState.Value == GameState.WaitingForDrivers)
            BeginCountdown();
    }

    private int CountPlayersInVehicles()
    {
        int count = 0;
        PlayerVehicleInteraction[] interactions =
            FindObjectsByType<PlayerVehicleInteraction>(FindObjectsSortMode.None);

        foreach (PlayerVehicleInteraction interaction in interactions)
        {
            if (interaction.IsInVehicle)
                count++;
        }

        return count;
    }

    private void BeginCountdown()
    {
        if (_countdownCoroutine != null)
            return;

        CurrentState.Value = GameState.Countdown;
        _countdownCoroutine = StartCoroutine(CountdownRoutine());
    }

    private void CancelCountdown()
    {
        if (_countdownCoroutine == null)
            return;

        StopCoroutine(_countdownCoroutine);
        _countdownCoroutine = null;
        CountdownTimer.Value = 0f;
    }

    private IEnumerator CountdownRoutine()
    {
        float remaining = countdownSeconds;
        while (remaining > 0f)
        {
            CountdownTimer.Value = remaining;
            yield return null;
            remaining -= Time.deltaTime;
        }

        CountdownTimer.Value = 0f;
        _countdownCoroutine = null;
        BeginRace();
    }

    private void BeginRace()
    {
        _crossedStartOwnerIds.Clear();
        _finishedOwnerIds.Clear();
        _finishOrder.Clear();
        ResultsText.Value = string.Empty;
        RaceElapsedTime.Value = 0f;
        _raceStartTime = Time.time;
        CurrentState.Value = GameState.InProgress;
        RefreshAllVehicleInputs();
    }

    private IEnumerator ShowResultsAfterDelay()
    {
        yield return new WaitForSeconds(resultsDelaySeconds);
        _resultsCoroutine = null;
        ResultsText.Value = BuildResultsText();
        CurrentState.Value = GameState.Finished;
        RefreshAllVehicleInputs();
    }

    private string BuildResultsText()
    {
        StringBuilder builder = new();
        builder.AppendLine("Результаты матча");
        builder.AppendLine();

        for (int i = 0; i < _finishOrder.Count; i++)
        {
            FinishEntry entry = _finishOrder[i];
            builder.AppendLine($"{i + 1}. {GetNicknameForOwner(entry.OwnerId)} — {FormatRaceTime(entry.TimeSeconds)}");
        }

        HashSet<int> listedOwners = new();
        foreach (FinishEntry entry in _finishOrder)
            listedOwners.Add(entry.OwnerId);

        PlayerVehicleInteraction[] racers =
            FindObjectsByType<PlayerVehicleInteraction>(FindObjectsSortMode.None);

        foreach (PlayerVehicleInteraction racer in racers)
        {
            if (listedOwners.Contains(racer.OwnerId))
                continue;

            int place = _finishOrder.Count + 1;
            builder.AppendLine($"{place}. {GetNicknameForOwner(racer.OwnerId)} — не финишировал");
            listedOwners.Add(racer.OwnerId);
        }

        return builder.ToString().TrimEnd();
    }

    private static string GetNicknameForOwner(int ownerId)
    {
        PlayerNetwork[] players = FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None);
        foreach (PlayerNetwork player in players)
        {
            if (player.OwnerId == ownerId)
                return player.Nickname.Value;
        }

        return $"Player_{ownerId}";
    }

    public static string FormatRaceTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float remainingSeconds = seconds % 60f;

        if (minutes > 0)
            return $"{minutes}:{remainingSeconds:00.0} с";

        return $"{remainingSeconds:0.0} с";
    }

    private static void RefreshAllVehicleInputs()
    {
        NetworkVehicle[] vehicles = FindObjectsByType<NetworkVehicle>(FindObjectsSortMode.None);
        foreach (NetworkVehicle vehicle in vehicles)
            vehicle.RefreshDriverState();
    }
}
