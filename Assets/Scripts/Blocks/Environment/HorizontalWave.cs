﻿using System.Collections;
using UnityEngine;

public class HorizontalWave : WaterWave
{
    protected override IEnumerator WaveCoroutine()
    {
        while (true)
        {
            float duration = Random.Range(0.5f, 2.5f);

            float targetZ = Random.Range(0.2f, 1f);

            Vector3 initialScale = transform.localScale;
            Vector3 targetScale = new Vector3(initialScale.x, initialScale.y, targetZ);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                Vector3 newScale = Vector3.Lerp(initialScale, targetScale, t);
                transform.localScale = newScale;

                yield return null;
            }
            transform.localScale = targetScale;
        }
    }
}