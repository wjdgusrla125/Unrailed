

/*
using UnityEngine;

public class Temp : MonoBehaviour
{
        if (playerInfo.hitBlock == BlockType.CraftingTable)
        {
            if (playerInfo.itemType == ItemType.WoodPlank && playerInfo.CraftingTableObject.AbleInTableWood)
            {
                if (stackedObjects.Count >= 1)
                {
                    if (playerInfo.CraftingTableObject.WoodObjects.Count + (stackedObjects.Count + 1) > 3) return;
                    
                    Debug.Log("나무 2개 이상");
                    playerInfo.CraftingTableObject.OnTableItem(heldObject);
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
        }

        if (playerInfo.hitBlock == BlockType.DeskTable && playerInfo.itemType == ItemType.None && deskInfo.RailCount != 0)
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
        }
}*/