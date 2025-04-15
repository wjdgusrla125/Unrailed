

using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class UIManager: NetworkSingletonManager<UIManager>
{
    // private string _seed;
    
    [SerializeField]private TMP_InputField seedInput;
    [SerializeField]private GameObject lobbyUI;
    [SerializeField]private GameObject sessionUI;
    [SerializeField]private GameObject gameUI;
    
    [SerializeField]private Button startButton;
    [SerializeField]private TextMeshProUGUI startButtonText;

    //서버 시작
    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
    }
    
    //클라이언트로 접속
    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
    }
    
    
    //로비 UI 오픈
    public void OpenLobbyUI()
    {
        lobbyUI.SetActive(true);
        sessionUI.SetActive(false);
        gameUI.SetActive(false);
    }
    
    //인게임 UI 오픈
    public void OpenGameUI()
    {
        lobbyUI.SetActive(false);
        sessionUI.SetActive(false);
        gameUI.SetActive(true);
    }

    //세션창 UI 오픈
    public void OpenSessionUI()
    {
        lobbyUI.SetActive(false);
        sessionUI.SetActive(true);
        gameUI.SetActive(false);
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
    }
    
    
}
