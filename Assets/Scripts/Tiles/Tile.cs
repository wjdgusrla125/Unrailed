using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class Tile : NetworkBehaviour
{
    // NetworkVariable로 중요 상태 변수들을 바꿔서 자동 동기화되도록 함
    private NetworkVariable<ItemType> currentItemType = new NetworkVariable<ItemType>(ItemType.None);
    private NetworkVariable<int> itemCount = new NetworkVariable<int>(0);
    
    [SerializeField] private Transform itemPoint;
    
    // 스택 아이템 ID는 직렬화가 까다로우므로 ServerRpc와 ClientRpc로 동기화 처리
    private Stack<ulong> stackedItemIds = new Stack<ulong>();
    
    public List<ulong> debugStackItems = new List<ulong>();
    
    [SerializeField] private float stackHeight = 0.1f;
    [SerializeField] private GameObject initialItemPrefab;
    [SerializeField] private int initialItemCount = 1;
    
    // 클라이언트들에게 스택 상태를 동기화하기 위한 RPC
    [ClientRpc]
    public void SyncStackedItemsClientRpc(ulong[] items)
    {
        stackedItemIds.Clear();
        // 역순으로 추가해야 스택 순서가 유지됨
        for (int i = items.Length - 1; i >= 0; i--)
        {
            stackedItemIds.Push(items[i]);
        }
        UpdateDebugList();
        UpdateVisibleStack();
    }
    
    private void UpdateDebugList()
    {
        debugStackItems.Clear();
        debugStackItems.AddRange(stackedItemIds);
    }
    
    public Transform GetItemPoint()
    {
        return itemPoint;
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 순수 서버일 때만 초기 아이템을 스폰하도록 수정
        if (IsServer && !IsClient && initialItemPrefab != null)
        {
            SpawnInitialItems();
        }
        // 호스트 모드(서버+클라이언트)일 때는 한 번만 실행되도록 별도 조건 추가
        else if (IsServer && IsClient && initialItemPrefab != null && stackedItemIds.Count == 0)
        {
            SpawnInitialItems();
        }

        // NetworkVariable 값 변경 감지 이벤트 구독
        currentItemType.OnValueChanged += OnItemTypeChanged;
        itemCount.OnValueChanged += OnItemCountChanged;

        // 클라이언트가 접속할 때 현재 스택 상태 동기화
        if (IsClient && !IsServer)
        {
            // 서버에 현재 상태 요청
            RequestInitialStateServerRpc();
        }
    }
    
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        // 네트워크 변수 이벤트 구독 해제
        currentItemType.OnValueChanged -= OnItemTypeChanged;
        itemCount.OnValueChanged -= OnItemCountChanged;
    }
    
    private void OnItemTypeChanged(ItemType previous, ItemType current)
    {
        // ItemType이 변경되었을 때 필요한 처리
    }
    
    private void OnItemCountChanged(int previous, int current)
    {
        // ItemCount가 변경되었을 때 필요한 처리
    }
    
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
    
        // 모든 클라이언트에게 스택 상태 동기화
        SyncStackedItemsClientRpc(stackedItemIds.ToArray());
    }
    
    public Vector3 GetItemPositionAtHeight(int stackIndex)
    {
        Vector3 position = itemPoint.position;
        position.y += stackHeight * stackIndex;
        return position;
    }
    
    public bool CanPlaceItem(ItemType itemType, bool isStackable)
    {
        if (currentItemType.Value == ItemType.None)
            return true;
            
        if (currentItemType.Value == itemType && isStackable)
            return true;
            
        return false;
    }
    
    // 서버에서만 호출되어야 하는 메서드
    [ServerRpc(RequireOwnership = false)]
    public void AddItemServerRpc(ItemType itemType)
    {
        if (currentItemType.Value == ItemType.None)
            currentItemType.Value = itemType;
            
        itemCount.Value++;
    }
    
    // 클라이언트와 서버에서 호출 가능 (서버 검증 추가)
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
    
    // 서버에서만 호출되어야 하는 메서드
    [ServerRpc(RequireOwnership = false)]
    public void AddItemToStackServerRpc(ulong itemNetId)
    {
        stackedItemIds.Push(itemNetId);
        SyncStackedItemsClientRpc(stackedItemIds.ToArray());
    }
    
    // 클라이언트와 서버에서 호출 가능 (서버 검증 추가)
    public void AddItemToStack(ulong itemNetId)
    {
        if (IsServer)
        {
            // 서버에서만 실제로 아이템을 스택에 추가
            stackedItemIds.Push(itemNetId);
            // 모든 클라이언트에게 결과 동기화 (중복 방지를 위해 서버에서만 호출)
            SyncStackedItemsClientRpc(stackedItemIds.ToArray());
        }
        else if (IsClient && !IsServer)
        {
            // 클라이언트에서는 서버에 요청만 하고, 실제 작업은 서버 RPC 응답으로 처리
            AddItemToStackServerRpc(itemNetId);
        }
    }
    
    // 서버에서만 동작하는 메서드
    [ServerRpc(RequireOwnership = false)]
    public void RemoveTopItemFromStackServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong topItemId = RemoveTopItemFromStackInternal();
        // 호출자에게 결과 전달
        RemoveTopItemFromStackResponseClientRpc(topItemId, rpcParams.Receive.SenderClientId);
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void RequestInitialStateServerRpc(ServerRpcParams rpcParams = default)
    {
        // 요청한 클라이언트에게 현재 상태 전송
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
        // 스택 상태 초기화
        stackedItemIds.Clear();
    
        // 역순으로 추가해야 스택 순서가 유지됨
        for (int i = items.Length - 1; i >= 0; i--)
        {
            stackedItemIds.Push(items[i]);
        }
    
        UpdateDebugList();
        UpdateVisibleStack();
    }
    
    [ClientRpc]
    public void RemoveTopItemFromStackResponseClientRpc(ulong itemId, ulong requestingClientId)
    {
        // 요청한 클라이언트에게만 결과 전달
        if (NetworkManager.Singleton.LocalClientId == requestingClientId)
        {
            // 클라이언트 측에서 처리할 콜백 함수나 이벤트 트리거
            OnItemRemoved(itemId);
        }
    }
    
    // 내부 구현 (서버에서만 호출)
    private ulong RemoveTopItemFromStackInternal()
    {
        if (stackedItemIds.Count == 0) return 0;
        
        ulong topItemId = stackedItemIds.Pop();
        
        // 아이템 카운트 감소 및 타입 업데이트
        itemCount.Value--;
        if (itemCount.Value <= 0)
        {
            itemCount.Value = 0;
            currentItemType.Value = ItemType.None;
        }
        
        // 스택 상태 동기화
        SyncStackedItemsClientRpc(stackedItemIds.ToArray());
        
        return topItemId;
    }
    
    // 서버와 클라이언트 모두에서 호출 가능
    public ulong RemoveTopItemFromStack()
    {
        if (IsServer)
        {
            return RemoveTopItemFromStackInternal();
        }
        else
        {
            // 클라이언트에서는 서버에 요청
            RemoveTopItemFromStackServerRpc();
            // 클라이언트에서는 0을 반환하고, 실제 값은 ClientRpc 응답에서 처리
            return 0;
        }
    }
    
    // 클라이언트에서 아이템이 제거되었을 때 호출되는 콜백
    private void OnItemRemoved(ulong itemId)
    {
        // 클라이언트 측에서 필요한 처리 (UI 업데이트 등)
    }
    
    public ulong PeekTopItemFromStack()
    {
        if (stackedItemIds.Count == 0) return 0;
        return stackedItemIds.Peek();
    }
    
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
    
    private NetworkObject GetNetworkObjectById(ulong objectId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            return netObj;
        }
        return null;
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void RemoveItemServerRpc()
    {
        itemCount.Value--;
    
        if (itemCount.Value <= 0)
        {
            itemCount.Value = 0;
            currentItemType.Value = ItemType.None;
            stackedItemIds.Clear();
            SyncStackedItemsClientRpc(new ulong[0]);
        }
    }
    
    public void RemoveItem()
    {
        if (IsServer)
        {
            itemCount.Value--;
        
            if (itemCount.Value <= 0)
            {
                itemCount.Value = 0;
                currentItemType.Value = ItemType.None;
                stackedItemIds.Clear();
                SyncStackedItemsClientRpc(new ulong[0]);
            }
        }
        else
        {
            RemoveItemServerRpc();
        }
    }
    
    public ItemType GetCurrentItemType()
    {
        return currentItemType.Value;
    }
    
    public int GetItemCount()
    {
        return itemCount.Value;
    }
    
    public ulong[] GetStackedItemsArray()
    {
        return stackedItemIds.ToArray();
    }
    
    public Stack<ulong> GetStackedItemIds()
    {
        return stackedItemIds;
    }
    
    public int GetStackSize()
    {
        return stackedItemIds.Count;
    }
    
    public void SetInitialItem(GameObject itemPrefab, int count = 1)
    {
        initialItemPrefab = itemPrefab;
        initialItemCount = count;
    }
}