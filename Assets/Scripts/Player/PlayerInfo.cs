using Sound;
using System.Collections;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class PlayerInfo : MonoBehaviour
{
    [Header("Ray 설정")]
    public float rayDistance = 0.8f;

    [Header("OverlapBox 설정")]
    public Vector3 boxHalfExtents = new Vector3(0.3f, 0.5f, 0.4f);

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
        FindBucket();
    }

    public void Update()
    {
        if (!bucketAssigned)
        {
            FindBucket();
        }

        if (itemType == ItemType.WaterInBucket)
            WaterRaycast();
        else
            PlayerRaycast();
    }

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
        if (itemType == ItemType.Axe)
            SoundManager.Instance.PlaySound(AxeHitSound);
    }

    private bool HandleCheckDigBlock()
    {
        if (itemType == ItemType.Axe && hitBlock == BlockType.Wood) return true;
        if (itemType == ItemType.Pickaxe && hitBlock == BlockType.IronOre) return true;
        if (itemType == ItemType.Bucket && hitBlock == BlockType.Water) return true;
        if ((itemType == ItemType.Axe || itemType == ItemType.Pickaxe) && hitBlock == BlockType.Enemy) return true;
        return false;
    }

    private void PlayerRaycast()
    {
        int excludeLayer = LayerMask.NameToLayer("Tile");
        int layerMask = ~(1 << excludeLayer);

        Vector3 boxCenter = transform.position + transform.forward * (rayDistance * 0.5f) + Vector3.up * 0.5f;
        Collider[] hits = Physics.OverlapBox(boxCenter, boxHalfExtents, transform.rotation, layerMask);

        bool found = false;

        foreach (var col in hits)
        {
            CraftingTableObject = col.GetComponent<CraftingTable>();
            deskInfo = col.GetComponent<DeskInfo>();

            if (CraftingTableObject != null)
            {
                hitBlock = BlockType.CraftingTable;
                hitOBJ = col.gameObject;
                found = true;
                break;
            }
            else if (deskInfo != null)
            {
                hitBlock = BlockType.DeskTable;
                hitOBJ = col.gameObject;
                found = true;
                break;
            }

            BlockType blockType = BlockType.None;
            var breakable = col.GetComponent<BreakableObject>();
            if (breakable != null)
            {
                blockType = breakable.BlockTypeProperty;
            }

            var burnTrain = col.GetComponent<BurnTrainObject>();
            if (blockType == BlockType.None && burnTrain == null)
            {
                continue;
            }

            hitBlock = blockType;
            hitOBJ = col.gameObject;
            found = true;

            if (blockType == BlockType.Water && HandleCheckDigBlock() && waterCoroutine == null && itemType == ItemType.Bucket)
            {
                waterCoroutine = StartCoroutine(BucketInWaterCorutine());
            }

            if (!IsDig && digCoroutine == null && blockType != BlockType.Water)
            {
                digCoroutine = StartCoroutine(DigBlockCorutine());
            }

            break;
        }

        if (!found)
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

            if (waterCoroutine != null)
            {
                StopCoroutine(waterCoroutine);
                waterCoroutine = null;
            }

            IsDig = false;
        }
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

    private void OnDrawGizmos()
    {
        Vector3 boxCenter = transform.position + transform.forward * (rayDistance * 0.5f) + Vector3.up * 0.5f;
        Gizmos.color = Color.green;
        Gizmos.matrix = Matrix4x4.TRS(boxCenter, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, boxHalfExtents * 2);
    }
}
