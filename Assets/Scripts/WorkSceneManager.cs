
using System;
using Unity.Netcode;
using UnityEngine;

public class WorkSceneManager: SingletonManager<WorkSceneManager>
{
    public RailController firstRail;
    public GameObject trainCarHeadPrefab;
    
    private void Start()
    {
        NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
        NetworkManager.Singleton.OnClientStarted += HandleClientStarted;
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnTransportFailure += HandleTransportFailure;
        
        NetworkManager.Singleton.StartServer();
    }
        
    private void OnDisable()
    {
        if (NetworkManager.Singleton)
        {
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
            NetworkManager.Singleton.OnClientStarted -= HandleClientStarted;
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnTransportFailure -= HandleTransportFailure;
        }
    }
    
    private void HandleServerStarted()
    {
        if(trainCarHeadPrefab) Instantiate(trainCarHeadPrefab);
        Debug.Log("서버 시작 완료.");
    }

    private void HandleClientStarted()
    {
        Debug.Log("클라이언트 시작 완료.");
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"클라이언트 연결됨: {clientId}");
    }

    private void HandleTransportFailure()
    {
        Debug.LogError("네트워크 전송 실패.");
    }
}
