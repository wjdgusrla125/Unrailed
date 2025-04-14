using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BreakableObject : MonoBehaviour 
{
    [SerializeField] private int BlockHpCount;
    [SerializeField] private BlockType blockType;
    [SerializeField] private List<GameObject> DropGameObject;
    
    public BlockType BlockTypeProperty
    {
        get { return blockType; }
        set { blockType = value; }
    }
    
    public void CheckRay(ItemType itemType)
    {
        if (itemType == ItemType.Axe && blockType == BlockType.Wood)
        {
            BlockHpCount--;
        }
        else if (itemType == ItemType.Pickaxe && blockType == BlockType.IronOre)
        {
            BlockHpCount--;
        }
        else if (itemType == ItemType.Bucket && blockType == BlockType.Water)
        {
            BlockHpCount--;
        }
        
        if (BlockHpCount == 0)
            DestroyBlock();
    }

    public void DestroyBlock()
    {
        GameObject dropObject = GameObject.Instantiate(DropGameObject[(int)blockType - 1]);
        dropObject.transform.position = gameObject.transform.position;
        dropObject.GetComponent<NetworkObject>().Spawn();
        Destroy(gameObject);
    }
}