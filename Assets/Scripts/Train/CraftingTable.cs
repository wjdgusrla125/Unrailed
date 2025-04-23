using System;
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
    private void Start()
    {
        targetDesk = FindObjectOfType<DeskInfo>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsClient && !IsServer)
        {
            RequestInitialStateServerRpc();
        }
    }

    private void Update()
    {
        TryCraftRail();
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
        // 서버에서만 진행
        if (!IsServer) return;

        // 레일을 만들 재료가 충분한지 확인
        if (woodCount.Value >= WOOD_REQUIRED && ironCount.Value >= IRON_REQUIRED)
        {
            // DeskInfo의 현재 레일 개수 확인
            if (targetDesk.RailCount < 3)
            {
                // 재료 소비
                ulong woodItemId = RemoveTopWoodItemInternal();
                ulong ironItemId = RemoveTopIronItemInternal();
        
                // 아이템 네트워크 오브젝트 파괴
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(woodItemId, out NetworkObject woodObj))
                    woodObj.Despawn(true);
            
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(ironItemId, out NetworkObject ironObj))
                    ironObj.Despawn(true);
        
                // 레일 개수 증가
                targetDesk.RailCount++;
        
                // 수정된 부분: 현재 레일 개수에 맞게 애니메이션 단계 설정
                targetDesk.UpdateRailAnimation(targetDesk.RailCount);
            }
        }
    }

    // [ClientRpc]
    // private void UpdateRailAnimationClientRpc(int railCount)
    // {
    //     // 애니메이터가 있는 경우 GetRails 파라미터 설정
    //     // railCount에 따라 단계별로 증가
    //     if (targetDesk != null && targetDesk.GetComponent<Animator>() != null)
    //     {
    //         targetDesk.GetComponent<Animator>().SetInteger("GetRails", railCount);
    //     }
    // }
    
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