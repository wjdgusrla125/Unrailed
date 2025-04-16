
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
        if(NetworkManager.Singleton.IsServer) StartCoroutine(Spawn());
        // firstRail = WorkSceneManager.Instance.firstRail;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            Debug.Log("Z입력, 기차 출발");
            StartAllTrains();
        }
        if (Input.GetKeyDown(KeyCode.X))
        {
            Debug.Log("X입력, 기차 정지");
            StopAllTrains();
        }
    }

    private const float START_COUNTDOWN = 5.0F;

    public void StartTrainCount()
    {
        StartCoroutine(CountDownAndStart());
    }

    private IEnumerator CountDownAndStart()
    {
        Debug.Log("카운트다운 시작");
        
        yield return new WaitForSeconds(START_COUNTDOWN - 3f);
        Debug.Log("3초 전");

        yield return new WaitForSeconds(1f);
        Debug.Log("2초 전");

        yield return new WaitForSeconds(1f);
        Debug.Log("1초 전");

        yield return new WaitForSeconds(1f);
        // StartAllTrains();
    }

    public void StartAllTrains()
    {
        foreach (var keyValuePair in trains)
        {
            keyValuePair.Value.StartTrain();
        }
    }

    public void StopAllTrains()
    {
        foreach (var keyValuePair in trains)
        {
            keyValuePair.Value.StopTrain();
        }
    }

    IEnumerator Spawn()
    {
        yield return null;
        foreach (var keyValuePair in trains)
        {
            NetworkObject no = keyValuePair.Value.GetComponent<NetworkObject>();
            no.Spawn();
        }
    }

    public void PlaySpawnAnimation()
    {
        foreach (var keyValuePair in trains)
        {
            keyValuePair.Value.PlaySpawnAnimation(MapGenerator.SPAWN_OFFSET);
        }
    }
}
