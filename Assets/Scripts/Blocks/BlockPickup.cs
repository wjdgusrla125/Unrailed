using UnityEngine;

public class BlockPickup : MonoBehaviour
{
    [SerializeField] private HandleType handleType;

    // 바닥에 있는 아이템 획득.
    public void PickupHand(GameObject obj)
    {
        switch(obj.GetComponent<BlockDigCheck>().BlockTypeProperty)
        {
            case BlockDigCheck.BlockType.Wood:
                handleType = HandleType.WoodPickup;
                break;
            case BlockDigCheck.BlockType.Iron:
                handleType = HandleType.IronPickup;
                break;
        }
    }

    // 손에 들고 있는 아이템 드랍.
    public void DropHand()
    {
        switch(handleType)
        {
            case HandleType.Bucket_In_Water:
                handleType = HandleType.Bucket;
                break;
            case HandleType.WoodPickup:
                handleType = HandleType.None;
                break;
            case HandleType.IronPickup:
                handleType = HandleType.None;
                break;
        }
    }
}
