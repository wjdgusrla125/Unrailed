using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 5f;      // 이동 속도
    [SerializeField] private float rotationSpeed = 10f; // 회전 속도
    
    [Header("네트워크 변수")]
    private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>();
    
    // 로컬 이동 입력
    private Vector2 movementInput;
    private Rigidbody rb;
    private BoxCollider boxCollider;
    private Camera mainCamera;
    private Vector3 lastServerPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
        
        if (rb == null)
        {
            Debug.LogError("Rigidbody 컴포넌트가 없습니다!");
        }
        
        if (boxCollider == null)
        {
            Debug.LogError("BoxCollider 컴포넌트가 없습니다!");
        }
        
        // Rigidbody 설정
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY; // Y축 위치와 회전 고정
            rb.useGravity = false; // 중력 비활성화 (Y축 고정과 함께 사용)
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // 연속 충돌 감지
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsOwner)
        {
            mainCamera = Camera.main;
        }
        else
        {
            // 네트워크에서 동기화될 때는 물리 시스템이 작동하지 않도록 설정
            if (rb != null)
            {
                rb.isKinematic = !IsOwner;
            }
        }
        
        lastServerPosition = transform.position;
    }

    private void Update()
    {
        if (IsOwner)
        {
            // 로컬 플레이어 회전
            HandleRotation();
        }
        else
        {
            // 소유자가 아닐 경우 네트워크 위치로 보간
            transform.position = Vector3.Lerp(transform.position, networkPosition.Value, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation.Value, Time.deltaTime * 10f);
        }
    }
    
    private void FixedUpdate()
    {
        if (IsOwner)
        {
            // 로컬 플레이어 이동 (물리 업데이트에서 실행)
            MovePlayer();
            
            // 서버에 위치 업데이트 전송 (매 프레임이 아닌 일정 간격으로)
            if (Vector3.Distance(transform.position, lastServerPosition) > 0.1f)
            {
                UpdatePositionServerRpc(transform.position, transform.rotation);
                lastServerPosition = transform.position;
            }
        }
    }

    public void SetMovementInput(Vector2 input)
    {
        movementInput = input;
    }

    private void MovePlayer()
    {
        if (rb == null || mainCamera == null) return;
        
        // 카메라 기준 이동 방향 계산
        Vector3 forward = mainCamera.transform.forward;
        Vector3 right = mainCamera.transform.right;
        
        // 이동이 수평면에서만 이루어지도록 함
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        
        // 이동 벡터 생성
        Vector3 moveDirection = (right * movementInput.x + forward * movementInput.y).normalized;
        
        // 이동 적용
        if (moveDirection.magnitude > 0.1f)
        {
            // 새로운 속도 계산 (Y축 속도는 0으로 유지)
            Vector3 newVelocity = moveDirection * moveSpeed;
            newVelocity.y = 0f; // Y축은 고정
            
            // 속도 적용
            rb.linearVelocity = newVelocity;
        }
        else
        {
            // 이동 입력이 없을 때는 속도를 0으로 설정
            rb.linearVelocity = Vector3.zero;
        }
    }
    
    private void HandleRotation()
    {
        // 이동 방향이 있을 때만 회전
        if (movementInput.magnitude > 0.1f)
        {
            // 카메라 기준 이동 방향 계산
            Vector3 forward = mainCamera.transform.forward;
            Vector3 right = mainCamera.transform.right;
            
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            
            Vector3 moveDirection = (right * movementInput.x + forward * movementInput.y).normalized;
            
            // 이동 방향으로 캐릭터 회전
            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }
    
    public Vector3 GetCurrentVelocity()
    {
        if (rb != null)
        {
            return rb.linearVelocity;
        }
        return Vector3.zero;
    }

    public float GetMaxSpeed()
    {
        return moveSpeed;
    }

    [ServerRpc]
    private void UpdatePositionServerRpc(Vector3 position, Quaternion rotation)
    {
        // 위치 유효성 검사 (선택 사항)
        if (Vector3.Distance(position, networkPosition.Value) > 10f)
        {
            // 잠재적 치트나 오류, 플레이어를 원래 위치로 되돌림
            TeleportClientRpc(networkPosition.Value, networkRotation.Value);
            return;
        }
        
        // 네트워크 변수 업데이트
        networkPosition.Value = position;
        networkRotation.Value = rotation;
    }

    [ClientRpc]
    private void TeleportClientRpc(Vector3 position, Quaternion rotation)
    {
        if (IsOwner)
        {
            // Rigidbody의 속도를 0으로 리셋하고 위치를 설정
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }
            transform.position = position;
            transform.rotation = rotation;
        }
    }
}