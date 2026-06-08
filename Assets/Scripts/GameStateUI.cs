using TMPro;
using UnityEngine;

public class GameStateUI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject waitingRoot;
    [SerializeField] private GameObject resultsRoot;
    [SerializeField] private GameObject matchTimerRoot;
    [SerializeField] private GameObject countdownRoot;
    [SerializeField] private TMP_Text waitingText;
    [SerializeField] private TMP_Text countdownText;

    private bool _bound;
    private bool _hideLobbyUi;

    private void Start()
    {
        if (!gameManager)
            gameManager = GameManager.Instance;

        if (!gameManager)
            gameManager = FindFirstObjectByType<GameManager>();

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
        _hideLobbyUi = true;
        RefreshAll();
    }

    private void Bind()
    {
        if (_bound || !gameManager)
            return;

        gameManager.CurrentState.OnChange += OnStateChanged;
        gameManager.ConnectedPlayers.OnChange += OnConnectedPlayersChanged;
        gameManager.CountdownTimer.OnChange += OnCountdownChanged;
        _bound = true;
    }

    private void Unbind()
    {
        if (!_bound || !gameManager)
            return;

        gameManager.CurrentState.OnChange -= OnStateChanged;
        gameManager.ConnectedPlayers.OnChange -= OnConnectedPlayersChanged;
        gameManager.CountdownTimer.OnChange -= OnCountdownChanged;
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

    private void RefreshAll()
    {
        if (!gameManager)
            return;

        bool showLobby = !_hideLobbyUi;
        GameManager.GameState state = gameManager.CurrentState.Value;

        if (waitingRoot)
            waitingRoot.SetActive(showLobby && state != GameManager.GameState.Countdown);

        if (resultsRoot)
            resultsRoot.SetActive(false);

        if (matchTimerRoot)
            matchTimerRoot.SetActive(false);

        if (countdownRoot)
            countdownRoot.SetActive(state == GameManager.GameState.Countdown);

        RefreshLobbyText();
        RefreshCountdown();
    }

    private void RefreshLobbyText()
    {
        if (!waitingText || !gameManager || _hideLobbyUi)
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
        if (!countdownText || !gameManager)
            return;

        if (gameManager.CurrentState.Value != GameManager.GameState.Countdown)
        {
            countdownText.text = string.Empty;
            return;
        }

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, gameManager.CountdownTimer.Value));
        countdownText.text = seconds > 0 ? seconds.ToString() : "Старт!";
    }
}
