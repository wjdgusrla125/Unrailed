﻿using System;
using System.Collections;
using System.Collections.Generic;
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
    private System.Random _rng; //랜덤 시드값
    public int rndSeedOffset;
    
    public bool isBoltTile = false; //볼트가 생성될 타일인지 확인
    [NonSerialized] public GameObject BoltPrefab; //생성될 Bolt Object, 귀찮으니까 MapGenerator에서 코드로 지정

    [SerializeField, Header("블럭 위에 소환될 오브젝트")]
    private GameObject[] envPrefab;
    [SerializeField, Header("오브젝트가 파괴될 때 주변에 튈 파편 오브젝트")]
    private GameObject fragPrefab;
    
    [SerializeField]
    private Vector3 envOffset = new Vector3(0, 0.5f, 0);
    private MeshRenderer _meshRenderer;
    private GameObject _env;

    public Transform desiredParent;

    public bool railed; //이 타일에 레일이 설치되었는지

    public abstract TileType BlockTileType { get; }

    private void Awake()
    {
        _meshRenderer = GetComponent<MeshRenderer>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (MapGenerator.TryGetInstance() == null) return;
        
        rndSeedOffset = (int)NetworkObjectId;
        
        SetSeed();
        
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
            if (!isBoltTile)
            {
                CreateEnv();
                SetEnv();
            }
            else CreateBolt();
        }
        
    }

    private void SetSeed()
    {
        int seedValue = MapGenerator.Instance.GetSeed().GetHashCode() + rndSeedOffset;
        _rng = new System.Random(seedValue);
    }
    
    // envPrefab를 생성하고 부모 설정 후, tileType에 맞는 회전 및 스케일을 적용
    private void CreateEnv()
    {
        if (envPrefab == null || envPrefab.Length == 0)
            return;
        
        // null 요소가 아닌 prefab만 걸러서 리스트 생성
        List<GameObject> validPrefabs = new List<GameObject>();
        foreach (var prefab in envPrefab)
        {
            if (prefab != null)
                validPrefabs.Add(prefab);
        }

        if (validPrefabs.Count == 0)
            return;

        int prefabIndex = _rng.Next(0, validPrefabs.Count);
        GameObject chosenPrefab = validPrefabs[prefabIndex];

        _env = Instantiate(chosenPrefab, transform.position + envOffset, Quaternion.identity);
        NetworkObject envObj = _env.GetComponent<NetworkObject>();
        if (envObj)
        {
            envObj.Spawn();
            BreakableObject bo = envObj.GetComponent<BreakableObject>();
            if (bo)
            {
                envObj.GetComponent<BreakableObject>().TileInfo = gameObject.GetComponent<Tile>();
            }
            ulong parentId = transform.GetComponent<NetworkObject>().NetworkObjectId;
            ulong childId = envObj.NetworkObjectId;
            StartCoroutine(SetParentCoroutine(parentId, childId));
        }
    }

    public void CreateBolt()
    {
        _env = Instantiate(BoltPrefab, transform.position + envOffset, Quaternion.identity);
        NetworkObject boltObj = _env.GetComponent<NetworkObject>();
        if (boltObj)
        {
            
            boltObj.Spawn();
            ulong parentId = NetworkObjectId;
            ulong childId = boltObj.NetworkObjectId;
            // Debug.Log($"볼트생성, parentId: {parentId}, childId: {childId}");
            StartCoroutine(SetParentCoroutine(parentId, childId));
        }
    }


    private IEnumerator SetParentCoroutine(ulong parentId, ulong childId)
    {
        yield return new WaitForSeconds(1.0f);
        RpcManager.Instance.SetParentRpc(parentId, childId);
    }
    
    // tileType별 회전 및 스케일 설정 로직
    protected virtual void SetEnv()
    {
        float scale = 1;
        float rotation = 0;
        switch (BlockTileType)
        {
            case TileType.Grass:
                break;
            case TileType.Wood:
                scale = (float)(_rng.NextDouble() * 0.25 + 0.75); // 0.75 ~ 1.0
                rotation = (float)(_rng.NextDouble() * 360);
                break;
            case TileType.Mountain:
                if (!DetermineUniformMountain())
                {
                    scale = (float)(_rng.NextDouble() * 0.4 + 0.4); // 0.4 ~ 0.8
                    rotation = _rng.Next(0, 4) * 90f;
                }
                else
                {
                    scale = (float)(_rng.NextDouble() * 1.0 + 0.5); // 0.5 ~ 1.5
                    rotation = _rng.Next(0, 4) * 90f;
                }
                break;
            case TileType.Iron:
                if (!DetermineUniformIron())
                {
                    scale = (float)(_rng.NextDouble() * 0.4 + 0.4);
                    rotation = _rng.Next(0, 4) * 90f;
                }
                else
                {
                    scale = (float)(_rng.NextDouble() * 0.6 + 0.4);
                    rotation = _rng.Next(0, 4) * 90f;
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
        // Debug.Log($"스케일 변경: {scale}");
        if (_env)
            _env.transform.localScale = new Vector3(1, scale, 1);
    }

    public void SetEnvRotation(float yAngle)
    {
        // Debug.Log($"로테이션 변경{yAngle}");
        if (_env)
        {
            Vector3 currentEuler = _env.transform.rotation.eulerAngles;
            _env.transform.rotation = Quaternion.Euler(currentEuler.x, yAngle, currentEuler.z);
        }
    }

    // 역 오브젝트는 무조건 위에서 내려온다.
    public IEnumerator AnimateStationDrop(float duration, float dropOffset)
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

    protected abstract void BlockInit();

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
    
    //env를 Despawn한 뒤 자신을 Despawn한다.
    public void DespawnBlockAndEnv()
    {
        // env 오브젝트 Despawn
        if (_env)
        {
            NetworkObject envNetObj = _env.GetComponent<NetworkObject>();
            if (envNetObj != null)
            {
                envNetObj.Despawn();
            }
        }
        
        NetworkObject.Despawn();
    }
    
    public bool HasEnvObject()
    {
        return _env != null;
    }
}
