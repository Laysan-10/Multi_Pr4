using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameStateUI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject waitingRoot;
    [SerializeField] private GameObject resultsRoot;
    [SerializeField] private TMP_Text matchTimerText;
    [SerializeField] private TMP_Text resultsText;
    [SerializeField] private TMP_Text waitingText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Color resultsBackgroundColor = new(0f, 0f, 0f, 0.65f);
    [SerializeField] private Vector2 resultsBackgroundPadding = new(40f, 30f);

    private bool _hasConnected;
    private Image _resultsBackground;

    private void Start()
    {
        if (!gameManager) gameManager = GameManager.Instance;
        if (!waitingText) waitingText = FindTmpInChildren(waitingRoot, "Waitingtext");
        if (!statusText && matchTimerText) statusText = matchTimerText;
        EnsureResultsBackground();

        _hasConnected = ConnectionUI.HasConnected;
        ConnectionUI.OnConnected += HandleConnected;
        Bind();
        RefreshAll();
    }

    private void Update()
    {
        if (!gameManager || !_hasConnected) return;
        RefreshTimerText();
    }

    private void HandleConnected()
    {
        _hasConnected = true;
        RefreshAll();
    }

    private void Bind()
    {
        if (!gameManager) return;

        gameManager.CurrentState.OnChange += (_, _, _) => RefreshAll();
        gameManager.ResultsText.OnChange += (_, value, _) =>
        {
            if (resultsText) resultsText.text = value;
        };
        gameManager.StatusMessage.OnChange += (_, value, _) => UpdateStatusTexts(value);
        gameManager.ConnectedPlayers.OnChange += (_, _, _) => RefreshAll();
    }

    private void RefreshAll()
    {
        if (!gameManager || !_hasConnected) return;

        GameManager.GameState state = gameManager.CurrentState.Value;

        if (waitingRoot)
            waitingRoot.SetActive(state is GameManager.GameState.WaitingForPlayers
                or GameManager.GameState.Lobby or GameManager.GameState.Countdown);

        if (resultsRoot)
            resultsRoot.SetActive(state == GameManager.GameState.ShowingResults);

        if (_resultsBackground)
            _resultsBackground.gameObject.SetActive(state == GameManager.GameState.ShowingResults);

        if (matchTimerText)
            matchTimerText.gameObject.SetActive(state is GameManager.GameState.Countdown
                or GameManager.GameState.InProgress
                or GameManager.GameState.ShowingResults);

        if (resultsText)
            resultsText.text = gameManager.ResultsText.Value;

        UpdateStatusTexts(gameManager.StatusMessage.Value);
        RefreshTimerText();
    }

    private void RefreshTimerText()
    {
        if (!matchTimerText || !gameManager) return;

        GameManager.GameState state = gameManager.CurrentState.Value;

        if (state == GameManager.GameState.Countdown)
        {
            float seconds = Mathf.CeilToInt(gameManager.CountdownRemaining.Value);
            matchTimerText.text = seconds > 0 ? $"\u0421\u0442\u0430\u0440\u0442: {seconds}" : "\u0421\u0442\u0430\u0440\u0442!";
            return;
        }

        if (state == GameManager.GameState.InProgress)
        {
            if (gameManager.FinishCountdownRemaining.Value > 0f)
            {
                float seconds = Mathf.CeilToInt(gameManager.FinishCountdownRemaining.Value);
                matchTimerText.text = $"\u0418\u0442\u043e\u0433\u0438 \u0447\u0435\u0440\u0435\u0437: {seconds}";
            }
            else
            {
                matchTimerText.text = GameManager.FormatRaceTime(gameManager.RaceElapsedTime.Value);
            }
            return;
        }

        if (state == GameManager.GameState.ShowingResults)
        {
            matchTimerText.text = "\u0418\u0442\u043e\u0433\u0438";
        }
    }

    private void UpdateStatusTexts(string message)
    {
        if (waitingText) waitingText.text = message;
        if (statusText && gameManager)
        {
            GameManager.GameState state = gameManager.CurrentState.Value;
            if (state is GameManager.GameState.WaitingForPlayers
                or GameManager.GameState.Lobby
                or GameManager.GameState.Countdown)
                statusText.text = message;
        }
    }

    private static TMP_Text FindTmpInChildren(GameObject root, string childName)
    {
        if (!root) return null;
        Transform child = root.transform.Find(childName);
        return child ? child.GetComponent<TMP_Text>() : null;
    }

    private void EnsureResultsBackground()
    {
        if (!resultsRoot || !resultsText) return;

        Transform existing = resultsRoot.transform.Find("ResultsBackground");
        GameObject backgroundObject = existing
            ? existing.gameObject
            : new GameObject("ResultsBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        backgroundObject.transform.SetParent(resultsRoot.transform, false);
        backgroundObject.transform.SetAsFirstSibling();

        _resultsBackground = backgroundObject.GetComponent<Image>();
        _resultsBackground.color = resultsBackgroundColor;
        _resultsBackground.raycastTarget = false;

        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        RectTransform textRect = resultsText.GetComponent<RectTransform>();

        backgroundRect.anchorMin = textRect.anchorMin;
        backgroundRect.anchorMax = textRect.anchorMax;
        backgroundRect.pivot = textRect.pivot;
        backgroundRect.anchoredPosition = textRect.anchoredPosition;
        backgroundRect.sizeDelta = textRect.sizeDelta + resultsBackgroundPadding;

        resultsText.transform.SetAsLastSibling();
        backgroundObject.SetActive(false);
    }

    private void OnDestroy() => ConnectionUI.OnConnected -= HandleConnected;
}
