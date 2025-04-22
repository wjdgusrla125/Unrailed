using Unity.VisualScripting;
using UnityEngine;

public class BucketInfo : MonoBehaviour
{
    private Item ItemInfo;
    void Start()
    {
        ItemInfo = gameObject.GetComponent<Item>();
    }

    private void Update()
    {
        if (ItemInfo.ItemType == ItemType.Bucket)
            gameObject.transform.GetChild(0).transform.GetChild(5).gameObject.SetActive(false);
        if (ItemInfo.ItemType == ItemType.WaterInBucket)
            gameObject.transform.GetChild(0).transform.GetChild(5).gameObject.SetActive(true);
    }
}
