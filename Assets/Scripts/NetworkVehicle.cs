using System.Collections;
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
    private Coroutine _boostCoroutine;
    private int _baseMaxSpeed;
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;

    public readonly SyncVar<int> DriverOwnerId = new(-1, new());

    public bool HasDriver => DriverOwnerId.Value >= 0;
    public float EnterDistance => enterDistance;
    public Vector3 ExitOffset => exitOffset;

    private void Awake()
    {
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;

        _carController = GetComponent<PrometeoCarController>();
        if (_carController)
            _baseMaxSpeed = _carController.maxSpeed;
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
        if (asServer)
            GameManager.Instance?.OnVehicleDriverChanged();
    }

    private void ApplyDriverState()
    {
        if (!_carController)
            return;

        bool allowInput = HasDriver && IsOwner && GameManager.CanDriveVehicle;
        _carController.enabled = allowInput;
    }

    private void Update()
    {
        if (!IsOwner || !_carController)
            return;

        bool shouldDrive = HasDriver && GameManager.CanDriveVehicle;
        if (_carController.enabled != shouldDrive)
            _carController.enabled = shouldDrive;
    }

    public bool CanBeEnteredBy(NetworkObject playerNob)
    {
        if (!playerNob || HasDriver)
            return false;

        if (GameManager.Instance && GameManager.Instance.CurrentState.Value != GameManager.GameState.Lobby)
            return false;

        return Vector3.Distance(transform.position, playerNob.transform.position) <= enterDistance;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestEnterServerRpc(NetworkObject playerNob, NetworkConnection conn = null)
    {
        if (!playerNob || HasDriver)
            return;

        if (GameManager.Instance && GameManager.Instance.CurrentState.Value != GameManager.GameState.Lobby)
            return;

        if (!CanBeEnteredBy(playerNob))
            return;

        PlayerVehicleInteraction interaction = playerNob.GetComponent<PlayerVehicleInteraction>();
        if (!interaction || interaction.IsInVehicle)
            return;

        DriverOwnerId.Value = playerNob.OwnerId;
        GiveOwnership(playerNob.Owner);

        interaction.EnterVehicle(this);
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
    }

    [Server]
    public void ForceReleaseDriver(NetworkObject playerNob)
    {
        if (!IsServerStarted)
            return;

        if (playerNob)
        {
            PlayerVehicleInteraction interaction = playerNob.GetComponent<PlayerVehicleInteraction>();
            if (interaction && interaction.IsInVehicle && interaction.CurrentVehicle == this)
            {
                Vector3 exitPosition = transform.position
                    + transform.right * exitOffset.x
                    + Vector3.up * exitOffset.y
                    + transform.forward * exitOffset.z;
                interaction.ExitVehicle(exitPosition);
            }
        }

        DriverOwnerId.Value = -1;
        RemoveOwnership();
    }

    [Server]
    public void ForceReset(Vector3 position, Quaternion rotation)
    {
        if (!IsServerStarted)
            return;

        foreach (PlayerVehicleInteraction interaction in FindObjectsByType<PlayerVehicleInteraction>(FindObjectsSortMode.None))
        {
            if (!interaction.IsInVehicle || interaction.CurrentVehicle != this)
                continue;
            ForceReleaseDriver(interaction.NetworkObject);
        }

        DriverOwnerId.Value = -1;
        RemoveOwnership();
        StopBoostObserversRpc();
        ResetVehicleSpeedObserversRpc();
        ApplyResetTransform(position, rotation);
        ResetTransformObserversRpc(position, rotation);

        if (_carController)
            _carController.enabled = false;
    }

    [Server]
    public void ForceResetToInitialPosition()
    {
        ForceReset(_initialPosition, _initialRotation);
    }

    private void ApplyResetTransform(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);

        if (TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }

        if (TryGetComponent(out FishNet.Component.Transforming.NetworkTransform networkTransform))
        {
            networkTransform.Teleport();
            networkTransform.ForceSend(1);
        }
    }

    [ObserversRpc(BufferLast = true)]
    private void ResetTransformObserversRpc(Vector3 position, Quaternion rotation)
    {
        ApplyResetTransform(position, rotation);

        if (_carController)
            _carController.enabled = false;
    }

    [ObserversRpc]
    private void StopBoostObserversRpc()
    {
        if (_boostCoroutine != null)
        {
            StopCoroutine(_boostCoroutine);
            _boostCoroutine = null;
        }
    }

    [ObserversRpc]
    private void ResetVehicleSpeedObserversRpc()
    {
        if (_carController)
            _carController.maxSpeed = _baseMaxSpeed;
    }

    [Server]
    public void ApplySpeedBoost(int bonusSpeed, float duration)
    {
        if (!IsServerStarted || !_carController || !HasDriver)
            return;

        ApplySpeedBoostObserversRpc(bonusSpeed, duration);
    }

    [ObserversRpc]
    private void ApplySpeedBoostObserversRpc(int bonusSpeed, float duration)
    {
        if (!IsOwner || !_carController)
            return;

        if (_boostCoroutine != null)
            StopCoroutine(_boostCoroutine);

        _boostCoroutine = StartCoroutine(BoostRoutine(bonusSpeed, duration));
    }

    private IEnumerator BoostRoutine(int bonusSpeed, float duration)
    {
        _carController.maxSpeed = _baseMaxSpeed + bonusSpeed;
        yield return new WaitForSeconds(duration);
        _carController.maxSpeed = _baseMaxSpeed;
        _boostCoroutine = null;
    }
}
