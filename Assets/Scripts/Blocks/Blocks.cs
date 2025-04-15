using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public enum TileType
{
    Grass,
    Wood,
    Mountain,
    Iron,
    River
}

public abstract class Blocks : NetworkBehaviour
{
    public ClusterGroup ClusterGroup;
    private System.Random rng; //랜덤 시드값
    public int rndSeedOffset;

    [SerializeField, Header("블럭 위에 소환될 오브젝트")]
    private GameObject[] envPrefab;
    [SerializeField, Header("오브젝트가 파괴될 때 주변에 튈 파편 오브젝트")]
    private GameObject fragPrefab;
    
    [SerializeField]
    private Vector3 envOffset = new Vector3(0, 0.5f, 0);
    
    [NonSerialized]
    private GameObject _selectedPrefab;
    
    private MeshRenderer _meshRenderer;
    private GameObject _env;

    public Transform desiredParent;

    // 각 파생 클래스가 자신의 타일 타입을 지정하도록 추상 프로퍼티 추가
    public abstract TileType BlockTileType { get; }

    private void Awake()
    {
        _meshRenderer = GetComponent<MeshRenderer>();
    }

    public void SetSeed()
    {
        int seedValue = MapGenerator.Instance.GetSeed().GetHashCode() + rndSeedOffset;
        rng = new System.Random(seedValue);
        
        CreateBlock();
        AdditionalCreateBlock();
        
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();


        if (NetworkManager.Singleton.IsHost)
        {
            if (desiredParent)
            {
                ulong parentId = desiredParent.GetComponent<NetworkObject>().NetworkObjectId;
                ulong childId = transform.GetComponent<NetworkObject>().NetworkObjectId;
                RpcManager.Instance.SetParentRpc(parentId, childId);
            }
            else
            {
                Debug.LogError("desiredParent가 null임!!");
            }
        }
        
    }
    
    // envPrefab를 생성하고 부모 설정 후, tileType에 맞는 회전 및 스케일을 적용
    private void CreateBlock()
    {
        if (envPrefab == null || envPrefab.Length == 0)
            return;
        
        int prefabIndex = rng.Next(0, envPrefab.Length);
        _env = Instantiate(envPrefab[prefabIndex], transform.position + envOffset, Quaternion.identity);
        _env.transform.SetParent(transform);

        ApplyEnvSettings();
    }
    
    // tileType별 회전 및 스케일 설정 로직
    protected virtual void ApplyEnvSettings()
    {
        float scale = 1;
        float rotation = 0;
        switch (BlockTileType)
        {
            case TileType.Grass:
                break;
            case TileType.Wood:
                scale = (float)(rng.NextDouble() * 0.25 + 0.75); // 0.75 ~ 1.0
                rotation = (float)(rng.NextDouble() * 360);
                break;
            case TileType.Mountain:
                if (!DetermineUniformMountain())
                {
                    scale = (float)(rng.NextDouble() * 0.4 + 0.4); // 0.4 ~ 0.8
                    rotation = rng.Next(0, 4) * 90f;
                }
                else
                {
                    scale = (float)(rng.NextDouble() * 1.0 + 0.5); // 0.5 ~ 1.5
                    rotation = rng.Next(0, 4) * 90f;
                }
                break;
            case TileType.Iron:
                if (!DetermineUniformIron())
                {
                    scale = (float)(rng.NextDouble() * 0.4 + 0.4);
                    rotation = rng.Next(0, 4) * 90f;
                }
                else
                {
                    scale = (float)(rng.NextDouble() * 0.6 + 0.4);
                    rotation = rng.Next(0, 4) * 90f;
                }
                break;
            case TileType.River:
                break;
            default:
                break;
        }

        SetEnvScale(scale);
        SetEnvRotation(rotation);
    }

    // 기본적으로 false를 반환하는 메서드, 필요에 따라 파생 클래스에서 오버라이드 가능
    protected virtual bool DetermineUniformMountain() => false;
    protected virtual bool DetermineUniformIron() => false;

    public void SetEnvScale(float scale)
    {
        Debug.Log($"스케일 변경: {scale}");
        if (_env)
            _env.transform.localScale = new Vector3(1, scale, 1);
    }

    public void SetEnvRotation(float yAngle)
    {
        Debug.Log($"로테이션 변경{yAngle}");
        if (_env)
        {
            Vector3 currentEuler = _env.transform.rotation.eulerAngles;
            _env.transform.rotation = Quaternion.Euler(currentEuler.x, yAngle, currentEuler.z);
        }
    }

    // env 오브젝트 애니메이션 관련 로직
    public IEnumerator AnimateEnvDrop(float duration, float dropOffset)
    {
        if (!_env)
            yield break;

        Vector3 targetLocalPos = _env.transform.localPosition;
        Vector3 startLocalPos = new Vector3(targetLocalPos.x, dropOffset, targetLocalPos.z);
        _env.transform.localPosition = startLocalPos;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
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

    public void SetRendererActive(bool active)
    {
        if (_meshRenderer)
            _meshRenderer.enabled = active;

        Renderer[] childRenderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in childRenderers)
        {
            if (!ReferenceEquals(r.gameObject, gameObject))
                r.enabled = active;
        }
    }

}
