

using Unity.Netcode;
using UnityEngine;

public class RpcManager: NetworkSingletonManager<RpcManager>
{
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log("Rpc 매니저 스폰됨");
    }

    #region UI관련
    
    //게임 시작 시 호출. UI를 인게임 UI로 변경하고, 맵을 생성한다.
    [Rpc(SendTo.Everyone)]
    public void StartGameClientRpc()
    {
        UIManager.Instance.OpenGameUI();

        if (NetworkManager.Singleton.IsHost)
        {
            MapGenerator.Instance.StartMapGeneration();
        }
    }

    #endregion
    
    #region 맵 생성 RPC

    //접속한 플레이어들의 seed를 설정한다.
    [Rpc(SendTo.NotMe)]
    public void SetSeedRpc(string seed)
    {
        MapGenerator.Instance.SetSeed(seed);
    }

    //오브젝트의 Parent를 설정한다.
    [Rpc(SendTo.Server)]
    public void SetParentRpc(ulong parentId, ulong childId)
    {
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
