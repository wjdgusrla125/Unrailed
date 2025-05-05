using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Cluster : NetworkBehaviour
{
    public ClusterGroup ClusterGroup;
    public bool isSpecial = false;
    private float _spawnOffset;

    public void SetOffset(float spawnOffset)
    {
        // Debug.Log("spawnOffset" + spawnOffset);
        _spawnOffset = spawnOffset;
    }

    public void PlaySpawnAnimation(int delay)
    {
        StartCoroutine(SpawnCoroutine(delay));
    }

    //타일 스폰 애니메이션
    private IEnumerator SpawnCoroutine(int delay)
    {
        float fadeDuration = isSpecial ? 0f : 3.0f;
        float moveDuration = isSpecial ? 0f : 2.5f;

        Vector3 finalPos;
        if (ClusterGroup is { Direction: ClusterDirection.Upper })
            finalPos = transform.position + Vector3.down * _spawnOffset;
        else
            finalPos = transform.position + Vector3.up * _spawnOffset;

        // 렌더러를 끄고 대기
        foreach (Transform child in transform)
        {
            Blocks block = child.GetComponent<Blocks>();
            if (block)
                block.SetRendererActive(false);
        }

        yield return new WaitForSeconds(delay * 0.02f);

        if (isSpecial)
        {
            RpcManager.Instance.ToggleLoadingScreenRpc(false);
        }

        // 레일 생성
        if (MapGenerator.Instance.IsInitialGeneration && ClusterGroup.Tiles.Contains(MapGenerator.Instance.GetPosA()))
        {
            //첫생성이면
            MapGenerator.Instance.SpawnRails();
        }
        else if (!MapGenerator.Instance.IsInitialGeneration &&
                 ClusterGroup.Tiles.Contains(MapGenerator.Instance.GetPosB()))
        {
            //재생성이면
            MapGenerator.Instance.SpawnRails(MapGenerator.Instance.GetOldWidth());
        }


        // 렌더러 다시 활성화하고 env 드랍 애니메이션 실행
        foreach (Transform child in transform)
        {
            Blocks block = child.GetComponent<Blocks>();
            if (block)
                block.SetRendererActive(true);
            if (block is StartPoint or EndPoint)
            {
                //시작점 또는 도착점이면 스테이션의 스폰애니메이션을 재생
                block.StartCoroutine(block.AnimateStationDrop(fadeDuration, _spawnOffset * 2));
            }
            
            if (IsServer)
            {
                //초기 아이템의 스폰 애니메이션 재생
                Tile tile = block.GetComponent<Tile>();
                if (tile) tile.SpawnInitialItems();
            }
        }

        float directionSign = ClusterGroup.Direction == ClusterDirection.Upper ? 1f : -1f;
        this.PlaySpawn(directionSign * _spawnOffset, finalPos.y, moveDuration);
    }


    private float EaseOutQuart(float t)
    {
        return 1f - Mathf.Pow(1f - t, 4f);
    }

    public void DespawnCluster()
    {
        StartCoroutine(DespawnCoroutine());
        // // 클러스터 내부의 모든 자식 Block들을 순회
        // foreach (Transform child in transform)
        // {
        //     Blocks block = child.GetComponent<Blocks>();
        //     if (block)
        //     {
        //         // Block과 그 자식 env Despawn 처리
        //         block.DespawnBlockAndEnv();
        //     }
        // }
        //
        // NetworkObject.Despawn();
    }

    private IEnumerator DespawnCoroutine()
    {
        //디스폰도 순차적으로 딜레이를 준다.
        int siblingCount = transform.parent.childCount;
        int myIndex = transform.GetSiblingIndex();
        int reversedIdx = (siblingCount - 1) - myIndex;
        yield return new WaitForSeconds(reversedIdx * 0.02f);

        Vector3 startPos = transform.position;
        Vector3 endPos = (ClusterGroup.Direction == ClusterDirection.Upper)
            ? startPos + Vector3.up * _spawnOffset
            : startPos + Vector3.down * _spawnOffset;

        float duration = isSpecial ? 0f : 2.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = 1f - EaseOutQuart(1f - t);
            transform.position = Vector3.Lerp(startPos, endPos, easedT);
            yield return null;
        }

        transform.position = endPos;

        foreach (Transform child in transform)
        {
            if (child.TryGetComponent<Blocks>(out var block))
                block.DespawnBlockAndEnv();
        }

        // 5) 네트워크 Despawn
        NetworkObject.Despawn();
    }
}