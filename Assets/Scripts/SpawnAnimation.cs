using System;
using System.Collections;
using UnityEngine;

public static class SpawnAnimation
{
    //이징
    private static float EaseOutQuart(float t)
        => 1f - Mathf.Pow(1f - t, 4f);

    private static IEnumerator Animate(Transform tr,
        Vector3 startPos,
        Vector3 endPos,
        float duration,
        Action onComplete)
    {
        tr.position = startPos;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutQuart(t);
            tr.position = Vector3.Lerp(startPos, endPos, eased);
            yield return null;
        }

        tr.position = endPos;
        onComplete?.Invoke();
    }

    /// <summary>
    /// offset 만큼 위/아래(spawn)에서 destOffset 위치로 낙하한다.
    /// </summary>
    public static void PlaySpawn(this MonoBehaviour mb,
        float offset,
        float destOffset = 0f,
        float duration = 2.5f,
        Action onComplete = null)
    {
        Transform tr = mb.transform;
        Vector3 raw = tr.position;
        Vector3 finalPos = new Vector3(raw.x, destOffset, raw.z);
        Vector3 startPos = finalPos + Vector3.down * (-offset);

        mb.StartCoroutine(Animate(tr, startPos, finalPos, duration, onComplete));
    }

    /// <summary>
    /// destOffset 위치 위로 spawnOffset 만큼 올렸다가 destOffset으로 낙하한다.
    /// </summary>
    public static void PlaySpawnToGround(this MonoBehaviour mb,
        float spawnOffset,
        float destOffset = 0f,
        float duration = 2.5f,
        Action onComplete = null)
    {
        Transform tr = mb.transform;
        Vector3 raw = tr.position;
        Vector3 finalPos = new Vector3(raw.x, destOffset, raw.z);
        Vector3 startPos = finalPos + Vector3.up * spawnOffset;

        mb.StartCoroutine(Animate(tr, startPos, finalPos, duration, onComplete));
    }

    /// <summary>
    /// spawnY 지점에서 destOffset + spawnY 위치로 낙하한다.
    /// </summary>
    public static void PlaySpawnFromHeight(this MonoBehaviour mb,
        float spawnY,
        float destOffset = 0f,
        float duration = 2.5f,
        Action onComplete = null)
    {
        Transform tr = mb.transform;
        Vector3 startPos = new Vector3(tr.position.x, spawnY, tr.position.z);
        Vector3 endPos = new Vector3(tr.position.x, destOffset + spawnY, tr.position.z);

        mb.StartCoroutine(Animate(tr, startPos, endPos, duration, onComplete));
    }
}