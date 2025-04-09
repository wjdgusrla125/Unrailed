using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BlockPickup : NetworkBehaviour
{
    public Transform handPosition;
    public Transform twoHandPosition;
    public float detectionDistance = 1.5f;  // 감지 거리
    public Vector3 boxSize = new Vector3(1f, 0.5f, 1f);  // 감지 박스 크기
    public LayerMask pickupLayerMask;
    public InputReader inputReader; 

    public NetworkObject heldObject = null;
    private bool canPickup = false;
    private NetworkObject targetObject = null;
    [SerializeField] private PlayerInfo playerInfo;
    
    // 스택 관련 변수 추가
    private int stackCount = 0;
    private const int maxStackSize = 3;
    
    // 스택된 아이템의 오프셋 (아이템간 간격)
    [SerializeField] private Vector3 stackOffset = new Vector3(0, 0.1f, 0);
    
    // 스택된 오브젝트 저장 리스트
    private List<NetworkObject> stackedObjects = new List<NetworkObject>();

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

        // 플레이어 위치에서 아래쪽으로 약간 떨어진 위치를 중심으로 박스 검사
        Vector3 boxCenter = transform.position + Vector3.down * (detectionDistance * 0.5f);
        
        // OverlapBox를 사용하여 감지
        Collider[] colliders = Physics.OverlapBox(boxCenter, boxSize * 0.5f, Quaternion.identity, pickupLayerMask);
        
        if (colliders.Length > 0)
        {
            // 가장 가까운 물체를 찾기
            float closestDistance = float.MaxValue;
            NetworkObject closestNetObj = null;
            
            foreach (Collider collider in colliders)
            {
                NetworkObject netObj = collider.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    float distance = Vector3.Distance(transform.position, collider.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestNetObj = netObj;
                    }
                }
            }
            
            if (closestNetObj != null)
            {
                targetObject = closestNetObj;
                canPickup = true;
            }
            else
            {
                canPickup = false;
                targetObject = null;
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

            // 메인 아이템 위치 설정
            heldObject.transform.position = holdPosition.position;
            heldObject.transform.rotation = holdPosition.rotation;
            
            // 스택된 아이템들의 위치 업데이트
            UpdateStackedItemPositions(holdPosition);
        }
    }
    
    // 스택된 아이템들의 위치 업데이트
    private void UpdateStackedItemPositions(Transform basePosition)
    {
        for (int i = 0; i < stackedObjects.Count; i++)
        {
            if (stackedObjects[i] != null && stackedObjects[i].gameObject.activeSelf)
            {
                // 스택 순서에 따라 오프셋 계산 (i+1 인덱스로 시작)
                Vector3 offset = stackOffset * (i + 1);
                stackedObjects[i].transform.position = basePosition.position + offset;
                stackedObjects[i].transform.rotation = basePosition.rotation;
            }
        }
    }
    
    private void OnInteract(bool pressed)
    {
        if (!pressed) return;
        if (!IsOwner) return;

        if (heldObject != null && canPickup) 
        {
            // 스택 가능한 동일 아이템인지 확인
            Item heldItem = heldObject.GetComponent<Item>();
            Item targetItem = targetObject.GetComponent<Item>();
            
            if (heldItem != null && targetItem != null && 
                heldItem.IsStackable && targetItem.IsStackable && 
                heldItem.ItemType == targetItem.ItemType && 
                stackCount < maxStackSize)
            {
                // 동일한 스택 가능 아이템이면 스택
                StackItemServerRpc(targetObject.NetworkObjectId);
            }
            else
            {
                // 아니면 기존처럼 스왑
                SwapObjectServerRpc(heldObject.NetworkObjectId, targetObject.NetworkObjectId);
            }
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
                    
                    // 새로운 아이템 픽업 시 스택 카운트를 1로 초기화
                    playerPickup.stackCount = 1;
                    playerPickup.stackedObjects.Clear();
                }
            }
        }
    }

    [ServerRpc]
    void RequestDropServerRpc(ulong objectId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
            return;

        // 먼저 스택된 아이템들 드롭
        DropStackedItemsClientRpc();

        // 메인 아이템 드롭
        netObj.RemoveOwnership();
        ClearHeldObjectClientRpc(objectId);
    }

    [ClientRpc]
    void DropStackedItemsClientRpc()
    {
        if (!IsOwner) return;
        
        // 스택된 아이템들 드롭
        float dropOffset = 0.2f; // 아이템 간 간격
        
        for (int i = 0; i < stackedObjects.Count; i++)
        {
            if (stackedObjects[i] != null)
            {
                NetworkObject netObj = stackedObjects[i];
                netObj.gameObject.SetActive(true);
                netObj.transform.SetParent(null);
                
                // 아이템을 주변에 드롭
                Vector3 position = transform.position;
                position.y = 0.1f;
                // 각 아이템이 다른 위치에 떨어지도록 오프셋 추가
                position += new Vector3(
                    Random.Range(-dropOffset, dropOffset),
                    0,
                    Random.Range(-dropOffset, dropOffset)
                );
                
                netObj.transform.position = position;
                netObj.transform.rotation = Quaternion.identity;
            }
        }
        
        // 스택 리스트 초기화
        stackedObjects.Clear();
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
                stackCount = 0; // 스택 카운트 초기화
                
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
        
        // 스택된 아이템들 드롭
        DropStackedItemsClientRpc();
        
        heldNetObj.RemoveOwnership();
        ClearHeldObjectClientRpc(heldObjectId);
        
        newNetObj.ChangeOwnership(clientId);
        SetHeldObjectClientRpc(newTargetId, NetworkObjectId);
    }
    
    // 새로운 스택 아이템 처리 함수
    [ServerRpc]
    void StackItemServerRpc(ulong targetId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject netObj))
            return;
        
        AddToStackClientRpc(targetId, NetworkObjectId);
    }
    
    [ClientRpc]
    void AddToStackClientRpc(ulong objectId, ulong playerId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject itemObj) &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerId, out NetworkObject playerObj))
        {
            BlockPickup playerPickup = playerObj.GetComponent<BlockPickup>();
            if (playerPickup != null && playerPickup.stackCount < maxStackSize)
            {
                // 스택 카운트 증가
                playerPickup.stackCount++;
                
                // 스택 리스트에 아이템 추가
                if (playerPickup.IsOwner)
                {
                    playerPickup.stackedObjects.Add(itemObj);
                    
                    // UI 업데이트 등 필요한 작업 수행
                    Debug.Log($"Stack count increased: {playerPickup.stackCount}");
                    
                    // PlayerInfo에 스택 카운트 정보 업데이트 (필요한 경우)
                    if (playerPickup.playerInfo != null && playerPickup.heldObject != null)
                    {
                        Item heldItem = playerPickup.heldObject.GetComponent<Item>();
                        if (heldItem != null)
                        {
                            // 여기서 필요에 따라 PlayerInfo에 스택 카운트 정보를 전달할 수 있음
                            // 예: playerPickup.playerInfo.SetStackCount(playerPickup.stackCount);
                        }
                    }
                }
            }
        }
    }
    
    private void UpdatePlayerItemType(ItemType type)
    {
        if (playerInfo != null)
        {
            playerInfo.SetItemType(type);
        }
    }
    
    // 스택 카운트 가져오기 함수
    public int GetStackCount()
    {
        return stackCount;
    }
    
    // 디버그용 - 박스 시각화
    private void OnDrawGizmos()
    {
        Vector3 boxCenter = transform.position + Vector3.down * detectionDistance * 0.5f;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(boxCenter, boxSize);
    }
}