using System;
using Unity.Netcode;
using UnityEngine;

public class PlayerAnimation : NetworkBehaviour
{ 
    [SerializeField] private Animator animator; 
    [SerializeField] private InputReader inputReader;
    [SerializeField] private PlayerMovement playerMovement;

    // 애니메이션 파라미터 이름 상수
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int Interact = Animator.StringToHash("Interact");

    // 네트워크 변수 추가 - 이동 상태 동기화용
    private NetworkVariable<bool> networkIsMoving = new NetworkVariable<bool>();
    private NetworkVariable<float> networkMoveSpeed = new NetworkVariable<float>();

    // 현재 이동 입력값 저장 (로컬 플레이어용)
    private Vector2 currentMovementInput;
    
    private void Awake()
    {
        if (animator == null)
        {
            Debug.LogError("Animator 컴포넌트가 할당되지 않았습니다!");
        }
        
        if (inputReader == null && IsOwner)
        {
            Debug.LogError("InputReader가 할당되지 않았습니다!");
        }

        if (playerMovement == null)
        {
            Debug.LogError("PlayerMovement가 할당되지 않았습니다!");
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // 로컬 플레이어에 대해서만 입력 이벤트 구독
        if (IsOwner)
        {
            if (inputReader != null)
            {
                inputReader.MoveEvent += HandleMove;
                inputReader.InteractEvent += HandleInteract;
            }
        }
        
        // 네트워크 변수 변화 이벤트 구독
        networkIsMoving.OnValueChanged += OnIsMovingChanged;
        networkMoveSpeed.OnValueChanged += OnMoveSpeedChanged;
    }

    public override void OnNetworkDespawn()
    {
        // 네트워크 변수 이벤트 구독 해제
        networkIsMoving.OnValueChanged -= OnIsMovingChanged;
        networkMoveSpeed.OnValueChanged -= OnMoveSpeedChanged;
        
        // 로컬 플레이어일 때만 구독 해제
        if (IsOwner)
        {
            if (inputReader != null)
            {
                inputReader.MoveEvent -= HandleMove;
                inputReader.InteractEvent -= HandleInteract;
            }
        }
        
        base.OnNetworkDespawn();
    }

    private void Update()
    {
        UpdateAnimation();
    }

    private void HandleMove(Vector2 moveInput)
    {
        // 로컬 플레이어의 입력값 저장
        if (IsOwner)
        {
            currentMovementInput = moveInput;
            
            // 이동 중인지 확인
            bool isMoving = moveInput.magnitude > 0.1f;
            float moveSpeed = Mathf.Clamp01(moveInput.magnitude);
            
            // 서버에 이동 상태 업데이트 요청
            UpdateMovementStateServerRpc(isMoving, moveSpeed);
        }
    }

    [ServerRpc]
    private void UpdateMovementStateServerRpc(bool isMoving, float moveSpeed)
    {
        // 서버에서 네트워크 변수 업데이트
        networkIsMoving.Value = isMoving;
        networkMoveSpeed.Value = moveSpeed;
    }
    
    private void OnIsMovingChanged(bool previousValue, bool newValue)
    {
        // 네트워크 변수가 변경되면 애니메이터 업데이트
        if (animator != null && !IsOwner)
        {
            animator.SetBool(IsMoving, newValue);
        }
    }
    
    private void OnMoveSpeedChanged(float previousValue, float newValue)
    {
        // 네트워크 변수가 변경되면 애니메이터 업데이트
        if (animator != null && !IsOwner)
        {
            animator.SetFloat(MoveSpeed, newValue);
        }
    }

    private void HandleInteract(bool interactValue)
    {
        if (IsOwner && interactValue)
        {
            // 로컬 플레이어의 상호작용 애니메이션 트리거
            TriggerInteractServerRpc();
        }
    }

    [ServerRpc]
    private void TriggerInteractServerRpc()
    {
        // 서버에서 모든 클라이언트에게 상호작용 애니메이션 동기화
        TriggerInteractClientRpc();
    }

    [ClientRpc]
    private void TriggerInteractClientRpc()
    {
        // 모든 클라이언트에서 상호작용 애니메이션 재생
        if (animator != null)
        {
            animator.SetTrigger(Interact);
        }
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;

        // 로컬 플레이어는 직접 입력값 사용
        if (IsOwner)
        {
            UpdateMovementAnimationFromInput();
        }
        // 네트워크 플레이어는 네트워크 변수 기반 애니메이션 (이미 OnValueChanged에서 처리됨)
        // 네트워크 변수 이벤트로 처리되므로 여기서는 추가 로직이 필요 없음
    }

    private void UpdateMovementAnimationFromInput()
    {
        // 이동 중인지 확인
        bool isMoving = currentMovementInput.magnitude > 0.1f;
        
        // 애니메이터 파라미터 설정
        animator.SetBool(IsMoving, isMoving);
        
        // 이동 속도를 애니메이터에 전달 (0~1 사이 값)
        float moveSpeed = Mathf.Clamp01(currentMovementInput.magnitude);
        animator.SetFloat(MoveSpeed, moveSpeed);
    }
}