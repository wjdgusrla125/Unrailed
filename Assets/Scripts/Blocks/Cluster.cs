using System.Collections;
using UnityEngine;

public class Cluster : MonoBehaviour
{
    public ClusterGroup ClusterGroup;

    public void Spawn(int delay)
    {
        StartCoroutine(SpawnCoroutine(delay));
    }

    private IEnumerator SpawnCoroutine(int delay)
    {
        Vector3 originalPos = transform.position;
        float spawnOffset = 30.0f; //이동거리
        
        if (ClusterGroup is { Direction: ClusterDirection.Upper })
        {
            transform.position = originalPos + Vector3.up * spawnOffset;
        }
        else 
        {
            transform.position = originalPos + Vector3.down * spawnOffset;
        }

        yield return new WaitForSeconds(delay * 0.03f);

        float moveDuration = 1.0f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            transform.position = Vector3.Lerp(startPos, originalPos, t);
            yield return null;
        }
        transform.position = originalPos;
    }
}