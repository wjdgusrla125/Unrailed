using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

public class WaterFall : MonoBehaviour
{
    public int direction;
    [SerializeField] private Transform sideWaveAnchor;
    [SerializeField] private Transform centerWaveAnchor;
    [SerializeField] private GameObject waterEffect;
    private WaterWave[] _sideWaves;
    private WaterWave[] _centerWaves;
    
    private void Awake()
    {
        if(direction == 0) Debug.LogError("waterFall에 direction을 설정하지 않음");

        int childCount = sideWaveAnchor.childCount;
        _sideWaves = new WaterWave[childCount];
        for (int i = 0; i < childCount; i++)
        {
            _sideWaves[i] = sideWaveAnchor.GetChild(i).GetComponent<WaterWave>();
        }
        
        childCount = centerWaveAnchor.childCount;
        _centerWaves = new WaterWave[childCount];
        for (int i = 0; i < childCount; i++)
        {
            _centerWaves[i] = centerWaveAnchor.GetChild(i).GetComponent<WaterWave>();
        }
        
    }

    public void SetWaterFall(int dir)
    {
        foreach (WaterWave waterWave in _sideWaves)
        {
            waterWave.waterFall = this;
            waterWave.WaveObjectInit(dir);
        }
        foreach (WaterWave waterWave in _centerWaves)
        {
            waterWave.waterFall = this;
            waterWave.WaveObjectInit(dir);
        }
    }

    private void OnEnable()
    {
        StartWaterFall();
    }

    private void OnDisable()
    {
        StopWaterFall();
    }

    public void CreateWaterEffect(Vector3 position, Transform parent)
    {
        Quaternion rot = Quaternion.identity;
        switch (direction)
        {
            case 1:
                rot = Quaternion.Euler(0, 0, 90);
                break;
            case 3:
            case 7:
                rot = Quaternion.Euler(0, -90, 90);
                break;
        }

        GameObject waterEffectGo = null;
        
        switch (direction)
        {
            case 1:
            case 3:
                waterEffectGo = Instantiate(waterEffect, Vector3.zero, rot, parent);
                Vector3 localPos = waterEffectGo.transform.localPosition;
                Vector3 pos = new Vector3(localPos.y + 50, -0.01f, 10);
                waterEffectGo.transform.localPosition = pos;
                waterEffectGo.transform.SetParent(transform);
                break;
            case 7:
                Vector3 offsetPosition = position + Vector3.up * 20 + Vector3.left * 0.65f;
                waterEffectGo = Instantiate(waterEffect, offsetPosition, rot);
        
                waterEffectGo.transform.SetParent(transform, true);
                break;
        }

        if (!waterEffectGo) return;
        
        waterEffectGo.transform.localScale = new Vector3(0.05f, 0.01f, 0.01f);
        StartCoroutine(WaterEffectFallCoroutine(waterEffectGo));
    }

    private IEnumerator WaterEffectFallCoroutine(GameObject waterEffectGo)
    {
        float duration = 4.0f;
        float scaleDuration = 1.0f;
        float elapsedTime = 0f;
        float dropSpeed = 5.0f;

        Vector3 initialScale = waterEffectGo.transform.localScale;
        Vector3 targetScale = new Vector3(initialScale.x * 2f, initialScale.y, initialScale.z);
        
        while (elapsedTime < duration)
        {
            waterEffectGo.transform.position += Vector3.down * (dropSpeed * Time.deltaTime);
        
            if (elapsedTime < scaleDuration)
            {
                float scaleT = Mathf.Clamp01(elapsedTime / scaleDuration);
                float newScaleX = Mathf.Lerp(initialScale.x, targetScale.x, scaleT);
                Vector3 currentScale = waterEffectGo.transform.localScale;
                currentScale.x = newScaleX;
                waterEffectGo.transform.localScale = currentScale;
            }
        
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        Destroy(waterEffectGo);
    }

    private void StartWaterFall()
    {
        foreach (WaterWave waterWave in _sideWaves)
        {
            waterWave.StartWave();
        }
        foreach (WaterWave waterWave in _centerWaves)
        {
            waterWave.StartWave();
        }
    }

    private void StopWaterFall()
    {
        foreach (WaterWave waterWave in _sideWaves)
        {
            waterWave.StopWave();
        }
        foreach (WaterWave waterWave in _centerWaves)
        {
            waterWave.StopWave();
        }
    }
}
