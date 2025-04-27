using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class Tile : NetworkBehaviour
{
    private NetworkVariable<ItemType> currentItemType = new NetworkVariable<ItemType>(ItemType.None);
    private NetworkVariable<int> itemCount = new NetworkVariable<int>(0);

    [SerializeField] private Transform itemPoint;
    [SerializeField] private float stackHeight = 0.2f;
    [SerializeField] private GameObject initialItemPrefab;
    [SerializeField] private int initialItemCount = 1;

    private Stack<ulong> stackedItemIds = new Stack<ulong>();
    public List<ulong> debugStackItems = new List<ulong>();

    //네트워크 동기화 및 초기화 관련
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer && !IsClient && initialItemPrefab != null)
            Invoke("SpawnInitialItems", 4.5f);
        else if (IsServer && IsClient && initialItemPrefab != null && stackedItemIds.Count == 0)
            Invoke("SpawnInitialItems", 4.5f);

        currentItemType.OnValueChanged += OnItemTypeChanged;
        itemCount.OnValueChanged += OnItemCountChanged;

        if (IsClient && !IsServer)
            RequestInitialStateServerRpc();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        currentItemType.OnValueChanged -= OnItemTypeChanged;
        itemCount.OnValueChanged -= OnItemCountChanged;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestInitialStateServerRpc(ServerRpcParams rpcParams = default)
    {
        SyncInitialStateClientRpc(stackedItemIds.ToArray(), new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId }
            }
        });
    }

    [ClientRpc]
    private void SyncInitialStateClientRpc(ulong[] items, ClientRpcParams rpcParams = default)
    {
        stackedItemIds.Clear();
        for (int i = items.Length - 1; i >= 0; i--)
            stackedItemIds.Push(items[i]);

        UpdateDebugList();
        UpdateVisibleStack();
    }

    [ClientRpc]
    public void SyncStackedItemsClientRpc(ulong[] items)
    {
        stackedItemIds.Clear();
        debugStackItems.Clear();

        // 역순으로 스택에 추가 (스택 순서 유지를 위해)
        for (int i = items.Length - 1; i >= 0; i--)
        {
            if (items[i] != 0)
            {
                stackedItemIds.Push(items[i]);
                debugStackItems.Add(items[i]);
            }
        }

        // 수정: itemCount 값도 업데이트
        if (IsServer)
        {
            itemCount.Value = stackedItemIds.Count;
        }

        // 수정: 타입이 없는 경우에만 타입 업데이트
        if (stackedItemIds.Count > 0 && currentItemType.Value == ItemType.None)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(stackedItemIds.Peek(), out NetworkObject netObj))
            {
                Item item = netObj.GetComponent<Item>();
                if (item != null && IsServer)
                {
                    currentItemType.Value = item.ItemType;
                }
            }
        }
        else if (stackedItemIds.Count == 0 && IsServer)
        {
            currentItemType.Value = ItemType.None;
        }

        // 스택 시각적 업데이트
        UpdateVisibleStack();
    }
    
    //아이템 스택 조작 관련
    public void AddItem(ItemType itemType)
    {
        if (IsServer)
        {
            if (currentItemType.Value == ItemType.None)
                currentItemType.Value = itemType;

            itemCount.Value++;
        }
        else
        {
            AddItemServerRpc(itemType);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddItemServerRpc(ItemType itemType)
    {
        if (currentItemType.Value == ItemType.None)
            currentItemType.Value = itemType;

        itemCount.Value++;
    }

    public void AddItemToStack(ulong itemNetId)
    {
        if (IsServer)
        {
            stackedItemIds.Push(itemNetId);
            SyncStackedItemsClientRpc(stackedItemIds.ToArray());
        }
        else
        {
            AddItemToStackServerRpc(itemNetId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddItemToStackServerRpc(ulong itemNetId)
    {
        stackedItemIds.Push(itemNetId);
        SyncStackedItemsClientRpc(stackedItemIds.ToArray());
    }

    public ulong RemoveTopItemFromStack()
    {
        if (IsServer)
            return RemoveTopItemFromStackInternal();
        else
        {
            RemoveTopItemFromStackServerRpc();
            return 0;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RemoveTopItemFromStackServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong topItemId = RemoveTopItemFromStackInternal();
        RemoveTopItemFromStackResponseClientRpc(topItemId, rpcParams.Receive.SenderClientId);
    }

    private ulong RemoveTopItemFromStackInternal()
    {
        if (stackedItemIds.Count == 0) return 0;

        ulong topItemId = stackedItemIds.Pop();
        itemCount.Value--;

        if (itemCount.Value <= 0)
        {
            itemCount.Value = 0;
            currentItemType.Value = ItemType.None;
        }

        SyncStackedItemsClientRpc(stackedItemIds.ToArray());
        return topItemId;
    }

    [ClientRpc]
    public void RemoveTopItemFromStackResponseClientRpc(ulong itemId, ulong requestingClientId)
    {
        if (NetworkManager.Singleton.LocalClientId == requestingClientId)
            OnItemRemoved(itemId);
    }

    public ulong PeekTopItemFromStack()
    {
        if (stackedItemIds.Count == 0) return 0;
        return stackedItemIds.Peek();
    }
    
    public void ForceAddItemToStackFromServer(ulong itemNetId)
    {
        if (!IsServer) return;

        Debug.Log($"[DEBUG] ForceAddItemToStackFromServer called with itemNetId: {itemNetId}");
        
        // Stack의 Contains 메소드 사용 여부 검사
        bool containsItem = false;
        Debug.Log($"[DEBUG] Current stack count: {stackedItemIds.Count}");
        
        // 스택의 모든 아이템 출력
        foreach (ulong id in stackedItemIds)
        {
            Debug.Log($"[DEBUG] Stack contains item: {id}");
            if (id == itemNetId)
            {
                containsItem = true;
                Debug.Log($"[DEBUG] Found duplicate item {itemNetId} in stack!");
            }
        }
        
        // 중복 확인 (이미 스택에 있는 아이템인지)
        if (containsItem)
        {
            Debug.LogWarning($"Item {itemNetId} is already in the stack. Skipping.");
            return;
        }

        Debug.Log($"[DEBUG] Checking if item exists in NetworkManager...");
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out NetworkObject netObj))
        {
            Debug.Log($"[DEBUG] Found NetworkObject for itemNetId: {itemNetId}");
            
            // 아이템 타입 확인 및 저장
            Item item = netObj.GetComponent<Item>();
            if (item != null)
            {
                Debug.Log($"[DEBUG] Item component found, type: {item.ItemType}");
                if (currentItemType.Value == ItemType.None)
                {
                    Debug.Log($"[DEBUG] Setting tile item type to: {item.ItemType}");
                    currentItemType.Value = item.ItemType;
                }
            }
            else
            {
                Debug.LogError($"[DEBUG] Item component NOT found on NetworkObject: {itemNetId}");
            }
        
            // 스택에 추가
            Debug.Log($"[DEBUG] Adding item {itemNetId} to stack");
            stackedItemIds.Push(itemNetId);
            Debug.Log($"[DEBUG] New stack count: {stackedItemIds.Count}");
            itemCount.Value = stackedItemIds.Count;
        
            // 아이템 위치 및 회전 설정
            Debug.Log($"[DEBUG] Setting item position and rotation");
            netObj.transform.position = GetItemPositionAtHeight(stackedItemIds.Count - 1);
            netObj.transform.rotation = itemPoint.rotation;
        
            // 시각적 업데이트를 위해 모든 클라이언트에 동기화
            Debug.Log($"[DEBUG] Calling SyncStackedItemsClientRpc");
            SyncStackedItemsClientRpc(stackedItemIds.ToArray());
        }
        else
        {
            Debug.LogError($"[DEBUG] NetworkObject NOT found for itemNetId: {itemNetId}");
        }
    }
    
    [ClientRpc]
    public void ForceSetStackClientRpc(ulong[] itemIds)
    {
        stackedItemIds.Clear();
    
        // 역순으로 스택에 추가 (스택 특성 유지)
        for (int i = itemIds.Length - 1; i >= 0; i--)
        {
            if (itemIds[i] != 0)
            {
                stackedItemIds.Push(itemIds[i]);
            }
        }
    
        UpdateDebugList();
        UpdateVisibleStack();
    }
    
    //아이템 시각화 및 디버깅
    public void UpdateVisibleStack()
    {
        ulong[] itemArray = stackedItemIds.ToArray();

        for (int i = 0; i < itemArray.Length; i++)
        {
            NetworkObject netObj = GetNetworkObjectById(itemArray[itemArray.Length - 1 - i]);
            if (netObj != null)
            {
                netObj.gameObject.SetActive(true);
                netObj.transform.position = GetItemPositionAtHeight(i);
                netObj.transform.rotation = itemPoint.rotation;
            }
        }
    }

    private void UpdateDebugList()
    {
        debugStackItems.Clear();
        debugStackItems.AddRange(stackedItemIds);
    }

    private void OnItemRemoved(ulong itemId)
    {
        // 클라이언트 후처리 (UI 등)
    }

    public Vector3 GetItemPositionAtHeight(int stackIndex)
    {
        Vector3 position = itemPoint.position;
        position.y += stackHeight * stackIndex;
        return position;
    }

    //초기 아이템 생성
    private void SpawnInitialItems()
    {
        if (initialItemPrefab == null) return;

        Item itemComponent = initialItemPrefab.GetComponent<Item>();
        if (itemComponent == null) return;

        currentItemType.Value = itemComponent.ItemType;
        int count = itemComponent.IsStackable ? Mathf.Clamp(initialItemCount, 1, 3) : 1;

        for (int i = 0; i < count; i++)
        {
            GameObject itemInstance = Instantiate(initialItemPrefab, GetItemPositionAtHeight(i), itemPoint.rotation);
            NetworkObject netObj = itemInstance.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                stackedItemIds.Push(netObj.NetworkObjectId);
                itemCount.Value++;
            }
        }

        SyncStackedItemsClientRpc(stackedItemIds.ToArray());
    }

    public void DropitialItems(GameObject obj)
    {
        Item itemComponent = obj.GetComponent<Item>();
        if (itemComponent == null) return;

        currentItemType.Value = itemComponent.ItemType;
        int count = itemComponent.IsStackable ? Mathf.Clamp(initialItemCount, 1, 3) : 1;

        for (int i = 0; i < count; i++)
        {
            GameObject itemInstance = Instantiate(obj, GetItemPositionAtHeight(i), itemPoint.rotation);
            NetworkObject netObj = itemInstance.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                stackedItemIds.Push(netObj.NetworkObjectId);
                itemCount.Value++;
            }
        }

        SyncStackedItemsClientRpc(stackedItemIds.ToArray());
    }

    //상태 접근자 (Getter)
    public ItemType GetCurrentItemType() => currentItemType.Value;
    public int GetStackSize() => stackedItemIds.Count;

    private NetworkObject GetNetworkObjectById(ulong objectId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
            return netObj;
        return null;
    }
    
    //이벤트 핸들러
    private void OnItemTypeChanged(ItemType previous, ItemType current)
    {
        // 처리 로직
    }

    private void OnItemCountChanged(int previous, int current)
    {
        // 처리 로직
    }
}