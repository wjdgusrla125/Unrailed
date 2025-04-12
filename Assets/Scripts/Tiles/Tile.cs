using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class Tile : NetworkBehaviour
{
    private NetworkVariable<ItemType> currentItemType = new NetworkVariable<ItemType>(ItemType.None);
    private NetworkVariable<int> itemCount = new NetworkVariable<int>(0);

    [SerializeField] private Transform itemPoint;
    [SerializeField] private float stackHeight = 0.1f;
    [SerializeField] private GameObject initialItemPrefab;
    [SerializeField] private int initialItemCount = 1;

    private Stack<ulong> stackedItemIds = new Stack<ulong>();
    public List<ulong> debugStackItems = new List<ulong>();

    //네트워크 동기화 및 초기화 관련
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer && !IsClient && initialItemPrefab != null)
            SpawnInitialItems();
        else if (IsServer && IsClient && initialItemPrefab != null && stackedItemIds.Count == 0)
            SpawnInitialItems();

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

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != 0)
            {
                stackedItemIds.Push(items[i]);
                debugStackItems.Add(items[i]);
            }
        }

        UpdateVisibleStack();
        // itemCount.Value = stackedItemIds.Count;
        //
        // if (stackedItemIds.Count > 0 && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(stackedItemIds.Peek(), out NetworkObject netObj))
        // {
        //     Item item = netObj.GetComponent<Item>();
        //     if (item != null)
        //         currentItemType.Value = item.ItemType;
        // }
        // else
        // {
        //     currentItemType.Value = ItemType.None;
        // }
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

        stackedItemIds.Push(itemNetId);
        itemCount.Value++;

        SyncStackedItemsClientRpc(stackedItemIds.ToArray());
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