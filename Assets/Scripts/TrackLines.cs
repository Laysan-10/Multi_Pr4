using FishNet;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class TrackFinishLine : MonoBehaviour
{
    [SerializeField] private Vector3 triggerSize = new(20f, 5f, 1f);
    [SerializeField] private Vector3 triggerCenter = new(0f, 1.5f, 0f);

    private void Awake()
    {
        Configure(triggerSize, triggerCenter);
    }

    public void Configure(Vector3 size, Vector3 center)
    {
        triggerSize = size;
        triggerCenter = center;

        if (!TryGetComponent(out BoxCollider boxCollider))
            boxCollider = gameObject.AddComponent<BoxCollider>();

        boxCollider.isTrigger = true;
        boxCollider.size = triggerSize;
        boxCollider.center = triggerCenter;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!InstanceFinder.IsServerStarted) return;
        if (!GameManager.IsMatchInProgress) return;

        NetworkVehicle vehicle = other.GetComponentInParent<NetworkVehicle>();
        if (vehicle != null && vehicle.HasDriver)
            GameManager.Instance?.RegisterFinish(vehicle);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(triggerCenter, triggerSize);
        Gizmos.matrix = oldMatrix;
    }
}
