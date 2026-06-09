using FishNet;
using UnityEngine;

public class RaceResetManager : MonoBehaviour
{
    public static RaceResetManager Instance { get; private set; }
    [SerializeField] private float playerSpawnHeightOffset = 1.5f;

    private void Awake()
    {
        Instance = this;
    }

    public void ResetRace()
    {
        if (!InstanceFinder.IsServerStarted) return;

        PlayerSpawnPoints spawnPoints = PlayerSpawnPoints.Instance;
        PlayerNetwork[] players = FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None);

        foreach (PlayerNetwork player in players)
        {
            if (!player) continue;

            PlayerVehicleInteraction interaction = player.GetComponent<PlayerVehicleInteraction>();
            if (interaction && interaction.CurrentVehicle)
                interaction.CurrentVehicle.ForceReleaseDriver(player.NetworkObject);
        }

        foreach (NetworkVehicle vehicle in FindObjectsByType<NetworkVehicle>(FindObjectsSortMode.None))
        {
            if (vehicle)
                vehicle.ForceResetToInitialPosition();
        }

        for (int i = 0; i < players.Length; i++)
        {
            PlayerNetwork player = players[i];
            if (!player) continue;

            PlayerVehicleInteraction interaction = player.GetComponent<PlayerVehicleInteraction>();
            Vector3 spawnPos = spawnPoints
                ? spawnPoints.GetSpawnPosition(i) + Vector3.up * playerSpawnHeightOffset
                : player.transform.position;

            if (interaction)
                interaction.ForceResetToSpawn(spawnPos);
            else
                player.transform.position = spawnPos;

            if (player.TryGetComponent(out Rigidbody rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep();
            }

            player.Hp.Value = 100;
            player.IsAlive.Value = true;
        }
    }
}
