using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

[RequireComponent(typeof(PrometeoCarController))]
public class NetworkVehicle : NetworkBehaviour
{
    [SerializeField] private float enterDistance = 4f;
    [SerializeField] private Vector3 exitOffset = new(2f, 0.5f, 0f);

    private PrometeoCarController _carController;

    public readonly SyncVar<int> DriverOwnerId = new(-1, new());

    public bool HasDriver => DriverOwnerId.Value >= 0;
    public float EnterDistance => enterDistance;
    public Vector3 ExitOffset => exitOffset;

    private void Awake()
    {
        _carController = GetComponent<PrometeoCarController>();
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        DriverOwnerId.OnChange += OnDriverChanged;
        ApplyDriverState();
    }

    public override void OnStopNetwork()
    {
        DriverOwnerId.OnChange -= OnDriverChanged;
        base.OnStopNetwork();
    }

    private void OnDriverChanged(int prev, int next, bool asServer)
    {
        ApplyDriverState();
    }

    public void RefreshDriverState()
    {
        ApplyDriverState();
    }

    private void ApplyDriverState()
    {
        if (!_carController)
            return;

        bool allowInput = HasDriver && IsOwner && GameManager.IsMatchInProgress;
        _carController.enabled = allowInput;
    }

    public bool CanBeEnteredBy(NetworkObject playerNob)
    {
        if (!playerNob || HasDriver)
            return false;

        return Vector3.Distance(transform.position, playerNob.transform.position) <= enterDistance;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestEnterServerRpc(NetworkObject playerNob, NetworkConnection conn = null)
    {
        if (!playerNob || HasDriver)
            return;

        if (!CanBeEnteredBy(playerNob))
            return;

        PlayerVehicleInteraction interaction = playerNob.GetComponent<PlayerVehicleInteraction>();
        if (!interaction || interaction.IsInVehicle)
            return;

        DriverOwnerId.Value = playerNob.OwnerId;
        GiveOwnership(playerNob.Owner);

        interaction.EnterVehicle(this);
        GameManager.Instance?.NotifyDriverStateChanged();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestExitServerRpc(NetworkObject playerNob)
    {
        if (!playerNob || !HasDriver || DriverOwnerId.Value != playerNob.OwnerId)
            return;

        PlayerVehicleInteraction interaction = playerNob.GetComponent<PlayerVehicleInteraction>();
        if (!interaction || !interaction.IsInVehicle || interaction.CurrentVehicle != this)
            return;

        Vector3 exitPosition = transform.position
            + transform.right * exitOffset.x
            + Vector3.up * exitOffset.y
            + transform.forward * exitOffset.z;

        interaction.ExitVehicle(exitPosition);
        DriverOwnerId.Value = -1;
        RemoveOwnership();
        GameManager.Instance?.NotifyDriverStateChanged();
    }

    public void ForceReleaseDriver()
    {
        if (!IsServerStarted || !HasDriver)
            return;

        DriverOwnerId.Value = -1;
        RemoveOwnership();
    }
}
