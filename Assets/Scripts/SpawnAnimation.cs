using System;
using System.Collections;
using UnityEngine;

public static class SpawnAnimation
{
    /// <summary>
    /// MonoBehaviour에 애니메이션 코루틴을 붙여 실행합니다.
    /// </summary>
    /// <param name="mb">this: 호출하는 MonoBehaviour</param>
    /// <param name="offset">스폰 위치 오프셋</param>
    /// <param name="destOffset">도착 위치 오프셋</param>
    /// <param name="duration">이동 지속시간 (기본 2.5초)</param>
    /// <param name="delay">시작 전 딜레이 (기본 0초)</param>
    /// <param name="onComplete">도착 액션</param>
    public static void PlaySpawn(this MonoBehaviour mb, 
        float offset, 
        float destOffset = 0f,
        float duration = 2.5f, 
        float delay = 0f,
        Action onComplete = null)
    {
        mb.StartCoroutine(SpawnCoroutine(mb.transform, -offset, destOffset, duration, delay, onComplete));
    }

    private static IEnumerator SpawnCoroutine(Transform tr, 
        float offset, 
        float destOffset,
        float duration, 
        float delay,
        Action onComplete)
    {
        if (delay > 0f) 
            yield return new WaitForSeconds(delay);

        Vector3 raw = tr.position;
        Vector3 finalPos = new Vector3(raw.x, destOffset, raw.z);
        Vector3 startPos = finalPos + Vector3.down * offset;
        tr.position = startPos;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 4f);
            tr.position = Vector3.Lerp(startPos, finalPos, eased);
            yield return null;
        }

        tr.position = finalPos;
        onComplete?.Invoke();
    }

    /// <summary>
    /// destOffset + spawnOffset 위에서 시작해서
    /// destOffset 위치로 내려오는 애니메이션
    /// </summary>
    public static void PlaySpawnToGround(this MonoBehaviour mb,
        float spawnOffset,
        float destOffset = 0f,
        float duration = 2.5f,
        float delay = 0f,
        Action onComplete = null)
    {
        mb.StartCoroutine(SpawnToGroundCoroutine(mb.transform,
            spawnOffset,
            destOffset,
            duration,
            delay,
            onComplete));
    }

    private static IEnumerator SpawnToGroundCoroutine(Transform tr,
        float spawnOffset,
        float destOffset,
        float duration,
        float delay,
        Action onComplete)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Vector3 raw = tr.position;
        Vector3 finalPos = new Vector3(raw.x, destOffset, raw.z);

        tr.position = finalPos + Vector3.up * spawnOffset;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 4f);
            tr.position = Vector3.Lerp(finalPos + Vector3.up * spawnOffset, finalPos, eased);
            yield return null;
        }

        tr.position = finalPos;
        onComplete?.Invoke();
    }
}