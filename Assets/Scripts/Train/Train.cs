using System;
using System.Collections;
using Sound;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

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

    [Header("기차 속도")] protected float Speed => manager.Speed;

    [Header("앞/뒤 기차")] [SerializeField] protected Train frontTrainCar;
    [SerializeField] protected Train backTrainCar;
    public bool IsTail => backTrainCar == null;

    [Header("기차 몸통 / 파괴 오브젝트 / 불 이펙트 / 경고(캔버스) 오브젝트")] [SerializeField]
    protected GameObject trainObject;

    [SerializeField] protected GameObject destroyObject;
    [SerializeField] protected GameObject fire;
    [SerializeField] protected GameObject warning;
    [SerializeField, Tooltip("Head에만 있음")] protected ParticleSystem smoke;
    public GameObject[] countdownObject;

    [Header("기차 프리팹")] [SerializeField] protected GameObject headPrefab;
    [SerializeField] protected GameObject waterTankPrefab;
    [SerializeField] protected GameObject chestBoxPrefab;
    [SerializeField] protected GameObject craftDeskPrefab;

    [Header("사운드")] [SerializeField] protected AudioClip hornSound;
    [SerializeField] protected AudioClip destroySound;
    [SerializeField] protected AudioClip countdownSound;
    [SerializeField] protected AudioClip fireSound;
    [SerializeField] protected AudioClip warningSound;
    [SerializeField] protected AudioClip engineSound;

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

    private bool isPaused = false;
    private Coroutine movementCoroutine;
    private Camera _camera;

    #endregion

    #region 초기화

    private void Awake()
    {
        _camera = Camera.main;
    }

    public void InitTrain()
    {
        InitManager(); //매니저를 등록
        InitPrefabs(); //프리팹을 등록
        InitPosition(); //위치 설정
        InitObject(); //오브젝트를 셋팅
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
            trainPos.x -= index == 1 ? 2.13f : 1.26f;
            if (index == 1) trainPos -= (HEAD_OFFSET - OTHER_OFFSET);
            transform.position = trainPos;
        }
        else
        {
            UIManager.Instance.gameUI.InitDistance(transform.position.x);
            // Vector3 trainPos = new Vector3(3, 0, 9);
            // trainPos += HEAD_OFFSET;
            // transform.position = trainPos;
        }
    }

    private void InitObject()
    {
        StopSmoke();
        trainObject.SetActive(true);
        destroyObject.SetActive(false);
        fire.SetActive(false);
        warning.SetActive(false);
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

    #region 명령

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

    public void CallCountdown(int idx)
    {
        if (countdownObject is not { Length: > 0 }) return;

        if (idx == 0)
        {
            Debug.LogWarning("CallCountdown은 1-Index로 취급할 것!!");
            return;
        }

        for (int i = 0; i < countdownObject.Length; i++)
        {
            if (i == 0) continue;
            countdownObject[i].SetActive(i == idx);
        }

        SoundManager.Instance.PlaySound(countdownSound);
    }

    private void DestroyTrain()
    {
        if (IsTail)
        {
            SoundManager.Instance.PlayBGM(SoundManager.Instance.bgmClips[0], 0.5f); //마지막 열차가 파괴되면 bgm을 변경
            _camera.GetComponent<CameraController>().GameOverCameraMoving();
        }

        trainObject.SetActive(false);
        destroyObject.SetActive(true);
        SoundManager.Instance.PlaySound(destroySound);
        GetComponent<Collider>().enabled = false;
        StartCoroutine(CameraShake(0.5f, 0.15f));
    }

    public void RecallCountdown()
    {
        if (countdownObject is not { Length: > 0 }) return;

        for (int i = 0; i < countdownObject.Length; i++)
        {
            if (i == 0) continue;
            countdownObject[i].SetActive(false);
        }
    }

    private void UpdateTrainCar(TrainType trainType)
    {
    }

    public void PlaySpawnAnimation(float spawnOffset)
    {
        // StartCoroutine(SpawnCoroutine(spawnOffset));

        float destOffset = this is Train_Head ? HEAD_OFFSET.y : OTHER_OFFSET.y;
        if (index == 3)
        {
            //마지막 열차의 경우엔 소환된 이후 출발 카운트다운을 진행
            this.PlaySpawnToGround(spawnOffset, destOffset,
                duration: 2.5f, delay: 0f,
                onComplete: () => manager.StartTrainCount());
        }
        else
        {
            this.PlaySpawnToGround(spawnOffset, destOffset);
        }
    }

    private void StartSmoke()
    {
        if (this is not Train_Head) return;
        smoke.Play();
    }

    private void StopSmoke()
    {
        if (this is not Train_Head) return;
        smoke.Stop();
    }

    #endregion

    #region 기능

    private IEnumerator PlayHornSound()
    {
        SoundManager.Instance.PlaySound(hornSound, volume: 0.1f);
        yield return new WaitForSeconds(hornSound.length - 0.15f);
        SoundManager.Instance.PlaySoundsSeq(engineSound, "Engine", loop: true, volume: 0.1f);
    }

    private IEnumerator CameraShake(float duration, float magnitude)
    {
        if (_camera)
        {
            Vector3 originalPos = _camera.transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude * 0.5f;
                _camera.transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
                elapsed += Time.deltaTime;
                yield return null;
            }

            _camera.transform.localPosition = originalPos;
        }
    }

    private IEnumerator MoveToRail(bool isHead)
    {
        StartCoroutine(PlayHornSound());
        StartSmoke();
        bool pause = false;

        while (true)
        {
            // 일시정지 처리
            while (isPaused)
            {
                if (!pause)
                {
                    pause = true;
                    SoundManager.Instance.StopSoundWithTag("Engine");
                    if (isHead) StopSmoke();
                }

                yield return null;
            }

            if (pause)
            {
                pause = false;
                StartCoroutine(PlayHornSound());
                if (isHead) StartSmoke();
            }

            if (!destinationRail)
            {
                Debug.LogWarning("목표 레일 없음");
                break;
            }

            float offsetY = isHead ? HEAD_OFFSET.y : OTHER_OFFSET.y;
            Vector3 currentCenter = new Vector3(
                destinationRail.transform.position.x,
                offsetY,
                destinationRail.transform.position.z
            );

            if (destinationRail.nextRail)
            {
                var nextRc = destinationRail.nextRail.GetComponent<RailController>();
                if (!nextRc)
                {
                    Debug.LogError("[MoveToRail] 다음 레일에 RailController 없음");
                    yield break;
                }

                Vector3 nextCenter = new Vector3(
                    nextRc.transform.position.x,
                    offsetY,
                    nextRc.transform.position.z
                );
                Vector3 dir = (nextCenter - currentCenter).normalized;
                Quaternion targetRot = Quaternion.LookRotation(dir);

                // 머리(앞칸) 목표 위치
                float cellOffset = isHead ? 0.5f : 0f;
                Vector3 targetHead = isHead
                    ? currentCenter + (targetRot * Vector3.forward * cellOffset)
                    : nextCenter;

                // 분기 호출
                if (isHead)
                    yield return StartCoroutine(
                        MoveHeadSegment(nextCenter, targetRot)
                    );
                else
                    yield return StartCoroutine(
                        MoveOneSegment(targetHead, targetRot)
                    );

                SetDestinationRail(nextRc);
                if (isHead) UIManager.Instance.gameUI.UpdateDistance(transform.position.x);
            }
            else
            {
                // 마지막 레일: pivot→currentCenter, head→currentCenter+forward*offset
                Vector3 lastCenter = currentCenter;
                Quaternion lastRot = transform.rotation;
                float cellOffset = isHead ? 0.5f : 0f;
                Vector3 targetHead = isHead
                    ? lastCenter + (lastRot * Vector3.forward * cellOffset)
                    : lastCenter;

                if (isHead)
                    yield return StartCoroutine(
                        MoveHeadSegment(lastCenter, lastRot)
                    );
                else
                    yield return StartCoroutine(
                        MoveOneSegment(targetHead, lastRot)
                    );

                // 게임오버
                if (isHead)
                {
                    GameManager.Instance.GameOver();
                    manager.GameOver();
                }

                DestroyTrain();
                break;
            }
        }

        movementCoroutine = null;
    }

    // 1칸짜리 이동, 회전
    private IEnumerator MoveOneSegment(Vector3 targetHead, Quaternion targetRot)
    {
        const float angularSpeedFactor = 90f;
        while (true)
        {
            if (isPaused)
            {
                yield return null;
                continue;
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetHead,
                Speed * Time.deltaTime
            );
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                Speed * angularSpeedFactor * Time.deltaTime
            );

            if (Vector3.Distance(transform.position, targetHead) < 0.01f &&
                Quaternion.Angle(transform.rotation, targetRot) < 0.5f)
                break;

            yield return null;
        }

        transform.position = targetHead;
        transform.rotation = targetRot;
    }

    //head용 이동 로직
    private IEnumerator MoveHeadSegment(Vector3 nextCenter, Quaternion targetRot)
    {
        float cellOffset = 0.5f;
        float heightY = HEAD_OFFSET.y;

        Vector3 forward = transform.rotation * Vector3.forward;
        Vector3 startPivot = transform.position - forward * cellOffset;
        startPivot = new Vector3(startPivot.x, heightY, startPivot.z);

        Vector3 endPivot = new Vector3(nextCenter.x, heightY, nextCenter.z);

        float segmentLen = Vector3.Distance(startPivot, endPivot);
        float duration = segmentLen / Speed;
        float elapsed = 0f;
        Quaternion startRot = transform.rotation;

        while (elapsed < duration)
        {
            if (isPaused)
            {
                yield return null;
                continue;
            }

            float t = elapsed / duration;

            Vector3 pivotPos = Vector3.Lerp(startPivot, endPivot, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            transform.position = pivotPos + (transform.rotation * Vector3.forward * cellOffset);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRot;
        transform.position = endPivot + (transform.rotation * Vector3.forward * cellOffset);
    }
    
    #endregion
}