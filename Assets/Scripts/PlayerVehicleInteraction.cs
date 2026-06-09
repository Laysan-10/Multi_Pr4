using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerVehicleInteraction : NetworkBehaviour
{
    [SerializeField] private float enterDistance = 4f;
    [SerializeField] private MeshRenderer playerMeshRenderer;
    [SerializeField] private Collider playerCollider;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerShooting playerShooting;
    [SerializeField] private PlayerCamera playerCamera;
    [SerializeField] private TMP_Text enterVehiclePrompt;
    [SerializeField] private string enterPromptText = "Нажмите E, чтобы сесть в машину";
    [SerializeField] private string exitPromptText = "Нажмите E, чтобы выйти из машины";

    public readonly SyncVar<NetworkObject> OccupiedVehicle = new(null, new());

    public bool IsInVehicle => OccupiedVehicle.Value != null;
    public NetworkVehicle CurrentVehicle =>
        OccupiedVehicle.Value ? OccupiedVehicle.Value.GetComponent<NetworkVehicle>() : null;

    private void Awake()
    {
        if (!playerMeshRenderer)
            playerMeshRenderer = GetComponent<MeshRenderer>();
        if (!playerCollider)
            playerCollider = GetComponent<Collider>();
        if (!playerMovement)
            playerMovement = GetComponent<PlayerMovement>();
        if (!playerShooting)
            playerShooting = GetComponent<PlayerShooting>();
        if (!playerCamera)
            playerCamera = GetComponent<PlayerCamera>();
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        OccupiedVehicle.OnChange += OnOccupiedVehicleChanged;
        ApplyInVehicleState(OccupiedVehicle.Value);

        if (Owner.IsLocalClient && enterVehiclePrompt)
            enterVehiclePrompt.gameObject.SetActive(false);
    }

    public override void OnStopNetwork()
    {
        OccupiedVehicle.OnChange -= OnOccupiedVehicleChanged;
        base.OnStopNetwork();
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned)
            return;

        UpdateEnterPrompt();

        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            return;

        if (IsInVehicle)
            CurrentVehicle.RequestExitServerRpc(NetworkObject);
        else
            TryEnterNearestVehicle();
    }

    private void UpdateEnterPrompt()
    {
        if (!enterVehiclePrompt)
            return;

        if (IsInVehicle)
        {
            enterVehiclePrompt.gameObject.SetActive(true);
            enterVehiclePrompt.text = exitPromptText;
            return;
        }

        bool nearVehicle = FindNearestEnterableVehicle() != null;
        enterVehiclePrompt.gameObject.SetActive(nearVehicle);
        if (nearVehicle)
            enterVehiclePrompt.text = enterPromptText;
    }

    private NetworkVehicle FindNearestEnterableVehicle()
    {
        NetworkVehicle[] vehicles = FindObjectsByType<NetworkVehicle>(FindObjectsSortMode.None);
        NetworkVehicle nearest = null;
        float nearestDistance = enterDistance;

        foreach (NetworkVehicle vehicle in vehicles)
        {
            if (!vehicle.CanBeEnteredBy(NetworkObject))
                continue;

            float distance = Vector3.Distance(transform.position, vehicle.transform.position);
            if (distance > nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = vehicle;
        }

        return nearest;
    }

    private void TryEnterNearestVehicle()
    {
        NetworkVehicle nearest = FindNearestEnterableVehicle();
        if (nearest)
            nearest.RequestEnterServerRpc(NetworkObject);
    }

    public void EnterVehicle(NetworkVehicle vehicle)
    {
        if (!IsServerStarted || !vehicle)
            return;

        OccupiedVehicle.Value = vehicle.NetworkObject;
    }

    public void ExitVehicle(Vector3 exitPosition)
    {
        if (!IsServerStarted)
            return;

        transform.SetParent(null);
        transform.position = exitPosition;
        OccupiedVehicle.Value = null;
    }

    [Server]
    public void ForceResetToSpawn(Vector3 spawnPosition)
    {
        transform.SetParent(null);
        transform.position = spawnPosition;
        OccupiedVehicle.Value = null;
        ApplyResetStateObserversRpc(spawnPosition);
    }

    [ObserversRpc]
    private void ApplyResetStateObserversRpc(Vector3 spawnPosition)
    {
        transform.SetParent(null);
        transform.position = spawnPosition;
        ApplyInVehicleState(null);

        if (TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }
    }

    private void OnOccupiedVehicleChanged(NetworkObject prev, NetworkObject next, bool asServer)
    {
        ApplyInVehicleState(next);
    }

    private void ApplyInVehicleState(NetworkObject vehicleNob)
    {
        bool inVehicle = vehicleNob != null;
        NetworkVehicle vehicle = inVehicle ? vehicleNob.GetComponent<NetworkVehicle>() : null;

        if (inVehicle && vehicle)
        {
            transform.SetParent(vehicle.transform);
            transform.localPosition = new Vector3(0f, 0.5f, 0f);
            transform.localRotation = Quaternion.identity;
        }
        else
        {
            transform.SetParent(null);
        }

        if (playerMeshRenderer)
            playerMeshRenderer.enabled = !inVehicle;

        if (playerCollider)
            playerCollider.enabled = !inVehicle;

        if (playerMovement)
            playerMovement.enabled = !inVehicle;

        if (playerShooting)
            playerShooting.enabled = !inVehicle;

        if (IsOwner && playerCamera)
        {
            if (inVehicle && vehicle)
                playerCamera.SetFollowTarget(vehicle.transform, true);
            else
                playerCamera.SetFollowTarget(transform, false);
        }
    }
}
