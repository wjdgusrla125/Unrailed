using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public abstract class Blocks: MonoBehaviour
{
    public ClusterGroup ClusterGroup;
    
    [SerializeField, Header("블럭 위에 소환될 오브젝트")] private GameObject[] envPrefab;
    [SerializeField, Header("오브젝트가 파괴될 때 주변에 튈 파편 오브젝트")] private GameObject fragPrefab;
    [SerializeField] private Vector3 envOffset = new(0, 0.5f, 0);
    [NonSerialized] private GameObject _selectedPrefab;
    private MeshRenderer _meshRenderer;
    private GameObject _env;

    private void Awake()
    {
        _meshRenderer = GetComponent<MeshRenderer>();
        CreateBlock();
        AdditionalCreateBlock();
    }

    public void SetRendererActive(bool active)
    {
        if(_meshRenderer) _meshRenderer.enabled = active;
        Renderer[] childRenderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer1 in childRenderers)
        {
            if (!Equals(renderer1.gameObject, gameObject))
            {
                renderer1.enabled = active;
            }
        }
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
        _env = Instantiate(_selectedPrefab, transform.position + envOffset, Quaternion.identity);
        _env.transform.SetParent(transform);

    }

    public void SetEnvScale(float scale)
    {
        if (!_env) return;
        
        _env.transform.localScale = new Vector3(1, scale, 1);
    }
    
    public void SetEnvRotation(float yAngle)
    {
        if (!_env) return;
        
        Vector3 currentEuler = _env.transform.rotation.eulerAngles;
        _env.transform.rotation = Quaternion.Euler(currentEuler.x, yAngle, currentEuler.z);
    }
    

    //기차역 스폰 타일일 시 기차역은 따로 위에서 떨어지도록 함.
    public IEnumerator AnimateEnvDrop(float duration, float dropOffset)
    {
        if (!_env) yield break;
            
        Vector3 targetLocalPos = _env.transform.localPosition;
        Vector3 startLocalPos = new Vector3(targetLocalPos.x, dropOffset, targetLocalPos.z);
        _env.transform.localPosition = startLocalPos;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // 마찬가지로 easeOutCubic 대신 easeOutQuart를 사용해볼 수 있습니다.
            float easedT = EaseOutQuart(t);
            _env.transform.localPosition = Vector3.Lerp(startLocalPos, targetLocalPos, easedT);
            yield return null;
        }
        _env.transform.localPosition = targetLocalPos;
    }

    private float EaseOutQuart(float t)
    {
        return 1f - Mathf.Pow(1f - t, 4f);
    }
    
    protected abstract void AdditionalCreateBlock();
}