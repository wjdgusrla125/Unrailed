using Unity.Netcode;
using UnityEngine;

public class EnemySpawner : NetworkBehaviour
{
    [SerializeField] private GameObject enemyPrefab;  // 생성할 적 프리팹
    [SerializeField] private Transform[] spawnPoints; // 적이 생성될 위치들
    [SerializeField] private int initialEnemyCount = 5; // 처음에 생성할 적의 수
    
    // 네트워크가 시작되고 이 객체가 서버에서 활성화될 때 자동으로 호출됩니다
    public override void OnNetworkSpawn()
    {
        // 서버에서만 적을 생성합니다
        if (IsServer)
        {
            Invoke("SpawnInitialEnemies", 10);
            //SpawnInitialEnemies();
        }
    }
    
    // 초기 적들을 생성하는 메서드
    private void SpawnInitialEnemies()
    {
        for (int i = 0; i < initialEnemyCount; i++)
        {
            SpawnEnemy();
        }
    }
    
    // 단일 적을 생성하는 메서드
    private void SpawnEnemy()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("스폰 포인트가 설정되지 않았습니다!");
            return;
        }
        
        if (enemyPrefab == null)
        {
            Debug.LogError("적 프리팹이 설정되지 않았습니다!");
            return;
        }
        
        // 랜덤한 스폰 포인트 선택
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        
        // 선택된 위치에 적 생성 (네트워크 생성)
        GameObject enemyInstance = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
        NetworkObject networkObject = enemyInstance.GetComponent<NetworkObject>();
        
        if (networkObject != null)
        {
            // 네트워크에 적 객체 스폰 (모든 클라이언트에게 동기화됨)
            networkObject.Spawn();
        }
        else
        {
            Debug.LogError("적 프리팹에 NetworkObject 컴포넌트가 없습니다!");
            Destroy(enemyInstance);
        }
    }
    
    // 서버 RPC를 통해 새 적을 생성하는 메서드 (클라이언트가 서버에게 적 생성을 요청할 수 있음)
    [ServerRpc(RequireOwnership = false)]
    public void SpawnEnemyServerRpc()
    {
        SpawnEnemy();
    }
}