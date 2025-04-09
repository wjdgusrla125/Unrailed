using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerInfo : MonoBehaviour
{
    [SerializeField] private Vector3 PlayerPos;
    [SerializeField] private Vector3 PlayerFront;
    
    private float rayDistance = 0.8f;
    private float digwaitTime = 1f;
    
    [Header("플레이어 상태")]
    [SerializeField] private ItemType itemType;
    public bool IsDig = false;
    
    [Header("앞에 인식된 블럭 타입")]
    [SerializeField] private BlockType hitBlock;
    
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
        // 플레이어 위치나 바라보는 방향이 바뀌었는지 확인
        if (PlayerPos != gameObject.transform.position || PlayerFront != gameObject.transform.forward)
        {
            PlayerPos = Vector3.zero;
            PlayerFront = Vector3.zero;
            IsDig = false;
        }

        // 레이 생성: 플레이어 위치에서 약간 위로 올린 위치에서 앞으로
        Ray ray = new Ray(transform.position + new Vector3(0, 0.5f, 0), transform.forward);
        RaycastHit hit;

        // 레이캐스트 수행
        if (Physics.Raycast(ray, out hit, rayDistance))
        {
            PlayerPos = gameObject.transform.position;
            PlayerFront = gameObject.transform.forward;

            // BreakableObject 컴포넌트를 가진 오브젝트인지 확인
            BreakableObject breakable = hit.collider.gameObject.GetComponent<BreakableObject>();
            if (breakable != null)
            {
                BlockType blockType = breakable.BlockTypeProperty;

                if (blockType == BlockType.None) return;

                hitBlock = blockType;
                StartCoroutine(DigBlockCorutine());
            }
            else
            {
                // 컴포넌트가 없으면 블럭 타입을 None으로 설정
                hitBlock = BlockType.None;
            }
        }
        else
        {
            // 레이가 아무것도 감지하지 못한 경우
            hitBlock = BlockType.None;
        }

        // 디버그용 레이 시각화
        Debug.DrawRay(transform.position + new Vector3(0, 0.5f, 0), transform.forward * rayDistance, Color.red);
    }
    
    IEnumerator DigBlockCorutine()
    {
        yield return new WaitForSeconds(0.5f);
        
        if(hitBlock != BlockType.None && !IsDig && HandleCheckDigBlock() && PlayerPos == gameObject.transform.position && PlayerFront == gameObject.transform.forward)
        {
            IsDig = true;
        }
    }
}