using System.Collections;
using System.Runtime.CompilerServices;
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

    private Coroutine digCoroutine;

    public void Update()
    {
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

            if (blockType == BlockType.None) return;

            hitBlock = blockType;
            hitOBJ = hit.collider.gameObject;

            // Dig 조건 체크 및 코루틴 실행
            if (!IsDig && digCoroutine == null)
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
}
