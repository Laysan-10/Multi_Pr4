using UnityEngine;

public class PlayerSpawnPoints : MonoBehaviour
{
    public static PlayerSpawnPoints Instance { get; private set; }

    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool drawGizmos;

    public Transform[] SpawnPoints => spawnPoints;

    private void Awake()
    {
        Instance = this;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            HideSpawnPointVisuals();
            return;
        }

        GameObject[] tagged = GameObject.FindGameObjectsWithTag("Spawner");
        if (tagged.Length == 0) return;

        spawnPoints = new Transform[tagged.Length];
        for (int i = 0; i < tagged.Length; i++)
            spawnPoints[i] = tagged[i].transform;

        HideSpawnPointVisuals();
    }

    public Vector3 GetSpawnPosition(int index)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return transform.position;

        Transform point = spawnPoints[index % spawnPoints.Length];
        return point ? point.position : transform.position;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.green;
        if (spawnPoints == null) return;

        foreach (Transform point in spawnPoints)
        {
            if (!point) continue;
            Gizmos.DrawWireSphere(point.position, 0.8f);
            Gizmos.DrawLine(point.position, point.position + Vector3.up * 2f);
        }
    }

    private void HideSpawnPointVisuals()
    {
        if (spawnPoints == null) return;

        foreach (Transform point in spawnPoints)
        {
            if (!point) continue;

            foreach (Renderer renderer in point.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;

            foreach (Collider collider in point.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
        }
    }
}
