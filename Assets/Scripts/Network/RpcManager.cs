using Sound;
using Unity.Netcode;
using UnityEngine;

public class RpcManager: NetworkSingletonManager<RpcManager>
{
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // Debug.Log("Rpc 매니저 스폰됨");
    }

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
        GameManager.Instance.CurrentGameState = GameState.InGame;

        SoundManager.Instance.FadeOutBGM();

        //초기 수치 입력
        UIManager.Instance.SetReaderBoardTextWrapper(
            MapGenerator.Instance.GetSeed(),
            "0",
            "0",
            "0.1"
        );

        if (NetworkManager.Singleton.IsHost)
        {
            MapGenerator.Instance.StartMapGeneration();

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
        if (UIManager.Instance.IsLoading == open || !NetworkManager.Singleton.IsHost) return;
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
