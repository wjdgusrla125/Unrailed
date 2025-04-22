using System.Collections;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Unity.AppUI.UI;
using UnityEngine;

public class PlayerInfo : MonoBehaviour
{
    private Vector3 PlayerPos;
    private Vector3 PlayerFront;
    public float rayDistance = 0.8f;
    private float digwaitTime = 1f;

    [Header("플레이어 상태")]
    public ItemType itemType;
    public bool IsDig = false;

    [Header("앞에 인식된 블럭 타입")]
    public BlockType hitBlock;
    public GameObject hitOBJ;

    [Header("앞에 인식된 열차")]
    public CraftingTable CraftingTableObject;
    public DeskInfo deskInfo;

    [Header("양동이 체크")]
    public GameObject Bucket;

    private Coroutine digCoroutine;
    private Coroutine waterCoroutine;

    public void Start()
    {
        BucketInfo temp = FindObjectOfType<BucketInfo>();
        Bucket = temp.gameObject;
    }
    public void Update()
    {
        if (itemType == ItemType.WaterInBucket)
            WaterRaycast();
        else
            PlayerRaycast();
    }

    public void SetItemType(ItemType newItemType)
    {
        itemType = newItemType;
    }

    public void DigDone()
    {
        hitOBJ.GetComponent<BreakableObject>()?.CheckRay(itemType);
    }

    private bool HandleCheckDigBlock()
    {
        if (itemType == ItemType.Axe && hitBlock == BlockType.Wood)
        {
            return true;
        }
        else if (itemType == ItemType.Pickaxe && hitBlock == BlockType.IronOre)
        {
            return true;
        }
        else if (itemType == ItemType.Bucket && hitBlock == BlockType.Water)
        {
            return true;
        }
        return false;
    }

    private void PlayerRaycast()
    {
        // 위치나 방향이 바뀌면 IsDig 상태 초기화
        if (PlayerPos != transform.position || PlayerFront != transform.forward || hitOBJ == null)
        {
            PlayerPos = transform.position;
            PlayerFront = transform.forward;
            IsDig = false;

            // 위치 바뀌면 코루틴도 초기화
            if (digCoroutine != null)
            {
                StopCoroutine(digCoroutine);
                digCoroutine = null;
            }
            if(waterCoroutine != null)
            {
                StopCoroutine(waterCoroutine);
                waterCoroutine = null;
            }
        }

        Ray ray = new Ray(transform.position + new Vector3(0, 0.5f, 0), transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, rayDistance))
        {
            PlayerPos = transform.position;
            PlayerFront = transform.forward;

            // 제작대/책상 체크
            CraftingTableObject = hit.collider.gameObject.GetComponent<CraftingTable>();
            if (CraftingTableObject != null)
            {
                hitBlock = BlockType.CraftingTable;
                Debug.Log("크래프팅 테이블 감지됨");
            }

            deskInfo = hit.collider.gameObject.GetComponent<DeskInfo>();
            if (deskInfo != null)
            {
                hitBlock = BlockType.DeskTable;
                Debug.Log("데스크 테이블 감지됨");
            }

            BlockType blockType = BlockType.None;

            var breakable = hit.collider.gameObject.GetComponent<BreakableObject>();
            if (breakable != null)
            {
                blockType = breakable.BlockTypeProperty;
            }

            if (blockType == BlockType.None && hit.collider.gameObject.GetComponent<BurnTrainObject>() == null) return;

            hitBlock = blockType;
            hitOBJ = hit.collider.gameObject;

            // 불 끄는 체크 및 코루틴 실행.
            if(blockType == BlockType.None)
            {

                if (hitOBJ.GetComponent<BurnTrainObject>().Isburn)
                {
                    hitOBJ.GetComponent<BurnTrainObject>().Isburn = false;
                }
                return;
            }

            // 물 뜨는 체크 및 코루틴 실행.
            if(blockType == BlockType.Water && HandleCheckDigBlock() && waterCoroutine == null && itemType == ItemType.Bucket)
            {
                waterCoroutine = StartCoroutine(BucketInWaterCorutine());
            }

            // Dig 조건 체크 및 코루틴 실행.
            if (!IsDig && digCoroutine == null && blockType != BlockType.Water)
            {
                digCoroutine = StartCoroutine(DigBlockCorutine());
            }
        }
        else
        {
            hitBlock = BlockType.None;
            hitOBJ = null;
            CraftingTableObject = null;
            deskInfo = null;

            // 감지 해제되면 코루틴도 중단
            if (digCoroutine != null)
            {
                StopCoroutine(digCoroutine);
                digCoroutine = null;
            }
        }

        Debug.DrawRay(transform.position + new Vector3(0, 0.5f, 0), transform.forward * rayDistance, Color.red);
    }

    private void WaterRaycast()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 0.8f, 1 << LayerMask.NameToLayer("Train"));

        if (hits.Length > 0)
        {
            Debug.Log("감지됨");
            foreach(Collider col in hits)
            {
                if (col.gameObject.GetComponent<BurnTrainObject>().Isburn)
                {
                    col.gameObject.GetComponent<BurnTrainObject>().Isburn = false;
                    itemType = ItemType.Bucket;
                    Bucket.GetComponent<Item>().ItemType = ItemType.Bucket;
                }
            }
        }
        
    }
    IEnumerator DigBlockCorutine()
    {
        yield return new WaitForSeconds(digwaitTime);
        if (hitBlock != BlockType.None && !IsDig && HandleCheckDigBlock() &&
            PlayerPos == transform.position && PlayerFront == transform.forward && hitOBJ != null)
        {
            IsDig = true;
        }

        digCoroutine = null;
    }

    IEnumerator BucketInWaterCorutine()
    {
        yield return new WaitForSeconds(digwaitTime);
        itemType = ItemType.WaterInBucket;
        Bucket.GetComponent<Item>().ItemType = ItemType.WaterInBucket;
        waterCoroutine = null;
    }

    void OnDrawGizmosSelected()
    {
        // 편의를 위해 Scene 뷰에 감지 범위를 그려줌
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(gameObject.transform.position, 0.8f);
    }
}
