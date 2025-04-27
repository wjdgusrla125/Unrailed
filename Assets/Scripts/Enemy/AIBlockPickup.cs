using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class AIBlockPickup : NetworkBehaviour
{
    public Transform handPosition;
    public Transform twoHandPosition;
    public float detectionDistance = 1.5f;
    public Vector3 boxSize;
    public LayerMask tileLayerMask;

    public Stack<NetworkObject> heldObjectStack = new Stack<NetworkObject>();
    public NetworkObject MainHeldObject => heldObjectStack.Count > 0 ? heldObjectStack.Peek() : null;
    public Tile currentTile = null;

    private const int maxStackSize = 3;
    [SerializeField] private Vector3 stackOffset = new Vector3(0, 0.2f, 0);

    void Update()
    {
        if (heldObjectStack.Count > 0)
        {
            NetworkObject mainObject = heldObjectStack.Peek();
            if (mainObject != null)
            {
                Item heldItem = mainObject.GetComponent<Item>();
                Transform holdPosition = (heldItem != null && heldItem.WithTwoHanded) ? twoHandPosition : handPosition;

                mainObject.transform.position = holdPosition.position;
                mainObject.transform.rotation = holdPosition.rotation;

                UpdateStackedItemPositions(holdPosition);
            }
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

    public bool DetectTileBelow()
    {
        Vector3 boxCenter = transform.position + Vector3.down * (detectionDistance * 0.5f);
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
                    float distance = Vector3.Distance(transform.position, tile.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTile = tile;
                    }
                }
            }
            currentTile = closestTile;
            return true;
        }
        currentTile = null;
        return false;
    }

    public bool TryPickupItem()
    {
        if (currentTile == null || currentTile.GetStackSize() == 0)
            return false;

        if (heldObjectStack.Count >= maxStackSize)
            return false;

        ulong topItemId = currentTile.PeekTopItemFromStack();
        if (topItemId == 0)
            return false;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(topItemId, out NetworkObject netObj))
            return false;

        if (IsServer)
        {
            currentTile.RemoveTopItemFromStack();
            netObj.ChangeOwnership(OwnerClientId);
        }

        heldObjectStack.Push(netObj);
        return true;
    }

    public bool TryDropItem()
    {
        if (currentTile == null || heldObjectStack.Count == 0)
            return false;

        NetworkObject heldObj = heldObjectStack.Pop();

        if (IsServer)
        {
            heldObj.RemoveOwnership();
            currentTile.ForceAddItemToStackFromServer(heldObj.NetworkObjectId);
        }

        return true;
    }
}
