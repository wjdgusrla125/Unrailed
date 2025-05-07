using Sound;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BreakableObject : NetworkBehaviour
{
    [SerializeField] public int BlockHpCount;
    [SerializeField] private BlockType blockType;
    [SerializeField] private List<GameObject> DropGameObject;
    private float shakeDuration = 0.29f; 
    private float shakeAmount = 0.1f;   
    private float decreaseFactor = 1f; 
    private Vector3 originalPos;
    [SerializeField] private GameObject HitParticle;
    [SerializeField] private GameObject DestroyParticle;
    public AudioClip BrokenSound;
    public int MeshObjectCount;
    public Tile TileInfo;

    // NetworkVariable 추가
    private NetworkVariable<int> NetBlockHpCount = new NetworkVariable<int>(
        0, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    public BlockType BlockTypeProperty
    {
        get { return blockType; }
        set { blockType = value; }
    }

    private void Start()
    {
        MeshObjectCount = gameObject.transform.GetChild(0).childCount;
        
        if (IsServer)
        {
            NetBlockHpCount.Value = BlockHpCount;
        }
        
        // NetworkVariable 값 변경 이벤트 구독
        NetBlockHpCount.OnValueChanged += OnBlockHpChanged;
    }
    
    public override void OnDestroy()
    {
        base.OnDestroy();
        // 이벤트 구독 해제
        NetBlockHpCount.OnValueChanged -= OnBlockHpChanged;
    }
    
    // HP 값이 변경될 때 실행되는 콜백
    private void OnBlockHpChanged(int previous, int current)
    {
        BlockHpCount = current;
        // 클라이언트에서 실행
        StartCoroutine(ShakeCoroutine());
        if (previous > current && current > 0 && HitParticle != null)
        {
            SpawnHitParticle();
        }
        
        if (blockType != BlockType.Enemy)
        {
            SetMeshObject();
        }
        
        if (current == 0)
        {
            if (!IsServer)
            {
                // 클라이언트에서는 시각적 효과만 처리
                SpawnDestroyEffect();
            }
        }
    }

    private IEnumerator ShakeCoroutine()
    {
        originalPos = transform.localPosition;
        float currentDuration = shakeDuration;
        
        while (currentDuration >= 0)
        {
            transform.localPosition = originalPos + Random.insideUnitSphere * shakeAmount;
            currentDuration -= Time.deltaTime * decreaseFactor;
            yield return null;
        }
        transform.localPosition = originalPos;
        yield return null;
    }

    public void SetMeshObject()
    {
        if (BlockHpCount == 0)
            return;

        if (BlockHpCount < MeshObjectCount && BlockHpCount != 0)
        {
            MeshObjectCount--;
            gameObject.transform.GetChild(0).GetChild(BlockHpCount).gameObject.SetActive(false);
        }
        else if (BlockHpCount == MeshObjectCount)
        {
            MeshObjectCount--;
            if (BlockHpCount == 1)
                return;
            gameObject.transform.GetChild(0).GetChild(BlockHpCount - 1).gameObject.SetActive(false);
        }
        else if (MeshObjectCount == 0)
        {
            Vector3 newScale = gameObject.transform.GetChild(0).transform.localScale - new Vector3(0.3f, 0.3f, 0.3f);
            newScale = Vector3.Max(newScale, Vector3.zero);
            gameObject.transform.GetChild(0).transform.localScale = newScale;
        }
    }

    public void CheckRay(ItemType itemType)
    {
        // 서버에서만 HP 업데이트를 처리
        if (!IsServer)
        {
            CheckRayServerRpc(itemType);
            return;
        }
        
        ProcessHit(itemType);
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void CheckRayServerRpc(ItemType itemType)
    {
        ProcessHit(itemType);
    }
    
    private void ProcessHit(ItemType itemType)
    {
        if (itemType == ItemType.Axe && blockType == BlockType.Wood)
        {
            NetBlockHpCount.Value--;
        }
        else if (itemType == ItemType.Pickaxe && blockType == BlockType.IronOre)
        {
            NetBlockHpCount.Value--;
        }
        else if ((itemType == ItemType.Pickaxe || itemType == ItemType.Axe) && blockType == BlockType.Enemy)
        {
            NetBlockHpCount.Value--;
        }
        
        if (NetBlockHpCount.Value == 0)
            DestroyBlock();
    }
    
    private void SpawnHitParticle()
    {
        GameObject temp = GameObject.Instantiate(HitParticle);
        temp.transform.position = gameObject.transform.position + new Vector3(0, 0.5f, 0);
        temp.transform.SetParent(gameObject.transform);
    }
    
    private void SpawnDestroyEffect()
    {
        GameObject temp = Instantiate(DestroyParticle);
        temp.transform.position = transform.position + new Vector3(0, 0.5f, 0);
        SoundManager.Instance.PlaySound(BrokenSound);
    }

    public void DestroyBlock()
    {
        // 서버에서만 실제 파괴 로직 수행
        if (!IsServer)
            return;
            
        if (TileInfo != null)
        {
            // 블록이 파괴될 때 타일 타입을 Grass로 변경
            Vector2Int tilePos = new Vector2Int(Mathf.RoundToInt(TileInfo.transform.position.x), Mathf.RoundToInt(TileInfo.transform.position.z));
            MapGenerator.Instance.Map[tilePos.x, tilePos.y] = MapGenerator.TileType.Grass;
            MapGenerator.Instance.ReassignTile(tilePos);
        }

        // 시각적 효과 처리 - 모든 클라이언트에 동기화
        SpawnDestroyEffectClientRpc();

        if (TileInfo != null)
        {
            DropItemsClientRpc();
        }

        // 객체 파괴를 모든 클라이언트에 동기화
        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    [ClientRpc]
    private void SpawnDestroyEffectClientRpc()
    {
        SpawnDestroyEffect();
    }
    
    [ClientRpc]
    private void DropItemsClientRpc()
    {
        if (TileInfo != null && DropGameObject != null && DropGameObject.Count > 0)
        {
            int index = (int)blockType - 1;
            if (index >= 0 && index < DropGameObject.Count)
            {
                TileInfo.DropitialItems(DropGameObject[index]);
            }
        }
    }
}