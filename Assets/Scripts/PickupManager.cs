using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

public class PickupManager : MonoBehaviour
{
    [SerializeField] private GameObject boostPickupPrefab;
    [SerializeField] private RoadPickupSpawnPoint[] spawnPoints;
    [SerializeField] private int generatedSpawnCount = 6;
    [SerializeField] private float respawnDelay = 12f;
    [SerializeField] private float minSpawnSeparation = 8f;

    private bool _serverSpawnedPickups;

    private void Start()
    {
        if (!InstanceFinder.NetworkManager)
            return;

        InstanceFinder.NetworkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
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

        if (args.ConnectionState != LocalConnectionState.Started)
            return;

        if (_serverSpawnedPickups)
            return;

        _serverSpawnedPickups = true;
        SpawnAll();
    }

    private void SpawnAll()
    {
        List<Vector3> positions = CollectSpawnPositions();
        foreach (Vector3 position in positions)
            SpawnPickup(position);
    }

    private List<Vector3> CollectSpawnPositions()
    {
        List<Vector3> positions = new();

        if (spawnPoints == null || spawnPoints.Length == 0)
            spawnPoints = FindObjectsByType<RoadPickupSpawnPoint>(FindObjectsSortMode.None);

        foreach (RoadPickupSpawnPoint point in spawnPoints)
        {
            if (!point || !point.TryGetSpawnPosition(out Vector3 position))
                continue;

            if (IsFarEnough(position, positions, minSpawnSeparation))
                positions.Add(position);
        }

        if (positions.Count > 0)
            return positions;

        return GeneratePositionsFromRoadColliders();
    }

    private List<Vector3> GeneratePositionsFromRoadColliders()
    {
        List<Vector3> positions = new();
        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);

        foreach (Collider collider in colliders)
        {
            if (!collider || collider.isTrigger)
                continue;

            if (!RoadPickupSpawnPoint.IsRoadTransform(collider.transform))
                continue;

            Bounds bounds = collider.bounds;
            Vector3 candidate = new(bounds.center.x, bounds.max.y + 0.75f, bounds.center.z);
            if (IsFarEnough(candidate, positions, minSpawnSeparation))
                positions.Add(candidate);
        }

        positions.Sort((a, b) => a.x.CompareTo(b.x));

        if (positions.Count > generatedSpawnCount)
            positions = positions.GetRange(0, generatedSpawnCount);

        return positions;
    }

    private static bool IsFarEnough(Vector3 candidate, List<Vector3> existing, float minDistance)
    {
        foreach (Vector3 position in existing)
        {
            if (Vector3.Distance(candidate, position) < minDistance)
                return false;
        }

        return true;
    }

    public void OnPickedUp(Vector3 position)
    {
        StartCoroutine(RespawnAfterDelay(position));
    }

    private IEnumerator RespawnAfterDelay(Vector3 position)
    {
        yield return new WaitForSeconds(respawnDelay);

        if (RoadPickupSpawnPoint.TrySnapToRoadSurface(position, out Vector3 snapped))
            SpawnPickup(snapped);
        else
            SpawnPickup(position);
    }

    private void SpawnPickup(Vector3 position)
    {
        if (!boostPickupPrefab || !InstanceFinder.ServerManager || !InstanceFinder.IsServerStarted)
            return;

        GameObject go = Instantiate(boostPickupPrefab, position, Quaternion.identity);
        if (!go.TryGetComponent(out NetworkObject networkObject) ||
            !go.TryGetComponent(out SpeedBoostPickup pickup))
        {
            Destroy(go);
            return;
        }

        pickup.Init(this);
        InstanceFinder.ServerManager.Spawn(networkObject);
    }
}
