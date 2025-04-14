using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CraftingTable : MonoBehaviour 
{
    // 테이블 공간 체크
    public bool AbleInTableWood = true;
    public bool AbleInTableIron = true;
    public bool CreateDone = false;
    // 테이블에 나무 또는 철을 올려놨을 때 오브젝트 처리.
    public List<GameObject> WoodObjects;
    public List<GameObject> IronObjects;
    public GameObject WoodPos;
    public GameObject IronPos;
    [Header("연결된 데스크")]
    [SerializeField] private GameObject CraftingDesk;

    public void OnEnable()
    {
        CraftingDesk.GetComponent<DeskInfo>().CreateDoneRail += DoneEvent;
    }

    public void DoneEvent()
    {
        CreateDone = true;
        CreateRail();
    }
    public void CreateDoneDestroy()
    {
        GameObject temp = WoodObjects[WoodObjects.Count - 1];
        WoodObjects.RemoveAt(WoodObjects.Count - 1);
        Destroy(temp);

        temp = IronObjects[IronObjects.Count - 1];
        IronObjects.RemoveAt(IronObjects.Count - 1);
        Destroy(temp);
    }

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

    public void CreateRail()
    {
        if(WoodObjects.Count >= 1 && IronObjects.Count >= 1 && CraftingDesk.GetComponent<DeskInfo>().RailCount < 3)
        {
            if (CreateDone)
            {
                CreateDoneDestroy();
                CreateDone = false;
            }
            else
                return;

            switch (CraftingDesk.GetComponent<DeskInfo>().RailCount)
            {
                case 0:
                    Debug.Log("1개");
                    CraftingDesk.GetComponent<Animator>().SetInteger("GetRails", 1);
                    break;
                case 1:
                    Debug.Log("2개");
                    CraftingDesk.GetComponent<Animator>().SetInteger("GetRails", 2);
                    break;
                case 2:
                    Debug.Log("3개");
                    CraftingDesk.GetComponent<Animator>().SetInteger("GetRails", 3);
                    break;
            }
        }
    }

    public void OnTableItem(NetworkObject itemObject)
    {
        if (itemObject.gameObject.GetComponent<Item>().ItemType == ItemType.WoodPlank && AbleInTableWood)
        {
            WoodObjects.Add(itemObject.gameObject);
            CreateRail();
        }
        else if(itemObject.gameObject.GetComponent<Item>().ItemType == ItemType.Iron && AbleInTableIron)
        {
            IronObjects.Add(itemObject.gameObject);
            CreateRail();
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
