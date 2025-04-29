
using System;
using System.Collections;
using System.Collections.Generic;
using Sound;
using Unity.Netcode;
using UnityEngine;

public class TrainManager: MonoBehaviour
{
    public Dictionary<int, Train> trains = new ();
    private CameraController _cameraController;
    public float Speed { get; private set; }
    private const float START_COUNTDOWN = 5.0F;

    private void Awake()
    {
        if(NetworkManager.Singleton.IsServer) StartCoroutine(Spawn());
        if (Camera.main != null) _cameraController = Camera.main.GetComponent<CameraController>();
        GameManager.Instance.trainManager = this;
        Speed = 0.1f;
        // firstRail = WorkSceneManager.Instance.firstRail;
    }

    public void SetSpeed(float value)
    {
        Speed = value;
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

    public void AllTrainsDespawn()
    {
        //헤드를 제외한 모든 객체를 디스폰한 뒤
        foreach (var kvp in trains)
        {
            if (kvp.Value is not Train_Head)
            {
                kvp.Value.NetworkObject.Despawn();
            }
        }
        
        //헤드를 디스폰한다.
        foreach (var kvp in trains)
        { 
            kvp.Value.NetworkObject.Despawn();
        }
    }

    public void StartTrainCount()
    {
        StartCoroutine(CountDownAndStart());
    }

    private IEnumerator CountDownAndStart()
    {
        Debug.Log("카운트다운 시작");

        int maxDisplay = Mathf.Min(trains[0].countdownObject.Length, (int)START_COUNTDOWN);

        if (START_COUNTDOWN > maxDisplay)
            yield return new WaitForSeconds(START_COUNTDOWN - maxDisplay);

        for (int n = maxDisplay; n >= 1; n--)
        {
            if (n <= 5)
            {
                trains[0].CallCountdown(n); //5초남았을때부터 카운트다운을 띄움
            }
            yield return new WaitForSeconds(1f);
        }

        trains[0].RecallCountdown();
        _cameraController.InitCamera(this);
        // _cameraController.StartCamera();
        StartAllTrains();
        SoundManager.Instance.PlayBGM(SoundManager.Instance.bgmClips[1], 0.5f);
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

    //게임 오버 시 기차 속도를 빠르게 함.
    public void GameOver()
    {
        Speed = 0.8f;
    }
}
