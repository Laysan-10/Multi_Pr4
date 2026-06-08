#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public static class RoadPickupSpawnPointMenu
{
    [MenuItem("Tools/Racing/Create Road Boost Spawn Points")]
    private static void CreateRoadBoostSpawnPoints()
    {
        Transform parent = GameObject.Find("RoadBoostSpawns")?.transform;
        if (!parent)
        {
            GameObject root = new GameObject("RoadBoostSpawns");
            parent = root.transform;
            Undo.RegisterCreatedObjectUndo(root, "Create Road Boost Spawns");
        }

        Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
        int created = 0;

        foreach (Collider collider in colliders)
        {
            if (!collider || collider.isTrigger)
                continue;

            if (!RoadPickupSpawnPoint.IsRoadTransform(collider.transform))
                continue;

            Bounds bounds = collider.bounds;
            Vector3 position = new(bounds.center.x, bounds.max.y + 0.75f, bounds.center.z);

            if (!RoadPickupSpawnPoint.TrySnapToRoadSurface(position, out Vector3 snapped))
                continue;

            if (HasNearbySpawnPoint(parent, snapped, 8f))
                continue;

            GameObject point = new GameObject($"BoostSpawn_{created + 1}");
            Undo.RegisterCreatedObjectUndo(point, "Create Road Boost Spawn");
            point.transform.SetParent(parent);
            point.transform.position = snapped;
            point.AddComponent<RoadPickupSpawnPoint>();
            created++;

            if (created >= 8)
                break;
        }

        Debug.Log($"Created {created} road boost spawn points.");
    }

    private static bool HasNearbySpawnPoint(Transform parent, Vector3 position, float minDistance)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            if (Vector3.Distance(parent.GetChild(i).position, position) < minDistance)
                return true;
        }

        return false;
    }
}
#endif
