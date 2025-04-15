using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Cluster : NetworkBehaviour
{
    public ClusterGroup ClusterGroup;

    private int _delay;
    public bool isSpecial = false;
    private float _spawnOffset;
    
    public void SetOffset(float spawnOffset)
    {
        Debug.Log("spawnOffset" + spawnOffset);
        _spawnOffset = spawnOffset;
    }

    public void PlaySpawnAnimation(int delay)
    {
        StartCoroutine(SpawnCoroutine(delay));
    }
    
    //타일 스폰 애니메이션
    private IEnumerator SpawnCoroutine(int delay)
    {
        Vector3 finalPos;
        if (ClusterGroup is { Direction: ClusterDirection.Upper })
            finalPos = transform.position + Vector3.down * _spawnOffset;
        else
            finalPos = transform.position + Vector3.up * _spawnOffset;
        

        // 렌더러 끄기 (애니메이션 효과를 위해)
        foreach (Transform child in transform)
        {
            Blocks block = child.GetComponent<Blocks>();
            if (block)
                block.SetRendererActive(false);
        }
        yield return new WaitForSeconds(delay * 0.03f);

        foreach (Transform child in transform)
        {
            Blocks block = child.GetComponent<Blocks>();
            if (block)
                block.SetRendererActive(true);
            if (block is StartPoint or EndPoint)
            {
                block.StartCoroutine(block.AnimateEnvDrop(3.0f, _spawnOffset * 2));
            }
        }
    
        // 오프셋된 위치에서 원래의 최종 위치(finalPos)로 이동
        float moveDuration = 3.0f; // 이동 시간
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            float easedT = EaseOutQuart(t);
            transform.position = Vector3.Lerp(startPos, finalPos, easedT);
            yield return null;
        }
        transform.position = finalPos;
    }



    
    private float EaseOutQuart(float t)
    {
        return 1f - Mathf.Pow(1f - t, 4f);
    }
}