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
    [Header("�÷��̾� ����")]
    [SerializeField] private HandleType PlayerHandleType;
    [SerializeField] private bool IsDig = false;
    [Header("�տ� �νĵ� �� Ÿ��")]
    [SerializeField] private BlockDigCheck.BlockType hitBlock;

    #region �÷��̾� �ڷ�ƾ ó��.
    IEnumerator DigBlockCorutine()
    {
        yield return new WaitForSeconds(0.5f);
        if(hitBlock != BlockDigCheck.BlockType.None && !IsDig && HandleCheckDigBlock() && PlayerPos == gameObject.transform.position && PlayerFront == gameObject.transform.forward)
        {
            //if (PlayerOBJ.GetComponent<Animator>().GetBool("IsMoving") == true)
            //    yield break;
            IsDig = true;
            Debug.Log("ĳ�� ��!");
            // ���⿡ ĳ�� �ִϸ��̼� ����ϸ� �� ��?
        }
    }
    #endregion

    #region �÷��̾� ���� �� ����ĳ��Ʈ �ڵ�.
    // �÷��̾��� �� ���� üũ.
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
    // �÷��̾ ĳ���� �� üũ.
    public bool HandleCheckDigBlock()
    {
        if (PlayerHandleType == HandleType.Axe && hitBlock == BlockType.Wood)
        {
            Debug.Log("���� �ν�");
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
    // �÷��̾� ���� ĳ��Ʈ.
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

        // ����ĳ��Ʈ ����.
        if (Physics.Raycast(ray, out hit, rayDistance))
        {
            PlayerPos = gameObject.transform.position;
            PlayerFront = gameObject.transform.forward;
            BlockDigCheck.BlockType blockComponent = hit.collider.gameObject.GetComponent<BlockDigCheck>().BlockTypeProperty;
            if (blockComponent == BlockType.None)
                return;
            //Debug.Log("���� �ν��� �� Ÿ�� : " + blockComponent);
            hitBlock = blockComponent;
            StartCoroutine(DigBlockCorutine());
        }
        else
            hitBlock = BlockDigCheck.BlockType.None;

        // ����׿� ���� �ð�ȭ.
        Debug.DrawRay(transform.position + new Vector3(0, 0.5f, 0), transform.forward * rayDistance, Color.red);
    }

    #endregion

    public void Update()
    {
        // ���� ĳ��Ʈ.
        PlayerRaycast();
        // �ڵ� Ÿ�Կ� ���� �ð�ȭ.
        ShowPlayerHandle();
    }
}
