using UnityEngine;

public class RaceTrackSetup : MonoBehaviour
{
    [Header("Финишная линия")]
    [SerializeField] private Vector3 finishLinePosition = new(69.82f, 0.22f, 141.07f);
    [SerializeField] private Vector3 finishLineEuler = Vector3.zero;
    [SerializeField] private Vector3 finishTriggerSize = new(20f, 5f, 1f);

    [Header("Точки спавна усилений (если не заданы в сцене)")]
    [SerializeField] private Vector3[] defaultBoostSpawnPositions =
    {
        new(65f, 0.5f, 70f),
        new(75f, 0.5f, 100f),
        new(70f, 0.5f, 120f)
    };

    private void Awake()
    {
        EnsureFinishLine();
        EnsureBoostSpawns();
    }

    private void EnsureFinishLine()
    {
        TrackFinishLine existingFinishLine = FindFirstObjectByType<TrackFinishLine>();
        if (existingFinishLine)
        {
            existingFinishLine.Configure(finishTriggerSize, new Vector3(0f, 1.5f, 0f));
            return;
        }

        GameObject finish = new GameObject("FinishLine");
        finish.transform.SetPositionAndRotation(finishLinePosition, Quaternion.Euler(finishLineEuler));

        BoxCollider collider = finish.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = finishTriggerSize;
        collider.center = new Vector3(0f, 1.5f, 0f);

        TrackFinishLine finishLine = finish.AddComponent<TrackFinishLine>();
        finishLine.Configure(finishTriggerSize, collider.center);
    }

    private void EnsureBoostSpawns()
    {
        if (GameObject.FindGameObjectsWithTag("BoostSpawner").Length > 0)
        {
            BoostPickupManager existing = GetComponent<BoostPickupManager>();
            existing?.RefreshSpawnPointsFromScene();
            return;
        }

        GameObject parent = new GameObject("BoostSpawners");
        parent.transform.SetParent(transform);

        for (int i = 0; i < defaultBoostSpawnPositions.Length; i++)
        {
            GameObject point = new GameObject($"BoostSpawner_{i + 1}");
            point.transform.SetParent(parent.transform);
            point.transform.position = defaultBoostSpawnPositions[i];
            point.tag = "BoostSpawner";
        }

        BoostPickupManager manager = GetComponent<BoostPickupManager>();
        if (!manager) manager = gameObject.AddComponent<BoostPickupManager>();
        manager.RefreshSpawnPointsFromScene();
    }
}
