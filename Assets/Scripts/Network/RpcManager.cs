using Sound;
using Unity.Netcode;
using UnityEngine;

public class RpcManager: NetworkSingletonManager<RpcManager>
{
    private bool _nextMapGenerated = false; //맵제네레이션을 여러번 실행하지 않게 하기 위한 플래그
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // Debug.Log("Rpc 매니저 스폰됨");
    }

    #region 인게임 RPC
    
    //열차를 파괴
    [Rpc(SendTo.Everyone)]
    public void DestroyTrainRpc(ulong trainId, bool isTail)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(trainId,
                out NetworkObject train))
        {
            train.GetComponent<Train>().DestroyTrain(isTail);
        }
    }

    [Rpc(SendTo.Everyone)]
    public void JoinShopRpc()
    {
        _nextMapGenerated = false;

        // 🔽 안전하게 Null 체크 후 직접 호출
        if (ExpandingCircleDetector.Instance != null)
        {
            ExpandingCircleDetector.Instance.JoinShop();
        }
        else if (GameManager.Instance != null && GameManager.Instance.shop != null)
        {
            GameManager.Instance.shop.JoinShop();
        }
        else
        {
            Debug.LogWarning("JoinShopRpc: 상점 UI 컴포넌트를 찾을 수 없습니다.");
        }

        SoundManager.Instance.PlayBGM(GameManager.Instance.shopBGM, 0.5f);
    }
    
    [Rpc(SendTo.Everyone)]
    public void ExitShopRpc()
    {
        if (_nextMapGenerated) return;
        _nextMapGenerated = true;
        SoundManager.Instance.PlayBGM(SoundManager.Instance.bgmClips[0], 0.5f);
        
        GameManager.Instance.shop.ExitShop();
        if (NetworkManager.Singleton.IsHost)
        {
            MapGenerator.Instance.NextMapGeneration();
        }

        GameManager.Instance.trainManager.RestartTrainCount();
    }
    
    [Rpc(SendTo.Everyone)]
    public void ToggleGameOverObjectRpc(ulong id, bool active)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject obj))
        {
            obj.gameObject.SetActive(active);
        }
    }

    [Rpc(SendTo.Everyone)]
    public void GameOverRpc()
    {
        GameManager.Instance.GameOver();
    }
    
    
    [Rpc(SendTo.NotMe)] //호스트가 아닌 플레이어에게도 PosB를 설정
    public void BroadcastPosBRpc(int x, int y)
    {
        MapGenerator.Instance.SetPosB(x, y);
    }

    #endregion

    #region UI관련

    [Rpc(SendTo.Everyone)]
    public void ChangeGameStateRpc(int value)
    {
        GameManager.Instance.CurrentGameState = (GameState)value;
    }
    
    //게임 시작 시 호출. UI를 인게임 UI로 변경하고, 맵을 생성한다.
    [Rpc(SendTo.Everyone)]
    public void StartGameClientRpc()
    {
        UIManager.Instance.OpenGameUI();
        UIManager.Instance.CloseGameOverMenu();
        GameManager.Instance.CurrentGameState = GameState.InGame;

        SoundManager.Instance.FadeOutBGM();

        //초기 수치 입력
        UIManager.Instance.gameUI.SetReaderBoardText(
            MapGenerator.Instance.GetSeed(),
            "0",
            "0",
            "0.1"
        );

        MapGenerator.Instance.StartMapGeneration();
        
        if (NetworkManager.Singleton.IsHost)
        {
            // Resources에서 PlayerPrefab 로드
            GameObject playerPrefab = Resources.Load<GameObject>("Prefabs/Player");
            if (playerPrefab == null)
            {
                Debug.LogError("Resources/Prefabs/Player 프리팹을 찾을 수 없습니다.");
                return;
            }

            Vector3 spawnPos = new Vector3(0f, 0.5f, 7f);

            foreach (var clientPair in NetworkManager.Singleton.ConnectedClients)
            {
                ulong clientId = clientPair.Key;

                NetworkObject oldPlayer = clientPair.Value.PlayerObject;
                if (oldPlayer != null && oldPlayer.IsSpawned)
                {
                    oldPlayer.Despawn(true);
                }

                GameObject newPlayer = Object.Instantiate(playerPrefab, spawnPos, Quaternion.identity);

                NetworkObject netObj = newPlayer.GetComponent<NetworkObject>();
                netObj.SpawnAsPlayerObject(clientId, true);
            }
        }
    }

    //로딩스크린을 토글한다.
    [Rpc(SendTo.Everyone)]
    public void ToggleLoadingScreenRpc(bool open)
    {
        if (UIManager.Instance.IsLoading == open) return;
        UIManager.Instance.ToggleLoadingScreen(open);
    }
    
    private Vector3 GetSpawnPosition(ulong clientId)
    {
        return new Vector3(clientId * 2f, 0f, 0f);
    }

    #endregion
    
    #region 맵 생성 RPC

    //접속한 플레이어들의 seed를 설정한다.
    [Rpc(SendTo.NotMe)]
    public void SetSeedRpc(string seed)
    {
        MapGenerator.Instance.SetSeed(seed);
    }

    //폭포를 활성화
    [Rpc(SendTo.Everyone)]
    public void ToggleWaterFallRpc(ulong waterBlockId, int waterFallNumber)
    {
        
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(waterBlockId,
                out NetworkObject waterFallObject))
        {
            waterFallObject.GetComponent<Water>().ActivateWaterFall(waterFallNumber);
        }
    }

    //오브젝트의 Parent를 설정한다.
    [Rpc(SendTo.Server)] //NetworkObject의 Parent 설정은 Host만이 가능
    public void SetParentRpc(ulong parentId, ulong childId)
    {
        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.LogError("호스트가 아닌 클라이언트에서 SetParentRpc가 불렸음");
            return;
        }
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(parentId, out NetworkObject parentObject))
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(childId, out NetworkObject childObject))
            {
                // Debug.Log($"child: {childObject}, parent: {parentObject}");
                childObject.transform.SetParent(parentObject.transform);
                
            }
            else
            {
                // Debug.LogWarning("해당 NetworkObjectId에 해당하는 Child 객체를 찾을 수 없음: " + childId);
            }
        }
        else
        {
            // Debug.LogWarning("해당 NetworkObjectId에 해당하는 Parent 객체를 찾을 수 없음: " + parentId);
        }
    }

    [Rpc(SendTo.Everyone)]
    public void SetBlockEnvScaleRpc(ulong objectId, float scale)
    {
        
    }

    [Rpc(SendTo.Everyone)]
    public void SetBlockEnvRotationRpc(ulong objectId, float rotation)
    {
        
    }

    #endregion
}
