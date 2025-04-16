using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class Train : NetworkBehaviour
{
    #region 변수 선언

    protected enum TrainType
    {
        Head,      // 기차 머리
        WaterTank, // 물탱크
        ChestBox,  // 재료보관함
        CraftDesk, // 작업대
    }

    [SerializeField] private TrainManager manager; // 모든 기차를 관리하는 매니저

    [Header("기차 번호 (0~)")]
    [SerializeField]
    protected int index; // 기차 번호(머리는 고정으로 0)

    [Header("기차 속도")]
    protected float Speed => manager.speed;

    [Header("앞/뒤 기차")]
    [SerializeField] protected Train frontTrainCar;
    [SerializeField] protected Train backTrainCar;

    [Header("기차 프리팹")]
    [SerializeField] protected GameObject headPrefab;
    [SerializeField] protected GameObject waterTankPrefab;
    [SerializeField] protected GameObject chestBoxPrefab;
    [SerializeField] protected GameObject craftDeskPrefab;

    // 위치 오프셋
    private readonly Vector3 HEAD_OFFSET = new(0, 0.95f, 0);
    private readonly Vector3 OTHER_OFFSET = new(0, 0.8f, 0);

    private RailController _passedRail;      // 지나친 레일
    private RailController _destinationRail; // 목적지 레일

    // 기차 타입
    private TrainType _type;

    protected virtual TrainType Type
    {
        get => _type;
        set
        {
            if (value == _type) return;
            _type = value;
            UpdateTrainCar(_type);
        }
    }

    // 이동 일시정지를 위한 플래그와 코루틴 참조
    private bool isPaused = false;
    private Coroutine movementCoroutine;

    #endregion

    #region 초기화

    public void InitTrain()
    {
        InitManager();
        InitPrefabs();
        InitPosition();
        InitTrainCar();
    }

    private void InitManager()
    {
        SetManager(this is Train_Head ? GetComponent<TrainManager>() : frontTrainCar.GetManager());
        manager.trains.TryAdd(index, this);
        // Debug.Log($"Train등록: {index}, {name}");
    }

    private void InitPrefabs()
    {
        headPrefab = Resources.Load<GameObject>("TrainPrefabs/Train_Head");
        waterTankPrefab = Resources.Load<GameObject>("TrainPrefabs/Train_Water_Tank");
        chestBoxPrefab = Resources.Load<GameObject>("TrainPrefabs/Train_Chest_Box");
        craftDeskPrefab = Resources.Load<GameObject>("TrainPrefabs/Train_Craft_Desk");
    }

    private void InitPosition()
    {
        if (this is Train_Head)
        {
            Vector3 tempPos = new Vector3(3, 0, 9);
            tempPos += HEAD_OFFSET;
            transform.position = tempPos;
        }
        else
        {
            Vector3 tempPos = new Vector3(3.2f, 0, 9);
            tempPos.x -= index * 1.1f + 0.7f;
            tempPos += HEAD_OFFSET;
            transform.position = tempPos;
        }
    }

    protected abstract void InitTrainCar();

    #endregion

    #region CRUD

    public TrainManager GetManager()
    {
        TrainManager trainManager = manager ? manager : null;
        return trainManager;
    }

    public void SetManager(TrainManager trainManager)
    {
        manager = trainManager;
    }

    public void SetTrainIndex(int idx)
    {
        index = idx;
    }

    public void SetFrontTrainCar(Train train)
    {
        frontTrainCar = train;
    }

    public void SetBackTrainCar(Train train)
    {
        backTrainCar = train;
    }

    public void SetDestinationRail(RailController rail)
    {
        if (_destinationRail)
            _passedRail = _destinationRail;
        _destinationRail = rail;
    }

    #endregion

    /// <summary>
    /// StartTrain이 호출되면 일시 정지 플래그를 해제하고, 이동 코루틴이 실행 중이 아니라면 시작합니다.
    /// 이미 실행중인 경우에는 isPaused 플래그가 false가 되어 루프가 재개됩니다.
    /// </summary>
    public void StartTrain()
    {
        isPaused = false;
        if (_destinationRail)
        {
            bool isHead = this is Train_Head;
            if (movementCoroutine == null)
            {
                movementCoroutine = StartCoroutine(MoveToRail(isHead));
            }
        }
    }

    /// <summary>
    /// StopTrain이 호출되면 이동 코루틴 내 루프에서 isPaused를 true로 체크하여 일시 정지합니다.
    /// </summary>
    public void StopTrain()
    {
        isPaused = true;
    }

    private IEnumerator MoveToRail(bool isHead)
    {
        while (_destinationRail)
        {
            // 일시 정지 상태면 계속 대기
            while (isPaused)
                yield return null;

            if (_destinationRail.nextRail)
            {
                RailController nextRailController = _destinationRail.nextRail.GetComponent<RailController>();
                if (!nextRailController)
                {
                    Debug.LogError("[MoveToRail] 다음 레일에 RailController 컴포넌트가 없음");
                    break;
                }

                // 현재 rail 중심(A)와 다음 rail 중심(B) 계산 (Y는 HEAD_OFFSET 적용)
                Vector3 currentCenter = new Vector3(_destinationRail.transform.position.x, HEAD_OFFSET.y,
                    _destinationRail.transform.position.z);
                Vector3 nextCenter = new Vector3(nextRailController.transform.position.x, HEAD_OFFSET.y,
                    nextRailController.transform.position.z);

                // 다음 rail로의 이동 방향과 목표 회전 계산 (레일의 연결 방향)
                Vector3 nextDirection = (nextCenter - currentCenter).normalized;
                Quaternion targetRotation = Quaternion.LookRotation(nextDirection);

                // 목표 머리 위치는 열차 타입에 따라 다르게 설정:
                //  - 2칸 열차 (Train_Head): rail 중심에서 앞으로 cellOffset(=1)만큼 이동한 위치
                //  - 1칸 열차: 바로 다음 rail의 중심이 목표 위치가 됨
                float cellOffset = isHead ? 1f : 0f;
                Vector3 targetHead = isHead
                    ? currentCenter + (targetRotation * Vector3.forward * cellOffset)
                    : nextCenter;

                // 이동 및 회전 코루틴 호출
                yield return StartCoroutine(MoveAndRotate(targetHead, targetRotation, isHead));

                // 이동 완료 후, 다음 rail로 업데이트
                SetDestinationRail(nextRailController);
            }
            else
            {
                // 다음 rail이 없는 경우, 마지막 rail 중심으로 머리를 이동
                Vector3 currentCenter = new Vector3(_destinationRail.transform.position.x, HEAD_OFFSET.y,
                    _destinationRail.transform.position.z);
                while (Vector3.Distance(transform.position, currentCenter) > 0.01f)
                {
                    while (isPaused)
                        yield return null;

                    transform.position = Vector3.MoveTowards(transform.position, currentCenter, Speed * Time.deltaTime);
                    yield return null;
                }

                Debug.Log("[MoveToRail] 레일 끝에 도달했습니다.");
                break;
            }
        }
        // 코루틴 종료 시 코루틴 참조를 초기화
        movementCoroutine = null;
    }

    /// <summary>
    /// 머리와 꼬리의 목표 위치 보간을 통해 열차의 회전 동안 자연스러운 이동을 구현한다.
    /// - 2칸 열차 (Train_Head): 꼬리를 rail 중심에 고정시켜 머리와의 오프셋(1)을 유지하며 보간함.
    /// - 1칸 열차: 단순 선형 보간을 통해 현재 위치에서 목표 rail 중심으로 이동하고 회전함.
    /// </summary>
    /// <param name="targetHead">목표 머리 위치 (2칸: rail 중심 + 오프셋, 1칸: 다음 rail의 중심)</param>
    /// <param name="targetRot">목표 회전 (다음 rail의 연결 방향)</param>
    /// <param name="isHead">Train_Head(2칸) 여부. true이면 2칸 열차, false이면 1칸 열차</param>
    private IEnumerator MoveAndRotate(Vector3 targetHead, Quaternion targetRot, bool isHead)
    {
        // 목표 보간 시간 계산
        float duration = Mathf.Max(Vector3.Distance(transform.position, targetHead) / Speed,
            Quaternion.Angle(transform.rotation, targetRot) / (Speed * 90f));
        float elapsed = 0f;
        Vector3 startHead = transform.position;
        Quaternion startRot = transform.rotation;

        if (isHead)
        {
            // 2칸 열차의 경우: 꼬리 보간을 사용하여 머리와 꼬리 오프셋(1)을 유지함
            float cellOffset = 1f;
            // 현재 rail 중심 (A)는 꼬리가 머무를 목표 rail의 중심
            Vector3 currentRailCenter = new Vector3(_destinationRail.transform.position.x, HEAD_OFFSET.y,
                _destinationRail.transform.position.z);
            // 현재 꼬리 위치 (T): 머리 기준, 로컬 (0,0,-cellOffset)
            Vector3 startTail = startHead + (startRot * new Vector3(0, 0, -cellOffset));
            // 최종 꼬리 목표는 rail 중심
            Vector3 endTail = currentRailCenter;

            while (elapsed < duration)
            {
                while (isPaused)
                    yield return null;

                float t = elapsed / duration;
                Quaternion newRot = Quaternion.Slerp(startRot, targetRot, t);
                Vector3 tailAnchor = Vector3.Lerp(startTail, endTail, t);
                Vector3 newHeadPos = tailAnchor + (newRot * (Vector3.forward * cellOffset));

                transform.rotation = newRot;
                transform.position = newHeadPos;

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            // 1칸 열차의 경우: 단순 선형 보간 (피봇이 rail 중심에 위치)
            while (elapsed < duration)
            {
                while (isPaused)
                    yield return null;

                float t = elapsed / duration;
                transform.position = Vector3.Lerp(startHead, targetHead, t);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        // 최종 보정
        transform.rotation = targetRot;
        transform.position = targetHead;
    }

    private void UpdateTrainCar(TrainType trainType)
    {
        // 기차 카/프리팹 업데이트 로직을 작성합니다.
    }
}
