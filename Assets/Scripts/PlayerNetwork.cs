using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private PlayerShooting playerShooting;

    public readonly SyncVar<string> Nickname = new("Player", new());
    public readonly SyncVar<int> Hp = new(100, new());
    public readonly SyncVar<bool> IsAlive = new(true, new());
    public readonly SyncVar<int> Score = new(0, new());

    private void Awake()
    {
        IsAlive.OnChange += IsAlive_OnChange;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!Owner.IsLocalClient)
            return;

        ConnectionUI.OnNameChanged -= SubmitNicknameServerRpc;
        ConnectionUI.OnNameChanged += SubmitNicknameServerRpc;
        SubmitNicknameServerRpc(ConnectionUI.PlayerNickname);
    }

    public override void OnStopClient()
    {
        ConnectionUI.OnNameChanged -= SubmitNicknameServerRpc;
        base.OnStopClient();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitNicknameServerRpc(string nick)
    {
        string safeValue = string.IsNullOrWhiteSpace(nick)
            ? $"Player_{OwnerId}"
            : nick.Trim();
        Nickname.Value = safeValue;
    }

    private void IsAlive_OnChange(bool prev, bool next, bool asServer)
    {
        if (meshRenderer)
            meshRenderer.enabled = next;
    }
}
