using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class CustomNetworkManager : MonoBehaviour
{
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    private int nextSpawnPointIndex = 0;

    private void Awake()
    {
        // 스폰 포인트가 비어있으면 씬에서 찾기
        if (spawnPoints.Count == 0)
        {
            GameObject[] spawnPointObjects = GameObject.FindGameObjectsWithTag("SpawnPoint");
            foreach (GameObject spawnPoint in spawnPointObjects)
            {
                spawnPoints.Add(spawnPoint.transform);
            }
        }
    }

    private void Start()
    {
        // NetworkManager가 씬에 존재하는지 확인
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager가 씬에 존재하지 않습니다!");
            return;
        }

        // ConnectionApprovalCallback 설정
        NetworkManager.Singleton.ConnectionApprovalCallback = ApproveConnection;
    }

    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // 연결 승인 (필요에 따라 조건 추가 가능)
        response.Approved = true;
        response.CreatePlayerObject = true;
        
        // 플레이어 위치 설정
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        
        if (spawnPoints.Count > 0)
        {
            // 스폰 포인트가 있는 경우 라운드 로빈으로 할당
            Transform selectedSpawnPoint = spawnPoints[nextSpawnPointIndex];
            spawnPosition = selectedSpawnPoint.position;
            spawnRotation = selectedSpawnPoint.rotation;
            
            // 다음 스폰 포인트 인덱스 업데이트
            nextSpawnPointIndex = (nextSpawnPointIndex + 1) % spawnPoints.Count;
        }
        else
        {
            // 스폰 포인트가 없는 경우 랜덤 위치 생성
            spawnPosition = new Vector3(
                Random.Range(-10f, 10f),
                1f, // 바닥보다 약간 위
                Random.Range(-10f, 10f)
            );
            spawnRotation = Quaternion.identity;
        }
        
        // 응답에 위치와 회전 설정
        response.Position = spawnPosition;
        response.Rotation = spawnRotation;
        
        // 선택적: 플레이어 연결 정보 로깅
        Debug.Log($"클라이언트 ID {request.ClientNetworkId}가 위치 {spawnPosition}에 스폰됩니다.");
    }
}