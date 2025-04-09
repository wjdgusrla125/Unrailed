using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System;

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
    [SerializeField] private DeskInfo deskInfo;
    // 스택 관련 변수 추가
    private int stackCount = 0;
    private const int maxStackSize = 3;
    
    // 스택된 아이템의 오프셋 (아이템간 간격)
    [SerializeField] private Vector3 stackOffset = new Vector3(0, 0.1f, 0);
    [SerializeField] private Vector3 dropOffset = new Vector3(0, 0.1f, 0); // 버릴 때 쌓임 간격

    // 스택된 오브젝트 저장 리스트
    private List<NetworkObject> stackedObjects = new List<NetworkObject>();
    
    // 새로 추가: 바닥에 쌓인 오브젝트들을 감지하기 위한 변수
    private List<NetworkObject> detectedStackedObjects = new List<NetworkObject>();
    private float stackDetectionRadius = 0.5f; // 스택 감지 반경

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

    public void Start()
    {
        deskInfo = FindObjectOfType<DeskInfo>();
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
                
                // 새로 추가: 감지된 오브젝트 주변의 스택된 오브젝트들 찾기
                FindStackedObjectsAround(targetObject);
            }
            else
            {
                canPickup = false;
                targetObject = null;
                detectedStackedObjects.Clear();
            }
        }
        else
        {
            canPickup = false;
            targetObject = null;
            detectedStackedObjects.Clear();
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
    
    // 새로 추가: 대상 오브젝트 주변의 스택된 오브젝트들을 찾는 함수
    private void FindStackedObjectsAround(NetworkObject baseObject)
    {
        detectedStackedObjects.Clear();
        
        if (baseObject == null) return;
        
        // 기본 위치는 대상 오브젝트의 위치
        Vector3 basePosition = baseObject.transform.position;
        
        // 쌓여있는 물체 감지를 위해 주변 Collider 검사
        Collider[] nearbyColliders = Physics.OverlapSphere(
            basePosition, stackDetectionRadius, pickupLayerMask);
        
        // 대상 오브젝트의 아이템 타입 확인
        Item baseItem = baseObject.GetComponent<Item>();
        if (baseItem == null || !baseItem.IsStackable) return;
        
        // 먼저 기본 오브젝트를 리스트에 추가
        detectedStackedObjects.Add(baseObject);
        
        // 높이 정렬을 위한 임시 리스트
        List<NetworkObject> tempObjects = new List<NetworkObject>();
        
        foreach (Collider collider in nearbyColliders)
        {
            NetworkObject netObj = collider.GetComponent<NetworkObject>();
            Item item = collider.GetComponent<Item>();
            
            // 기본 오브젝트와 다른 오브젝트이고, 같은 타입의 스택 가능한 아이템이면 추가
            if (netObj != null && netObj != baseObject && item != null && 
                item.IsStackable && item.ItemType == baseItem.ItemType)
            {
                tempObjects.Add(netObj);
            }
        }
        
        // 높이에 따라 정렬 (낮은 것부터 높은 순으로)
        tempObjects.Sort((obj1, obj2) => 
            obj1.transform.position.y.CompareTo(obj2.transform.position.y));
        
        // 정렬된 오브젝트들을 감지 리스트에 추가 (최대 maxStackSize-1개 추가 가능)
        foreach (NetworkObject obj in tempObjects)
        {
            if (detectedStackedObjects.Count < maxStackSize)
            {
                detectedStackedObjects.Add(obj);
            }
            else
            {
                break;
            }
        }
        
        Debug.Log($"Found {detectedStackedObjects.Count} stacked objects");
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
        

        /*if (playerInfo.hitBlock == BlockType.CraftingTable)
        {
            if (playerInfo.itemType == ItemType.WoodPlank && playerInfo.CraftingTableObject.AbleInTableWood)
            {
                if (stackedObjects.Count >= 1)
                {
                    if (playerInfo.CraftingTableObject.WoodObjects.Count + (stackedObjects.Count + 1) > 3)
                        return;
                    Debug.Log("나무 2개 이상");
                    playerInfo.CraftingTableObject.OnTableItem(heldObject);
                    // 스택된 아이템들 드롭
                    for (int i = 0; i < stackedObjects.Count; i++)
                    {
                        if (stackedObjects[i] != null)
                        {
                            playerInfo.CraftingTableObject.OnTableItem(stackedObjects[i]);
                        }
                    }
                    stackedObjects.Clear();
                    heldObject.RemoveOwnership();
                    RequestDropServerRpc(heldObject.NetworkObjectId);
                }
                else
                {
                    playerInfo.CraftingTableObject.OnTableItem(heldObject);
                    heldObject.RemoveOwnership();
                    RequestDropServerRpc(heldObject.NetworkObjectId);
                }
            }
            else if (playerInfo.itemType == ItemType.Iron && playerInfo.CraftingTableObject.AbleInTableIron)
            {
                if (stackedObjects.Count >= 1)
                {
                    if (playerInfo.CraftingTableObject.IronObjects.Count + (stackedObjects.Count + 1) > 3)
                        return;
                    Debug.Log("철 2개 이상");
                    playerInfo.CraftingTableObject.OnTableItem(heldObject);
                    // 스택된 아이템들 드롭
                    for (int i = 0; i < stackedObjects.Count; i++)
                    {
                        if (stackedObjects[i] != null)
                        {
                            playerInfo.CraftingTableObject.OnTableItem(stackedObjects[i]);
                        }
                    }
                    stackedObjects.Clear();
                    heldObject.RemoveOwnership();
                    RequestDropServerRpc(heldObject.NetworkObjectId);
                }
                else
                {
                    playerInfo.CraftingTableObject.OnTableItem(heldObject);
                    heldObject.RemoveOwnership();
                    RequestDropServerRpc(heldObject.NetworkObjectId);
                }
            }
            return;
        }*/

        /*if (playerInfo.hitBlock == BlockType.DeskTable && playerInfo.itemType == ItemType.None && deskInfo.RailCount != 0)
        {
            switch (deskInfo.RailCount)
            {
                case 1:
                    GameObject temp = GameObject.Instantiate(deskInfo.GetRailObject());
                    temp.gameObject.transform.position = playerInfo.gameObject.transform.position;
                    RequestPickUpServerRpc(temp.GetComponent<NetworkObject>().NetworkObjectId);
                    break;
                case 2:
                    break;
                case 3:
                    break;
            }
            return;
        }*/


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
            // 새로 추가: 스택된 오브젝트들이 있으면 모두 집도록 처리
            if (detectedStackedObjects.Count > 1)
            {
                RequestPickUpStackServerRpc(targetObject.NetworkObjectId);
            }
            else
            {
                // 기존 로직 유지
                RequestPickUpServerRpc(targetObject.NetworkObjectId);
            }
        }
        else if (heldObject != null)
        {
            RequestDropServerRpc(heldObject.NetworkObjectId);
        }
    }

    // 새로 추가: 스택된 오브젝트들을 한번에 집는 함수
    [ServerRpc]
    void RequestPickUpStackServerRpc(ulong targetId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject netObj))
            return;

        // 첫 번째 오브젝트(가장 아래 있는)를 메인 아이템으로 설정
        netObj.ChangeOwnership(rpcParams.Receive.SenderClientId);
        PickUpStackClientRpc(targetId, NetworkObjectId);
    }
    
    [ClientRpc]
    void PickUpStackClientRpc(ulong objectId, ulong playerId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj) &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerId, out NetworkObject playerObj))
        {
            BlockPickup playerPickup = playerObj.GetComponent<BlockPickup>();
            if (playerPickup != null && playerPickup.IsOwner)
            {
                // 첫 번째 아이템 설정
                playerPickup.heldObject = netObj;
                playerPickup.stackCount = 1;
                playerPickup.stackedObjects.Clear();
                
                // 감지된 스택 아이템 처리
                List<NetworkObject> stackList = new List<NetworkObject>(playerPickup.detectedStackedObjects);
                
                // 첫 번째는 이미 메인 아이템으로 설정했으므로 제외
                stackList.RemoveAt(0);
                
                // 나머지 아이템들을 스택에 추가
                foreach (NetworkObject stackObj in stackList)
                {
                    if (playerPickup.stackCount < maxStackSize)
                    {
                        playerPickup.stackedObjects.Add(stackObj);
                        playerPickup.stackCount++;
                        
                        // 서버에 소유권 변경 요청
                        playerPickup.RequestStackItemOwnershipServerRpc(stackObj.NetworkObjectId);
                    }
                    else
                    {
                        break;
                    }
                }
                
                // 플레이어 아이템 타입 업데이트
                Item heldItem = netObj.GetComponent<Item>();
                if (heldItem != null && playerPickup.playerInfo != null)
                {
                    playerPickup.UpdatePlayerItemType(heldItem.ItemType);
                }
                else
                {
                    playerPickup.UpdatePlayerItemType(ItemType.None);
                }
                
                Debug.Log($"Picked up stack. Total count: {playerPickup.stackCount}");
            }
        }
    }
    
    [ServerRpc]
    void RequestStackItemOwnershipServerRpc(ulong objectId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
            return;
            
        netObj.ChangeOwnership(rpcParams.Receive.SenderClientId);
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

        // 스택된 아이템 및 메인 아이템 드롭
        DropAllItemsClientRpc(transform.position);

        netObj.RemoveOwnership();
        ClearHeldObjectClientRpc(objectId);
    }

    [ClientRpc]
    void DropAllItemsClientRpc(Vector3 dropPosition)
    {
        if (!IsOwner) return;
        
        // 기준 위치 설정 - 플레이어 앞쪽 약간 떨어진 곳
        Vector3 basePosition = dropPosition + transform.forward * 0.5f;
        basePosition.y = 0.1f; // 바닥에 위치하도록 높이 조정
        
        // 스택된 아이템들 드롭 - 수직으로 쌓이게 배치
        for (int i = 0; i < stackedObjects.Count; i++)
        {
            if (stackedObjects[i] != null)
            {
                NetworkObject netObj = stackedObjects[i];
                netObj.gameObject.SetActive(true);
                netObj.transform.SetParent(null);
                
                // 아이템을 수직으로 쌓아서 배치
                Vector3 stackPosition = basePosition + dropOffset * (i + 1);
                netObj.transform.position = stackPosition;
                netObj.transform.rotation = Quaternion.identity;
            }
        }
        
        // 메인 아이템도 같은 위치에 배치 (가장 아래)
        if (heldObject != null)
        {
            heldObject.transform.position = basePosition;
            heldObject.transform.rotation = Quaternion.identity;
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
            
            // ClearHeldObject는 이제 위치 조정을 하지 않음 (DropAllItemsClientRpc에서 처리)
            
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
        DropAllItemsClientRpc(heldNetObj.transform.position);
        
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
        
        // 스택 처리 - 이제 비활성화하지 않고 보이게 스택함
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
        
        // 스택 감지 반경 시각화 (선택한 오브젝트가 있을 때)
        if (targetObject != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(targetObject.transform.position, stackDetectionRadius);
        }
    }
}