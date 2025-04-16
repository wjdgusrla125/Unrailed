
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TrainManager: MonoBehaviour
{
    public Dictionary<int, Train> trains = new ();
    public RailController firstRail;
    [Header("기차 속도")] public float speed;

    private void Awake()
    {
        StartCoroutine(Spawn());
        firstRail = WorkSceneManager.Instance.firstRail;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            Debug.Log("Q입력, 기차 출발");
            foreach (var keyValuePair in trains)
            {
                keyValuePair.Value.StartTrain();
            }
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("E입력, 기차 정지");
            foreach (var keyValuePair in trains)
            {
                keyValuePair.Value.StopTrain();
            }
        }
    }

    IEnumerator Spawn()
    {
        yield return new WaitForSeconds(1f);
        Debug.Log("열차스폰시작");
        foreach (var keyValuePair in trains)
        {
            // Debug.Log($"{keyValuePair.Key}번 열차 스폰");
            keyValuePair.Value.SetDestinationRail(firstRail);
            NetworkObject no = keyValuePair.Value.GetComponent<NetworkObject>();
            no.Spawn();
        }
        Debug.Log("열차스폰완료");

        Debug.Log("열차 출발");
        foreach (var keyValuePair in trains)
        {
            keyValuePair.Value.StartTrain();
        }
    }
}
