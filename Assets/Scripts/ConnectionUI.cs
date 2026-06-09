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
    [SerializeField] private Button startClientButton;
    [SerializeField] private TMP_InputField nicknameInputField;

    public static event Action OnConnected;
    public static event Action<string> OnNameChanged;

    public static bool HasConnected { get; private set; }
    public static string PlayerNickname { get; private set; } = "Player";

    private void Awake()
    {
        HasConnected = false;

        if (!loginPanelRoot)
        {
            Transform panel = FindChildByName(transform, "PlayerConnection");
            if (panel) loginPanelRoot = panel.gameObject;
        }

        if (loginPanelRoot) loginPanelRoot.SetActive(true);
    }

    private void Start()
    {
        if (!networkManager) networkManager = InstanceFinder.NetworkManager;
        if (startClientButton) startClientButton.onClick.AddListener(StartClient);
        if (nicknameInputField) nicknameInputField.onValueChanged.AddListener(ChangeName);

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
            HideLoginPanel();
    }

    private void StartClient()
    {
        SaveNickname();
        if (networkManager) networkManager.ClientManager.StartConnection();
    }

    private void HideLoginPanel()
    {
        HasConnected = true;
        if (loginPanelRoot) loginPanelRoot.SetActive(false);
        OnConnected?.Invoke();
    }

    private void SaveNickname()
    {
        PlayerNickname = string.IsNullOrWhiteSpace(nicknameInputField?.text)
            ? "Player" : nicknameInputField.text.Trim();
        OnNameChanged?.Invoke(PlayerNickname);
    }

    private void ChangeName(string name)
    {
        PlayerNickname = name.Trim();
        OnNameChanged?.Invoke(PlayerNickname);
    }

    private static Transform FindChildByName(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
                return child;
        }

        return null;
    }
}
