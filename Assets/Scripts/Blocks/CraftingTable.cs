using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CraftingTable : MonoBehaviour 
{
    // 테이블 공간 체크
    public bool AbleInTableWood = true;
    public bool AbleInTableIron = true;
    // 테이블에 나무 또는 철을 올려놨을 때 오브젝트 처리.
    public List<GameObject> WoodObjects;
    public List<GameObject> IronObjects;
    public GameObject WoodPos;
    public GameObject IronPos;

    public void Update()
    {
        if (WoodObjects.Count >= 3)
            AbleInTableWood = false;
        else
            AbleInTableWood = true;
        if (IronObjects.Count >= 3)
            AbleInTableIron = false;
        else
            AbleInTableIron = true;
        SetTableObjectPosition();
    }

    public void OnTableItem(NetworkObject itemObject)
    {
        if (itemObject.gameObject.GetComponent<Item>().ItemType == ItemType.WoodPlank && AbleInTableWood)
        {
            WoodObjects.Add(itemObject.gameObject);
        }
        else if(itemObject.gameObject.GetComponent<Item>().ItemType == ItemType.Iron && AbleInTableIron)
        {
            IronObjects.Add(itemObject.gameObject);
        }
    }

    public void SetTableObjectPosition()
    { 
        if(WoodObjects.Count != 0)
        {
            for(int i = 0; i < WoodObjects.Count; i++)
            {
                WoodObjects[i].transform.position = WoodPos.gameObject.transform.position + new Vector3(0, 0.22f * (WoodObjects.Count - (i + 1)), 0) - new Vector3(0, 0.2f, 0);
            }
        }
        if (IronObjects.Count != 0)
        {
            for (int i = 0; i < IronObjects.Count; i++)
            {
                IronObjects[i].transform.position = IronPos.gameObject.transform.position + new Vector3(0, 0.22f * (IronObjects.Count - (i + 1)), 0) - new Vector3(0, 0.2f, 0);
            }
        }
    }
}
