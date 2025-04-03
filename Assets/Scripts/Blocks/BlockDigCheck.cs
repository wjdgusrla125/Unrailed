using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public enum HandleType
{
    None,
    Axe,
    Pickaxe,
    Bucket,
    Bucket_In_Water,
    WoodPickup,
    IronPickup
}
public class BlockDigCheck : MonoBehaviour 
{
    public enum BlockType
    {
        None,
        Wood,
        Iron,
        Water
    }

    [SerializeField] private int BlockHpCount;
    [SerializeField] private BlockType blockType;
    [SerializeField] private List<GameObject> DropGameObject;

    // 블럭 정보 get, set
    public BlockType BlockTypeProperty
    {
        get { return blockType; }
        set { blockType = value; }
    }

    // 플레이어 키 입력에 대한 반응.
    public void CheckRay(HandleType handleType)
    {
        // 여긴 충돌 처리나 캐는 처리 어떻게 할지 모르니 주석.
        /*if (handleType == HandleType.Bucket_In_Water || handleType == HandleType.WoodPickup || handleType == HandleType.IronPickup)
            return;
        if (handleType == HandleType.None && blockType == BlockType.None)
            return;*/

        if (handleType == HandleType.Axe && blockType == BlockType.Wood)
        {
            BlockHpCount--;
        }
        else if (handleType == HandleType.Pickaxe && blockType == BlockType.Iron)
        {
            BlockHpCount--;
        }
        else if (handleType == HandleType.Bucket && blockType == BlockType.Water)
        {
            BlockHpCount--;
        }
        
        if (BlockHpCount == 0)
            DestroyBlock();
    }

    public void DestroyBlock()
    {
        GameObject dropObject = GameObject.Instantiate(DropGameObject[(int)blockType]);
    }
}
