using Unity.Netcode.Components;
using Unity.Netcode;
using UnityEngine;

public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        CanCommitToTransform = IsOwner;
    }

    protected void Update()
    {
        CanCommitToTransform = IsOwner;
        
        if (NetworkManager != null && (NetworkManager.IsConnectedClient || NetworkManager.IsListening))
        {
            if (CanCommitToTransform && transform.hasChanged)
            {
                //TryCommitTransformToServer(transform, NetworkManager.LocalTime.Time);
                transform.hasChanged = false;
            }
        }
    }
}
