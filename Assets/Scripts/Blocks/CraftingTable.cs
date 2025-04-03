using System.Collections.Generic;
using UnityEngine;

public class CraftingTable : MonoBehaviour 
{
    // 테이블 공간 체크
    public bool InTableWood = false;
    public bool InTableIron = false;
    // 테이블에 나무 또는 철을 올려놨을 때 오브젝트 처리.
    public GameObject WoodObject;
    public GameObject IronObject;

    public void Update()
    {
        WoodObject.SetActive(InTableWood);
        IronObject.SetActive(InTableIron);
    }
}
