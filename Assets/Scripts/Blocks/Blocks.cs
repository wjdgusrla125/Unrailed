using System;
using UnityEngine;
using Random = UnityEngine.Random;

public abstract class Blocks: MonoBehaviour
{
    [SerializeField, Header("블럭 위에 소환될 오브젝트")] private GameObject[] envPrefab;
    [SerializeField, Header("오브젝트가 파괴될 때 주변에 튈 파편 오브젝트")] private GameObject fragPrefab;

    [NonSerialized] private GameObject _selectedPrefab;

    private void Awake()
    {
        CreateBlock();
    }
    
    //블록 생성
    private void CreateBlock()
    {
        if (envPrefab == null || envPrefab.Length == 0)
        {
            // Debug.LogError($"{name} 블럭에 envPrefab이 할당되지 않았음.");
            return;
        }
        
        _selectedPrefab = envPrefab[Random.Range(0, envPrefab.Length)];

        AdditionalCreateBlock();
    }

    protected abstract void AdditionalCreateBlock();
}