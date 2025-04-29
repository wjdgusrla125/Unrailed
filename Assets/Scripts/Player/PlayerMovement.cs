using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 5f;      // 이동 속도
    [SerializeField] private float rotationSpeed = 10f; // 회전 속도
    
    [Header("충돌 설정")]
    [SerializeField] private LayerMask obstacleLayer;   // 장애물 레이어
    [SerializeField] private float skinWidth = 0.05f;   // 충돌 감지 여유 공간
    
    [Header("네트워크 변수")]
    private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>();
    
    // 로컬 이동 입력
    private Vector2 movementInput;
    private Rigidbody rb;
    private BoxCollider boxCollider;
    private Camera mainCamera;
    private Vector3 lastServerPosition;
    private Vector3 currentVelocity;

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
            rb.isKinematic = true; // 물리 엔진 영향을 받지 않도록 설정
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsOwner)
        {
            mainCamera = Camera.main;
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
        // 이동 방향 계산
        Vector3 moveDirection = new Vector3(movementInput.x, 0f, movementInput.y).normalized;
        
        if (moveDirection.magnitude > 0.1f)
        {
            // 목표 속도 계산
            Vector3 targetVelocity = moveDirection * moveSpeed;
            
            // 현재 속도에서 목표 속도로 부드럽게 전환
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, 0.3f);
        }
        else
        {
            // 입력이 없을 때 속도 감소
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, 0.2f);
        }
        
        // 분리된 축으로 이동 처리 (X와 Z 분리)
        Vector3 movement = currentVelocity * Time.fixedDeltaTime;
        
        // X축과 Z축을 분리하여 이동 및 충돌 검사
        MoveInDirection(new Vector3(movement.x, 0, 0)); // X축 이동
        MoveInDirection(new Vector3(0, 0, movement.z)); // Z축 이동
    }
    
    private void MoveInDirection(Vector3 movement)
    {
        if (movement.magnitude < 0.001f) return; // 미세한 이동은 무시
        
        // 이동 방향
        Vector3 direction = movement.normalized;
        float distance = movement.magnitude;
        
        // 박스 캐스트를 위한 사이즈와 중심 계산
        Vector3 boxCenter = boxCollider.center + transform.position;
        Vector3 boxSize = boxCollider.size - new Vector3(skinWidth * 2, skinWidth * 2, skinWidth * 2); // 여유 공간 제외
        
        // 박스 캐스트로 충돌 검사
        RaycastHit hit;
        bool collided = Physics.BoxCast(
            boxCenter, 
            boxSize * 0.5f, 
            direction, 
            out hit, 
            transform.rotation, 
            distance + skinWidth, 
            obstacleLayer
        );
        
        if (collided)
        {
            // 충돌한 지점까지만 이동 (여유 공간 고려)
            float moveDistance = Mathf.Max(0, hit.distance - skinWidth);
            transform.position += direction * moveDistance;
            
            // 충돌한 축의 속도를 0으로 설정
            if (Mathf.Abs(direction.x) > 0.5f)
            {
                currentVelocity.x = 0;
            }
            if (Mathf.Abs(direction.z) > 0.5f)
            {
                currentVelocity.z = 0;
            }
        }
        else
        {
            // 충돌하지 않으면 전체 거리 이동
            transform.position += movement;
        }
    }
    
    private void HandleRotation()
    {
        // 이동 방향이 있을 때만 회전
        if (movementInput.magnitude > 0.1f)
        {
            // 월드 좌표계 기준 이동 방향 계산
            Vector3 moveDirection = new Vector3(movementInput.x, 0f, movementInput.y).normalized;
            
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
        return currentVelocity;
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
            // 속도를 0으로 리셋하고 위치를 설정
            currentVelocity = Vector3.zero;
            transform.position = position;
            transform.rotation = rotation;
        }
    }
}