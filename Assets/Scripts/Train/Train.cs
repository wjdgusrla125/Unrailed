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

    [Header("기차 몸통 / 파괴 오브젝트 / 불 이펙트 / 경고(캔버스) 오브젝트")] 
    [SerializeField] protected GameObject trainObject;
    [SerializeField] protected GameObject destroyObject;
    [SerializeField] protected GameObject fire;
    [SerializeField] protected GameObject warning;
    [SerializeField, Tooltip("Head에만 있음")] protected ParticleSystem smoke;
    public GameObject[] countdownObject;

    [Header("기차 프리팹")] [SerializeField] protected GameObject headPrefab;
    [SerializeField] protected GameObject waterTankPrefab;
    [SerializeField] protected GameObject chestBoxPrefab;
    [SerializeField] protected GameObject craftDeskPrefab;

    [Header("사운드")] 
    [SerializeField] protected AudioClip hornSound;
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

    // 이동 일시정지를 위한 플래그와 코루틴 참조
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
        if(IsTail)
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
        StartCoroutine(SpawnCoroutine(spawnOffset));
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
            while (isPaused)
            {
                if(!pause)
                {
                    pause = true;
                    SoundManager.Instance.StopSoundWithTag("Engine");
                    StopSmoke();
                }
                yield return null;
            }

            if (pause)
            {
                pause = false;
                StartCoroutine(PlayHornSound()); //멈췄다가 출발하면 다시 경적소리를 낸다.
                StartSmoke();
            }

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
                    if (isHead) UIManager.Instance.gameUI.UpdateDistance(transform.position.x);
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
                    if (this is Train_Head)
                    {
                        GameManager.Instance.GameOver();
                        manager.GameOver();
                    }

                    DestroyTrain();
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
        const float angularSpeedFactor = 90f;
        float cellOffset = isHead ? 1f : 0f;
        bool pause = false;

        while (true)
        {
            // 일시정지 처리
            while (isPaused)
            {
                if(!pause)
                {
                    Debug.Log("멈춤");
                    pause = true;
                    SoundManager.Instance.StopSoundWithTag("Engine");
                    StopSmoke();
                }
                yield return null;
            }

            if (pause)
            {
                Debug.Log("재출발");
                pause = false;
                StartCoroutine(PlayHornSound()); //멈췄다가 출발하면 다시 경적소리를 낸다.
                StartSmoke();
            }

            if (isHead)
            {
                Vector3 currentRailCenter = new Vector3(
                    destinationRail.transform.position.x,
                    HEAD_OFFSET.y,
                    destinationRail.transform.position.z
                );

                Vector3 tailAnchor = Vector3.MoveTowards(
                    transform.position - transform.rotation * Vector3.forward * cellOffset,
                    currentRailCenter,
                    Speed * Time.deltaTime
                );
                transform.position = tailAnchor + (transform.rotation * Vector3.forward * cellOffset);

                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRot,
                    Speed * angularSpeedFactor * Time.deltaTime
                );
            }
            else
            {
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
            }

            if (Vector3.Distance(transform.position, targetHead) < 0.01f
                && Quaternion.Angle(transform.rotation, targetRot) < 0.5f)
            {
                break;
            }

            yield return null;
        }

        transform.position = targetHead;
        transform.rotation = targetRot;
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

    #endregion
}