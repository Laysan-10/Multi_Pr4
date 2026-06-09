using FishNet.Object;
using UnityEngine;

public class SpeedBoostPickup : NetworkBehaviour
{
    [SerializeField] private int speedBonus = 30;
    [SerializeField] private float boostDuration = 5f;

    private BoostPickupManager _manager;
    private Vector3 _spawnPosition;

    public void Init(BoostPickupManager manager)
    {
        _manager = manager;
        _spawnPosition = transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServerInitialized) return;

        NetworkVehicle vehicle = other.GetComponentInParent<NetworkVehicle>();
        if (!vehicle || !vehicle.HasDriver) return;

        vehicle.ApplySpeedBoost(speedBonus, boostDuration);
        _manager?.OnPickedUp(_spawnPosition);
        Despawn(DespawnType.Destroy);
    }
}
