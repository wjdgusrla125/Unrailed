
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TrainManager: MonoBehaviour
{
    public Dictionary<int, Train> trains = new ();
    public RailController firstRail;
    private CameraController _cameraController;
    [Header("기차 속도")] public float speed;
    private const float START_COUNTDOWN = 5.0F;

    private void Awake()
    {
        if(NetworkManager.Singleton.IsServer) StartCoroutine(Spawn());
        if (Camera.main != null) _cameraController = Camera.main.GetComponent<CameraController>();
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
        
        _cameraController.InitCamera(this);
        _cameraController.StartCamera();
        StartAllTrains();
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
