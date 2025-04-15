using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private InputReader inputReader;
    [SerializeField] private PlayerMovement movement;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // 로컬 플레이어에 대해서만 입력 처리 활성화
        if (IsOwner)
        {
            // 입력 이벤트에 구독
            inputReader.MoveEvent += HandleMove;
            inputReader.InteractEvent += HandleInteract;
        }
        else
        {
            // 로컬 플레이어가 아닌 경우 이 컴포넌트 비활성화
            enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            // 오브젝트가 제거될 때 입력 이벤트 구독 해제
            inputReader.MoveEvent -= HandleMove;
            inputReader.InteractEvent -= HandleInteract;
        }

        base.OnNetworkDespawn();
    }

    private void HandleMove(Vector2 moveInput)
    {
        if (IsOwner)
        {
            // 이동 입력을 이동 컴포넌트로 전달
            movement.SetMovementInput(moveInput);
        }
    }

    private void HandleInteract(bool interactValue)
    {
        if (IsOwner && interactValue)
        {
            // 상호작용 실행
            RequestInteractServerRpc();
        }
    }
    
    

    [ServerRpc]
    private void RequestInteractServerRpc()
    {
        
    }
}