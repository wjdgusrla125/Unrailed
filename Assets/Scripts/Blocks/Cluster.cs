using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class Cluster : MonoBehaviour
{
    public ClusterGroup ClusterGroup;
    public bool isSpecial = false;

    public void Spawn(int delay)
    {
        StartCoroutine(SpawnCoroutine(delay));
    }

    private IEnumerator SpawnCoroutine(int delay)
    {
        Vector3 originalPos = transform.position;
        
        float spawnOffset = 20.0f; // 이동 거리
        float moveDuration = 3.0f; //이동 시간
        
        // if (!isSpecial) //스페셜 그룹이면 스폰 애니메이션을 스킵하도록 하는 조건문
        {
            if (ClusterGroup is { Direction: ClusterDirection.Upper })
                transform.position = originalPos + Vector3.up * spawnOffset;
            else
                transform.position = originalPos + Vector3.down * spawnOffset;
            
            // 렌더러 끄기
            foreach (Transform child in transform)
            {
                Blocks block = child.GetComponent<Blocks>();
                if (block)
                    block.SetRendererActive(false);
            }
            yield return new WaitForSeconds(delay * 0.03f);
            // 렌더러 켜기
            foreach (Transform child in transform)
            {
                Blocks block = child.GetComponent<Blocks>();
                if (block)
                    block.SetRendererActive(true);
                
                if (block is StartPoint or EndPoint)
                {
                    block.StartCoroutine(block.AnimateEnvDrop(moveDuration, spawnOffset * 2));
                }
            }
            // float moveDuration = 0.1f;
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / moveDuration);
                // 아래 줄에서 easeOutCubic을 easeOutQuart로 교체 가능
                float easedT = EaseOutQuart(t); 
                transform.position = Vector3.Lerp(startPos, originalPos, easedT);
                yield return null;
            }
        }
        
        transform.position = originalPos;
    }
    
    private float EaseOutQuart(float t)
    {
        return 1f - Mathf.Pow(1f - t, 4f);
    }
}