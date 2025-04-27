using Unity.Netcode;
using Unity.Behavior;
using UnityEngine;

public class EnemyFSM : NetworkBehaviour
{
    private GameObject player;
    private BehaviorGraphAgent behaviorAgent;

    private float searchInterval = 1f; // 1초마다 갱신
    private float searchTimer = 0f;

    private void Start()
    {
        Setup();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Setup();
    }

    private void Setup()
    {
        behaviorAgent = GetComponent<BehaviorGraphAgent>();

        UpdateClosestPlayer();
    }

    private void Update()
    {
        if (!IsServer) return; // 서버만

        searchTimer += Time.deltaTime;
        if (searchTimer >= searchInterval)
        {
            searchTimer = 0f;
            UpdateClosestPlayer();
        }
    }

    private void UpdateClosestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        if (players == null || players.Length == 0)
        {
            Debug.LogWarning("[EnemyFSM] 플레이어를 찾지 못했습니다.");
            return;
        }

        GameObject closest = null;
        float closestDistance = float.MaxValue;
        Vector3 myPosition = transform.position;

        foreach (var p in players)
        {
            if (p == null) continue;

            float distance = Vector3.Distance(myPosition, p.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = p;
            }
        }

        if (closest != null && closest != player)
        {
            player = closest;
            behaviorAgent.SetVariableValue("Player", player);
        }
    }
}