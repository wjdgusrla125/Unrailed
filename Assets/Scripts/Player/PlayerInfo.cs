using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerInfo : MonoBehaviour
{
    private Vector3 PlayerPos;
    private Vector3 PlayerFront;
    private float rayDistance = 0.8f;
    private float digwaitTime = 1f;
    
    [Header("플레이어 상태")]
    public ItemType itemType;
    public bool IsDig = false;
    [Header("앞에 인식된 블럭 타입")]
    public BlockType hitBlock;
    [Header("앞에 인식된 열차")]
    public CraftingTable CraftingTableObject;
    public DeskInfo deskInfo;
    
    public void Update()
    {
        PlayerRaycast();
    }
    
    public void SetItemType(ItemType newItemType)
    {
        itemType = newItemType;
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
        if (PlayerPos != gameObject.transform.position || PlayerFront != gameObject.transform.forward)
        {
            PlayerPos = new Vector3(0, 0, 0);
            PlayerFront = new Vector3(0, 0, 0);
            IsDig = false;
        }
        
        Ray ray = new Ray(transform.position + new Vector3(0, 0.5f, 0), transform.forward);
        RaycastHit hit;

        // 레이캐스트 수행.
        if (Physics.Raycast(ray, out hit, rayDistance))
        {
            PlayerPos = gameObject.transform.position;
            PlayerFront = gameObject.transform.forward;
            
            // 제작대와 책상 감지
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
            
            if (hit.collider.gameObject.GetComponent<BreakableObject>() != null)
            {
                blockType = hit.collider.gameObject.GetComponent<BreakableObject>().BlockTypeProperty;
            }
            
            if (blockType == BlockType.None) return;
            
            hitBlock = blockType;
            
            if (!IsDig)
            {
                StartCoroutine(DigBlockCorutine());
            }
        }
        else
        {
            hitBlock = BlockType.None;
            
            // 레이캐스트가 아무것도 감지하지 못했을 때 참조 제거
            CraftingTableObject = null;
            deskInfo = null;
        }
        
        Debug.DrawRay(transform.position + new Vector3(0, 0.5f, 0), transform.forward * rayDistance, Color.red);
    }
    
    IEnumerator DigBlockCorutine()
    {
        yield return new WaitForSeconds(1f);
        
        if(hitBlock != BlockType.None && !IsDig && HandleCheckDigBlock() && PlayerPos == gameObject.transform.position && PlayerFront == gameObject.transform.forward)
        {
            IsDig = true;
        }
    }
}