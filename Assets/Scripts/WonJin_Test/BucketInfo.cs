using Unity.Netcode;
using UnityEngine;

public class BucketInfo : NetworkBehaviour
{
    public NetworkVariable<ItemType> SyncedItemType = new NetworkVariable<ItemType>(
        ItemType.Bucket,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private void Update()
    {
        var visual = transform.GetChild(0).GetChild(5).gameObject;
        visual.SetActive(SyncedItemType.Value == ItemType.WaterInBucket);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetItemTypeServerRpc(ItemType itemType)
    {
        SyncedItemType.Value = itemType;
    }
}