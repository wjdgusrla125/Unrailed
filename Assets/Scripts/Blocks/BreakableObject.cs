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

    // 외형 동기화용
    [SerializeField] private Transform modelChild;

    private NetworkVariable<int> NetBlockHpCount = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> yRotation = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<float> yScale = new NetworkVariable<float>(
        1f,
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
        modelChild = gameObject.transform.GetChild(0);

        if (IsServer)
        {
            NetBlockHpCount.Value = BlockHpCount;
        }

        NetBlockHpCount.OnValueChanged += OnBlockHpChanged;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        ApplyVisuals();

        yRotation.OnValueChanged += (_, _) => ApplyVisuals();
        yScale.OnValueChanged += (_, _) => ApplyVisuals();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        NetBlockHpCount.OnValueChanged -= OnBlockHpChanged;
        yRotation.OnValueChanged -= (_, _) => ApplyVisuals();
        yScale.OnValueChanged -= (_, _) => ApplyVisuals();
    }

    private void OnBlockHpChanged(int previous, int current)
    {
        BlockHpCount = current;
        StartCoroutine(ShakeCoroutine());

        if (previous > current && current > 0 && HitParticle != null)
        {
            SpawnHitParticle();
        }

        if (blockType != BlockType.Enemy)
        {
            SetMeshObject();
        }

        if (current == 0 && !IsServer)
        {
            SpawnDestroyEffect();
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
        if (!IsServer)
            return;

        if (TileInfo != null)
        {
            Vector2Int tilePos = new Vector2Int(Mathf.RoundToInt(TileInfo.transform.position.x), Mathf.RoundToInt(TileInfo.transform.position.z));
            MapGenerator.Instance.Map[tilePos.x, tilePos.y] = MapGenerator.TileType.Grass;
            MapGenerator.Instance.ReassignTile(tilePos);
        }

        SpawnDestroyEffectClientRpc();

        if (TileInfo != null)
        {
            DropItemsClientRpc();
        }

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

    // ✅ Host에서 호출하는 외형 동기화 메소드
    [ServerRpc(RequireOwnership = false)]
    public void SetVisualsServerRpc(float rotation, float scale)
    {
        yRotation.Value = rotation;
        yScale.Value = scale;
    }

    private void ApplyVisuals()
    {
        if (modelChild == null) return;

        modelChild.localRotation = Quaternion.Euler(0f, yRotation.Value, 0f);
        Vector3 curScale = gameObject.transform.localScale;
        gameObject.transform.localScale = new Vector3(curScale.x, yScale.Value, curScale.z);
    }
}
