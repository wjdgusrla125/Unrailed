using UnityEngine;
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
        // 로컬 플레이어(소유자)만 타일 감지 및 상호작용 처리
        if (IsOwner)
        {
            DetectTiles();
            canInteract = (currentTile != null);
        }

        // 모든 클라이언트에서 들고 있는 아이템 위치 업데이트
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

                // 메인 물체 위치 업데이트 - 모든 클라이언트에서 실행
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
            // secondClosestTile = secondClosest;  <- Remove this line
        }
        else
        {
            currentTile = null;
            // secondClosestTile = null;  <- Remove this line
        }
    }
    
    private void UpdateStackedItemPositions(Transform basePosition)
    {
        // 스택에서 물체 배열로 변환 (역순으로 처리하여 스택 순서 유지)
        NetworkObject[] stackArray = heldObjectStack.ToArray();
        
        // 메인 물체(인덱스 0)는 이미 위치 설정이 되어 있으므로 1부터 시작
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
        if (!pressed || !IsOwner || currentTile == null) return;
        
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
            // 타일에 있는 스택의 크기가 3 이하인 경우에만 스왑
            if (tileStackSize <= 3)
            {
                // 플레이어가 들고 있는 아이템 ID를 배열로 준비
                NetworkObject[] playerItems = heldObjectStack.ToArray();
                ulong[] playerItemIds = new ulong[playerItems.Length];
        
                for (int i = 0; i < playerItems.Length; i++)
                {
                    playerItemIds[i] = playerItems[i].NetworkObjectId;
                }
        
                // 아이템 ID 배열을 서버 RPC에 전달
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
                // 스택 가능한 아이템을 여러 개 들고 있고, 타일에 일반 아이템이 있을 때
                // 플레이어가 들고 있는 아이템 ID 배열 준비
                NetworkObject[] playerItems = heldObjectStack.ToArray();
                ulong[] playerItemIds = new ulong[playerItems.Length];
        
                for (int i = 0; i < playerItems.Length; i++)
                {
                    playerItemIds[i] = playerItems[i].NetworkObjectId;
                }
        
                // 아이템 ID 배열을 서버 RPC에 전달
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
            // 일반 아이템을 들고 있고 타일에 스택 가능 아이템이 있는 경우
            RequestSwapWithAllStackedItemsServerRpc(mainObject.NetworkObjectId, currentTile.NetworkObjectId);
        }
        else
        {
            SwapObjectWithTileServerRpc(mainObject.NetworkObjectId, topTileItem.NetworkObjectId, currentTile.NetworkObjectId);
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
    
        // 실제 가져갈 수 있는 개수 계산 (최대 maxStackSize, 타일에 있는 아이템 개수 중 작은 값)
        count = Mathf.Min(count, maxStackSize, tile.GetStackSize());
    
        // 타일의 아이템 타입을 기억 (버그 수정)
        ItemType tileItemType = tile.GetCurrentItemType();
    
        List<ulong> itemIds = new List<ulong>();
    
        // 아이템 하나씩 제거하면서 목록에 추가
        for (int i = 0; i < count; i++)
        {
            ulong itemId = tile.RemoveTopItemFromStack();
            if (itemId == 0 || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemId, out NetworkObject netObj))
            {
                continue;
            }
        
            netObj.ChangeOwnership(clientId); // 소유권 변경 주석 제거
            itemIds.Add(itemId);
        }
    
        if (itemIds.Count > 0)
        {
            // 모든 클라이언트에게 타일 스택 업데이트 알림
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

        netObj.ChangeOwnership(rpcParams.Receive.SenderClientId); // 소유권 변경 주석 제거
    
        // 타일 스택 업데이트 추가
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
        Debug.Log($"[DEBUG] RequestSwapStackWithSingleItemServerRpc called with tileId: {tileId}, playerItems: {playerItemIds.Length}");
        
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
        {
            Debug.LogError($"[DEBUG] Tile NetworkObject not found with ID: {tileId}");
            return;
        }

        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null || tile.GetStackSize() == 0)
        {
            Debug.LogError($"[DEBUG] Tile component not found or empty, ID: {tileId}");
            return;
        }

        ulong clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[DEBUG] Client ID: {clientId}, Tile stack size: {tile.GetStackSize()}");

        // 타일 위 아이템 정보 확인
        ulong tileItemId = tile.PeekTopItemFromStack();
        if (tileItemId == 0 || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileItemId, out NetworkObject tileItemObj))
        {
            Debug.LogError($"[DEBUG] Tile top item not found with ID: {tileItemId}");
            return;
        }

        // 아이템 타입 저장
        ItemType playerItemType = ItemType.None;
        if (playerItemIds.Length > 0 && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerItemIds[0], out NetworkObject firstItem))
        {
            Item item = firstItem.GetComponent<Item>();
            if (item != null)
            {
                playerItemType = item.ItemType;
                Debug.Log($"[DEBUG] Player item type: {playerItemType}");
            }
        }
        
        // 1. 타일에서 기존 아이템 먼저 제거 (스택 최상단)
        tile.RemoveTopItemFromStack();
        Debug.Log($"[DEBUG] Removed top item {tileItemId} from tile");
        
        // 2. 타일에 플레이어 아이템 타입 설정
        if (playerItemType != ItemType.None)
        {
            Debug.Log($"[DEBUG] Setting tile item type to: {playerItemType}");
            tile.AddItem(playerItemType);
        }
        
        // 3. 들고 있던 모든 아이템의 소유권 제거 및 타일에 추가
        Debug.Log($"[DEBUG] Adding player items to tile: {playerItemIds.Length}");
        foreach (ulong id in playerItemIds)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject netObj))
            {
                netObj.RemoveOwnership();
                Debug.Log($"[DEBUG] Removed ownership of item {id}");
                
                // 각 아이템을 타일에 직접 추가
                tile.ForceAddItemToStackFromServer(id);
                Debug.Log($"[DEBUG] Added item {id} to tile stack");
            }
        }
        
        // 타일 스택 상태 모든 클라이언트에 동기화
        UpdateTileStackClientRpc(tileId);
        
        // 4. 타일에 있던 아이템을 플레이어가 소유
        tileItemObj.ChangeOwnership(clientId);
        Debug.Log($"[DEBUG] Changed ownership of tile item {tileItemId} to client {clientId}");
        
        // 5. 플레이어에게 타일에서 가져온 아이템 설정
        ulong[] singleItemArray = new ulong[] { tileItemId };
        SetHeldObjectStackClientRpc(singleItemArray, NetworkObjectId);
        Debug.Log($"[DEBUG] Set player held item to {tileItemId}");
    }

    [ServerRpc]
    void RequestSwapDifferentTypeStacksServerRpc(ulong tileId, ulong[] playerItemIds, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"[DEBUG] RequestSwapDifferentTypeStacksServerRpc called with tileId: {tileId}, playerItems: {playerItemIds.Length}");
        
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
        {
            Debug.LogError($"[DEBUG] Tile NetworkObject not found with ID: {tileId}");
            return;
        }
        
        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null || tile.GetStackSize() == 0)
        {
            Debug.LogError($"[DEBUG] Tile component not found or empty, ID: {tileId}");
            return;
        }
        
        ulong clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[DEBUG] Client ID: {clientId}, Tile stack size: {tile.GetStackSize()}");
        
        // 플레이어 아이템 타입 저장
        ItemType playerItemType = ItemType.None;
        if (playerItemIds.Length > 0 && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerItemIds[0], out NetworkObject firstItem))
        {
            Item item = firstItem.GetComponent<Item>();
            if (item != null)
            {
                playerItemType = item.ItemType;
                Debug.Log($"[DEBUG] Player item type: {playerItemType}");
            }
        }
        
        // 타일에서 모든 아이템 제거하고 목록에 저장
        List<ulong> tileItemIds = new List<ulong>();
        int tileStackSize = tile.GetStackSize();
        
        Debug.Log($"[DEBUG] Removing items from tile...");
        for (int i = 0; i < tileStackSize; i++)
        {
            ulong itemId = tile.RemoveTopItemFromStack();
            if (itemId != 0 && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemId, out NetworkObject netObj))
            {
                netObj.ChangeOwnership(clientId);
                tileItemIds.Add(itemId);
                Debug.Log($"[DEBUG] Changed ownership of item {itemId} to client {clientId}");
            }
        }
        
        // 타일 초기화 및 아이템 타입 설정
        if (playerItemType != ItemType.None)
        {
            Debug.Log($"[DEBUG] Setting tile item type to: {playerItemType}");
            tile.AddItem(playerItemType);
        }
        
        // 플레이어가 들고 있던 아이템을 타일에 추가
        if (playerItemIds.Length > 0)
        {
            Debug.Log($"[DEBUG] Adding player items to tile: {playerItemIds.Length}");
            
            // 모든 플레이어 아이템 소유권 제거
            foreach (ulong id in playerItemIds)
            {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject netObj))
                {
                    netObj.RemoveOwnership();
                    Debug.Log($"[DEBUG] Removed ownership of item {id}");
                    
                    // 각 아이템을 타일에 직접 추가
                    tile.ForceAddItemToStackFromServer(id);
                    Debug.Log($"[DEBUG] Added item {id} to tile stack");
                }
            }
            
            // 타일 스택 상태 모든 클라이언트에 동기화
            UpdateTileStackClientRpc(tileId);
        }
        
        // 타일의 아이템을 플레이어에게 설정
        if (tileItemIds.Count > 0)
        {
            Debug.Log($"[DEBUG] Setting held stack for player with {tileItemIds.Count} items");
            SetHeldObjectStackClientRpc(tileItemIds.ToArray(), NetworkObjectId);
        }
        else
        {
            Debug.Log($"[DEBUG] No tile items to give to player");
            UpdatePlayerItemTypeClientRpc(ItemType.None, NetworkObjectId);
        }
    }

    // 아이템 배열을 타일에 안전하게 추가하는 ServerRpc
    [ServerRpc]
    void AddItemsToTileServerRpc(ulong tileId, ulong[] itemIds)
    {
        Debug.Log($"[DEBUG] AddItemsToTileServerRpc called with tileId: {tileId}, itemIds count: {itemIds.Length}");
    
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
        {
            Debug.LogError($"[DEBUG] Tile NetworkObject not found with ID: {tileId}");
            return;
        }
    
        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null)
        {
            Debug.LogError($"[DEBUG] Tile component not found on object with ID: {tileId}");
            return;
        }
    
        // 각 아이템을 타일에 추가
        foreach (ulong itemId in itemIds)
        {
            Debug.Log($"[DEBUG] Processing item {itemId}");
            if (itemId != 0)
            {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemId, out NetworkObject netObj))
                {
                    Debug.Log($"[DEBUG] Found NetworkObject for item {itemId}, calling ForceAddItemToStackFromServer");
                    tile.ForceAddItemToStackFromServer(itemId);
                }
                else
                {
                    Debug.LogError($"[DEBUG] NetworkObject NOT found for item {itemId}");
                }
            }
            else
            {
                Debug.LogWarning($"[DEBUG] Invalid item ID: 0");
            }
        }
    
        // 모든 클라이언트에 타일 스택 업데이트 알림
        Debug.Log($"[DEBUG] Calling UpdateTileStackClientRpc");
        UpdateTileStackClientRpc(tileId);
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
        
        // 소유자인 경우 스택에 추가
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
    
            // 타일에 ItemType 정보 추가 (스택 아이템의 기본 타입)
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
                
                    // 서버와 클라이언트 모두에서 타일에 아이템 추가
                    tile.AddItemToStack(netObj.NetworkObjectId);
                }
            }
    
            localPickup.heldObjectStack.Clear();
            localPickup.UpdateHeldObjectList();
            localPickup.UpdatePlayerItemType(ItemType.None);
        }
    }
    
    //아이템 스왑 관련
    
    
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
            tile.UpdateVisibleStack(); // Tile 클래스에 public으로 변경해야 함
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
    
    private void OnDrawGizmos()
    {
        Vector3 tileBoxCenter = transform.position + Vector3.down * (detectionDistance * 0.5f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(tileBoxCenter, boxSize);
    }
}