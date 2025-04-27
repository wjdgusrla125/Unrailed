using Sound;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BreakableObject : MonoBehaviour
{
    [SerializeField] private int BlockHpCount;
    [SerializeField] private BlockType blockType;
    [SerializeField] private List<GameObject> DropGameObject;
    private float shakeDuration = 0.29f;   // ��鸮�� �ð� (��)
    private float shakeAmount = 0.1f;   // ��鸲 ����
    private float decreaseFactor = 1f;  // �ð� �پ��� �ӵ�
    private Vector3 originalPos;
    [SerializeField] private GameObject HitParticle;
    [SerializeField] private GameObject DestroyParticle;
    public AudioClip BrokenSound;
    public int MeshObjectCount;
    public Tile TileInfo;

    public BlockType BlockTypeProperty
    {
        get { return blockType; }
        set { blockType = value; }
    }

    private void Start()
    {
        MeshObjectCount = gameObject.transform.GetChild(0).childCount;
    }

    private IEnumerator ShakeCoroutine()
    {
        originalPos = transform.localPosition;
        float currentDuration = shakeDuration;

        gameObject.GetComponent<Collider>().isTrigger = true;
        while (currentDuration >= 0)
        {
            transform.localPosition = originalPos + Random.insideUnitSphere * shakeAmount;
            currentDuration -= Time.deltaTime * decreaseFactor;
            yield return null;
        }
        transform.localPosition = originalPos;
        yield return null;
        gameObject.GetComponent<Collider>().isTrigger = false;
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
        StartCoroutine(ShakeCoroutine());
        if (BlockHpCount != 0)
        {
            GameObject temp = GameObject.Instantiate(HitParticle);
            temp.transform.position = gameObject.transform.position + new Vector3(0, 0.5f, 0);
            temp.transform.SetParent(gameObject.transform);
        }

        if (itemType == ItemType.Axe && blockType == BlockType.Wood)
        {
            BlockHpCount--;
        }
        else if (itemType == ItemType.Pickaxe && blockType == BlockType.IronOre)
        {
            BlockHpCount--;
        }
        
        SetMeshObject();
        if (BlockHpCount == 0)
            DestroyBlock();
    }

    public void DestroyBlock()
    {
        if (TileInfo != null)
        {
            // 블록이 파괴될 때 타일 타입을 Grass로 변경
            Vector2Int tilePos = new Vector2Int(Mathf.RoundToInt(TileInfo.transform.position.x), Mathf.RoundToInt(TileInfo.transform.position.z));
            MapGenerator.Instance.Map[tilePos.x, tilePos.y] = MapGenerator.TileType.Grass;
            MapGenerator.Instance.ReassignTile(tilePos);
        }

        GameObject temp = Instantiate(DestroyParticle);
        temp.transform.position = transform.position + new Vector3(0, 0.5f, 0);
        SoundManager.Instance.PlaySound(BrokenSound);

        if (TileInfo != null)
        {
            TileInfo.DropitialItems(DropGameObject[(int)blockType - 1]);
        }

        Destroy(gameObject);
    }
}