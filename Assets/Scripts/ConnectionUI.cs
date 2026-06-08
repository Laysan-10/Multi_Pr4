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
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button startClientButton;
    [SerializeField] private TMP_InputField nicknameInputField;
    [SerializeField] private GameObject[] hideOnConnect;

    public static event Action<string> OnNameChanged;
    public static event Action OnConnected;

    public static string PlayerNickname { get; private set; } = "Player";
    public static bool HasConnected { get; private set; }

    private bool _connectionUiHidden;

    private void Start()
    {
        if (!networkManager)
            networkManager = InstanceFinder.NetworkManager;

        if (startHostButton)
            startHostButton.onClick.AddListener(StartHost);

        if (startClientButton)
            startClientButton.onClick.AddListener(StartClient);

        if (nicknameInputField)
            nicknameInputField.onValueChanged.AddListener(ChangeName);

        if (networkManager)
            networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
    }

    private void OnDestroy()
    {
        if (networkManager)
            networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
            HideConnectionUi();
    }

    private void StartClient()
    {
        SaveNickname();
        if (networkManager)
            networkManager.ClientManager.StartConnection();

        DeactivateButtons();
    }

    private void StartHost()
    {
        SaveNickname();
        if (!networkManager)
            return;

        networkManager.ServerManager.StartConnection();
        networkManager.ClientManager.StartConnection();
        DeactivateButtons();
    }

    private void DeactivateButtons()
    {
        if (startHostButton)
            startHostButton.interactable = false;

        if (startClientButton)
            startClientButton.interactable = false;
    }

    private void HideConnectionUi()
    {
        if (_connectionUiHidden)
            return;

        _connectionUiHidden = true;
        HasConnected = true;

        if (hideOnConnect != null)
        {
            foreach (GameObject root in hideOnConnect)
            {
                if (root)
                    root.SetActive(false);
            }
        }

        OnConnected?.Invoke();
    }

    private void SaveNickname()
    {
        if (nicknameInputField)
            nicknameInputField.DeactivateInputField(true);

        string rawValue = nicknameInputField != null ? nicknameInputField.text : string.Empty;
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
