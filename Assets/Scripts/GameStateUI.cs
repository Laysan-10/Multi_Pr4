using TMPro;
using UnityEngine;

public class GameStateUI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject waitingRoot;
    [SerializeField] private GameObject resultsRoot;
    [SerializeField] private TMP_Text waitingText;
    [SerializeField] private TMP_Text matchTimerText;
    [SerializeField] private TMP_Text resultsText;

    private bool _bound;
    private bool _hasConnected;

    private void Start()
    {
        if (!gameManager)
            gameManager = GameManager.Instance;

        if (!gameManager)
            gameManager = FindFirstObjectByType<GameManager>();

        _hasConnected = ConnectionUI.HasConnected;

        ConnectionUI.OnConnected += HandleConnected;
        Bind();
        RefreshAll();
    }

    private void OnDestroy()
    {
        ConnectionUI.OnConnected -= HandleConnected;
        Unbind();
    }

    private void HandleConnected()
    {
        _hasConnected = true;
        RefreshAll();
    }

    private void Bind()
    {
        if (_bound || !gameManager)
            return;

        gameManager.CurrentState.OnChange += OnStateChanged;
        gameManager.ConnectedPlayers.OnChange += OnConnectedPlayersChanged;
        gameManager.CountdownTimer.OnChange += OnCountdownChanged;
        gameManager.RaceElapsedTime.OnChange += OnRaceElapsedChanged;
        gameManager.ResultsText.OnChange += OnResultsChanged;
        _bound = true;
    }

    private void Unbind()
    {
        if (!_bound || !gameManager)
            return;

        gameManager.CurrentState.OnChange -= OnStateChanged;
        gameManager.ConnectedPlayers.OnChange -= OnConnectedPlayersChanged;
        gameManager.CountdownTimer.OnChange -= OnCountdownChanged;
        gameManager.RaceElapsedTime.OnChange -= OnRaceElapsedChanged;
        gameManager.ResultsText.OnChange -= OnResultsChanged;
        _bound = false;
    }

    private void OnStateChanged(GameManager.GameState prev, GameManager.GameState next, bool asServer)
    {
        RefreshAll();
    }

    private void OnConnectedPlayersChanged(int prev, int next, bool asServer)
    {
        RefreshLobbyText();
    }

    private void OnCountdownChanged(float prev, float next, bool asServer)
    {
        RefreshCountdown();
    }

    private void OnRaceElapsedChanged(float prev, float next, bool asServer)
    {
        RefreshRaceTimer();
    }

    private void OnResultsChanged(string prev, string next, bool asServer)
    {
        RefreshResults();
    }

    private void RefreshAll()
    {
        if (!gameManager)
            return;

        GameManager.GameState state = gameManager.CurrentState.Value;
        bool showLobby = _hasConnected && state != GameManager.GameState.Finished;

        if (waitingRoot)
            waitingRoot.SetActive(showLobby && state != GameManager.GameState.Countdown);

        if (resultsRoot)
            resultsRoot.SetActive(state == GameManager.GameState.Finished);

        if (matchTimerText)
            matchTimerText.transform.parent.gameObject.SetActive(
                _hasConnected &&
                (state == GameManager.GameState.Countdown ||
                 state == GameManager.GameState.InProgress));

        RefreshLobbyText();
        RefreshCountdown();
        RefreshRaceTimer();
        RefreshResults();
    }

    private void RefreshLobbyText()
    {
        if (!waitingText || !gameManager || !_hasConnected)
            return;

        GameManager.GameState state = gameManager.CurrentState.Value;
        if (state == GameManager.GameState.WaitingForPlayers)
        {
            waitingText.text =
                $"Ожидание игроков: {gameManager.ConnectedPlayers.Value}/{gameManager.RequiredPlayersForUi}";
        }
        else if (state == GameManager.GameState.WaitingForDrivers)
        {
            waitingText.text = "Садитесь в машины";
        }
    }

    private void RefreshCountdown()
    {
        if (!matchTimerText || !gameManager)
            return;

        if (gameManager.CurrentState.Value != GameManager.GameState.Countdown)
            return;

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, gameManager.CountdownTimer.Value));
        matchTimerText.text = seconds > 0 ? seconds.ToString() : "Старт!";
    }

    private void RefreshRaceTimer()
    {
        if (!matchTimerText || !gameManager)
            return;

        if (gameManager.CurrentState.Value != GameManager.GameState.InProgress)
            return;

        matchTimerText.text = GameManager.FormatRaceTime(gameManager.RaceElapsedTime.Value);
    }

    private void RefreshResults()
    {
        if (!resultsText || !gameManager)
            return;

        if (gameManager.CurrentState.Value != GameManager.GameState.Finished)
        {
            resultsText.text = string.Empty;
            return;
        }

        resultsText.text = gameManager.ResultsText.Value;
    }
}
