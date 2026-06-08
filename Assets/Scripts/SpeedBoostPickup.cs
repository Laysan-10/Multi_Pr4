using FishNet.Object;
using UnityEngine;

public class SpeedBoostPickup : NetworkBehaviour
{
    [SerializeField] private float boostDuration = 4f;
    [SerializeField] private float speedMultiplier = 1.6f;

    private PickupManager _manager;
    private Vector3 _spawnPosition;

    public void Init(PickupManager manager)
    {
        _manager = manager;
        _spawnPosition = transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServerInitialized)
            return;

        PlayerVehicleInteraction interaction = other.GetComponentInParent<PlayerVehicleInteraction>();
        if (!interaction || !interaction.IsInVehicle)
            return;

        NetworkVehicle vehicle = interaction.CurrentVehicle;
        if (!vehicle || !vehicle.HasDriver)
            return;

        PrometeoCarController carController = vehicle.GetComponent<PrometeoCarController>();
        if (!carController)
            return;

        carController.ApplySpeedBoost(boostDuration, speedMultiplier);
        _manager?.OnPickedUp(_spawnPosition);
        Despawn(DespawnType.Destroy);
    }
}
