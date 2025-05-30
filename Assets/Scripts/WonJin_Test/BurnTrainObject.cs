﻿using Unity.Netcode;
using UnityEngine;

public class BurnTrainObject : NetworkBehaviour
{
    public NetworkVariable<bool> Isburn = new NetworkVariable<bool>(false);

    public GameObject FireEffect;

    void Start()
    {
        Transform findTransform = FindDeepChild(transform, "Fire");
        if (findTransform != null)
        {
            FireEffect = findTransform.gameObject;
            Debug.Log("FireEffect 할당 완료");
        }
        else
        {
            Debug.LogError("Fire 이펙트를 찾을 수 없음! 자식 오브젝트 구조를 확인하세요.");
        }
    }

    void Update()
    {
        FireEffect.SetActive(Isburn.Value);
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;

            Transform result = FindDeepChild(child, name);
            if (result != null)
                return result;
        }
        return null;
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void SetIsBurnServerRpc(bool value)
    {
        Isburn.Value = value;
    }
}