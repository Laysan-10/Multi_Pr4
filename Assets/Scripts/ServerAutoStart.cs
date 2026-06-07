using FishNet;
using UnityEngine;

public class ServerAutoStart : MonoBehaviour
{
    private void Start()
    {
        if (!Application.isBatchMode)
            return;

        InstanceFinder.ServerManager.StartConnection();
        Debug.Log("Dedicated server started (batch mode). Waiting for clients on Tugboat port.");
    }
}
