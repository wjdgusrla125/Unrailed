using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerInfo : MonoBehaviour
{
    [SerializeField] private Vector3 PlayerPos;
    [SerializeField] private Vector3 PlayerFront;
    
    private float rayDistance = 0.8f;
    private float digwaitTime = 1f;
    
    [Header("플레이어 상태")]
    public ItemType itemType;
    public bool IsDig = false;
    
    [Header("앞에 인식된 블럭 타입")]
    public BlockType hitBlock;
    [HideInInspector]
    public CraftingTable CraftingTableObject;

    public void Awake()
    {
        CraftingTableObject = FindObjectOfType<CraftingTable>();
    }

    public void Update()
    {
        PlayerRaycast();
    }
    
    public void SetItemType(ItemType newItemType)
    {
        itemType = newItemType;
    }
    
    public bool HandleCheckDigBlock()
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
    
    // 플레이어 레이 캐스트.
    public void PlayerRaycast()
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
            BlockType blockType = BlockType.None;
            if (hit.collider.gameObject.GetComponent<BreakableObject>() != null)
                blockType = hit.collider.gameObject.GetComponent<BreakableObject>().BlockTypeProperty;

            if (blockType == BlockType.None) return;
            
            hitBlock = blockType;
            if (!IsDig)
                StartCoroutine(DigBlockCorutine());
        }
        else
        {
            hitBlock = BlockType.None;
        }
        
        // 디버그용 레이 시각화.
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