using UnityEngine;

/// <summary>
/// Marker for boost pickup spawn locations. Position must be above a road collider.
/// </summary>
public class RoadPickupSpawnPoint : MonoBehaviour
{
    [SerializeField] private float roadRaycastHeight = 4f;
    [SerializeField] private float roadRaycastDistance = 12f;

    public bool TryGetSpawnPosition(out Vector3 position)
    {
        position = transform.position;

        if (TrySnapToRoadSurface(transform.position, out Vector3 snapped))
        {
            position = snapped;
            return true;
        }

        return false;
    }

    public static bool TrySnapToRoadSurface(Vector3 fromPosition, out Vector3 snappedPosition)
    {
        snappedPosition = fromPosition;
        Vector3 origin = fromPosition + Vector3.up * 4f;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            return false;

        if (!IsRoadTransform(hit.collider.transform))
            return false;

        snappedPosition = hit.point + Vector3.up * 0.75f;
        return true;
    }

    public static bool IsRoadTransform(Transform transformToCheck)
    {
        Transform current = transformToCheck;
        while (current != null)
        {
            string name = current.name.ToLowerInvariant();
            if (name.Contains("road") && !name.Contains("sidewalk"))
                return true;

            current = current.parent;
        }

        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = TryGetSpawnPosition(out _) ? Color.cyan : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.6f);
    }
}
