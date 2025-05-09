using Sound;
using System.Collections;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class PlayerInfo : MonoBehaviour
{
    public float rayDistance = 0.8f;
    private float digwaitTime = 0.5f;

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
    private bool bucketAssigned = false;

    [Header("사운드")]
    public AudioClip PickAxeHitSound;
    public AudioClip AxeHitSound;
    public AudioClip WaterDrawSound;
    public AudioClip WaterSpraySound;
    private Coroutine digCoroutine;
    private Coroutine waterCoroutine;

    public void Start()
    {
        // 초기 버킷 찾기 시도
        FindBucket();
    }

    public void Update()
    {
        // 버킷이 아직 할당되지 않았다면 계속 찾기 시도
        if (!bucketAssigned)
        {
            FindBucket();
        }

        if (itemType == ItemType.WaterInBucket)
            WaterRaycast();
        else
            PlayerRaycast();
    }

    // 버킷을 찾는 메서드
    private void FindBucket()
    {
        if (Bucket == null)
        {
            BucketInfo temp = FindObjectOfType<BucketInfo>();
            
            if (temp != null)
            {
                Bucket = temp.gameObject;
                bucketAssigned = true;
            }
        }
    }

    public void SetItemType(ItemType newItemType)
    {
        itemType = newItemType;
    }

    public void DigDone()
    {
        hitOBJ.GetComponent<BreakableObject>()?.CheckRay(itemType);
        if (itemType == ItemType.Pickaxe)
            SoundManager.Instance.PlaySound(PickAxeHitSound);
        if(itemType == ItemType.Axe)
            SoundManager.Instance.PlaySound(AxeHitSound);
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
        else if ((itemType == ItemType.Axe || itemType == ItemType.Pickaxe) && hitBlock == BlockType.Enemy)
        {
            return true;
        }
        return false;
    }

    private void PlayerRaycast()
    {
        int excludeLayer = LayerMask.NameToLayer("Tile");
        int layerMask = ~(1 << excludeLayer);

        if (hitOBJ == null)
        {
            IsDig = false;

            if (digCoroutine != null)
            {
                StopCoroutine(digCoroutine);
                digCoroutine = null;
            }
            if (waterCoroutine != null)
            {
                StopCoroutine(waterCoroutine);
                waterCoroutine = null;
            }
        }

        Ray ray = new Ray(transform.position + new Vector3(0, 0.5f, 0), transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, rayDistance, layerMask))
        {
            CraftingTableObject = hit.collider.gameObject.GetComponent<CraftingTable>();
            deskInfo = hit.collider.gameObject.GetComponent<DeskInfo>();

            if (CraftingTableObject != null)
            {
                hitBlock = BlockType.CraftingTable;
                hitOBJ = hit.collider.gameObject;
                Debug.Log("크래프팅 테이블 감지됨");
                return;
            }
            else if (deskInfo != null)
            {
                hitBlock = BlockType.DeskTable;
                hitOBJ = hit.collider.gameObject;
                Debug.Log("데스크 테이블 감지됨");
                return;
            }

            BlockType blockType = BlockType.None;
            var breakable = hit.collider.gameObject.GetComponent<BreakableObject>();
            if (breakable != null)
            {
                blockType = breakable.BlockTypeProperty;
            }

            var burnTrain = hit.collider.gameObject.GetComponent<BurnTrainObject>();
            if (blockType == BlockType.None && burnTrain == null)
            {
                hitBlock = BlockType.None;
                hitOBJ = null;
                return;
            }

            hitBlock = blockType;
            hitOBJ = hit.collider.gameObject;

            if (blockType == BlockType.Water && HandleCheckDigBlock() && waterCoroutine == null && itemType == ItemType.Bucket)
            {
                waterCoroutine = StartCoroutine(BucketInWaterCorutine());
            }

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

        foreach (var col in hits)
        {
            BurnTrainObject burn = col.GetComponent<BurnTrainObject>();

            if (burn != null && burn.Isburn.Value)
            {
                bool bucketHasWater = Bucket != null && Bucket.GetComponent<BucketInfo>().SyncedItemType.Value == ItemType.WaterInBucket;

                if (!bucketHasWater) return;
                
                WaterTank tank = col.GetComponent<WaterTank>();
                
                if (tank != null)
                {
                    tank.CoolingTankServerRpc();
                }
                else
                {
                    burn.SetIsBurnServerRpc(false);
                }
                
                itemType = ItemType.Bucket;

                if (Bucket != null)
                {
                    Bucket.GetComponent<BucketInfo>()?.SetItemTypeServerRpc(ItemType.Bucket);
                }

                SoundManager.Instance.PlaySound(WaterSpraySound);
                break;
            }
        }
    }
    
    IEnumerator DigBlockCorutine()
    {
        yield return new WaitForSeconds(digwaitTime);
        if (hitBlock != BlockType.None && !IsDig && HandleCheckDigBlock() && hitOBJ != null)
        {
            IsDig = true;
        }

        digCoroutine = null;
    }

    IEnumerator BucketInWaterCorutine()
    {
        yield return new WaitForSeconds(digwaitTime);

        itemType = ItemType.WaterInBucket;

        if (Bucket != null)
        {
            Bucket.GetComponent<BucketInfo>()?.SetItemTypeServerRpc(ItemType.WaterInBucket);
        }

        SoundManager.Instance.PlaySound(WaterDrawSound);

        waterCoroutine = null;
    }

}