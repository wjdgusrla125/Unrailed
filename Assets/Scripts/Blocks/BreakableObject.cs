using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BreakableObject : MonoBehaviour
{
    [SerializeField] private int BlockHpCount;
    [SerializeField] private BlockType blockType;
    [SerializeField] private List<GameObject> DropGameObject;
    private float shakeDuration = 0.29f;   // 흔들리는 시간 (초)
    private float shakeAmount = 0.1f;   // 흔들림 강도
    private float decreaseFactor = 1f;  // 시간 줄어드는 속도
    private Vector3 originalPos;
    [SerializeField] private GameObject HitParticle;
    [SerializeField] private GameObject DestroyParticle;
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
        GameObject temp = GameObject.Instantiate(DestroyParticle);
        temp.transform.position = gameObject.transform.position + new Vector3(0,0.5f,0);

        TileInfo.DropitialItems(DropGameObject[(int)blockType - 1]);
        Destroy(gameObject);
    }
}