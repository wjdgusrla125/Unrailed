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
        Head, // 기차 머리
        WaterTank, // 물탱크
        ChestBox, // 재료보관함
        CraftDesk, // 작업대
    }

    [SerializeField] protected TrainManager manager; // 모든 기차를 관리하는 매니저

    [Header("기차 번호 (0~)")] [SerializeField]
    protected int index; // 기차 번호(머리는 고정으로 0)

    [Header("기차 속도")] protected float Speed => manager.speed;

    [Header("앞/뒤 기차")] [SerializeField] protected Train frontTrainCar;
    [SerializeField] protected Train backTrainCar;

    [Header("기차 프리팹")] [SerializeField] protected GameObject headPrefab;
    [SerializeField] protected GameObject waterTankPrefab;
    [SerializeField] protected GameObject chestBoxPrefab;
    [SerializeField] protected GameObject craftDeskPrefab;

    // 위치 오프셋
    private readonly Vector3 HEAD_OFFSET = new(0, 0.5f, 0);
    private readonly Vector3 OTHER_OFFSET = new(0, 0.35f, 0);

    private RailController _passedRail; // 지나친 레일
    [SerializeField, Header("확인용")] protected RailController destinationRail; // 목적지 레일

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
        if (NetworkManager.Singleton.IsServer) InitTrainCar();
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
        if (this is not Train_Head)
        {
            Vector3 trainPos = frontTrainCar.transform.position;
            trainPos.x -= index == 1 ? 1.6f : 0.95f;
            // trainPos += OTHER_OFFSET;
            transform.position = trainPos;
        }
        else
        {
            // Vector3 trainPos = new Vector3(3, 0, 9);
            // trainPos += HEAD_OFFSET;
            // transform.position = trainPos;
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
        if (destinationRail)
            _passedRail = destinationRail;
        destinationRail = rail;
    }

    #endregion

    public void StartTrain()
    {
        isPaused = false;
        if (destinationRail)
        {
            bool isHead = this is Train_Head;
            if (movementCoroutine == null)
            {
                movementCoroutine = StartCoroutine(MoveToRail(isHead));
            }
            else
            {
                Debug.Log("이미 코루틴 진행중");
            }
        }
        else
        {
            Debug.Log("목적지 없음");
        }
    }

    public void StopTrain()
    {
        isPaused = true;
    }

    private IEnumerator MoveToRail(bool isHead)
    {
        while (true)
        {
            while (isPaused)
                yield return null;

            if (destinationRail)
            {
                float offsetY = this is Train_Head ? HEAD_OFFSET.y : OTHER_OFFSET.y;
                Vector3 currentCenter = new Vector3(
                    destinationRail.transform.position.x,
                    offsetY,
                    destinationRail.transform.position.z
                );

                if (destinationRail.nextRail)
                {
                    var nextRailController = destinationRail.nextRail.GetComponent<RailController>();
                    if (!nextRailController)
                    {
                        Debug.LogError("[MoveToRail] 다음 레일에 RailController 컴포넌트가 없음");
                        yield break;
                    }

                    Vector3 nextCenter = new Vector3(
                        nextRailController.transform.position.x,
                        offsetY,
                        nextRailController.transform.position.z
                    );
                    Vector3 nextDirection = (nextCenter - currentCenter).normalized;
                    Quaternion targetRotation = Quaternion.LookRotation(nextDirection);
                    Vector3 targetHead = isHead
                        ? currentCenter + (targetRotation * Vector3.forward * 1f)
                        : nextCenter;

                    yield return StartCoroutine(MoveAndRotate(targetHead, targetRotation, isHead));
                    SetDestinationRail(nextRailController);
                }
                else
                {
                    // 다음 레일 없음: 마지막 레일 중심까지 이동 후 게임오버 판단
                    if (Vector3.Distance(transform.position, currentCenter) > 0.01f)
                    {
                        transform.position = Vector3.MoveTowards(
                            transform.position,
                            currentCenter,
                            Speed * Time.deltaTime
                        );
                        yield return null;
                        continue;
                    }

                    // 이미 중심에 도착했는데 nextRail이 추가되지 않음: 게임오버
                    Debug.Log("게임오버");
                    break;
                }
            }
            else
            {
                // 목적지 레일 자체가 없을 때
                Debug.LogWarning("목표 레일이 없음");
                break;
            }
        }

        movementCoroutine = null;
    }

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
            float cellOffset = 1f;
            Vector3 currentRailCenter = new Vector3(destinationRail.transform.position.x, HEAD_OFFSET.y,
                destinationRail.transform.position.z);
            Vector3 startTail = startHead + (startRot * new Vector3(0, 0, -cellOffset));
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
    }

    public void PlaySpawnAnimation(float spawnOffset)
    {
        StartCoroutine(SpawnCoroutine(spawnOffset));
    }

    //스폰 애니메이션
    private IEnumerator SpawnCoroutine(float spawnOffset)
    {
        Vector3 finalPos = transform.position + Vector3.down * spawnOffset;

        float moveDuration = 2.5f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            float easedT = EaseOutQuart(t);
            transform.position = Vector3.Lerp(startPos, finalPos, easedT);
            yield return null;
        }

        transform.position = finalPos;

        if (index == 3) //마지막 열차가 소환된 이후 열차 출발 카운트다운 시작
        {
            manager.StartTrainCount();
        }
    }

    private float EaseOutQuart(float t)
    {
        return 1f - Mathf.Pow(1f - t, 4f);
    }
}