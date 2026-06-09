using System.Collections;
using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

public class BoostPickupManager : MonoBehaviour
{
    public static BoostPickupManager Instance { get; private set; }

    [SerializeField] private GameObject speedBoostPrefab;
    [SerializeField] private Transform[] boostSpawnPoints;
    [SerializeField] private float respawnDelay = 15f;

    private bool _serverSpawnedPickups;

    private void Awake()
    {
        Instance = this;
        RefreshSpawnPointsFromScene();
    }

    public void RefreshSpawnPointsFromScene()
    {
        GameObject[] tagged = GameObject.FindGameObjectsWithTag("BoostSpawner");
        if (tagged.Length == 0) return;

        boostSpawnPoints = new Transform[tagged.Length];
        for (int i = 0; i < tagged.Length; i++)
            boostSpawnPoints[i] = tagged[i].transform;
    }

    private void Start()
    {
        if (InstanceFinder.NetworkManager)
            InstanceFinder.NetworkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;

        StartCoroutine(SpawnWhenServerIsReady());
    }

    private void OnDestroy()
    {
        if (InstanceFinder.NetworkManager)
            InstanceFinder.NetworkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
    }

    private void OnServerConnectionState(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped)
            _serverSpawnedPickups = false;
        if (args.ConnectionState != LocalConnectionState.Started) return;
        TrySpawnAll();
    }

    private IEnumerator SpawnWhenServerIsReady()
    {
        float waitedSeconds = 0f;
        while (!InstanceFinder.IsServerStarted)
        {
            waitedSeconds += Time.deltaTime;
            if (waitedSeconds >= 5f)
            {
                Debug.LogWarning("[BoostPickupManager] Server is not started. Speed boosts spawn only on host/server.");
                waitedSeconds = 0f;
            }

            yield return null;
        }

        TrySpawnAll();
    }

    private void TrySpawnAll()
    {
        if (_serverSpawnedPickups) return;

        RefreshSpawnPointsFromScene();

        if (!speedBoostPrefab)
        {
            Debug.LogWarning("[BoostPickupManager] Speed Boost Prefab is not assigned.");
            return;
        }

        if (boostSpawnPoints == null || boostSpawnPoints.Length == 0)
        {
            Debug.LogWarning("[BoostPickupManager] No BoostSpawner points found.");
            return;
        }

        _serverSpawnedPickups = true;
        SpawnAll();
    }

    private void SpawnAll()
    {
        if (boostSpawnPoints == null || boostSpawnPoints.Length == 0) return;
        int spawnedCount = 0;
        foreach (Transform point in boostSpawnPoints)
        {
            if (!point) continue;

            SpawnPickup(point.position);
            spawnedCount++;
        }

        Debug.Log($"[BoostPickupManager] Spawned {spawnedCount} speed boost pickups.");
    }

    public void OnPickedUp(Vector3 position)
    {
        StartCoroutine(RespawnAfterDelay(position));
    }

    private IEnumerator RespawnAfterDelay(Vector3 position)
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnPickup(position);
    }

    private void SpawnPickup(Vector3 position)
    {
        if (!speedBoostPrefab)
        {
            Debug.LogWarning("[BoostPickupManager] Cannot spawn speed boost: prefab is missing.");
            return;
        }

        if (!InstanceFinder.IsServerStarted)
            return;

        GameObject go = Instantiate(speedBoostPrefab, position, Quaternion.identity);
        if (!go.TryGetComponent(out SpeedBoostPickup pickup))
        {
            Destroy(go);
            return;
        }

        pickup.Init(this);

        if (!go.TryGetComponent(out NetworkObject networkObject))
        {
            Destroy(go);
            return;
        }

        InstanceFinder.ServerManager.Spawn(networkObject);
    }

    private void OnDrawGizmos()
    {
        RefreshSpawnPointsFromScene();

        Gizmos.color = Color.yellow;
        if (boostSpawnPoints == null) return;

        foreach (Transform point in boostSpawnPoints)
        {
            if (!point) continue;
            Gizmos.DrawWireSphere(point.position, 0.7f);
            Gizmos.DrawLine(point.position, point.position + Vector3.up * 2f);
        }
    }
}
