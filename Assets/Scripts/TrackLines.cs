using FishNet;
using UnityEngine;

public class TrackStartLine : MonoBehaviour
{
    [SerializeField] private Vector3 triggerSize = new(10f, 3f, 3.5f);
    [SerializeField] private Vector3 triggerCenter = new(0f, 1.5f, 0f);

    private void Awake()
    {
        ConfigureTrigger(GetComponent<BoxCollider>());
    }

    private void ConfigureTrigger(BoxCollider collider)
    {
        if (!collider)
            collider = gameObject.AddComponent<BoxCollider>();

        collider.isTrigger = true;
        collider.size = triggerSize;
        collider.center = triggerCenter;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        if (!GameManager.IsMatchInProgress)
            return;

        NetworkVehicle vehicle = other.GetComponentInParent<NetworkVehicle>();
        if (!vehicle || !vehicle.HasDriver)
            return;

        GameManager.Instance?.RegisterStartCross(vehicle);
    }
}

public class TrackFinishLine : MonoBehaviour
{
    [SerializeField] private Vector3 triggerSize = new(10f, 3f, 3.5f);
    [SerializeField] private Vector3 triggerCenter = new(0f, 1.5f, 0f);

    private void Awake()
    {
        ConfigureTrigger(GetComponent<BoxCollider>());
    }

    private void ConfigureTrigger(BoxCollider collider)
    {
        if (!collider)
            collider = gameObject.AddComponent<BoxCollider>();

        collider.isTrigger = true;
        collider.size = triggerSize;
        collider.center = triggerCenter;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!InstanceFinder.IsServerStarted)
            return;

        if (!GameManager.IsMatchInProgress)
            return;

        NetworkVehicle vehicle = other.GetComponentInParent<NetworkVehicle>();
        if (!vehicle || !vehicle.HasDriver)
            return;

        GameManager.Instance?.RegisterFinish(vehicle);
    }
}
