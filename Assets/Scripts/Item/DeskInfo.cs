using System;
using System.Threading;
using UnityEngine;

public class DeskInfo : MonoBehaviour
{
    [Header("현재 생성된 레일")]
    public int RailCount = 0;

    [HideInInspector]
    public bool CanCreateRail = true;
    [Header("레일 오브젝트")]
    [SerializeField] private GameObject RailObject;

    public event Action CreateDoneRail;

    public void RailCreateDone()
    {
        RailCount++;
        CreateDoneRail.Invoke();
    }

    public void RailCountCheck()
    {
        if (RailCount == 3)
            CanCreateRail = false;
        else
            CanCreateRail = true;
    }

    public void GetRail()
    {
        
    }

    public GameObject GetRailObject()
    {
        return RailObject;
    }

    public void Update()
    {
        RailCountCheck();
    }

}