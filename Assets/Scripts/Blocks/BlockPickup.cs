using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BlockPickup : NetworkBehaviour
{
    public Transform handPosition;
    public Transform twoHandPosition;
    public float detectionDistance = 1.5f;
    public Vector3 boxSize;
    public LayerMask tileLayerMask;
    public InputReader inputReader;

    // heldObject를 스택으로 변경
    private Stack<NetworkObject> heldObjectStack = new Stack<NetworkObject>();
    
    [SerializeField] private List<NetworkObject> heldObjectList = new List<NetworkObject>();
    
    // 메인 아이템에 쉽게 접근하기 위한 프로퍼티
    public NetworkObject MainHeldObject => heldObjectStack.Count > 0 ? heldObjectStack.Peek() : null;
    
    private bool canInteract = false;
    public Tile currentTile = null;
    
    [SerializeField] private PlayerInfo playerInfo;
    
    private const int maxStackSize = 4;
    
    [SerializeField] private Vector3 stackOffset = new Vector3(0, 0.1f, 0);

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
                RequestSwapDifferentTypeStacksServerRpc(currentTile.NetworkObjectId);
            }
            // 3개 초과인 경우 아무 일도 일어나지 않음 (그냥 return)
            return;
        }
        
        if (heldObjectStack.Count >= maxStackSize && tileStackSize >= 1 && isSameItemType && areBothStackable)
        {
            RequestPlaceStackOnTileServerRpc(currentTile.NetworkObjectId);
        }
        else if (heldObjectStack.Count > 1 && (!isSameItemType || !areBothStackable))
        {
            // Remove secondClosestTile check and related functionality
            if (heldItem.IsStackable && !tileItem.IsStackable)
            {
                // 스택 가능한 아이템을 여러 개 들고 있고, 타일에 일반 아이템이 있을 때
                RequestSwapStackWithSingleItemServerRpc(currentTile.NetworkObjectId);
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
            // 중복 아이템 체크 추가 - 이미 들고 있는 아이템은 다시 추가하지 않음
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
                // 리스트 업데이트
                playerPickup.UpdateHeldObjectList();
            
                Item heldItem = netObj.GetComponent<Item>();
                if (heldItem != null && playerPickup.playerInfo != null)
                {
                    playerPickup.UpdatePlayerItemType(heldItem.ItemType);
                }
                
                // 즉시 위치 업데이트
                Transform holdPosition = heldItem != null && heldItem.WithTwoHanded ? 
                    playerPickup.twoHandPosition : playerPickup.handPosition;
                    
                netObj.transform.position = holdPosition.position;
                netObj.transform.rotation = holdPosition.rotation;
                
                // 스택된 아이템들의 위치도 업데이트
                playerPickup.UpdateStackedItemPositions(holdPosition);
            }
        }
        // 비소유자 클라이언트도 아이템 위치 업데이트
        else
        {
            // 해당 오브젝트가 다른 플레이어의 스택에 들어갔으므로 시각적으로 업데이트
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

        // 모든 클라이언트가 시각적 위치 업데이트를 위한 정보 준비
        Transform holdPosition = playerPickup.handPosition;
        ItemType itemType = ItemType.None;
        
        if (playerPickup.IsLocalPlayer || playerPickup.IsOwner)
        {
            playerPickup.heldObjectStack.Clear();

            HashSet<ulong> processedIds = new HashSet<ulong>(); // 중복 방지를 위한 해시셋 추가

            for (int i = objectIds.Length - 1; i >= 0; i--) // 역순으로 스택에 추가 (첫 번째 항목이 맨 위에 오도록)
            {
                ulong objectId = objectIds[i];
                
                // 이미 처리한 아이템이면 건너뜀
                if (processedIds.Contains(objectId)) continue;
                
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
                {
                    playerPickup.heldObjectStack.Push(netObj);
                    processedIds.Add(objectId); // 처리된 ID 추가
                    
                    // 첫 번째 아이템의 타입 설정 및 홀드 위치 결정
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

            // 리스트 업데이트
            playerPickup.UpdateHeldObjectList();

            if (playerPickup.playerInfo != null)
            {
                playerPickup.UpdatePlayerItemType(itemType);
            }
        }
        
        // 모든 클라이언트에서 아이템 위치 업데이트
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

        DropStackedItemsOnTileClientRpc(tileId);
    }
    
    [ServerRpc]
    void RequestPlaceStackOnTileServerRpc(ulong tileId, ServerRpcParams rpcParams = default)
    {
        if (heldObjectStack.Count == 0 || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
            return;

        DropStackedItemsOnTileClientRpc(tileId);
    }
    
    [ServerRpc]
    void UpdateTileServerRpc(ItemType itemType, ServerRpcParams rpcParams = default)
    {
        UpdateTileClientRpc(itemType);
    }
    
    [ClientRpc]
    void UpdateTileClientRpc(ItemType itemType)
    {
        if (IsOwner && currentTile != null)
        {
            currentTile.AddItem(itemType);
        }
    }

    [ClientRpc]
    void DropStackedItemsClientRpc()
    {
        // 각 클라이언트에서 자신의 NetworkObject에 대해서만 작업 수행
        if (!IsOwner && NetworkObjectId != NetworkObjectId) return;

        float dropOffset = 0.2f;
        NetworkObject[] stackedArray = heldObjectStack.ToArray();

        foreach (NetworkObject netObj in stackedArray)
        {
            if (netObj != null)
            {
                netObj.gameObject.SetActive(true);
                netObj.transform.SetParent(null);
            
                // 클라이언트에서는 RemoveOwnership이 작동하지 않으므로 서버 RPC로 처리해야 함
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
        // 리스트 업데이트
        UpdateHeldObjectList();
        UpdatePlayerItemType(ItemType.None);
    }
    
    [ClientRpc]
    void DropObjectOnTileClientRpc(ulong objectId, ulong tileId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj) ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
            return;
    
        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null) return;

        // 서버에서만 실제 타일에 아이템 추가를 처리하도록 수정
        if (IsServer)
        {
            tile.AddItemToStack(objectId);
        }
        // 시각적 업데이트는 모든 클라이언트에서 진행
        // 하지만 AddItemToStack 호출 없이 위치만 업데이트

        BlockPickup localPickup = GetLocalBlockPickup();
        if (localPickup != null && (IsLocalPlayer || IsOwner))
        {
            // 스택에서 해당 아이템 제거
            NetworkObject[] stackArray = localPickup.heldObjectStack.ToArray();
            localPickup.heldObjectStack.Clear();
        
            foreach (NetworkObject obj in stackArray)
            {
                if (obj.NetworkObjectId != objectId)
                {
                    localPickup.heldObjectStack.Push(obj);
                }
            }
        
            // 리스트 업데이트
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
    
    [ClientRpc]
    void DropStackedItemsOnTileClientRpc(ulong tileId)
    {
        if (!IsOwner) return;
    
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
        {
            return;
        }
            
        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null) return;
    
        ItemType itemType = ItemType.None;
    
        if (heldObjectStack.Count > 0)
        {
            NetworkObject mainObject = heldObjectStack.Peek();
            Item heldItem = mainObject.GetComponent<Item>();
            if (heldItem != null)
            {
                itemType = heldItem.ItemType;
            }
        }
    
        foreach (NetworkObject netObj in heldObjectStack)
        {
            if (netObj != null)
            {
                netObj.gameObject.SetActive(true);
                netObj.transform.SetParent(null);
                netObj.RemoveOwnership();
            
                // 서버에서만 실제 타일에 아이템 추가
                if (IsServer)
                {
                    tile.AddItemToStack(netObj.NetworkObjectId);
                }
            
                if (itemType != ItemType.None && IsServer)
                {
                    tile.AddItem(itemType);
                    itemType = ItemType.None; // Only add type once
                }
            }
        }
    
        heldObjectStack.Clear();
        // 리스트 업데이트
        UpdateHeldObjectList();
        UpdatePlayerItemType(ItemType.None);
    }
    
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
    
        // 타일 컴포넌트 가져오기
        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null) return;
    
        // 타일에서 아이템 제거 (스택 초기화를 위해)
        tile.RemoveTopItemFromStack();
    
        // 플레이어가 스택을 들고 있으면 타일에 모두 놓기
        if (heldObjectStack.Count > 1)
        {
            DropStackedItemsOnTileClientRpc(tileId);
        }
        else // 단일 아이템만 들고 있는 경우
        {
            heldNetObj.RemoveOwnership();
            DropObjectOnTileClientRpc(heldObjectId, tileId);
        }
    
        // 타일에서 새 아이템 집기
        //newNetObj.ChangeOwnership(clientId);
        AddToHeldStackClientRpc(newTargetId, NetworkObjectId);
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
            
            //netObj.ChangeOwnership(rpcParams.Receive.SenderClientId);
            AddToHeldStackClientRpc(itemId, NetworkObjectId);
        }
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
    
        // 플레이어가 들고 있는 스택에서 해당 아이템 제거 (먼저 수행)
        NetworkObject[] stackArray = heldObjectStack.ToArray();
        heldObjectStack.Clear();
    
        // 타일의 스택 아이템들 저장
        List<ulong> tileItemIds = new List<ulong>();
        int itemsToPickup = Mathf.Min(tile.GetStackSize(), maxStackSize);
    
        for (int i = 0; i < itemsToPickup; i++)
        {
            ulong itemId = tile.RemoveTopItemFromStack();
            if (itemId == 0) continue;
        
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemId, out NetworkObject netObj))
            {
                //netObj.ChangeOwnership(clientId);
                tileItemIds.Add(itemId);
            }
        }
    
        // 타일에 들고 있던 일반 아이템 놓기
        heldNetObj.RemoveOwnership();
        tile.AddItemToStack(heldObjectId);
    
        // 타일의 아이템 타입 업데이트
        Item heldItem = heldNetObj.GetComponent<Item>();
        if (heldItem != null)
        {
            tile.AddItem(heldItem.ItemType);
        }
    
        // 플레이어에게 스택 아이템들 추가
        if (tileItemIds.Count > 0)
        {
            SetHeldObjectStackClientRpc(tileItemIds.ToArray(), NetworkObjectId);
        }
        else
        {
            // 아이템을 얻지 못한 경우 플레이어 상태 업데이트
            UpdatePlayerItemTypeClientRpc(ItemType.None, NetworkObjectId);
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
    
    [ServerRpc]
    void RequestSwapStackWithSingleItemServerRpc(ulong tileId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
        {
            return;
        }
    
        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null || tile.GetStackSize() == 0) return;
    
        ulong clientId = rpcParams.Receive.SenderClientId;
    
        // 타일에 있는 아이템 (일반 아이템)
        ulong tileItemId = tile.PeekTopItemFromStack(); // 먼저 확인만 하고 제거는 아직 안 함
        if (tileItemId == 0 || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileItemId, out NetworkObject tileItemObj))
        {
            return;
        }
    
        // 현재 플레이어가 들고 있는 아이템 목록을 저장
        NetworkObject[] currentHeldItems = new NetworkObject[heldObjectStack.Count];
        heldObjectStack.CopyTo(currentHeldItems, 0);
    
        // 플레이어의 스택 비우기
        heldObjectStack.Clear();
    
        // 타일의 일반 아이템 제거
        tile.RemoveTopItemFromStack();
    
        // 일반 아이템을 플레이어에게 전달
        //tileItemObj.ChangeOwnership(clientId);
    
        // 플레이어가 들고 있던 스택 아이템들을 타일에 놓기
        foreach (NetworkObject heldItem in currentHeldItems)
        {
            if (heldItem != null)
            {
                heldItem.RemoveOwnership();
                tile.AddItemToStack(heldItem.NetworkObjectId);
            }
        }
    
        // 타일에서 가져온 아이템만 플레이어에게 설정
        ulong[] singleItemArray = new ulong[] { tileItemId };
        SetHeldObjectStackClientRpc(singleItemArray, NetworkObjectId);
    
        // 타일의 스택 업데이트 명시적 호출
        UpdateTileStackClientRpc(tileId);
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
    
    [ServerRpc]
    void RequestSwapDifferentTypeStacksServerRpc(ulong tileId, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tileId, out NetworkObject tileObj))
        {
            return;
        }
        
        Tile tile = tileObj.GetComponent<Tile>();
        if (tile == null || tile.GetStackSize() == 0) return;
        
        ulong clientId = rpcParams.Receive.SenderClientId;
        
        // 타일의 스택 아이템들 저장
        List<ulong> tileItemIds = new List<ulong>();
        int tileStackSize = tile.GetStackSize();
        
        // 현재 플레이어가 들고 있는 아이템 목록을 저장
        NetworkObject[] currentHeldItems = new NetworkObject[heldObjectStack.Count];
        heldObjectStack.CopyTo(currentHeldItems, 0);
        
        // 타일의 ItemType 기억
        ItemType tileItemType = tile.GetCurrentItemType();
        
        // 타일의 모든 아이템을 가져옴
        for (int i = 0; i < tileStackSize; i++)
        {
            ulong itemId = tile.RemoveTopItemFromStack();
            if (itemId == 0) continue;
            
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemId, out NetworkObject netObj))
            {
                //netObj.ChangeOwnership(clientId);
                tileItemIds.Add(itemId);
            }
        }
        
        // 플레이어의 스택 비우기
        heldObjectStack.Clear();
        
        // 플레이어가 들고 있던 모든 아이템의 타입 기억
        ItemType playerItemType = ItemType.None;
        if (currentHeldItems.Length > 0 && currentHeldItems[0] != null)
        {
            Item item = currentHeldItems[0].GetComponent<Item>();
            if (item != null)
            {
                playerItemType = item.ItemType;
            }
        }
        
        // 플레이어가 들고 있던 스택 아이템들을 타일에 놓기
        foreach (NetworkObject heldItem in currentHeldItems)
        {
            if (heldItem != null)
            {
                heldItem.RemoveOwnership();
                tile.AddItemToStack(heldItem.NetworkObjectId);
            }
        }
        
        // 타일의 ItemType 업데이트
        if (playerItemType != ItemType.None)
        {
            UpdateTileClientRpc(playerItemType);
        }
        
        // 타일에서 가져온 아이템들을 플레이어에게 설정
        if (tileItemIds.Count > 0)
        {
            SetHeldObjectStackClientRpc(tileItemIds.ToArray(), NetworkObjectId);
        }
        else
        {
            // 아이템을 얻지 못한 경우 플레이어 상태 업데이트
            UpdatePlayerItemTypeClientRpc(ItemType.None, NetworkObjectId);
        }
        
        // 타일의 스택 업데이트 명시적 호출
        UpdateTileStackClientRpc(tileId);
    }
    
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
    
    private void OnDrawGizmos()
    {
        Vector3 tileBoxCenter = transform.position + Vector3.down * (detectionDistance * 0.5f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(tileBoxCenter, boxSize);
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
}