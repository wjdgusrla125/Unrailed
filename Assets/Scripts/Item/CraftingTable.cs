using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class CraftingTable : NetworkBehaviour
{
    private NetworkVariable<int> woodCount = new NetworkVariable<int>(0);
    private NetworkVariable<int> ironCount = new NetworkVariable<int>(0);
    
    private Stack<ulong> woodStackedItemIds = new Stack<ulong>();
    private Stack<ulong> ironStackedItemIds = new Stack<ulong>();
    public List<ulong> debugWoodItems = new List<ulong>();
    public List<ulong> debugIronItems = new List<ulong>();
    
    [SerializeField] private Transform woodTransform;
    [SerializeField] private Transform ironTransform;
    
    [SerializeField] private float stackHeight = 0.1f;
    [SerializeField] private DeskInfo targetDesk;
    
    private const int MAX_STACK_SIZE = 3;
    
    // 레일 제작에 필요한 재료 정의
    private const int WOOD_REQUIRED = 1;
    private const int IRON_REQUIRED = 1;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsClient && !IsServer)
        {
            RequestInitialStateServerRpc();
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void RequestInitialStateServerRpc(ServerRpcParams rpcParams = default)
    {
        SyncInitialStateClientRpc(
            woodStackedItemIds.ToArray(), 
            ironStackedItemIds.ToArray(), 
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId }
                }
            }
        );
    }
    
    public bool AddWoodItem(ulong itemNetId)
    {
        if (IsServer)
        {
            return AddWoodItemInternal(itemNetId);
        }
        else
        {
            AddWoodItemServerRpc(itemNetId);
            return true;
        }
    }
    
    public bool AddIronItem(ulong itemNetId)
    {
        if (IsServer)
        {
            return AddIronItemInternal(itemNetId);
        }
        else
        {
            AddIronItemServerRpc(itemNetId);
            return true;
        }
    }

    public ulong RemoveTopWoodItemFromStack()
    {
        if (IsServer)
            return RemoveTopWoodItemInternal();
        else
        {
            RemoveTopWoodItemServerRpc();
            return 0;
        }
    }
    
    public ulong RemoveTopIronItemFromStack()
    {
        if (IsServer)
            return RemoveTopIronItemInternal();
        else
        {
            RemoveTopIronItemServerRpc();
            return 0;
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void AddWoodItemServerRpc(ulong itemNetId)
    {
        bool result = AddWoodItemInternal(itemNetId);
        // 비어있는 응답은 제거해도 됨
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void AddIronItemServerRpc(ulong itemNetId)
    {
        bool result = AddIronItemInternal(itemNetId);
        // 비어있는 응답은 제거해도 됨
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void RemoveTopWoodItemServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong topItemId = RemoveTopWoodItemInternal();
        // 비어있는 응답은 제거해도 됨
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void RemoveTopIronItemServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong topItemId = RemoveTopIronItemInternal();
        // 비어있는 응답은 제거해도 됨
    }
    
    [ClientRpc]
    private void SyncInitialStateClientRpc(ulong[] woodItems, ulong[] ironItems, ClientRpcParams rpcParams = default)
    {
        woodStackedItemIds.Clear();
        for (int i = woodItems.Length - 1; i >= 0; i--)
            woodStackedItemIds.Push(woodItems[i]);
        
        ironStackedItemIds.Clear();
        for (int i = ironItems.Length - 1; i >= 0; i--)
            ironStackedItemIds.Push(ironItems[i]);

        UpdateDebugLists();
        UpdateVisibleStacks();
    }
    
    [ClientRpc]
    private void SyncWoodStackClientRpc(ulong[] items)
    {
        woodStackedItemIds.Clear();
        debugWoodItems.Clear();
        
        for (int i = items.Length - 1; i >= 0; i--)
        {
            if (items[i] != 0)
            {
                woodStackedItemIds.Push(items[i]);
                debugWoodItems.Add(items[i]);
            }
        }
        
        if (IsServer)
        {
            woodCount.Value = woodStackedItemIds.Count;
        }

        UpdateVisibleWoodStack();
    }
    
    [ClientRpc]
    private void SyncIronStackClientRpc(ulong[] items)
    {
        ironStackedItemIds.Clear();
        debugIronItems.Clear();
        
        for (int i = items.Length - 1; i >= 0; i--)
        {
            if (items[i] != 0)
            {
                ironStackedItemIds.Push(items[i]);
                debugIronItems.Add(items[i]);
            }
        }
        
        if (IsServer)
        {
            ironCount.Value = ironStackedItemIds.Count;
        }

        UpdateVisibleIronStack();
    }
    
    private bool AddWoodItemInternal(ulong itemNetId)
    {
        if (woodStackedItemIds.Count >= MAX_STACK_SIZE)
            return false;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out NetworkObject netObj))
        {
            Item item = netObj.GetComponent<Item>();
            if (item != null && item.ItemType == ItemType.WoodPlank)
            {
                woodStackedItemIds.Push(itemNetId);
                woodCount.Value = woodStackedItemIds.Count;
                
                netObj.transform.position = GetWoodPositionAtHeight(woodStackedItemIds.Count - 1);
                netObj.transform.rotation = Quaternion.identity;
                
                SyncWoodStackClientRpc(woodStackedItemIds.ToArray());
                
                TryCraftRail();
                return true;
            }
        }
        return false;
    }
    
    private bool AddIronItemInternal(ulong itemNetId)
    {
        if (ironStackedItemIds.Count >= MAX_STACK_SIZE)
            return false;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out NetworkObject netObj))
        {
            Item item = netObj.GetComponent<Item>();
            if (item != null && item.ItemType == ItemType.Iron)
            {
                ironStackedItemIds.Push(itemNetId);
                ironCount.Value = ironStackedItemIds.Count;
                
                netObj.transform.position = GetIronPositionAtHeight(ironStackedItemIds.Count - 1);
                netObj.transform.rotation = Quaternion.identity;
                
                SyncIronStackClientRpc(ironStackedItemIds.ToArray());
                
                TryCraftRail();
                return true;
            }
        }
        return false;
    }

    private ulong RemoveTopWoodItemInternal()
    {
        if (woodStackedItemIds.Count == 0) return 0;

        ulong topItemId = woodStackedItemIds.Pop();
        woodCount.Value = woodStackedItemIds.Count;

        SyncWoodStackClientRpc(woodStackedItemIds.ToArray());
        return topItemId;
    }
    
    private ulong RemoveTopIronItemInternal()
    {
        if (ironStackedItemIds.Count == 0) return 0;

        ulong topItemId = ironStackedItemIds.Pop();
        ironCount.Value = ironStackedItemIds.Count;

        SyncIronStackClientRpc(ironStackedItemIds.ToArray());
        return topItemId;
    }
    
    private void TryCraftRail()
    {
        // 서버에서만 레일 제작 처리
        if (!IsServer) return;
        
        // 타겟 데스크가 없거나 레일을 더 이상 만들 수 없으면 리턴
        if (targetDesk == null || !targetDesk.CanCreateRail) return;
        
        // 필요한 자원이 충분한지 확인
        if (woodStackedItemIds.Count >= WOOD_REQUIRED && ironStackedItemIds.Count >= IRON_REQUIRED)
        {
            // 재료 소비
            List<ulong> itemsToDestroyIds = new List<ulong>();
            
            // 나무 아이템 제거
            for (int i = 0; i < WOOD_REQUIRED; i++)
            {
                ulong itemId = RemoveTopWoodItemInternal();
                if (itemId != 0)
                {
                    itemsToDestroyIds.Add(itemId);
                }
            }
            
            // 철 아이템 제거
            for (int i = 0; i < IRON_REQUIRED; i++)
            {
                ulong itemId = RemoveTopIronItemInternal();
                if (itemId != 0)
                {
                    itemsToDestroyIds.Add(itemId);
                }
            }
            
            // 사용된 아이템 파괴
            foreach (ulong itemId in itemsToDestroyIds)
            {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemId, out NetworkObject netObj))
                {
                    netObj.Despawn(true);
                }
            }
            
            // 레일 생성 완료 알림
            targetDesk.RailCreateDone();
            
            // 디버그 리스트 업데이트
            UpdateDebugLists();
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void TryCraftRailServerRpc()
    {
        TryCraftRail();
    }
    
    private Vector3 GetWoodPositionAtHeight(int stackIndex)
    {
        Vector3 position = woodTransform.position;
        position.y += stackHeight * stackIndex;
        return position;
    }

    private Vector3 GetIronPositionAtHeight(int stackIndex)
    {
        Vector3 position = ironTransform.position;
        position.y += stackHeight * stackIndex;
        return position;
    }
    
    private void UpdateVisibleStacks()
    {
        UpdateVisibleWoodStack();
        UpdateVisibleIronStack();
    }

    private void UpdateVisibleWoodStack()
    {
        ulong[] itemArray = woodStackedItemIds.ToArray();

        for (int i = 0; i < itemArray.Length; i++)
        {
            NetworkObject netObj = GetNetworkObjectById(itemArray[itemArray.Length - 1 - i]);
            if (netObj != null)
            {
                netObj.gameObject.SetActive(true);
                netObj.transform.position = GetWoodPositionAtHeight(i);
                netObj.transform.rotation = Quaternion.identity;
            }
        }
    }

    private void UpdateVisibleIronStack()
    {
        ulong[] itemArray = ironStackedItemIds.ToArray();

        for (int i = 0; i < itemArray.Length; i++)
        {
            NetworkObject netObj = GetNetworkObjectById(itemArray[itemArray.Length - 1 - i]);
            if (netObj != null)
            {
                netObj.gameObject.SetActive(true);
                netObj.transform.position = GetIronPositionAtHeight(i);
                netObj.transform.rotation = Quaternion.identity;
            }
        }
    }

    private void UpdateDebugLists()
    {
        debugWoodItems.Clear();
        debugWoodItems.AddRange(woodStackedItemIds);
        
        debugIronItems.Clear();
        debugIronItems.AddRange(ironStackedItemIds);
    }
    
    private NetworkObject GetNetworkObjectById(ulong objectId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
            return netObj;
        
        return null;
    }
    
    public int GetWoodStackSize() => woodStackedItemIds.Count;
    public int GetIronStackSize() => ironStackedItemIds.Count;
    public bool CanAddWood() => woodStackedItemIds.Count < MAX_STACK_SIZE;
    public bool CanAddIron() => ironStackedItemIds.Count < MAX_STACK_SIZE;
}