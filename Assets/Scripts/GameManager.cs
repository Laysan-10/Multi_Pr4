using System.Collections;
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

    public int RequiredPlayersForUi => requiredPlayers;

    public readonly SyncVar<GameState> CurrentState =
        new(GameState.WaitingForPlayers, new SyncTypeSettings(0.5f));

    public readonly SyncVar<int> ConnectedPlayers = new(0, new SyncTypeSettings(0.25f));

    public readonly SyncVar<float> CountdownTimer = new(0f, new SyncTypeSettings(0.1f));

    private Coroutine _countdownCoroutine;
    private float _recountCooldown;

    public enum GameState
    {
        WaitingForPlayers,
        WaitingForDrivers,
        Countdown,
        InProgress
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
    }

    public void NotifyDriverStateChanged()
    {
        if (!IsServerInitialized)
            return;

        EvaluateDriverReadiness();
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
        if (CurrentState.Value == GameState.InProgress)
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
        CurrentState.Value = GameState.InProgress;
        RefreshAllVehicleInputs();
    }

    private static void RefreshAllVehicleInputs()
    {
        NetworkVehicle[] vehicles = FindObjectsByType<NetworkVehicle>(FindObjectsSortMode.None);
        foreach (NetworkVehicle vehicle in vehicles)
            vehicle.RefreshDriverState();
    }
}
