using UnityEngine;
using Unity.Netcode;

public class BlockPickup : NetworkBehaviour
{
    public Transform handPosition;
    public Transform twoHandPosition;
    public float rayDistance = 2.0f;
    public LayerMask pickupLayerMask;
    public InputReader inputReader; 

    public NetworkObject heldObject = null;
    private bool canPickup = false;
    private NetworkObject targetObject = null;
    [SerializeField] private PlayerInfo playerInfo;

    private void OnEnable()
    {
        if (inputReader != null)
        {
            inputReader.InteractEvent += OnInteract;
        }
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.InteractEvent -= OnInteract;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, rayDistance, pickupLayerMask))
        {
            NetworkObject netObj = hit.collider.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                targetObject = netObj;
                canPickup = true;
            }
        }
        else
        {
            canPickup = false;
            targetObject = null;
        }

        if (heldObject != null)
        {
            // 아이템이 양손 아이템인지 확인
            Item heldItem = heldObject.GetComponent<Item>();
            Transform holdPosition = handPosition; // 기본값은 handPosition
        
            if (heldItem != null && heldItem.WithTwoHanded)
            {
                holdPosition = twoHandPosition; // 양손 아이템이면 twoHandPosition으로 변경
            }

            heldObject.transform.position = holdPosition.position;
            heldObject.transform.rotation = holdPosition.rotation;
        }
    }
    
    private void OnInteract(bool pressed)
    {
        if (!pressed) return;
        if (!IsOwner) return;

        if (heldObject != null && canPickup) 
        {
            SwapObjectServerRpc(heldObject.NetworkObjectId, targetObject.NetworkObjectId);
        }
        else if (heldObject == null && canPickup)
        {
            RequestPickUpServerRpc(targetObject.NetworkObjectId);
        }
        else if (heldObject != null)
        {
            RequestDropServerRpc(heldObject.NetworkObjectId);
        }
    }

    [ServerRpc]
    void RequestPickUpServerRpc(ulong targetId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject netObj))
            return;

        netObj.ChangeOwnership(rpcParams.Receive.SenderClientId);
        SetHeldObjectClientRpc(targetId, NetworkObjectId);
    }

    [ClientRpc]
    void SetHeldObjectClientRpc(ulong objectId, ulong playerId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj) &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerId, out NetworkObject playerObj))
        {
            BlockPickup playerPickup = playerObj.GetComponent<BlockPickup>();
            if (playerPickup != null)
            {
                playerPickup.heldObject = netObj;
                
                if (playerPickup.IsOwner)
                {
                    Item heldItem = netObj.GetComponent<Item>();
                    if (heldItem != null && playerPickup.playerInfo != null)
                    {
                        playerPickup.UpdatePlayerItemType(heldItem.ItemType);
                    }
                    else
                    {
                        playerPickup.UpdatePlayerItemType(ItemType.None);
                    }
                }
            }
        }
    }

    [ServerRpc]
    void RequestDropServerRpc(ulong objectId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
            return;

        netObj.RemoveOwnership();
        ClearHeldObjectClientRpc(objectId);
    }

    [ClientRpc]
    void ClearHeldObjectClientRpc(ulong objectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            netObj.transform.SetParent(null);
            
            Vector3 position = netObj.transform.position;
            position.y = 0.1f;
            netObj.transform.position = position;
            netObj.transform.rotation = Quaternion.identity;

            if (IsOwner)
            {
                heldObject = null;
            
                UpdatePlayerItemType(ItemType.None);
            }
        }
    }
    
    [ServerRpc]
    void SwapObjectServerRpc(ulong heldObjectId, ulong newTargetId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(heldObjectId, out NetworkObject heldNetObj) ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(newTargetId, out NetworkObject newNetObj))
        {
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;
        
        heldNetObj.RemoveOwnership();
        ClearHeldObjectClientRpc(heldObjectId);
        
        newNetObj.ChangeOwnership(clientId);
        SetHeldObjectClientRpc(newTargetId, NetworkObjectId);
    }
    
    private void UpdatePlayerItemType(ItemType type)
    {
        if (playerInfo != null)
        {
            playerInfo.SetItemType(type);
        }
    }
}