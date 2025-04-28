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
            
            // 서버: 클라이언트 중 하나가 세션에서 나갔을 때 호출, 클라이언트: 자신이 세션에서 나갔을 때 호출
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
            
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
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
                NetworkManager.Singleton.OnTransportFailure -= HandleTransportFailure;
            }
        }

        private void HandleServerStarted()
        {
            //WorkScene을 할 때는 비활성화
            UIManager.Instance.SetPlayerCountText(NetworkManager.Singleton.ConnectedClients.Count);
            UIManager.Instance.SetSeed();
            
            Debug.Log("서버 시작 완료.");
            // // UIManager.Instance.OpenSessionUI();
            // // UIManager.Instance.OpenGameUI();
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

        private void HandleClientDisconnected(ulong clientId)
        {
            Debug.Log($"클라이언트 연결 해제됨: {clientId}");

            switch (NetworkManager.Singleton.IsServer)
            {
                //세션에서 나가졌는데
                case false when !NetworkManager.Singleton.ShutdownInProgress:
                    //호스트가 아니면서 직접 종료한 게 아닐경우
                    Debug.Log("호스트가 나갔음.");
                    UIManager.Instance.OpenLobbyUI();
                    UIManager.Instance.CloseGameOverMenu();
                    return;
                case false when NetworkManager.Singleton.ShutdownInProgress:
                    //호스트가 아니면서 내가 종료했을 경우
                    Debug.Log("세션 종료");
                    UIManager.Instance.OpenLobbyUI();
                    UIManager.Instance.CloseGameOverMenu();
                    return;
                case true when clientId != NetworkManager.Singleton.LocalClientId:
                    //호스트일 때 다른 사람이 나갔을 경우
                    Debug.Log($"클라이언트 {clientId}가 세션에서 나감.");
                    UIManager.Instance.SetPlayerCountText(NetworkManager.Singleton.ConnectedClients.Count);
                    return;
                case true when clientId == NetworkManager.Singleton.LocalClientId:
                    //호스트일 때 자신이 나갔을 경우
                    Debug.Log("세션 종료(호스트).");
                    UIManager.Instance.OpenLobbyUI();
                    UIManager.Instance.CloseGameOverMenu();
                    return;
                default:
                    Debug.LogWarning("정의되지 않은 케이스");
                    break;
            }
        }

        private void HandleTransportFailure()
        {
            Debug.LogError("네트워크 전송 실패.");
        }
    }
}