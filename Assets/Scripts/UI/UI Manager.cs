using System;
using Sound;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class UIManager: NetworkSingletonManager<UIManager>
{
    // private string _seed;
    [Header("클라이언트 접속할 호스트 IP")]
    [SerializeField] private string hostIp; // 예: "192.168.0.15"
    
    [SerializeField]private TMP_InputField seedInput;
    [SerializeField]private GameObject lobbyUI;
    [SerializeField]private GameObject sessionUI;
    [SerializeField]public InGameUIController gameUI;
    [SerializeField]private GameObject loadingScreen;
    [SerializeField]private GameObject gameOverMenu;
    [SerializeField]private Button restartButton;
    [SerializeField]private Button leaveButton;
    
    [SerializeField]private Button startButton;
    [SerializeField]private TextMeshProUGUI startButtonText;
    [SerializeField]private AudioClip mousePressSound;
    [SerializeField]private AudioClip mouseUpSound;
    
    [NonSerialized]public bool IsLoading = false;
    
    //서버 시작
    public void StartHost()
    {
        var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
        // 모든 NIC 바인딩
        utp.ConnectionData.ServerListenAddress = "0.0.0.0";
        utp.ConnectionData.Port = 7777;
        Debug.Log($"[Host] Listening on {utp.ConnectionData.ListenEndPoint.Address}:{utp.ConnectionData.ListenEndPoint.Port}");
        NetworkManager.Singleton.StartHost();
    }
    
    
    
    //클라이언트로 접속
    public void StartClient()
    {
        var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
        utp.ConnectionData.Address = hostIp.Trim();
        utp.ConnectionData.Port    = 7777;
        Debug.Log($"[Client] Connecting to {utp.ConnectionData.Address}:{utp.ConnectionData.Port}");
        NetworkManager.Singleton.StartClient();
    }
    
    
    //로비 UI 오픈
    public void OpenLobbyUI()
    {
        lobbyUI.SetActive(true);
        sessionUI.SetActive(false);
        gameUI.gameObject.SetActive(false);
    }
    
    //인게임 UI 오픈
    public void OpenGameUI()
    {
        lobbyUI.SetActive(false);
        sessionUI.SetActive(false);
        gameUI.gameObject.SetActive(true);
    }

    //세션창 UI 오픈
    public void OpenSessionUI()
    {
        lobbyUI.SetActive(false);
        sessionUI.SetActive(true);
        gameUI.gameObject.SetActive(false);
    }

    //세션창에서 게임 시작 버튼 활성화 여부
    public void SetButtonInteractable(bool value)
    {
        startButton.interactable = value;
    }

    //맵제네레이터에서 사용될 맵 시드 저장
    public void SetSeed()
    {
        MapGenerator.Instance.SetSeed(seedInput.text);
        // _seed = string.IsNullOrEmpty(seedInput.text) ? string.Empty : seedInput.text;
    }

    //게임시작 버튼의 텍스트 설정
    public void SetPlayerCountText(int playerCount)
    {
        startButtonText.text = $"PlayerCount:\n{playerCount}";
    }
    
    //게임시작 버튼 클릭
    public void OnStartGameClicked()
    {
        // Debug.Log("버튼클릭");
        
        RpcManager.Instance.StartGameClientRpc();
        RpcManager.Instance.ToggleLoadingScreenRpc(true);
    }
    public void ToggleLoadingScreen(bool open)
    {
        IsLoading = open;
        loadingScreen.SetActive(open);
    }

    public void PlayUIButtonPressSound()
    {
        SoundManager.Instance.PlaySound(mousePressSound, SoundManager.SoundGroup.Ui, 1.0f);
    }

    public void PlayUIButtonUpSound()
    {
        SoundManager.Instance.PlaySound(mouseUpSound, SoundManager.SoundGroup.Ui, 0.5f);
    }

    public void OpenGameOverMenu()
    {
        gameOverMenu.SetActive(true);
        bool isHost = NetworkManager.Singleton.IsHost;
        restartButton.interactable = isHost;
        // leaveButton.interactable = isHost;
    }

    public void CloseGameOverMenu()
    {
        gameOverMenu.SetActive(false);
    }

    //호스트가 부름
    public void OnRestartButtonClicked()
    {
        if (!NetworkManager.Singleton.IsHost) return;
        MapGenerator.Instance.GameOverObjectDespawn();
        // CloseGameOverMenu();
        RpcManager.Instance.StartGameClientRpc();
        RpcManager.Instance.ToggleLoadingScreenRpc(true);
    }

    public void OnLeaveButtonClicked()
    {
        NetworkManager.Singleton.Shutdown();
        
        if (!NetworkManager.Singleton.IsHost) return;
        MapGenerator.Instance.GameOverObjectDespawn();
    }
    
}
