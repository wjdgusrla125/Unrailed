using System.Collections.Generic;
using UnityEngine;

public class CraftingTable : MonoBehaviour 
{
    // ���̺� ���� üũ
    public bool InTableWood = false;
    public bool InTableIron = false;
    // ���̺� ���� �Ǵ� ö�� �÷����� �� ������Ʈ ó��.
    public GameObject WoodObject;
    public GameObject IronObject;

    public void Update()
    {
        WoodObject.SetActive(InTableWood);
        IronObject.SetActive(InTableIron);
    }
}
