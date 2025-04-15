using System;
using Unity.Netcode;
using UnityEngine;

namespace Network
{
    public class NetworkEventHandler: SingletonManager<NetworkEventHandler>
    {
        [SerializeField]private RpcManager rpcManagerPrefab;
        
        private void Start()
        {
            // 서버 시작 시 호출
            NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
            
            // 클라이언트로 서버 접속 시 호출
            NetworkManager.Singleton.OnClientStarted += HandleClientStarted;
            
            // 다른 클라이언트가 서버에 접속 시 호출
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            
            // 네트워크 전송 실패시 호출
            NetworkManager.Singleton.OnTransportFailure += HandleTransportFailure;
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
            UIManager.Instance.SetPlayerCountText(NetworkManager.Singleton.ConnectedClients.Count);
            Debug.Log("서버 시작 완료.");
            UIManager.Instance.SetSeed();
            // UIManager.Instance.OpenSessionUI();
            // UIManager.Instance.OpenGameUI();
        }

        private void HandleClientStarted()
        {
            if (NetworkManager.Singleton.IsHost)
            {
                RpcManager rpcManager = Instantiate(rpcManagerPrefab);
                rpcManager.NetworkObject.Spawn();
            }
            
            UIManager.Instance.SetPlayerCountText(NetworkManager.Singleton.ConnectedClients.Count);
            UIManager.Instance.OpenSessionUI();
            UIManager.Instance.SetButtonInteractable(NetworkManager.Singleton.IsHost);
            Debug.Log("클라이언트 시작 완료.");
        }

        private void HandleClientConnected(ulong clientId)
        {
            Debug.Log($"클라이언트 연결됨: {clientId}");
            UIManager.Instance.SetPlayerCountText(NetworkManager.Singleton.ConnectedClients.Count);
            
            //호스트는 시드를 공유
            if (NetworkManager.Singleton.IsHost)
            {
                RpcManager.Instance.SetSeedRpc(MapGenerator.Instance.GetSeed());
            }
        }

        private void HandleTransportFailure()
        {
            Debug.LogError("네트워크 전송 실패.");
        }
    }
}