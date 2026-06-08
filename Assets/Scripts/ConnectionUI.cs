using System;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConnectionUI : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private GameObject loginPanelRoot;

    public static event Action<string> OnNameChanged;
    public static event Action OnConnected;

    public static string PlayerNickname { get; private set; } = "Player";
    public static bool HasConnected { get; private set; }

    private Button _startHostButton;
    private Button _startClientButton;
    private TMP_InputField _nicknameInputField;
    private bool _connectionUiHidden;

    private void Awake()
    {
        HasConnected = false;
        _connectionUiHidden = false;

        if (loginPanelRoot)
            loginPanelRoot.SetActive(true);

        CacheLoginReferences();
    }

    private void Start()
    {
        if (!networkManager)
            networkManager = InstanceFinder.NetworkManager;

        if (_startHostButton)
            _startHostButton.onClick.AddListener(StartHost);

        if (_startClientButton)
            _startClientButton.onClick.AddListener(StartClient);

        if (_nicknameInputField)
            _nicknameInputField.onValueChanged.AddListener(ChangeName);

        if (networkManager)
            networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
    }

    private void OnDestroy()
    {
        if (networkManager)
            networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
    }

    private void CacheLoginReferences()
    {
        if (!loginPanelRoot)
            return;

        foreach (Button button in loginPanelRoot.GetComponentsInChildren<Button>(true))
        {
            if (button.name.Contains("Host"))
                _startHostButton = button;
            else if (button.name.Contains("Client"))
                _startClientButton = button;
        }

        _nicknameInputField = loginPanelRoot.GetComponentInChildren<TMP_InputField>(true);
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
            HideLoginPanel();
    }

    private void StartClient()
    {
        SaveNickname();
        if (networkManager)
            networkManager.ClientManager.StartConnection();
    }

    private void StartHost()
    {
        SaveNickname();
        if (!networkManager)
            return;

        networkManager.ServerManager.StartConnection();
        networkManager.ClientManager.StartConnection();
    }

    private void HideLoginPanel()
    {
        if (_connectionUiHidden)
            return;

        _connectionUiHidden = true;
        HasConnected = true;

        if (loginPanelRoot)
            loginPanelRoot.SetActive(false);

        OnConnected?.Invoke();
    }

    private void SaveNickname()
    {
        if (_nicknameInputField)
            _nicknameInputField.DeactivateInputField(true);

        string rawValue = _nicknameInputField != null ? _nicknameInputField.text : string.Empty;
        if (string.IsNullOrWhiteSpace(rawValue))
            PlayerNickname = "Player";
        else
            PlayerNickname = rawValue.Trim();
    }

    private void ChangeName(string playerName)
    {
        PlayerNickname = string.IsNullOrEmpty(playerName) ? "Player" : playerName.Trim();
        OnNameChanged?.Invoke(playerName);
    }
}
