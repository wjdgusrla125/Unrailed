using System.Collections;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static BlockDigCheck;

public class PlayerInfo : MonoBehaviour
{
    [SerializeField] GameObject PlayerOBJ;
    [SerializeField] GameObject PlayerHandOBJ;
    [SerializeField] private Vector3 PlayerPos;
    [SerializeField] private Vector3 PlayerFront;
    private float rayDistance = 0.8f;
    private float digwaitTime = 1f;
    [Header("플레이어 상태")]
    [SerializeField] private HandleType PlayerHandleType;
    [SerializeField] private bool IsDig = false;
    [Header("앞에 인식된 블럭 타입")]
    [SerializeField] private BlockDigCheck.BlockType hitBlock;

    #region 플레이어 코루틴 처리.
    IEnumerator DigBlockCorutine()
    {
        yield return new WaitForSeconds(0.5f);
        if(hitBlock != BlockDigCheck.BlockType.None && !IsDig && HandleCheckDigBlock() && PlayerPos == gameObject.transform.position && PlayerFront == gameObject.transform.forward)
        {
            //if (PlayerOBJ.GetComponent<Animator>().GetBool("IsMoving") == true)
            //    yield break;
            IsDig = true;
            Debug.Log("캐는 중!");
            // 여기에 캐는 애니메이션 출력하면 될 듯?
        }
    }
    #endregion

    #region 플레이어 상태 및 레이캐스트 코드.
    // 플레이어의 손 상태 체크.
    public void ShowPlayerHandle()
    {
        switch(PlayerHandleType)
        {
            case HandleType.None:
                PlayerHandOBJ.transform.GetChild(1).gameObject.SetActive(false);
                PlayerHandOBJ.transform.GetChild(2).gameObject.SetActive(false);
                PlayerHandOBJ.transform.GetChild(3).gameObject.SetActive(false);
                break;
            case HandleType.Pickaxe:
                PlayerHandOBJ.transform.GetChild(1).gameObject.SetActive(true);
                PlayerHandOBJ.transform.GetChild(2).gameObject.SetActive(false);
                PlayerHandOBJ.transform.GetChild(3).gameObject.SetActive(false);
                break;
            case HandleType.Axe:
                PlayerHandOBJ.transform.GetChild(1).gameObject.SetActive(false);
                PlayerHandOBJ.transform.GetChild(2).gameObject.SetActive(true);
                PlayerHandOBJ.transform.GetChild(3).gameObject.SetActive(false);
                break;
            case HandleType.Bucket:
                PlayerHandOBJ.transform.GetChild(1).gameObject.SetActive(false);
                PlayerHandOBJ.transform.GetChild(2).gameObject.SetActive(false);
                PlayerHandOBJ.transform.GetChild(3).gameObject.SetActive(true);
                PlayerHandOBJ.transform.GetChild(3).GetChild(0).GetChild(5).gameObject.SetActive(false);
                break;
            case HandleType.Bucket_In_Water:
                PlayerHandOBJ.transform.GetChild(1).gameObject.SetActive(false);
                PlayerHandOBJ.transform.GetChild(2).gameObject.SetActive(false);
                PlayerHandOBJ.transform.GetChild(3).gameObject.SetActive(true);
                PlayerHandOBJ.transform.GetChild(3).GetChild(0).GetChild(5).gameObject.SetActive(true);
                break;
        }
    }
    // 플레이어가 캐려는 블럭 체크.
    public bool HandleCheckDigBlock()
    {
        if (PlayerHandleType == HandleType.Axe && hitBlock == BlockType.Wood)
        {
            Debug.Log("나무 인식");
            return true;
        }
        else if (PlayerHandleType == HandleType.Pickaxe && hitBlock == BlockType.Iron)
        {
            return true;
        }
        else if (PlayerHandleType == HandleType.Bucket && hitBlock == BlockType.Water)
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
            BlockDigCheck.BlockType blockComponent = hit.collider.gameObject.GetComponent<BlockDigCheck>().BlockTypeProperty;
            if (blockComponent == BlockType.None)
                return;
            //Debug.Log("현재 인식한 블럭 타입 : " + blockComponent);
            hitBlock = blockComponent;
            StartCoroutine(DigBlockCorutine());
        }
        else
            hitBlock = BlockDigCheck.BlockType.None;

        // 디버그용 레이 시각화.
        Debug.DrawRay(transform.position + new Vector3(0, 0.5f, 0), transform.forward * rayDistance, Color.red);
    }

    #endregion

    public void Update()
    {
        // 레이 캐스트.
        PlayerRaycast();
        // 핸들 타입에 따라 시각화.
        ShowPlayerHandle();
    }
}
