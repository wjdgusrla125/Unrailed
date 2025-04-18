using UnityEngine;
using Unity.Netcode;

public class BridgeController : NetworkBehaviour
{
    [SerializeField] private GameObject bridge;
    [SerializeField] private BoxCollider bridgeCollider;
    
    public void ActivateBridge()
    {
        if (bridge != null)
        {
            bridge.SetActive(true);
        }
        
        if (bridgeCollider != null)
        {
            bridgeCollider.enabled = false;
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void ActivateBridgeServerRpc()
    {
        ActivateBridgeClientRpc();
    }
    
    [ClientRpc]
    private void ActivateBridgeClientRpc()
    {
        ActivateBridge();
    }
}