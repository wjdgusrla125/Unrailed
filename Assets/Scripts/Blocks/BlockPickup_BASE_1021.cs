/*using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

public class BlockPickup : NetworkBehaviour
{
    public Transform handPosition;
    public Transform twoHandPosition;
    public float detectionDistance = 1.5f;
    public Vector3 boxSize;
    public LayerMask tileLayerMask;
    public InputReader inputReader;
    
    private Stack<NetworkObject> heldObjectStack = new Stack<NetworkObject>();
    
    [SerializeField] private List<NetworkObject> heldObjectList = new List<NetworkObject>();
    
    public NetworkObject MainHeldObject => heldObjectStack.Count > 0 ? heldObjectStack.Peek() : null;
    
    private bool canInteract = false;
    public Tile currentTile = null;
    
    [SerializeField] private PlayerInfo playerInfo;
    private const int maxStackSize = 4;
    [SerializeField] private Vector3 stackOffset = new Vector3(0, 0.2f, 0);
    
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
        if (IsOwner)
        {
            DetectTiles();
            canInteract = (currentTile != null);
        }
        
        if (heldObjectStack.Count > 0)
        {
            NetworkObject mainObject = heldObjectStack.Peek();
            if (mainObject != null)
            {
                Item heldItem = mainObject.GetComponent<Item>();
                Transform holdPosition = handPosition;
        
                if (heldItem != null && heldItem.WithTwoHanded)
                {
                    holdPosition = twoHandPosition;
                }
                
                mainObject.transform.position = holdPosition.position;
                mainObject.transform.rotation = holdPosition.rotation;
            
                UpdateStackedItemPositions(holdPosition);
            }
        }
    }
    
    private void DetectTiles()
    {
        Vector3 boxCenter = transform.position + Vector3.down * (detectionDistance * 0.5f);
        Vector3 boxSize = new Vector3(0.8f, 0.2f, 0.8f);
        
        Collider[] tileColliders = Physics.OverlapBox(boxCenter, boxSize * 0.5f, Quaternion.identity, tileLayerMask);
        
        if (tileColliders.Length > 0)
        {
            float closestDistance = float.MaxValue;
            Tile closestTile = null;
            
            foreach (Collider collider in tileColliders)
            {
                Tile tile = collider.GetComponent<Tile>();
                if (tile != null)
                {
                    float distance = Vector3.Distance(transform.position, collider.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTile = tile;
                    }
                }
            }
            
            currentTile = closestTile;
        }
        else
        {
            currentTile = null;
        }
    }
    
    private void UpdateStackedItemPositions(Transform basePosition)
    {
        NetworkObject[] stackArray = heldObjectStack.ToArray();
        
        for (int i = 1; i < stackArray.Length; i++)
        {
            NetworkObject stackedObject = stackArray[i];
            if (stackedObject != null && stackedObject.gameObject.activeSelf)
            {
                Vector3 offset = stackOffset * i;
                stackedObject.transform.position = basePosition.position + offset;
                stackedObject.transform.rotation = basePosition.rotation;
            }
        }
    }
    
    private void OnInteract(bool pressed)
    {
        if (!pressed || !IsOwner) return;
        
        if (playerInfo.hitBlock == BlockType.CraftingTable)
        {
            if (heldObjectStack.Count > 0 && playerInfo.CraftingTableObject != null)
            {
                HandlePlacementOnTable();
            }
        }

        if (playerInfo.hitBlock == BlockType.DeskTable && playerInfo.itemType == ItemType.None && playerInfo.deskInfo.RailCount != 0)
        {
            if (heldObjectStack.Count == 0)
            {
                RequestRailFromDeskServerRpc(playerInfo.deskInfo.NetworkObjectId);
            }
        }
        
        int tileStackSize = currentTile.GetStackSize();
        bool hasTileItems = tileStackSize > 0;
        
        if (heldObjectStack.Count == 0 && hasTileItems)
        {
            HandlePickupFromTile();
        }
        else if (heldObjectStack.Count > 0)
        {
            HandlePlacementOnTile();
        }
    }

    private void HandlePickupFromTile()
    {
        int tileStackSize = currentTile.GetStackSize();
        
        if (tileStackSize == 0) return;
        
        NetworkObject topItem = GetTopStackedItem(currentTile);
        if (topItem == null) return;
        
        Item itemComponent = topItem.GetComponent<Item>();
        if (itemComponent == null) return;
        
        if (itemComponent.IsStackable && tileStackSize > 1)
        {
            int itemsToPickup = Mathf.Min(tileStackSize, maxStackSize);
            RequestPickupMultipleFromStackServerRpc(currentTile.NetworkObjectId, itemsToPickup);
        }
        else
        {
            RequestPickUpFromStackServerRpc(currentTile.NetworkObjectId);
        }
    }

    private void HandlePlacementOnTile()
    {
        if (heldObjectStack.Count == 0) return;
    
        NetworkObject mainObject = heldObjectStack.Peek();
        
        Item heldItem = mainObject.GetComponent<Item>();
        
        if (heldItem == null) return;
    
        int tileStackSize = currentTile.GetStackSize();
        
        bool isTileEmpty = tileStackSize == 0;
        
        if (isTileEmpty)
        {
            if (heldObjectStack.Count > 1)
            {
                RequestDropAllOnTileServerRpc(mainObject.NetworkObjectId, currentTile.NetworkObjectId);
            }
            else
            {
                RequestDropOnTileServerRpc(mainObject.NetworkObjectId, currentTile.NetworkObjectId);
            }
            
            UpdateTileServerRpc(heldItem.ItemType);
            return;
        }
        
        NetworkObject topTileItem = GetTopStackedItem(currentTile);
        if (topTileItem == null) return;
        
        Item tileItem = topTileItem.GetComponent<Item>();
        if (tileItem == null) return;
        
        bool isSameItemType = heldItem.ItemType == tileItem.ItemType;
        bool areBothStackable = heldItem.IsStackable && tileItem.IsStackable;
        
        if (heldItem.IsStackable && tileItem.IsStackable && !isSameItemType)
        {
            if (tileStackSize <= 3)
            {
                NetworkObject[] playerItems = heldObjectStack.ToArray();
                ulong[] playerItemIds = new ulong[playerItems.Length];
        
                for (int i = 0; i < playerItems.Length; i++)
                {
                    playerItemIds[i] = playerItems[i].NetworkObjectId;
                }
                RequestSwapDifferentTypeStacksServerRpc(currentTile.NetworkObjectId, playerItemIds);
            }
            return;
        }
        
        if (heldObjectStack.Count >= maxStackSize && tileStackSize >= 1 && isSameItemType && areBothStackable)
        {
            RequestPlaceStackOnTileServerRpc(currentTile.NetworkObjectId);
        }
        else if (heldObjectStack.Count > 1 && (!isSameItemType || !areBothStackable))
        {
            if (heldItem.IsStackable && !tileItem.IsStackable)
            {
                NetworkObject[] playerItems = heldObjectStack.ToArray();
                ulong[] playerItemIds = new ulong[playerItems.Length];
        
                for (int i = 0; i < playerItems.Length; i++)
                {
                    playerItemIds[i] = playerItems[i].NetworkObjectId;
                }
                RequestSwapStackWithSingleItemServerRpc(currentTile.NetworkObjectId, playerItemIds);
            }
            else
            {
                RequestDropAllServerRpc();
                RequestPickUpFromStackServerRpc(currentTile.NetworkObjectId);
            }
        }
        else if (isSameItemType && areBothStackable && heldObjectStack.Count < maxStackSize && tileStackSize < (maxStackSize - heldObjectStack.Count))
        {
            int itemsToPickup = Mathf.Min(tileStackSize, maxStackSize - heldObjectStack.Count);
            RequestAddToStackFromTileServerRpc(currentTile.NetworkObjectId, itemsToPickup);
        }
        else if (!heldItem.IsStackable && tileItem.IsStackable)
        {
            RequestSwapWithAllStackedItemsServerRpc(mainObject.NetworkObjectId, currentTile.NetworkObjectId);
        }
        else
        {
            SwapObjectWithTileServerRpc(mainObject.NetworkObjectId, topTileItem.NetworkObjectId, currentTile.NetworkObjectId);
        }
    }

    private void HandlePlacementOnTable()
    {
        if (heldObjectStack.Count == 0) return;
    
        NetworkObject mainObject = heldObjectStack.Peek();
    
        Item heldItem = mainObject.GetComponent<Item>();
    
        if (heldItem == null) return;
        if (heldItem.ItemType != ItemType.Iron && heldItem.ItemType != ItemType.WoodPlank) return;
    
        int woodStackSize = playerInfo.CraftingTableObject.GetWoodStackSize();
        int ironStackSize = playerInfo.CraftingTableObject.GetIronStackSize();

        if (heldItem.ItemType == ItemType.WoodPlank)
        {
            if (playerInfo.CraftingTableObject.CanAddWood())
            {
                if (heldObjectStack.Count > (3 - woodStackSize))
                {
                    // 남은 갯수만 내려놓기
                    int itemsToPlace = 3 - woodStackSize;
                    RequestDropPartialOnWoodStackServerRpc(mainObject.NetworkObjectId, playerInfo.CraftingTableObject.NetworkObjectId, itemsToPlace);
                }
                else
                {
                    RequestDropAllOnWoodStackServerRpc(mainObject.NetworkObjectId, playerInfo.CraftingTableObject.NetworkObjectId);
                }
            }
        }
        else if(heldItem.ItemType == ItemType.Iron)
        {
            if (playerInfo.CraftingTableObject.CanAddIron())
            {
                if (heldObjectStack.Count > (3 - ironStackSize))
                {
                    // 남은 갯수만 내려놓기
                    int itemsToPlace = 3 - ironStackSize;
                    RequestDropPartialOnIronStackServerRpc(mainObject.NetworkObjectId, playerInfo.CraftingTableObject.NetworkObjectId, itemsToPlace);
                }
                else
                {
                    RequestDropAllOnIronStackServerRpc(mainObject.NetworkObjectId, playerInfo.CraftingTableObject.NetworkObjectId);
                }
            }
        }
    }

    #region ServerRPC

    //아이템 획득 관련
    [ServerRpc]
    void RequestPickupMultipleFromStackServerRpc(ulong tileId, int count, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
        {
            return;
        }
    
        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null || tile.GetStackSize() == 0) return;
    
        ulong clientId = rpcParams.Receive.SenderClientId;
        
        count = Mathf.Min(count, maxStackSize, tile.GetStackSize());
        
        ItemType tileItemType = tile.GetCurrentItemType();
    
        List<ulong> itemIds = new List<ulong>();
        
        for (int i = 0; i < count; i++)
        {
            ulong itemId = tile.RemoveTopItemFromStack();
            if (itemId == 0 || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemId, out NetworkObject netObj))
            {
                continue;
            }
        
            netObj.ChangeOwnership(clientId);
            itemIds.Add(itemId);
        }
    
        if (itemIds.Count > 0)
        {
            UpdateTileStackClientRpc(tileId);
            SetHeldObjectStackClientRpc(itemIds.ToArray(), NetworkObjectId);
        }
    }

    [ServerRpc]
    void RequestPickUpFromStackServerRpc(ulong tileId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
        {
            return;
        }
    
        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null || tile.GetStackSize() == 0) return;
    
        ulong topItemId = tile.RemoveTopItemFromStack();
        if (topItemId == 0 || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(topItemId, out NetworkObject netObj))
        {
            return;
        }

        netObj.ChangeOwnership(rpcParams.Receive.SenderClientId);
        
        UpdateTileStackClientRpc(tileId);
        AddToHeldStackClientRpc(topItemId, NetworkObjectId);
    }
    
    [ServerRpc]
    void RequestAddToStackFromTileServerRpc(ulong tileId, int count, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
            return;
            
        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null || tile.GetStackSize() == 0) return;
        
        count = Mathf.Min(count, maxStackSize - heldObjectStack.Count, tile.GetStackSize());
        
        for (int i = 0; i < count; i++)
        {
            ulong itemId = tile.RemoveTopItemFromStack();
            if (itemId == 0 || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemId, out NetworkObject netObj))
            {
                continue;
            }
            
            netObj.ChangeOwnership(rpcParams.Receive.SenderClientId);
            AddToHeldStackClientRpc(itemId, NetworkObjectId);
        }
    }
    
    //아이템 드랍 관련
    [ServerRpc]
    void RequestDropAllServerRpc(ServerRpcParams rpcParams = default)
    {
        if (heldObjectStack.Count == 0) return;
        
        DropStackedItemsClientRpc();
    }
    
    [ServerRpc]
    void RequestDropOnTileServerRpc(ulong objectId, ulong tileId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj) ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
            return;

        netObj.RemoveOwnership();
        DropObjectOnTileClientRpc(objectId, tileId);
    }
    
    [ServerRpc]
    void RequestDropAllOnTileServerRpc(ulong mainObjectId, ulong tileId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(mainObjectId, out NetworkObject netObj) ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
            return;

        netObj.RemoveOwnership();
        DropStackedItemsOnTileClientRpc(tileId);
    }
    
    //아이템 스왑 관련
    [ServerRpc]
    void SwapObjectWithTileServerRpc(ulong heldObjectId, ulong newTargetId, ulong tileId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(heldObjectId, out NetworkObject heldNetObj) ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(newTargetId, out NetworkObject newNetObj) ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
        {
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;
        
        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null) return;
        
        tile.RemoveTopItemFromStack();
        
        if (heldObjectStack.Count > 1)
        {
            DropStackedItemsOnTileClientRpc(tileId);
        }
        else
        {
            heldNetObj.RemoveOwnership();
            DropObjectOnTileClientRpc(heldObjectId, tileId);
        }
        
        newNetObj.ChangeOwnership(clientId);
        AddToHeldStackClientRpc(newTargetId, NetworkObjectId);
    }
    
    [ServerRpc]
    void RequestSwapWithAllStackedItemsServerRpc(ulong heldObjectId, ulong tileId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(heldObjectId, out NetworkObject heldNetObj) ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
        {
            return;
        }
    
        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null || tile.GetStackSize() == 0) return;
    
        ulong clientId = rpcParams.Receive.SenderClientId;
        
        NetworkObject[] stackArray = heldObjectStack.ToArray();
        heldObjectStack.Clear();
        
        List<ulong> tileItemIds = new List<ulong>();
        int itemsToPickup = Mathf.Min(tile.GetStackSize(), maxStackSize);
    
        for (int i = 0; i < itemsToPickup; i++)
        {
            ulong itemId = tile.RemoveTopItemFromStack();
            if (itemId == 0) continue;
        
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemId, out NetworkObject netObj))
            {
                netObj.ChangeOwnership(clientId);
                tileItemIds.Add(itemId);
            }
        }
        
        heldNetObj.RemoveOwnership();
        tile.AddItemToStack(heldObjectId);
        
        Item heldItem = heldNetObj.GetComponent<Item>();
        if (heldItem != null)
        {
            tile.AddItem(heldItem.ItemType);
        }
        
        if (tileItemIds.Count > 0)
        {
            SetHeldObjectStackClientRpc(tileItemIds.ToArray(), NetworkObjectId);
        }
        else
        {
            UpdatePlayerItemTypeClientRpc(ItemType.None, NetworkObjectId);
        }
    }
    
    [ServerRpc]
    void RequestSwapStackWithSingleItemServerRpc(ulong tileId, ulong[] playerItemIds, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
        {
            return;
        }

        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null || tile.GetStackSize() == 0)
        {
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;
        
        ulong tileItemId = tile.PeekTopItemFromStack();
        if (tileItemId == 0 || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileItemId, out NetworkObject tileItemObj))
        {
            return;
        }
        
        ItemType playerItemType = ItemType.None;
        if (playerItemIds.Length > 0 && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerItemIds[0], out NetworkObject firstItem))
        {
            Item item = firstItem.GetComponent<Item>();
            if (item != null)
            {
                playerItemType = item.ItemType;
            }
        }
        
        tile.RemoveTopItemFromStack();
        
        if (playerItemType != ItemType.None)
        {
            tile.AddItem(playerItemType);
        }
        
        foreach (ulong id in playerItemIds)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject netObj))
            {
                netObj.RemoveOwnership();
                tile.ForceAddItemToStackFromServer(id);
            }
        }
        
        UpdateTileStackClientRpc(tileId);
        tileItemObj.ChangeOwnership(clientId);
        
        ulong[] singleItemArray = new ulong[] { tileItemId };
        SetHeldObjectStackClientRpc(singleItemArray, NetworkObjectId);
    }

    [ServerRpc]
    void RequestSwapDifferentTypeStacksServerRpc(ulong tileId, ulong[] playerItemIds, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
        {
            return;
        }
        
        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null || tile.GetStackSize() == 0)
        {
            return;
        }
        
        ulong clientId = rpcParams.Receive.SenderClientId;
        
        ItemType playerItemType = ItemType.None;
        if (playerItemIds.Length > 0 && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerItemIds[0], out NetworkObject firstItem))
        {
            Item item = firstItem.GetComponent<Item>();
            if (item != null)
            {
                playerItemType = item.ItemType;
            }
        }
        
        List<ulong> tileItemIds = new List<ulong>();
        int tileStackSize = tile.GetStackSize();
        
        for (int i = 0; i < tileStackSize; i++)
        {
            ulong itemId = tile.RemoveTopItemFromStack();
            if (itemId != 0 && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemId, out NetworkObject netObj))
            {
                netObj.ChangeOwnership(clientId);
                tileItemIds.Add(itemId);
            }
        }
        
        if (playerItemType != ItemType.None)
        {
            tile.AddItem(playerItemType);
        }
        
        if (playerItemIds.Length > 0)
        {
            foreach (ulong id in playerItemIds)
            {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject netObj))
                {
                    netObj.RemoveOwnership();
                    tile.ForceAddItemToStackFromServer(id);
                }
            }
            UpdateTileStackClientRpc(tileId);
        }
        
        if (tileItemIds.Count > 0)
        {
            SetHeldObjectStackClientRpc(tileItemIds.ToArray(), NetworkObjectId);
        }
        else
        {
            UpdatePlayerItemTypeClientRpc(ItemType.None, NetworkObjectId);
        }
    }
    
    //타일 업데이트 관련
    [ServerRpc]
    private void UpdateTileServerRpc(ItemType itemType, ServerRpcParams rpcParams = default)
    {
        UpdateTileClientRpc(itemType);
    }
    
    [ServerRpc]
    void RequestPlaceStackOnTileServerRpc(ulong tileId, ServerRpcParams rpcParams = default)
    {
        if (heldObjectStack.Count == 0 || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
            return;

        DropStackedItemsOnTileClientRpc(tileId);
    }
    
    //제작대 관련
    [ServerRpc]
    private void RequestDropAllOnWoodStackServerRpc(ulong mainObjectId, ulong stackId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(mainObjectId, out NetworkObject netObj) ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(stackId, out NetworkObject tileObj))
            return;
        netObj.RemoveOwnership();
        DropStackedItemsOnWoodStackClientRpc(stackId);
    }
    
    [ServerRpc]
    private void RequestDropAllOnIronStackServerRpc(ulong mainObjectId, ulong stackId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(mainObjectId, out NetworkObject netObj) ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(stackId, out NetworkObject tileObj))
            return;
        
        netObj.RemoveOwnership();
        DropStackedItemsOnIronStackClientRpc(stackId);
    }
    
    [ServerRpc]
    private void RequestDropPartialOnWoodStackServerRpc(ulong mainObjectId, ulong stackId, int count, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(mainObjectId, out NetworkObject netObj) ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(stackId, out NetworkObject craftObj))
            return;
    
        DropPartialItemsOnWoodStackClientRpc(stackId, count);
    }

    [ServerRpc]
    private void RequestDropPartialOnIronStackServerRpc(ulong mainObjectId, ulong stackId, int count, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(mainObjectId, out NetworkObject netObj) ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(stackId, out NetworkObject craftObj))
            return;
    
        DropPartialItemsOnIronStackClientRpc(stackId, count);
    }
    
    [ServerRpc]
    private void RequestRailFromDeskServerRpc(ulong deskId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(deskId, out NetworkObject deskObj))
        {
            return;
        }
    
        GameObject railPrefab = playerInfo.deskInfo.GetRailObject();
        if (railPrefab == null)
        {
            return;
        }
    
        // 레일 객체를 생성하고 NetworkObject 컴포넌트를 가져옵니다
        GameObject railInstance = Instantiate(railPrefab);
        NetworkObject railNetObj = railInstance.GetComponent<NetworkObject>();

        if (railNetObj != null)
        {
            // 네트워크에 레일 객체를 스폰합니다
            railNetObj.Spawn();
        
            // 클라이언트 소유권을 설정합니다
            ulong clientId = rpcParams.Receive.SenderClientId;
            railNetObj.ChangeOwnership(clientId);
        
            // 데스크에서 레일을 가져옵니다
            playerInfo.deskInfo.GetRail();
        
            // 생성한 레일을 즉시 플레이어의 손에 추가합니다
            AddToHeldStackClientRpc(railNetObj.NetworkObjectId, NetworkObjectId);
        }
    }
    
    #endregion

    #region ClientRPC

    //플레이어 인벤토리 관련
    [ClientRpc]
    void AddToHeldStackClientRpc(ulong objectId, ulong playerId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj) ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerId, out NetworkObject playerObj))
        {
            return;
        }

        BlockPickup playerPickup = playerObj.GetComponent<BlockPickup>();
        if (playerPickup == null) return;
        
        if (playerPickup.IsLocalPlayer || playerPickup.IsOwner)
        {
            bool alreadyInStack = false;
            foreach (NetworkObject obj in playerPickup.heldObjectStack)
            {
                if (obj.NetworkObjectId == objectId)
                {
                    alreadyInStack = true;
                    break;
                }
            }
            
            if (!alreadyInStack)
            {
                playerPickup.heldObjectStack.Push(netObj);
                playerPickup.UpdateHeldObjectList();
            
                Item heldItem = netObj.GetComponent<Item>();
                if (heldItem != null && playerPickup.playerInfo != null)
                {
                    playerPickup.UpdatePlayerItemType(heldItem.ItemType);
                }
                
                Transform holdPosition = heldItem != null && heldItem.WithTwoHanded ? 
                    playerPickup.twoHandPosition : playerPickup.handPosition;
                    
                netObj.transform.position = holdPosition.position;
                netObj.transform.rotation = holdPosition.rotation;
                
                playerPickup.UpdateStackedItemPositions(holdPosition);
            }
        }
        else
        {
            Transform holdPosition = playerPickup.handPosition;
            Item heldItem = netObj.GetComponent<Item>();
            
            if (heldItem != null && heldItem.WithTwoHanded)
            {
                holdPosition = playerPickup.twoHandPosition;
            }
            
            netObj.transform.position = holdPosition.position;
            netObj.transform.rotation = holdPosition.rotation;
        }
    }
    
    [ClientRpc]
    void SetHeldObjectStackClientRpc(ulong[] objectIds, ulong playerId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerId, out NetworkObject playerObj))
        {
            return;
        }

        BlockPickup playerPickup = playerObj.GetComponent<BlockPickup>();
        if (playerPickup == null) return;
        
        Transform holdPosition = playerPickup.handPosition;
        ItemType itemType = ItemType.None;
        
        if (playerPickup.IsLocalPlayer || playerPickup.IsOwner)
        {
            playerPickup.heldObjectStack.Clear();

            HashSet<ulong> processedIds = new HashSet<ulong>();

            for (int i = objectIds.Length - 1; i >= 0; i--)
            {
                ulong objectId = objectIds[i];
                
                if (processedIds.Contains(objectId)) continue;
                
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
                {
                    playerPickup.heldObjectStack.Push(netObj);
                    processedIds.Add(objectId);
                    
                    if (i == 0)
                    {
                        Item item = netObj.GetComponent<Item>();
                        if (item != null)
                        {
                            itemType = item.ItemType;
                            if (item.WithTwoHanded)
                            {
                                holdPosition = playerPickup.twoHandPosition;
                            }
                        }
                    }
                }
            }
            
            playerPickup.UpdateHeldObjectList();

            if (playerPickup.playerInfo != null)
            {
                playerPickup.UpdatePlayerItemType(itemType);
            }
        }
        
        for (int i = 0; i < objectIds.Length; i++)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectIds[i], out NetworkObject netObj))
            {
                Vector3 offset = playerPickup.stackOffset * i;
                netObj.transform.position = holdPosition.position + offset;
                netObj.transform.rotation = holdPosition.rotation;
            }
        }
    }
    
    [ClientRpc]
    void UpdatePlayerItemTypeClientRpc(ItemType itemType, ulong playerId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerId, out NetworkObject playerObj))
        {
            return;
        }
    
        BlockPickup playerPickup = playerObj.GetComponent<BlockPickup>();
        if (playerPickup != null && playerPickup.IsOwner)
        {
            playerPickup.UpdatePlayerItemType(itemType);
        }
    }

    //아이템 놓기 관련
    [ClientRpc]
    private void DropStackedItemsClientRpc()
    {
        if (!IsOwner && NetworkObjectId != NetworkObjectId) return;

        float dropOffset = 0.2f;
        NetworkObject[] stackedArray = heldObjectStack.ToArray();

        foreach (NetworkObject netObj in stackedArray)
        {
            if (netObj != null)
            {
                netObj.gameObject.SetActive(true);
                netObj.transform.SetParent(null);
                
                if (IsServer)
                {
                    netObj.RemoveOwnership();
                }
            
                Vector3 position = transform.position;
                position.y = 0.1f;
                position += new Vector3(Random.Range(-dropOffset, dropOffset), 0, Random.Range(-dropOffset, dropOffset));
            
                netObj.transform.position = position;
                netObj.transform.rotation = Quaternion.identity;
            }
        }

        heldObjectStack.Clear();
        UpdateHeldObjectList();
        UpdatePlayerItemType(ItemType.None);
    }
    
    [ClientRpc]
    private void DropObjectOnTileClientRpc(ulong objectId, ulong tileId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj) ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
            return;
    
        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null) return;
        
        if (IsServer)
        {
            tile.AddItemToStack(objectId);
        }

        BlockPickup localPickup = GetLocalBlockPickup();
        if (localPickup != null && (IsLocalPlayer || IsOwner))
        {
            NetworkObject[] stackArray = localPickup.heldObjectStack.ToArray();
            localPickup.heldObjectStack.Clear();
        
            foreach (NetworkObject obj in stackArray)
            {
                if (obj.NetworkObjectId != objectId)
                {
                    localPickup.heldObjectStack.Push(obj);
                }
            }
            
            localPickup.UpdateHeldObjectList();
        
            if (localPickup.heldObjectStack.Count == 0)
            {
                localPickup.UpdatePlayerItemType(ItemType.None);
            }
            else
            {
                Item item = localPickup.heldObjectStack.Peek().GetComponent<Item>();
                if (item != null)
                {
                    localPickup.UpdatePlayerItemType(item.ItemType);
                }
            }
        }
    }
    
    [ClientRpc]
    private void DropStackedItemsOnTileClientRpc(ulong tileId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
            return;
    
        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null) return;

        BlockPickup localPickup = GetLocalBlockPickup();
        
        if (localPickup != null && (IsLocalPlayer || IsOwner))
        {
            ItemType itemType = ItemType.None;
    
            if (localPickup.heldObjectStack.Count > 0)
            {
                NetworkObject mainObject = localPickup.heldObjectStack.Peek();
                Item heldItem = mainObject.GetComponent<Item>();
                if (heldItem != null)
                {
                    itemType = heldItem.ItemType;
                }
            }
    
            NetworkObject[] stackArray = localPickup.heldObjectStack.ToArray();
            
            if (itemType != ItemType.None)
            {
                tile.AddItem(itemType);
            }
    
            foreach (NetworkObject netObj in stackArray)
            {
                if (netObj != null)
                {
                    netObj.gameObject.SetActive(true);
                    netObj.transform.SetParent(null);
                    tile.AddItemToStack(netObj.NetworkObjectId);
                }
            }
    
            localPickup.heldObjectStack.Clear();
            localPickup.UpdateHeldObjectList();
            localPickup.UpdatePlayerItemType(ItemType.None);
        }
    }
    
    //타일 업데이트 관련
    [ClientRpc]
    private void UpdateTileClientRpc(ItemType itemType)
    {
        if (IsOwner && currentTile != null)
        {
            currentTile.AddItem(itemType);
        }
    }
    
    [ClientRpc]
    void UpdateTileStackClientRpc(ulong tileId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
        {
            return;
        }
    
        Tile tile = tileObj.GetComponent<Tile>();
        if (tile != null)
        {
            tile.UpdateVisibleStack();
        }
    }
    
    //제작대 관련
    [ClientRpc]
    private void DropStackedItemsOnWoodStackClientRpc(ulong stackId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(stackId, out NetworkObject stackObj))
            return;
        
        CraftingTable craftingTable = stackObj.GetComponent<CraftingTable>();
        if(craftingTable == null) return;
        
        BlockPickup localPickup = GetLocalBlockPickup();
        
        if (localPickup != null && (IsLocalPlayer || IsOwner))
        {
            ItemType itemType = ItemType.None;
    
            if (localPickup.heldObjectStack.Count > 0)
            {
                NetworkObject mainObject = localPickup.heldObjectStack.Peek();
                Item heldItem = mainObject.GetComponent<Item>();
                if (heldItem != null)
                {
                    itemType = heldItem.ItemType;
                }
            }
    
            NetworkObject[] stackArray = localPickup.heldObjectStack.ToArray();
            
            foreach (NetworkObject netObj in stackArray)
            {
                if (netObj != null)
                {
                    netObj.gameObject.SetActive(true);
                    netObj.transform.SetParent(null);
                    craftingTable.AddWoodItem(netObj.NetworkObjectId);
                }
            }
    
            localPickup.heldObjectStack.Clear();
            localPickup.UpdateHeldObjectList();
            localPickup.UpdatePlayerItemType(ItemType.None);
        }
    }
    
    [ClientRpc]
    private void DropStackedItemsOnIronStackClientRpc(ulong stackId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(stackId, out NetworkObject stackObj))
            return;
        
        CraftingTable craftingTable = stackObj.GetComponent<CraftingTable>();
        if(craftingTable == null) return;
        
        BlockPickup localPickup = GetLocalBlockPickup();
        
        if (localPickup != null && (IsLocalPlayer || IsOwner))
        {
            ItemType itemType = ItemType.None;
    
            if (localPickup.heldObjectStack.Count > 0)
            {
                NetworkObject mainObject = localPickup.heldObjectStack.Peek();
                Item heldItem = mainObject.GetComponent<Item>();
                if (heldItem != null)
                {
                    itemType = heldItem.ItemType;
                }
            }
    
            NetworkObject[] stackArray = localPickup.heldObjectStack.ToArray();
            
            foreach (NetworkObject netObj in stackArray)
            {
                if (netObj != null)
                {
                    netObj.gameObject.SetActive(true);
                    netObj.transform.SetParent(null);
                    craftingTable.AddIronItem(netObj.NetworkObjectId);
                }
            }
    
            localPickup.heldObjectStack.Clear();
            localPickup.UpdateHeldObjectList();
            localPickup.UpdatePlayerItemType(ItemType.None);
        }
    }
    
    [ClientRpc]
    private void DropPartialItemsOnWoodStackClientRpc(ulong stackId, int count)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(stackId, out NetworkObject stackObj))
            return;
        
        CraftingTable craftingTable = stackObj.GetComponent<CraftingTable>();
        if(craftingTable == null) return;
        
        BlockPickup localPickup = GetLocalBlockPickup();
        
        if (localPickup != null && (IsLocalPlayer || IsOwner))
        {
            if (localPickup.heldObjectStack.Count > 0)
            {
                // 들고 있는 아이템을 임시 리스트에 복사
                List<NetworkObject> tempStack = new List<NetworkObject>(localPickup.heldObjectStack);
                localPickup.heldObjectStack.Clear();
                
                // count만큼만 크래프팅 테이블에 추가
                for (int i = 0; i < count && i < tempStack.Count; i++)
                {
                    NetworkObject netObj = tempStack[i];
                    if (netObj != null)
                    {
                        netObj.gameObject.SetActive(true);
                        netObj.transform.SetParent(null);
                        craftingTable.AddWoodItem(netObj.NetworkObjectId);
                    }
                }
                
                // 나머지 아이템은 다시 플레이어 스택에 추가
                for (int i = count; i < tempStack.Count; i++)
                {
                    localPickup.heldObjectStack.Push(tempStack[i]);
                }
                
                localPickup.UpdateHeldObjectList();
                
                // 스택이 비어있으면 아이템 타입을 None으로 설정
                if (localPickup.heldObjectStack.Count == 0)
                {
                    localPickup.UpdatePlayerItemType(ItemType.None);
                }
                else
                {
                    // 스택에 아이템이 남아있으면 현재 아이템 타입 유지
                    Item heldItem = localPickup.heldObjectStack.Peek().GetComponent<Item>();
                    if (heldItem != null)
                    {
                        localPickup.UpdatePlayerItemType(heldItem.ItemType);
                    }
                }
            }
        }
    }

    [ClientRpc]
    private void DropPartialItemsOnIronStackClientRpc(ulong stackId, int count)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(stackId, out NetworkObject stackObj))
            return;
        
        CraftingTable craftingTable = stackObj.GetComponent<CraftingTable>();
        if(craftingTable == null) return;
        
        BlockPickup localPickup = GetLocalBlockPickup();
        
        if (localPickup != null && (IsLocalPlayer || IsOwner))
        {
            if (localPickup.heldObjectStack.Count > 0)
            {
                // 들고 있는 아이템을 임시 리스트에 복사
                List<NetworkObject> tempStack = new List<NetworkObject>(localPickup.heldObjectStack);
                localPickup.heldObjectStack.Clear();
                
                // count만큼만 크래프팅 테이블에 추가
                for (int i = 0; i < count && i < tempStack.Count; i++)
                {
                    NetworkObject netObj = tempStack[i];
                    if (netObj != null)
                    {
                        netObj.gameObject.SetActive(true);
                        netObj.transform.SetParent(null);
                        craftingTable.AddIronItem(netObj.NetworkObjectId);
                    }
                }
                
                // 나머지 아이템은 다시 플레이어 스택에 추가
                for (int i = count; i < tempStack.Count; i++)
                {
                    localPickup.heldObjectStack.Push(tempStack[i]);
                }
                
                localPickup.UpdateHeldObjectList();
                
                // 스택이 비어있으면 아이템 타입을 None으로 설정
                if (localPickup.heldObjectStack.Count == 0)
                {
                    localPickup.UpdatePlayerItemType(ItemType.None);
                }
                else
                {
                    // 스택에 아이템이 남아있으면 현재 아이템 타입 유지
                    Item heldItem = localPickup.heldObjectStack.Peek().GetComponent<Item>();
                    if (heldItem != null)
                    {
                        localPickup.UpdatePlayerItemType(heldItem.ItemType);
                    }
                }
            }
        }
    }
    
    #endregion
    
    private void UpdatePlayerItemType(ItemType type)
    {
        if (playerInfo != null)
        {
            playerInfo.SetItemType(type);
        }
    }
    
    public NetworkObject GetTopStackedItem(Tile tile)
    {
        if (tile == null || tile.GetStackSize() == 0) return null;
    
        ulong topItemId = tile.PeekTopItemFromStack();

        if (topItemId == 0 || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(topItemId, out NetworkObject netObj)) 
            return null;
        
        return netObj;
    }
    
    private BlockPickup GetLocalBlockPickup()
    {
        foreach (var player in FindObjectsOfType<BlockPickup>())
        {
            if (player.IsLocalPlayer)
            {
                return player;
            }
        }
        return null;
    }
    
    private void UpdateHeldObjectList()
    {
        heldObjectList.Clear();
        if (heldObjectStack.Count > 0)
        {
            foreach (NetworkObject obj in heldObjectStack)
            {
                heldObjectList.Add(obj);
            }
        }
    }
}*/