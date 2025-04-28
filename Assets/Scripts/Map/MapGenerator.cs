using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class MapGenerator : SingletonManager<MapGenerator>
{
    #region 변수 선언 및 직렬화 필드

    [SerializeField, Header("맵 시드값(비울 경우 랜덤)")]
    private string mapSeed;

    private System.Random _masterRng; // 랜덤 시드 지정

    private bool _isMapGenerating = false;
    public Action<bool> IsMapGenerated; // 맵 생성 성공 여부 전달
    public GameObject gameOverObj;
    private Train _trainHead;
    private CameraController _camera;

    [Header("맵 크기 설정")] [SerializeField] private int width; // 맵의 가로 길이
    [SerializeField] private int height; // 맵의 세로 길이
    [SerializeField] private int pathLength; // 출발지점과 도착지점 사이의 거리
    [SerializeField] private int minHorizontalDistance; // 출발지점과 도착지점 사이의 최소 가로 거리
    [SerializeField] private int maxGrassTileCount; // 풀 타일 최대치
    [SerializeField] private int minGrassTileCount; // 풀 타일 최소치
    private int _curWidth; //현재 맵의 총 길이

    [Header("나무, 철 관련 설정")] [SerializeField]
    private int destructibleClusterCount; // 맵에 최소한 존재해야 하는 각 자원 클러스터의 양

    [SerializeField] private int minDestructibleClusterSize; // 클러스터 1개당 최소 크기
    [SerializeField] private int minWoodCount; // 맵에 최소한 존재해야 하는 나무의 양
    [SerializeField] private int minIronCount; // 맵에 최소한 존재해야 하는 철의 양

    [Header("산 관련 설정")] [SerializeField] private int mountainClusterCountMin; // 최소 산 개수
    [SerializeField] private int mountainClusterCountMax; // 최대 산 개수
    [SerializeField] private int mountainClusterSizeMin; // 산 하나당 최소 타일 개수
    [SerializeField] private int mountainClusterSizeMax; // 산 하나당 최대 타일 개수

    [Header("강 관련 설정")] [SerializeField] private int minRiverCount = 2; // 최소 강 개수
    [SerializeField] private int maxRiverCount = 3; // 최대 강 개수
    [SerializeField] private int minRiverLength = 8; // 강의 최소 셀 크기
    [SerializeField] private int maxRiverCellsAllowed = 35; // 강의 최대 셀 크기

    [SerializeField] private float lateralSpreadProbability = 0.4f; // 강 확산 확률
    // [SerializeField] private int elongatedRiverMinWidth = 1; // 강 최소 폭
    // [SerializeField] private int elongatedRiverMaxWidth = 2; // 강 최대 폭

    [Header("프리팹")] [SerializeField] private Transform clusterParentPrefab; // 생성된 클러스터들을 담을 오브젝트
    [SerializeField] private Transform oldMapParentPrefab; // 이전 라운드의 맵을 담을 오브젝트
    [SerializeField] private GameObject clusterPrefab; //생성된 블럭들을 클러스터별로 정리할 빈 프리팹
    [SerializeField] private GameObject grass0Prefab; //grass: 랜덤(5%로 grass1)
    [SerializeField] private GameObject grass1Prefab;
    [SerializeField] private GameObject wood0Prefab; //wood: 랜덤(50%)
    [SerializeField] private GameObject wood1Prefab;
    [SerializeField] private GameObject gameOverPrefab;


    [SerializeField]
    private GameObject iron0Prefab; //iron: 고정(스폰 시 주변 4방향이 iron으로 둘러싸여 있을 경우 iron0, 한 면이라도 다른 타일이 있을 경우 iron1)

    [SerializeField] private GameObject iron1Prefab;

    [SerializeField] private GameObject
        mountain0Prefab; //mountain: 고정(스폰 시 주변 4방향이 mountain으로 둘러싸여 있을 경우 mountain0, 한 면이라도 다른 타일이 있을 경우 mountain1)

    [SerializeField] private GameObject mountain1Prefab;
    [SerializeField] private GameObject riverPrefab;
    [SerializeField] private GameObject startPointPrefab;
    [SerializeField] private GameObject endPointPrefab;
    [SerializeField] private GameObject railPrefab;
    [SerializeField] private GameObject trainCarHeadPrefab;


    //오브젝트 소환 오프셋
    [NonSerialized] public static float SPAWN_OFFSET = 20F;
    private Vector3 _railSpawnOffset = new Vector3(0f, 0.53f, 0f);
    private Vector3 _trainSpawnOffset = new Vector3(0f, 0.5f, 0f);

    // 기존 타일 타입 열거형
    public enum TileType
    {
        None,
        Grass,
        Wood,
        Iron,
        Mountain,
        River
    }

    public TileType[,] Map;
    private Vector2Int _posA; // 시작점
    private Vector2Int _posB; // 도착점

    // 디버그용, Q키 쿨다운 (1초)
    private const float GenerateCooldown = 1f;
    private float _lastGenerateTime = -10f;

    // 반복 최대 횟수 상수
    private const int MAX_ITERATIONS = 10000;

    // 타일 좌표별 클러스터 그룹 할당 (각 타일은 반드시 단 하나의 클러스터 그룹에만 속함)
    private Dictionary<Vector2Int, ClusterGroup> _tileClusterGroupMap = new();

    // 종류별 클러스터 그룹 리스트
    private List<ClusterGroup> _specialClusterGroups = new();
    private List<ClusterGroup> _mountainClusterGroups = new();
    private List<ClusterGroup> _riverClusterGroups = new();
    private List<ClusterGroup> _resourceClusterGroups = new(); // Wood, Iron 클러스터
    private List<ClusterGroup> _grassClusterGroups = new();

    //생성된 타일들이 들어갈 부모
    private Transform _clusterParent;
    private Transform _oldMapParent;

    private int _extensionCount = 0; //맵 확장 횟수

    #endregion

    #region Event Function

    protected override void Awake()
    {
        base.Awake();
        _camera = Camera.main.GetComponent<CameraController>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q) && Time.time - _lastGenerateTime > GenerateCooldown)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            _lastGenerateTime = Time.time;
            Debug.Log("Q입력, 시드 랜덤화 후 맵 생성");
            AllTileDespawn();
            AllObjectsDespawn();
            mapSeed = string.Empty;
            StartCoroutine(GenerateMapCoroutine());
        }

        if (Input.GetKeyDown(KeyCode.E) && Time.time - _lastGenerateTime > GenerateCooldown)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            _lastGenerateTime = Time.time;
            Debug.Log("E입력, 시드 유지 맵 생성");
            AllTileDespawn();
            AllObjectsDespawn();
            StartCoroutine(GenerateMapCoroutine());
        }

        if (Input.GetKeyDown(KeyCode.R) && Time.time - _lastGenerateTime > GenerateCooldown)
        {
            if (!NetworkManager.Singleton.IsServer) return;
            _lastGenerateTime = Time.time;
            Debug.Log("R입력, 맵 확장");
            NextMapGeneration();
        }


        if (Input.GetKeyDown(KeyCode.T))
        {
            if (!NetworkManager.Singleton.IsServer) return;
            Debug.Log($"T입력, ({visitX}, {visitY}) visit 확인");
            CheckVisit();
        }
    }

    #endregion

    #region 타일 & 오브젝트 제거

    //모든 타일을 제거한다.
    public void AllTileDespawn()
    {
        List<Transform> children = new List<Transform>();
        for (int i = 0; i < _oldMapParent.childCount; i++)
        {
            children.Add(_oldMapParent.GetChild(i));
        }

        foreach (var t in children)
        {
            Cluster cluster = t.GetComponent<Cluster>();
            if (cluster)
            {
                cluster.DespawnCluster();
            }
            else Debug.LogError("네트워크 오브젝트가 없음.");
        }

        children.Clear();
        for (int i = 0; i < _clusterParent.childCount; i++)
        {
            children.Add(_clusterParent.GetChild(i));
        }

        foreach (var t in children)
        {
            Cluster cluster = t.GetComponent<Cluster>();
            if (cluster)
            {
                cluster.DespawnCluster();
            }
            else Debug.LogError("네트워크 오브젝트가 없음.");
            // Destroy(t.gameObject);
        }
    }

    //모든 오브젝트를 제거한다(타일, 게임오버 오브젝트 제외)
    public void AllObjectsDespawn()
    {
        RailManager.Instance.AllRailsDespawn();
        _trainHead.GetComponent<TrainManager>().AllTrainsDespawn();
    }

    public void GameOverObjectDespawn()
    {
        if (gameOverObj) gameOverObj.GetComponent<NetworkObject>().Despawn();
    }

    #endregion

    #region 맵 생성 메인 코루틴

    public void StartMapGeneration()
    {
        StartCoroutine(GenerateMapCoroutine());
    }

    private IEnumerator GenerateMapCoroutine()
    {
        _camera.ResetCamera(); //카메라를 초기상태로 전환
        
        _isMapGenerating = true;
        try
        {
            InitializeSeed(mapSeed); // 시드 설정
            InitializeMap(); // 맵 초기화
            InstantiateParent();
            SetPath(); // 경로 설정 (출발지와 도착지 결정)
            GenerateValidPath(); // 출발지에서 도착지까지 유효한 경로 생성
            GenerateMountains(); // 산(마운틴) 타일 생성
            EnsureStartEndInnerClear(); // 출발지와 도착지 주변 5x5 영역을 클리어(Grass로 설정)
            GenerateRivers(); // 강(리버) 타일 생성
            PlaceDestructibleObstacles(); // 파괴 가능한 장애물(나무, 철) 배치
            GenerateGrassToMountainClusters(); // Grass 타일을 산 타일로 변환하여 클러스터 생성
            EnsurePathConnectivity(); // 경로 연결성 보장
            EnsureStartEndInnerClear(); // 출발/도착지 주변 클리어 재보강
            EnsureWoodAccessibility(); // 나무(Wood) 클러스터 접근 통로 생성
            CountReachableWoodWithoutRiver(_posA);
            EnsureStartEndInnerClear(); // 출발/도착지 주변 클리어 재보강
            EnsureReachability(); // 도달 불가능한 영역을 산 타일로 전환하여 자연스럽게 함
            EnsureEndpointCorridor(); // 도착점에서 우측 끝까지 통로 확보
            FinalAdjustments(); // 최종 보정 작업 (부족한 자원 보충 등)
            EnsureStartEndInnerClear(); // 출발/도착지 주변 클리어 최종 확인
            AdjustPathForRiverAndWood(); // 강(River)와 나무(Wood)를 고려하여 경로 조정
            FinalizeGrassClusters(); // Grass 클러스터 그룹 최종화 및 분할
            FinalizeUnassignedClusters(); // 미할당 타일들의 클러스터 그룹 생성
            SplitDisconnectedClusterGroups(); // 분리된(연결되지 않은) 클러스터 그룹 분할
            StartCoroutine(InstantiateMap()); // 맵 오브젝트(타일) 생성 및 배치

            _isMapGenerating = false;
            IsMapGenerated?.Invoke(true);
        }
        catch (MapGenerationException ex)
        {
            Debug.LogError("맵 생성 실패: " + ex.Message);
            _isMapGenerating = false;
            IsMapGenerated?.Invoke(false);
        }

        yield break;
    }

    public void NextMapGeneration()
    {
        //카메라의 현재위치(정상적으로 게임이 진행됐다면 새로운 시작위치에 근접)를 이니셜 포지션으로 설정
        if (_camera) _camera.GetComponent<CameraController>().SetInitPosition();

        //기존 oldPath 자식들 전부 삭제
        List<Transform> children = new List<Transform>();
        for (int i = 0; i < _oldMapParent.childCount; i++)
        {
            children.Add(_oldMapParent.GetChild(i));
        }

        foreach (var t in children)
        {
            Cluster cluster = t.GetComponent<Cluster>();
            if (cluster)
            {
                cluster.DespawnCluster();
            }
            else Debug.LogError("네트워크 오브젝트가 없음.");
            // Destroy(t.gameObject);
        }

        // gameOverObj
        if (_oldMapParent.childCount > 0 && gameOverObj)
        {
            //기존 oldMapParent에 자식이 있으면 현재 clusterParent를 기준으로 게임오버 프리팹을 옮긴다.
            Debug.Log("위치 재설정");
            var blocks = _clusterParent.GetComponentsInChildren<Blocks>();
            if (blocks.Length > 0)
            {
                // 가장 왼쪽 타일의 x 좌표를 구해서 -15 만큼 이동
                float minX = blocks.Min(b => b.transform.position.x);
                Vector3 pos = gameOverObj.transform.position;
                pos.x = minX - 15f;
                gameOverObj.transform.position = pos;
            }
        }

        // 기존 맵 오브젝트들을 oldMapParent로 이동
        for (int i = _clusterParent.childCount - 1; i >= 0; i--)
        {
            Transform child = _clusterParent.GetChild(i);
            child.SetParent(_oldMapParent);
        }

        _posA = _posB;
        int oldWidth = _curWidth;
        _extensionCount++;
        // Debug.Log($"확장 후 extensionCount: {_extensionCount}");
        _curWidth = _posA.x + width;
        // Debug.Log($"[NextMapGeneration] curWidth: {_curWidth}");

        // 기존 타일은 그대로 복사하고, 새 영역은 Grass로 초기화
        TileType[,] oldMap = Map;
        Map = new TileType[_curWidth, height];
        for (int x = 0; x < oldWidth; x++)
        for (int y = 0; y < height; y++)
            Map[x, y] = oldMap[x, y];
        for (int x = oldWidth; x < _curWidth; x++)
        for (int y = 0; y < height; y++)
            Map[x, y] = TileType.Grass;

        _tileClusterGroupMap.Clear();
        _specialClusterGroups.Clear();
        _grassClusterGroups.Clear();
        _mountainClusterGroups.Clear();
        _riverClusterGroups.Clear();
        _resourceClusterGroups.Clear();

        // 시드에 extensionCount를 더해서 무작위성을 확보
        int extensionSeed = mapSeed.GetHashCode() + _extensionCount;
        _masterRng = new System.Random(extensionSeed);
        Random.InitState(extensionSeed);

        StartCoroutine(GenerateMapExtensionCoroutine(oldWidth));
    }

    private IEnumerator GenerateMapExtensionCoroutine(int oldWidth)
    {
        _isMapGenerating = true;
        try
        {
            SetPath(_posA.x, oldWidth); // 경로 설정 (출발지와 도착지 결정)
            GenerateValidPath(oldWidth); // 출발지에서 도착지까지 유효한 경로 생성
            GenerateMountains(oldWidth); // 산(마운틴) 타일 생성
            EnsureStartEndInnerClearExtension(oldWidth); // 도착지 주변 5x5 영역을 클리어(Grass로 설정)
            GenerateRivers(oldWidth); // 강(리버) 타일 생성
            PlaceDestructibleObstacles(oldWidth); // 파괴 가능한 장애물(나무, 철) 배치
            GenerateGrassToMountainClusters(oldWidth); // Grass 타일을 산 타일로 변환하여 클러스터 생성
            EnsurePathConnectivity(oldWidth); // 경로 연결성 보장
            EnsureStartEndInnerClearExtension(oldWidth); // 도착지 주변 클리어 재보강

            // EnsureWoodAccessibility(); // 나무(Wood) 클러스터 접근 통로 생성
            // CountReachableWoodWithoutRiver(_posA);
            // EnsureStartEndInnerClearExtension(oldWidth); // 도착지 주변 클리어 재보강
            //재생성 시엔 나무 보장 필요 없음

            ApplyLeftColumnTileTypeCopy(oldWidth); //새로 생성되는 가장 왼쪽타일을 50%확률로 이전 맵의 오른쪽 끝타일과 동일한 타일로 교체

            EnsureReachability(oldWidth); // 도달 불가능한 영역을 산 타일로 전환하여 자연스럽게 함
            EnsureEndpointCorridor(oldWidth); // 도착점에서 우측 끝까지 통로 확보
            FinalAdjustments(oldWidth); // 최종 보정 작업 (부족한 자원 보충 등)
            EnsureStartEndInnerClearExtension(oldWidth); // 도착지 주변 클리어 최종 확인
            // AdjustPathForRiverAndWood(oldWidth); // 강(River)와 나무(Wood)를 고려하여 경로 조정
            //재생성 시엔 강 보정 필요없음

            FinalizeGrassClusters(oldWidth); // Grass 클러스터 그룹 최종화 및 분할
            FinalizeUnassignedClusters(oldWidth); // 미할당 타일들의 클러스터 그룹 생성
            SplitDisconnectedClusterGroups(); // 분리된(연결되지 않은) 클러스터 그룹 분할
            StartCoroutine(InstantiateMap(oldWidth)); // 맵 오브젝트(타일) 생성 및 배치

            _isMapGenerating = false;
            IsMapGenerated?.Invoke(true);
        }
        catch (MapGenerationException ex)
        {
            Debug.LogError("맵 생성 실패: " + ex.Message);
            _isMapGenerating = false;
            IsMapGenerated?.Invoke(false);
        }

        yield break;
    }

    #endregion

    #region 클러스터 그룹 관련

    // 타일을 새 클러스터 그룹에 할당(이미 다른 그룹에 속해있다면 해당 그룹에서 제거 후 재할당)
    private void AssignTileToCluster(Vector2Int tile, ClusterGroup newGroup)
    {
        // 만약 이미 그룹에 할당되어 있다면 제거
        if (_tileClusterGroupMap.TryGetValue(tile, out ClusterGroup oldGroup))
        {
            oldGroup.Tiles.Remove(tile);
            // 그룹 크기가 변경되었으므로 필요시 중앙 타일을 재계산
            oldGroup.RecalculateCenterAndDirection(height);
        }

        // 새 그룹에 추가
        newGroup.Tiles.Add(tile);
        _tileClusterGroupMap[tile] = newGroup;
    }

    // 여러 타일을 모아서 새 클러스터 그룹으로 생성한 후 반환.
    private ClusterGroup CreateClusterGroupFromTiles(List<Vector2Int> tiles)
    {
        ClusterGroup group = new ClusterGroup();
        foreach (var tile in tiles)
        {
            AssignTileToCluster(tile, group);
        }

        group.RecalculateCenterAndDirection(height);
        return group;
    }

    // 타일의 타입 변경에 따른 재할당 메서드.
    public void ReassignTile(Vector2Int tile)
    {
        if (_tileClusterGroupMap.TryGetValue(tile, out ClusterGroup oldGroup))
        {
            oldGroup.Tiles.Remove(tile);
            oldGroup.RecalculateCenterAndDirection(height);
            _tileClusterGroupMap.Remove(tile);
            if (oldGroup.Tiles.Count == 0)
            {
                RemoveGroup(oldGroup);
            }
        }

        ClusterGroup candidateGroup = null;
        foreach (Vector2Int nb in GetCardinalNeighbors(tile))
        {
            if (IsInBounds(nb))
            {
                // 타일의 타입이 동일하고 인접 타일에 그룹 할당이 되어 있다면 candidateGroup 으로 선정
                if (Map[nb.x, nb.y] == Map[tile.x, tile.y] &&
                    _tileClusterGroupMap.TryGetValue(nb, out ClusterGroup neighborGroup))
                {
                    candidateGroup = neighborGroup;
                    break;
                }
            }
        }

        if (candidateGroup == null)
        {
            candidateGroup = new ClusterGroup();
            AddGroupToList(candidateGroup, Map[tile.x, tile.y]);
        }

        candidateGroup.Tiles.Add(tile);
        candidateGroup.RecalculateCenterAndDirection(height);
        _tileClusterGroupMap[tile] = candidateGroup;
    }

    // 그룹이 비었을 경우 전역 리스트에서 제거
    private void RemoveGroup(ClusterGroup group)
    {
        _grassClusterGroups.Remove(group);
        _mountainClusterGroups.Remove(group);
        _riverClusterGroups.Remove(group);
        _resourceClusterGroups.Remove(group);
    }

    //타일 타입에 따라 새 그룹을 전역 리스트에 추가
    private void AddGroupToList(ClusterGroup group, TileType type)
    {
        switch (type)
        {
            case TileType.Grass:
                _grassClusterGroups.Add(group);
                break;
            case TileType.Mountain:
                _mountainClusterGroups.Add(group);
                break;
            case TileType.River:
                _riverClusterGroups.Add(group);
                break;
            case TileType.Wood:
            case TileType.Iron:
                _resourceClusterGroups.Add(group);
                break;
            case TileType.None:
            default:
                break;
        }
    }

    // 타일 좌표를 인자로 받아 해당 타일이 속한 클러스터 그룹을 반환
    public ClusterGroup GetClusterInfo(Vector2Int tile)
    {
        return _tileClusterGroupMap.GetValueOrDefault(tile);
    }

    #endregion

    #region 시드 및 맵 초기화, 오브젝트 생성

    private void InitializeSeed(string seed = null)
    {
        mapSeed = string.IsNullOrEmpty(seed) ? DateTime.Now.Ticks.ToString() : seed;
        // if (string.IsNullOrEmpty(mapSeed))
        //     mapSeed = DateTime.Now.Ticks.ToString();

        _curWidth = width;

        int masterSeed = mapSeed.GetHashCode();
        _masterRng = new System.Random(masterSeed);
        Random.InitState(masterSeed);
    }

    private void InitializeMap()
    {
        Map = new TileType[_curWidth, height];
        for (int x = 0; x < _curWidth; x++)
        for (int y = 0; y < height; y++)
            Map[x, y] = TileType.Grass;
    }

    private void InstantiateParent()
    {
        _clusterParent = Instantiate(clusterParentPrefab);
        _clusterParent.GetComponent<NetworkObject>().Spawn();
        _oldMapParent = Instantiate(oldMapParentPrefab);
        _oldMapParent.GetComponent<NetworkObject>().Spawn();
    }

    #endregion

    #region 출발지, 도착지 설정

    // 출발지/도착지 결정 (출발지에서 도착지까지 맨해튼 거리가 pathLength, 수평 거리 최소)
    private void SetPath(int startX = 1, int oldWidth = 0)
    {
        _posA = oldWidth == 0
            ? new Vector2Int(startX, height / 2)
            : new Vector2Int(_posB.x, _posB.y);

        int localStartX = (oldWidth == 0) ? _posA.x : _posA.x - oldWidth;
        int minManhattanDistance = localStartX + 1;
        if (pathLength < minManhattanDistance)
        {
            Debug.LogError($"불가능한 설정: pathLength({pathLength})는 최소 {minManhattanDistance} 이상이어야 함.");
            return;
        }

        if (minHorizontalDistance > pathLength)
        {
            Debug.LogError($"불가능한 설정: minHorizontalDistance({minHorizontalDistance})가 pathLength({pathLength})보다 큼.");
            return;
        }

        int attempts = 0;
        int maxAttempts = MAX_ITERATIONS;
        do
        {
            int posXLocal = Random.Range(Mathf.Max(localStartX + minHorizontalDistance, localStartX + 1),
                (_curWidth - oldWidth) - 1);
            int posXAbs = (oldWidth == 0) ? posXLocal : posXLocal + oldWidth;
            _posB = new Vector2Int(posXAbs, Random.Range(1, height - 1));
            attempts++;
            if (attempts > maxAttempts)
                throw new MapGenerationException("SetPath: 최대 시도 횟수를 초과했습니다.");
        } while ((Mathf.Abs(_posB.x - _posA.x) + Mathf.Abs(_posB.y - _posA.y) != pathLength) ||
                 (Mathf.Abs(_posB.x - _posA.x) < minHorizontalDistance));
    }


    // 시작점에서 도착점까지 경로를 Grass로 설정
    private void GenerateValidPath(int oldWidth = 0)
    {
        Vector2Int current = _posA;
        int iterations = 0;
        while (current != _posB && iterations < MAX_ITERATIONS)
        {
            // Debug.Log($"posB: {_posB.x}:{_posB.y}, current: {current.x}:{current.y}");
            iterations++;

            if (current.x != _posB.x)
            {
                if (_posB.x > current.x)
                    current.x = Mathf.Min(current.x + 1, _curWidth - 1);
                else
                    current.x = Mathf.Max(current.x - 1, 0);
            }
            else if (current.y != _posB.y)
            {
                if (_posB.y > current.y)
                    current.y = Mathf.Min(current.y + 1, height - 1);
                else
                    current.y = Mathf.Max(current.y - 1, 0);
            }

            int localX = current.x - oldWidth;
            if (localX >= 0)
            {
                if (!IsInNewRegion(new Vector2Int(localX, current.y), oldWidth))
                {
                    throw new MapGenerationException("GenerateValidPath: local 좌표가 범위를 벗어났습니다.");
                }

                Map[current.x, current.y] = TileType.Grass;
                ReassignTile(current);
            }
        }

        if (iterations >= MAX_ITERATIONS)
            throw new MapGenerationException("GenerateValidPath: 반복 초과");
    }

    #endregion

    #region 산 생성 (Mountain)

    private void GenerateMountains(int oldWidth = 0)
    {
        int mountainClusters = _masterRng.Next(mountainClusterCountMin, mountainClusterCountMax + 1);
        for (int i = 0; i < mountainClusters; i++)
        {
            int localSeed = _masterRng.Next();
            System.Random localRng = new System.Random(localSeed);
            int edge = localRng.Next(0, 4);
            int startX = 0, startY = 0;
            switch (edge)
            {
                case 0:
                    startX = localRng.Next(oldWidth, _curWidth);
                    startY = height - 1;
                    break;
                case 1:
                    startX = localRng.Next(oldWidth, _curWidth);
                    startY = 0;
                    break;
                case 2:
                    startX = oldWidth;
                    startY = localRng.Next(0, height);
                    break;
                case 3:
                    startX = _curWidth - 1;
                    startY = localRng.Next(0, height);
                    break;
            }

            if ((startX == _posA.x && startY == _posA.y) ||
                (startX == _posB.x && startY == _posB.y))
                continue;

            int clusterSize = localRng.Next(mountainClusterSizeMin, mountainClusterSizeMax + 1);
            List<Vector2Int> mountainClusterTiles = new List<Vector2Int>();
            Queue<Vector2Int> mountainQueue = new Queue<Vector2Int>();
            Vector2Int startPos = new Vector2Int(startX, startY);
            mountainQueue.Enqueue(startPos);
            if (startX >= oldWidth)
            {
                Map[startX, startY] = TileType.Mountain;
                mountainClusterTiles.Add(startPos);
                ReassignTile(startPos);
            }

            int count = 1;
            int iterations = 0;
            while (mountainQueue.Count > 0 && count < clusterSize && iterations < MAX_ITERATIONS)
            {
                iterations++;
                Vector2Int cur = mountainQueue.Dequeue();
                foreach (var n in GetNeighbors8(cur))
                {
                    if (IsInBounds(n) && n.x >= oldWidth && Map[n.x, n.y] == TileType.Grass)
                    {
                        if (n == _posA || n == _posB)
                            continue;
                        float chance = 0.7f * (1f - (float)count / clusterSize);
                        if (localRng.NextDouble() < chance)
                        {
                            Map[n.x, n.y] = TileType.Mountain;
                            ReassignTile(n);
                            mountainQueue.Enqueue(n);
                            mountainClusterTiles.Add(n);
                            count++;
                        }
                    }
                }
            }

            if (iterations >= MAX_ITERATIONS)
                throw new MapGenerationException("GenerateMountains: 반복 초과");
            ClusterGroup mountainGroup = CreateClusterGroupFromTiles(mountainClusterTiles);
            _mountainClusterGroups.Add(mountainGroup);
        }

        ForceStartEndGrass();
    }

    #endregion

    #region 강 생성 (River)

    private void GenerateRivers(int oldWidth = 0)
    {
        int riverCount = Random.Range(minRiverCount, maxRiverCount + 1);
        int riversGenerated = 0;
        int outerAttempts = 0;
        while (riversGenerated < riverCount && outerAttempts < MAX_ITERATIONS)
        {
            outerAttempts++;
            var riverCells = riversGenerated == 0 ? GenerateElongatedRiver() : GenerateRoundedRiver();
            List<Vector2Int> validRiverCells = riverCells.Where(cell => cell.x >= oldWidth).ToList();
            // Debug.Log($"[GenerateRivers] 후보 강 셀 수: {riverCells.Count}, 확장 영역에서 유효한 셀 수: {validRiverCells.Count}");
            if (validRiverCells.Count >= minRiverLength)
            {
                if (validRiverCells.Count > maxRiverCellsAllowed)
                    validRiverCells = validRiverCells.GetRange(0, maxRiverCellsAllowed);
                foreach (var cell in validRiverCells)
                {
                    Map[cell.x, cell.y] = TileType.River;
                    ReassignTile(cell);
                }

                ClusterGroup riverGroup = CreateClusterGroupFromTiles(validRiverCells);
                _riverClusterGroups.Add(riverGroup);
                riversGenerated++;
            }
            else
            {
                // Debug.LogWarning("GenerateRivers: 최소 길이 미달");
            }
        }

        if (riversGenerated < riverCount)
            throw new MapGenerationException("GenerateRivers: 일부 강 생성 실패");
    }


    private List<Vector2Int> GenerateElongatedRiver()
    {
        int localSeed = _masterRng.Next();
        System.Random localRng = new System.Random(localSeed);
        const int maxAttempts = MAX_ITERATIONS;
        int attempt = 0;
        List<Vector2Int> candidateRiver = new List<Vector2Int>();
        int desiredCount = localRng.Next(minRiverLength, maxRiverCellsAllowed + 1);

        while (attempt < maxAttempts)
        {
            attempt++;
            candidateRiver.Clear();

            int edge = localRng.Next(0, 4);
            int startX = 0, startY = 0;
            Vector2Int direction = Vector2Int.zero;
            switch (edge)
            {
                case 0:
                    startX = localRng.Next(0, _curWidth);
                    startY = height - 1;
                    direction = new Vector2Int(0, -1);
                    break;
                case 1:
                    startX = localRng.Next(0, _curWidth);
                    startY = 0;
                    direction = new Vector2Int(0, 1);
                    break;
                case 2:
                    startX = 0;
                    startY = localRng.Next(0, height);
                    direction = new Vector2Int(1, 0);
                    break;
                case 3:
                    startX = _curWidth - 1;
                    startY = localRng.Next(0, height);
                    direction = new Vector2Int(-1, 0);
                    break;
            }

            Vector2Int current = new Vector2Int(startX, startY);
            candidateRiver.Add(current);

            int spineLength = desiredCount / 2;
            int spineAttempts = 0;
            while (candidateRiver.Count < spineLength && spineAttempts < MAX_ITERATIONS)
            {
                spineAttempts++;
                Vector2Int[] spineOffsets = new Vector2Int[]
                {
                    direction,
                    new Vector2Int(direction.x + 1, direction.y),
                    new Vector2Int(direction.x - 1, direction.y)
                };

                List<Vector2Int> validSpineOffsets = new List<Vector2Int>();
                foreach (var off in spineOffsets)
                {
                    Vector2Int nextPos = current + off;
                    if (IsInBounds(nextPos))
                        validSpineOffsets.Add(off);
                }

                if (validSpineOffsets.Count == 0)
                    break;
                Vector2Int chosenOffset = (localRng.NextDouble() < 0.7)
                    ? direction
                    : validSpineOffsets[localRng.Next(0, validSpineOffsets.Count)];
                current += chosenOffset;
                if (!candidateRiver.Contains(current))
                    candidateRiver.Add(current);
            }

            if (spineAttempts >= MAX_ITERATIONS)
                throw new MapGenerationException("GenerateElongatedRiver: spineAttempts 최대치를 초과했습니다.");

            // 측면 확장
            List<Vector2Int> lateralCandidates = new List<Vector2Int>(candidateRiver);
            foreach (var spineCell in candidateRiver)
            {
                Vector2Int perp1 = new Vector2Int(-direction.y, direction.x);
                Vector2Int perp2 = new Vector2Int(direction.y, -direction.x);

                Vector2Int[] lateralOffsets = new Vector2Int[] { perp1, perp2 };
                foreach (var lateral in lateralOffsets)
                {
                    Vector2Int lateralPos = spineCell + lateral;
                    if (IsInBounds(lateralPos) && !candidateRiver.Contains(lateralPos))
                    {
                        if (localRng.NextDouble() < lateralSpreadProbability)
                            lateralCandidates.Add(lateralPos);
                    }
                }
            }

            foreach (var pos in lateralCandidates)
            {
                if (candidateRiver.Count < maxRiverCellsAllowed && !candidateRiver.Contains(pos))
                    candidateRiver.Add(pos);
            }

            ExpandRiverCluster(candidateRiver, localRng);
            EnsureCardinalConnectivity(candidateRiver, localRng);
            LimitElongatedRiverWidth(candidateRiver);

            if (candidateRiver.Count >= minRiverLength && candidateRiver.Count <= maxRiverCellsAllowed)
            {
                // Debug.Log($"강 생성 완료, 시도 횟수: {attempt}");
                return new List<Vector2Int>(candidateRiver);
            }
        }

        throw new MapGenerationException("GenerateElongatedRiver: 목표 강 셀 수를 만족하는 강 생성에 실패했습니다.");
    }

    private List<Vector2Int> GenerateRoundedRiver()
    {
        int localSeed = _masterRng.Next();
        System.Random localRng = new System.Random(localSeed);
        const int maxAttempts = MAX_ITERATIONS;
        int attempt = 0;
        List<Vector2Int> candidateRiver = new List<Vector2Int>();
        int desiredCount = localRng.Next(minRiverLength, maxRiverCellsAllowed + 1);

        while (attempt < maxAttempts)
        {
            attempt++;
            candidateRiver.Clear();
            int edge = localRng.Next(0, 4);
            int startX = 0, startY = 0;
            switch (edge)
            {
                case 0:
                    startX = localRng.Next(0, _curWidth);
                    startY = height - 1;
                    break;
                case 1:
                    startX = localRng.Next(0, _curWidth);
                    startY = 0;
                    break;
                case 2:
                    startX = 0;
                    startY = localRng.Next(0, height);
                    break;
                case 3:
                    startX = _curWidth - 1;
                    startY = localRng.Next(0, height);
                    break;
            }

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            Vector2Int start = new Vector2Int(startX, startY);
            queue.Enqueue(start);
            candidateRiver.Add(start);

            int safetyCounter = 0;
            int maxSafety = MAX_ITERATIONS;
            while (candidateRiver.Count < desiredCount && safetyCounter < maxSafety &&
                   candidateRiver.Count < maxRiverCellsAllowed)
            {
                safetyCounter++;
                if (queue.Count == 0) break;
                Vector2Int cur = queue.Dequeue();

                foreach (var n in GetNeighbors8(cur))
                {
                    if (candidateRiver.Count >= maxRiverCellsAllowed)
                        break;
                    if (IsInBounds(n) && Map[n.x, n.y] == TileType.Grass && !candidateRiver.Contains(n))
                    {
                        if (localRng.NextDouble() < 0.5f)
                        {
                            candidateRiver.Add(n);
                            queue.Enqueue(n);
                            if (candidateRiver.Count >= desiredCount)
                                break;
                        }
                    }
                }
            }

            if (safetyCounter >= maxSafety)
                throw new MapGenerationException("GenerateRoundedRiver: safetyCounter 최대치를 초과했습니다.");

            ExpandRiverCluster(candidateRiver, localRng);
            EnsureCardinalConnectivity(candidateRiver, localRng);

            if (candidateRiver.Count >= desiredCount && candidateRiver.Count <= maxRiverCellsAllowed)
            {
                // Debug.Log($"강 생성 완료, 시도 횟수: {attempt}");
                return candidateRiver;
            }
        }

        throw new MapGenerationException("GenerateRoundedRiver: 목표 강 셀 수를 만족하는 강 생성에 실패했습니다.");
    }

    private void ExpandRiverCluster(List<Vector2Int> candidateRiver, System.Random rng)
    {
        Vector2Int initialRiverPoint = candidateRiver[0];
        int maxAllowedDistance = maxRiverCellsAllowed / 4;

        Queue<Vector2Int> queue = new Queue<Vector2Int>(candidateRiver);
        int iterations = 0;
        while (queue.Count > 0 && candidateRiver.Count < maxRiverCellsAllowed && iterations < MAX_ITERATIONS)
        {
            iterations++;
            Vector2Int cell = queue.Dequeue();
            int distance = Mathf.Abs(cell.x - initialRiverPoint.x) + Mathf.Abs(cell.y - initialRiverPoint.y);
            if (distance > maxAllowedDistance)
                continue;

            foreach (var n in GetNeighbors8(cell))
            {
                if (candidateRiver.Count >= maxRiverCellsAllowed)
                    break;
                if (IsInBounds(n) && Map[n.x, n.y] == TileType.Grass && !candidateRiver.Contains(n))
                {
                    if (rng.NextDouble() < lateralSpreadProbability)
                    {
                        candidateRiver.Add(n);
                        queue.Enqueue(n);
                    }
                }
            }
        }

        if (iterations >= MAX_ITERATIONS)
            throw new MapGenerationException("ExpandRiverCluster: 최대 반복 횟수를 초과했습니다.");
    }

    private void LimitElongatedRiverWidth(List<Vector2Int> candidateRiver)
    {
        List<Vector2Int> cellsToRemove = new List<Vector2Int>();
        foreach (var cell in new List<Vector2Int>(candidateRiver))
        {
            bool hasLeft = candidateRiver.Contains(new Vector2Int(cell.x - 1, cell.y));
            bool hasRight = candidateRiver.Contains(new Vector2Int(cell.x + 1, cell.y));
            if (!hasLeft || !hasRight)
                cellsToRemove.Add(cell);
        }

        foreach (var cell in cellsToRemove)
            candidateRiver.Remove(cell);
    }

    private void EnsureCardinalConnectivity(List<Vector2Int> candidateRiver, System.Random rng)
    {
        foreach (var cell in candidateRiver.ToArray())
        {
            bool hasCardinal = false;
            foreach (var c in GetCardinalNeighbors(cell))
            {
                if (IsInBounds(c) && candidateRiver.Contains(c))
                {
                    hasCardinal = true;
                    break;
                }
            }

            if (!hasCardinal && candidateRiver.Count < maxRiverCellsAllowed)
            {
                List<Vector2Int> candidates = new List<Vector2Int>();
                foreach (var c in GetCardinalNeighbors(cell))
                {
                    if (IsInBounds(c) && Map[c.x, c.y] == TileType.Grass && !candidateRiver.Contains(c))
                        candidates.Add(c);
                }

                if (candidates.Count > 0 && candidateRiver.Count < maxRiverCellsAllowed)
                {
                    Vector2Int chosen = candidates[rng.Next(0, candidates.Count)];
                    candidateRiver.Add(chosen);
                }
            }
        }
    }

    #endregion

    #region 초기 자원 생성 (Wood, Iron)

    private void PlaceDestructibleObstacles(int oldWidth = 0)
    {
        int localSeed = _masterRng.Next();
        System.Random localRng = new System.Random(localSeed);
        int totalWood = 0, totalIron = 0;
        bool assignWoodNext = true;
        for (int i = 0; i < destructibleClusterCount; i++)
        {
            int x = localRng.Next(oldWidth, _curWidth);
            int y = localRng.Next(0, height);
            if (Map[x, y] != TileType.Grass)
                continue;
            Vector2Int startPos = new Vector2Int(x, y);
            List<Vector2Int> cluster = GenerateDestructibleCluster(startPos);
            if (cluster.Count == 0)
                continue;
            TileType obstacleType;
            if (totalWood < minWoodCount && totalIron < minIronCount)
            {
                obstacleType = assignWoodNext ? TileType.Wood : TileType.Iron;
                assignWoodNext = !assignWoodNext;
            }
            else if (totalWood < minWoodCount)
                obstacleType = TileType.Wood;
            else if (totalIron < minIronCount)
                obstacleType = TileType.Iron;
            else
                continue;

            foreach (var cell in cluster)
            {
                if (cell.x >= oldWidth && Map[cell.x, cell.y] == TileType.Grass)
                {
                    Map[cell.x, cell.y] = obstacleType;
                    ReassignTile(cell);
                    if (obstacleType == TileType.Wood)
                        totalWood++;
                    else
                        totalIron++;
                }
            }

            ClusterGroup resourceGroup = CreateClusterGroupFromTiles(cluster);
            _resourceClusterGroups.Add(resourceGroup);
        }
    }

    private List<Vector2Int> GenerateDestructibleCluster(Vector2Int startPos)
    {
        return GenerateRandomNonHoleyCluster(startPos, minDestructibleClusterSize, MAX_ITERATIONS, _masterRng,
            _ => true, _ => true);
    }

    #endregion

    #region 보정 및 후처리

    // Grass 타일을 Mountain으로 전환
    private void GenerateGrassToMountainClusters(int oldWidth = 0)
    {
        int localSeed = _masterRng.Next();
        System.Random localRng = new System.Random(localSeed);
        int loopCounter = 0;
        while (CountGrassTiles() >= maxGrassTileCount && loopCounter < MAX_ITERATIONS)
        {
            loopCounter++;
            List<Vector2Int> randomGrassTiles = new List<Vector2Int>();
            int newRegionWidth = _curWidth - oldWidth;
            for (int localX = 0; localX < newRegionWidth; localX++)
            {
                int x = localX + oldWidth;
                for (int y = 0; y < height; y++)
                {
                    if (Map[x, y] == TileType.Grass)
                        randomGrassTiles.Add(new Vector2Int(x, y));
                }
            }

            if (randomGrassTiles.Count == 0)
            {
                Debug.Log("GenerateGrassToMountainClusters: 새 영역에 Grass 없음");
                break;
            }

            Vector2Int start = randomGrassTiles[localRng.Next(0, randomGrassTiles.Count)];
            int clusterSize = localRng.Next(mountainClusterSizeMin, mountainClusterSizeMax + 1);
            Queue<Vector2Int> mountainQueue = new Queue<Vector2Int>();
            mountainQueue.Enqueue(start);
            Map[start.x, start.y] = TileType.Mountain;
            ReassignTile(start);
            List<Vector2Int> mountainClusterTiles = new List<Vector2Int> { start };
            int count = 1;
            int iterations = 0;
            while (mountainQueue.Count > 0 && count < clusterSize && iterations < MAX_ITERATIONS)
            {
                iterations++;
                Vector2Int cur = mountainQueue.Dequeue();
                foreach (var n in GetNeighbors8(cur))
                {
                    if (n.x >= oldWidth && IsInBounds(n) && Map[n.x, n.y] == TileType.Grass)
                    {
                        float chance = 0.7f * (1f - (float)count / clusterSize);
                        if (localRng.NextDouble() < chance)
                        {
                            Map[n.x, n.y] = TileType.Mountain;
                            ReassignTile(n);
                            mountainQueue.Enqueue(n);
                            mountainClusterTiles.Add(n);
                            count++;
                            if (count >= clusterSize)
                                break;
                        }
                    }
                }
            }

            if (iterations >= MAX_ITERATIONS)
                throw new MapGenerationException("GenerateGrassToMountainClusters: 반복 초과");
            ClusterGroup mountainGroup = CreateClusterGroupFromTiles(mountainClusterTiles);
            _mountainClusterGroups.Add(mountainGroup);
        }

        ForceStartEndGrass();
    }

    // 출발/도착 주변 5*5 영역을 Grass로 설정 후 그룹 할당
    private void EnsureStartEndInnerClear()
    {
        //기존 스페셜 그룹 초기화
        _specialClusterGroups.Clear();

        List<Vector2Int> specialCellsA = new List<Vector2Int>();
        for (int dx = -2; dx <= 5; dx++)
        {
            for (int dy = -3; dy <= 2; dy++)
            {
                int nx = _posA.x + dx;
                int ny = _posA.y + dy;
                Vector2Int cellPos = new Vector2Int(nx, ny);
                if (IsInBounds(cellPos))
                {
                    Map[nx, ny] = TileType.Grass;
                    specialCellsA.Add(cellPos);
                }
            }
        }

        ClusterGroup specialGroupA = CreateClusterGroupFromTiles(specialCellsA);
        specialGroupA.Direction = ClusterDirection.Under;
        _specialClusterGroups.Add(specialGroupA);

        List<Vector2Int> specialCellsB = new List<Vector2Int>();
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                int nx = _posB.x + dx;
                int ny = _posB.y + dy;
                Vector2Int cellPos = new Vector2Int(nx, ny);
                if (IsInBounds(cellPos))
                {
                    Map[nx, ny] = TileType.Grass;
                    specialCellsB.Add(cellPos);
                }
            }
        }

        ClusterGroup specialGroupB = CreateClusterGroupFromTiles(specialCellsB);
        specialGroupB.Direction = ClusterDirection.Under;
        _specialClusterGroups.Add(specialGroupB);
    }

    // 도착 주변 5*5 영역을 Grass로 설정 후 그룹 할당
    private void EnsureStartEndInnerClearExtension(int oldWidth)
    {
        List<Vector2Int> specialCellsB = new List<Vector2Int>();
        for (int dx = -2; dx <= 2; dx++)
        for (int dy = -2; dy <= 2; dy++)
        {
            int nx = _posB.x + dx;
            int ny = _posB.y + dy;
            Vector2Int cellPos = new Vector2Int(nx, ny);
            if (IsInBounds(cellPos) && cellPos.x >= oldWidth)
            {
                Map[nx, ny] = TileType.Grass;
                specialCellsB.Add(cellPos);
            }
        }

        ClusterGroup specialGroupB = CreateClusterGroupFromTiles(specialCellsB);
        specialGroupB.Direction = ClusterDirection.Under;
        _specialClusterGroups.Add(specialGroupB);
    }

    // 부족한 Grass 타일 확보 (강 셀 제외)
    private void EnsureAdjacentGrassTiles(int oldWidth)
    {
        int currentGrass = 0;
        int newRegionWidth = _curWidth - oldWidth;
        for (int localX = 0; localX < newRegionWidth; localX++)
        {
            int x = localX + oldWidth;
            for (int y = 0; y < height; y++)
            {
                if (Map[x, y] == TileType.Grass)
                    currentGrass++;
            }
        }

        int iterations = 0;
        int maxIterations = MAX_ITERATIONS;
        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int localX = 0; localX < newRegionWidth; localX++)
        {
            int x = localX + oldWidth;
            for (int y = 0; y < height; y++)
            {
                if (Map[x, y] == TileType.Mountain)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    foreach (Vector2Int n in GetCardinalNeighbors(pos))
                    {
                        if (IsInBounds(n) &&
                            (Map[n.x, n.y] == TileType.Grass ||
                             Map[n.x, n.y] == TileType.Wood ||
                             Map[n.x, n.y] == TileType.Iron))
                        {
                            candidates.Add(pos);
                            break;
                        }
                    }
                }
            }
        }

        int localSeed = _masterRng.Next();
        System.Random localRng = new System.Random(localSeed);
        while (currentGrass < minGrassTileCount && iterations < maxIterations && candidates.Count > 0)
        {
            iterations++;
            int index = localRng.Next(0, candidates.Count);
            Vector2Int candidate = candidates[index];
            if (Map[candidate.x, candidate.y] == TileType.Mountain)
            {
                Map[candidate.x, candidate.y] = TileType.Grass;
                currentGrass++;
                ReassignTile(candidate);
                candidates.RemoveAt(index);
            }
        }

        if (iterations >= maxIterations)
            throw new MapGenerationException("EnsureAdjacentGrassTiles: 반복 초과");
    }

    // 도달 불가능한 영역을 Mountain으로 전환
    private void EnsureReachability(int oldWidth = 0)
    {
        bool[][] visited = new bool[_curWidth][];
        for (int i = 0; i < _curWidth; i++)
            visited[i] = new bool[height];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(_posA);
        visited[_posA.x][_posA.y] = true;
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };
        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            foreach (var d in directions)
            {
                Vector2Int next = cur + d;
                if (IsInBounds(next) && !visited[next.x][next.y])
                {
                    if (Map[next.x, next.y] != TileType.Mountain)
                    {
                        visited[next.x][next.y] = true;
                        queue.Enqueue(next);
                    }
                }
            }
        }

        int newRegionWidth = _curWidth - oldWidth;
        for (int localX = 0; localX < newRegionWidth; localX++)
        {
            int x = localX + oldWidth;
            for (int y = 0; y < height; y++)
            {
                if (!visited[x][y])
                {
                    Map[x, y] = TileType.Mountain;
                    ReassignTile(new Vector2Int(x, y));
                }
            }
        }
    }

    // 부족 자원 보충 및 인접 Grass 확보
    private void FinalAdjustments(int oldWidth = 0)
    {
        EnsureMinimumDestructibleObstacles(oldWidth);
        EnsureAdjacentGrassTiles(oldWidth);
    }

    private void EnsureMinimumDestructibleObstacles(int oldWidth)
    {
        int localSeed = _masterRng.Next();
        System.Random localRng = new System.Random(localSeed);
        int currentWood = 0, currentIron = 0;
        int newRegionWidth = _curWidth - oldWidth;
        for (int localX = 0; localX < newRegionWidth; localX++)
        {
            int x = localX + oldWidth;
            for (int y = 0; y < height; y++)
            {
                if (Map[x, y] == TileType.Wood)
                    currentWood++;
                if (Map[x, y] == TileType.Iron)
                    currentIron++;
            }
        }

        int maxAttempts = MAX_ITERATIONS;
        int attempts = 0;
        int targetClusterSize = minDestructibleClusterSize;
        while (((currentWood < minWoodCount) || (currentIron < minIronCount)) && attempts < maxAttempts)
        {
            attempts++;
            TileType targetResource;
            if (currentWood < minWoodCount && currentIron < minIronCount)
                targetResource = (localRng.NextDouble() < 0.5f) ? TileType.Wood : TileType.Iron;
            else if (currentWood < minWoodCount)
                targetResource = TileType.Wood;
            else
                targetResource = TileType.Iron;
            List<Vector2Int> candidates = new List<Vector2Int>();
            for (int localX = 0; localX < newRegionWidth; localX++)
            {
                int x = localX + oldWidth;
                for (int y = 0; y < height; y++)
                {
                    if (Map[x, y] == TileType.Mountain)
                    {
                        Vector2Int pos = new Vector2Int(x, y);
                        if (IsCellNear(pos, _posA, 2) || IsCellNear(pos, _posB, 2))
                            continue;
                        foreach (Vector2Int neighbor in GetCardinalNeighbors(pos))
                        {
                            if (IsInBounds(neighbor))
                            {
                                TileType neighborType = Map[neighbor.x, neighbor.y];
                                if (neighborType == TileType.Grass ||
                                    neighborType == TileType.Wood ||
                                    neighborType == TileType.Iron ||
                                    neighborType == TileType.River)
                                {
                                    candidates.Add(pos);
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (candidates.Count == 0)
                throw new MapGenerationException("EnsureMinimumDestructibleObstacles: 후보 없음.");
            int index = localRng.Next(0, candidates.Count);
            Vector2Int candidateTile = candidates[index];
            List<Vector2Int> cluster = GenerateResourceCluster(candidateTile, targetClusterSize, MAX_ITERATIONS);
            if (cluster.Count == 0)
            {
                candidates.RemoveAt(index);
                continue;
            }

            foreach (Vector2Int cell in cluster)
            {
                if (cell.x >= oldWidth)
                {
                    TileType cellType = Map[cell.x, cell.y];
                    if (cellType == TileType.Mountain || cellType == TileType.Grass || cellType == TileType.River)
                    {
                        Map[cell.x, cell.y] = targetResource;
                        ReassignTile(cell);
                    }
                }
            }

            currentWood = 0;
            currentIron = 0;
            for (int localX = 0; localX < newRegionWidth; localX++)
            {
                int x = localX + oldWidth;
                for (int y = 0; y < height; y++)
                {
                    if (Map[x, y] == TileType.Wood)
                        currentWood++;
                    if (Map[x, y] == TileType.Iron)
                        currentIron++;
                }
            }
        }

        if (currentWood < minWoodCount)
            Debug.LogWarning($"EnsureMinDestrObs: Wood 최소치({minWoodCount}) 미달. 최종: {currentWood}");
        if (currentIron < minIronCount)
            Debug.LogWarning($"EnsureMinDestrObs: Iron 최소치({minIronCount}) 미달. 최종: {currentIron}");
    }

    private List<Vector2Int> GenerateResourceCluster(Vector2Int start, int targetCount, int maxIterations)
    {
        return GenerateRandomNonHoleyCluster(
            start,
            targetCount,
            maxIterations,
            _masterRng,
            (pos) =>
            {
                TileType type = Map[pos.x, pos.y];
                return type is TileType.Grass or TileType.Mountain or TileType.River;
            },
            (pos) =>
            {
                TileType type = Map[pos.x, pos.y];
                return type is TileType.Grass or TileType.Mountain or TileType.River;
            }
        );
    }

    // 도착점에서 오른쪽 끝까지 통로 확보
    private void EnsureEndpointCorridor(int oldWidth = 0)
    {
        int row = _posB.y;
        for (int x = _posB.x; x < _curWidth; x++)
        {
            if (x >= oldWidth)
            {
                Map[x, row] = TileType.Grass;
                ReassignTile(new Vector2Int(x, row));
            }
        }

        if (row - 1 >= 0)
            for (int x = _posB.x; x < _curWidth; x++)
            {
                if (x >= oldWidth)
                {
                    Map[x, row - 1] = TileType.Grass;
                    ReassignTile(new Vector2Int(x, row - 1));
                }
            }

        if (row + 1 < height)
            for (int x = _posB.x; x < _curWidth; x++)
            {
                if (x >= oldWidth)
                {
                    Map[x, row + 1] = TileType.Grass;
                    ReassignTile(new Vector2Int(x, row + 1));
                }
            }
    }

    // 시작점에서 최소한 하나의 Wood 클러스터 접근 통로 생성
    private void EnsureWoodAccessibility(int oldWidth = 0)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> parent = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        queue.Enqueue(_posA);
        visited.Add(_posA);
        bool foundWood = false;
        Vector2Int targetWood = new Vector2Int(-1, -1);
        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            if (cur.x >= oldWidth && Map[cur.x, cur.y] == TileType.Wood)
            {
                targetWood = cur;
                foundWood = true;
                break;
            }

            foreach (var d in new Vector2Int[]
                         { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) })
            {
                Vector2Int nxt = cur + d;
                if (IsInBounds(nxt) && nxt.x >= oldWidth && !visited.Contains(nxt))
                {
                    if (Map[nxt.x, nxt.y] != TileType.Mountain && Map[nxt.x, nxt.y] != TileType.River)
                    {
                        queue.Enqueue(nxt);
                        visited.Add(nxt);
                        parent[nxt] = cur;
                    }
                }
            }
        }

        List<Vector2Int> path = new List<Vector2Int>();
        if (!foundWood)
        {
            int bestDist = int.MaxValue;
            for (int i = oldWidth; i < _curWidth; i++)
            for (int j = 0; j < height; j++)
            {
                if (Map[i, j] == TileType.Wood)
                {
                    int dist = Mathf.Abs(i - _posA.x) + Mathf.Abs(j - _posA.y);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        targetWood = new Vector2Int(i, j);
                    }
                }
            }

            if (bestDist == int.MaxValue)
            {
                Debug.LogWarning("EnsureWoodAccessibility: 새 영역에 Wood 없음");
                return;
            }

            Vector2Int cur = _posA;
            while (cur.x != targetWood.x)
            {
                path.Add(cur);
                cur.x += (targetWood.x > cur.x) ? 1 : -1;
            }

            while (cur.y != targetWood.y)
            {
                path.Add(cur);
                cur.y += (targetWood.y > cur.y) ? 1 : -1;
            }

            path.Add(targetWood);
        }
        else
        {
            Vector2Int cur = targetWood;
            while (cur != _posA)
            {
                path.Add(cur);
                cur = parent[cur];
            }

            path.Add(_posA);
            path.Reverse();
        }

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector2Int cell = path[i];
            if (cell.x >= oldWidth)
            {
                Map[cell.x, cell.y] = TileType.Grass;
                ReassignTile(cell);
            }
        }

        if (targetWood.x >= oldWidth && Map[targetWood.x, targetWood.y] != TileType.Wood)
        {
            Map[targetWood.x, targetWood.y] = TileType.Wood;
            ReassignTile(targetWood);
        }
    }

    // InstantiateMap() 호출 직전에 실행할 보정 함수
    private void AdjustPathForRiverAndWood(int oldWidth = 0)
    {
        List<Vector2Int> simplePath = FindSimplePath(_posA, _posB, pos =>
        {
            TileType type = Map[pos.x, pos.y];
            return type is TileType.Grass or TileType.Wood or TileType.Iron;
        });
        if (simplePath != null)
        {
            // Debug.Log("경로가 Grass, Wood, Iron 만으로 구성되어 있으므로 추가 보정이 필요 없음.");
            return;
        }
        else
        {
            // Debug.Log("Grass, Wood, Iron 만으로는 경로를 찾지 못했습니다. River를 포함한 경로를 보정합니다.");
        }

        Tuple<List<Vector2Int>, List<Vector2Int>> pathAndRivers = FindPathMinimizingRiver(_posA, _posB);
        List<Vector2Int> riverMinPath = pathAndRivers.Item1;
        List<Vector2Int> riverTilesOnPath = pathAndRivers.Item2;
        if (riverMinPath == null)
        {
            Debug.LogWarning("AdjustPathForRiverAndWood: 경로 찾을 수 없음");
            return;
        }

        int counter = 0;
        List<Vector2Int> convertedRiverTiles = new List<Vector2Int>();
        while (riverTilesOnPath.Count > 2 && counter < MAX_ITERATIONS)
        {
            counter++;
            int randomIndex = _masterRng.Next(riverTilesOnPath.Count);
            Vector2Int tile = riverTilesOnPath[randomIndex];
            if (tile.x >= oldWidth)
            {
                Map[tile.x, tile.y] = TileType.Grass;
                ReassignTile(tile);
                convertedRiverTiles.Add(tile);
                riverTilesOnPath.RemoveAt(randomIndex);
            }
        }

        if (counter >= MAX_ITERATIONS)
            throw new MapGenerationException("AdjustPathForRiverAndWood 반복 초과");
        int reachableWoodCount = CountReachableWoodWithoutRiver(_posA);
        if (reachableWoodCount < 2)
        {
            foreach (Vector2Int tile in convertedRiverTiles)
            {
                Map[tile.x, tile.y] = TileType.Wood;
                ReassignTile(tile);
            }
        }
    }

    private List<Vector2Int> FindSimplePath(Vector2Int start, Vector2Int end, Func<Vector2Int, bool> allowedPredicate)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> parent = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == end)
            {
                List<Vector2Int> path = new List<Vector2Int>();
                while (!current.Equals(start))
                {
                    path.Add(current);
                    current = parent[current];
                }

                path.Add(start);
                path.Reverse();
                return path;
            }

            foreach (var d in directions)
            {
                Vector2Int next = current + d;
                if (IsInBounds(next) && !visited.Contains(next) && allowedPredicate(next))
                {
                    visited.Add(next);
                    parent[next] = current;
                    queue.Enqueue(next);
                }
            }
        }

        return null;
    }

    private Tuple<List<Vector2Int>, List<Vector2Int>> FindPathMinimizingRiver(Vector2Int start, Vector2Int end)
    {
        int[][] cost = new int[_curWidth][];
        for (int i = 0; i < _curWidth; i++)
            cost[i] = new int[height];

        Vector2Int[][] parent = new Vector2Int[_curWidth][];
        for (int i = 0; i < _curWidth; i++)
            parent[i] = new Vector2Int[height];

        bool[][] visited = new bool[_curWidth][];
        for (int i = 0; i < _curWidth; i++)
            visited[i] = new bool[height];

        for (int x = 0; x < _curWidth; x++)
        for (int y = 0; y < height; y++)
        {
            cost[x][y] = int.MaxValue;
            parent[x][y] = new Vector2Int(-1, -1);
            visited[x][y] = false;
        }

        cost[start.x][start.y] = 0;
        List<Vector2Int> queue = new List<Vector2Int> { start };

        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        while (queue.Count > 0)
        {
            queue.Sort((a, b) => cost[a.x][a.y].CompareTo(cost[b.x][b.y]));
            Vector2Int current = queue[0];
            queue.RemoveAt(0);
            if (visited[current.x][current.y])
                continue;
            visited[current.x][current.y] = true;
            if (current == end)
                break;

            foreach (var d in directions)
            {
                Vector2Int next = current + d;
                if (IsInBounds(next) && Map[next.x, next.y] != TileType.Mountain)
                {
                    int tileCost = (Map[next.x, next.y] == TileType.River) ? 1 : 0;
                    int newCost = cost[current.x][current.y] + tileCost;
                    if (newCost < cost[next.x][next.y])
                    {
                        cost[next.x][next.y] = newCost;
                        parent[next.x][next.y] = current;
                        queue.Add(next);
                    }
                }
            }
        }

        if (cost[end.x][end.y] == int.MaxValue)
            return new Tuple<List<Vector2Int>, List<Vector2Int>>(null, null);

        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int node = end;
        while (node.x != -1 && node.y != -1)
        {
            path.Add(node);
            if (node == start)
                break;
            node = parent[node.x][node.y];
        }

        path.Reverse();

        List<Vector2Int> riverTiles = new List<Vector2Int>();
        foreach (var pos in path)
            if (Map[pos.x, pos.y] == TileType.River)
                riverTiles.Add(pos);

        return new Tuple<List<Vector2Int>, List<Vector2Int>>(path, riverTiles);
    }

    private int CountReachableWoodWithoutRiver(Vector2Int start)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        queue.Enqueue(start);
        visited.Add(start);
        int woodCount = 0;

        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            if (Map[cur.x, cur.y] == TileType.Wood)
                woodCount++;
            foreach (var d in directions)
            {
                Vector2Int next = cur + d;
                if (IsInBounds(next) && !visited.Contains(next))
                {
                    TileType type = Map[next.x, next.y];
                    if (type is TileType.Grass or TileType.Wood or TileType.Iron)
                    {
                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }
        }

        // Debug.Log($"[CountReachableWoodWithoutRiver] 최종 도달 가능한 Wood Count: {woodCount} (시작점: ({start.x}, {start.y}))");
        return woodCount;
    }

    //맵 재생성 전용: 맵의 왼쪽 끝을 일정 확률로 이전 맵의 오른쪽 끝과 동일한 타일로 교체
    private void ApplyLeftColumnTileTypeCopy(int oldWidth)
    {
        for (int y = 0; y < height; y++)
        {
            // 70% 확률로 변경
            if (Random.value < 0.3f)
            {
                Map[oldWidth, y] = Map[oldWidth - 1, y];
                ReassignTile(new Vector2Int(oldWidth, y));
            }
        }
    }

    #endregion

    #region 시작점, 도착점 보정

    private void ForceStartEndGrass()
    {
        Map[_posA.x, _posA.y] = TileType.Grass;
        ReassignTile(_posA);
        Map[_posB.x, _posB.y] = TileType.Grass;
        ReassignTile(_posB);
    }

    #endregion

    #region 경로 연결 보장 (Dijkstra)

    private void EnsurePathConnectivity(int oldWidth = 0)
    {
        int[][] cost = new int[_curWidth][];
        for (int i = 0; i < _curWidth; i++)
            cost[i] = new int[height];
        Vector2Int[][] parent = new Vector2Int[_curWidth][];
        for (int i = 0; i < _curWidth; i++)
            parent[i] = new Vector2Int[height];
        bool[][] visited = new bool[_curWidth][];
        for (int i = 0; i < _curWidth; i++)
            visited[i] = new bool[height];
        for (int x = 0; x < _curWidth; x++)
        for (int y = 0; y < height; y++)
        {
            cost[x][y] = int.MaxValue;
            parent[x][y] = new Vector2Int(-1, -1);
            visited[x][y] = false;
        }

        cost[_posA.x][_posA.y] = 0;
        List<Vector2Int> queue = new List<Vector2Int> { _posA };
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        // Debug.Log($"[EnsurePathConnectivity] 시작: {_posA.x},{_posA.y} / 도착: {_posB.x},{_posB.y}, oldWidth: {oldWidth}");

        int iterations = 0;
        while (queue.Count > 0 && iterations < MAX_ITERATIONS)
        {
            iterations++;
            queue.Sort((a, b) => cost[a.x][a.y].CompareTo(cost[b.x][b.y]));
            Vector2Int current = queue[0];
            queue.RemoveAt(0);
            if (visited[current.x][current.y])
                continue;
            visited[current.x][current.y] = true;
            if (current == _posB)
            {
                // Debug.Log($"[EnsurePathConnectivity] 도착점 도달 후 반복 횟수: {iterations}");
                break;
            }

            foreach (var d in directions)
            {
                Vector2Int next = current + d;
                if (IsInBounds(next))
                {
                    int tileCost = (Map[next.x, next.y] == TileType.Mountain) ? 1 : 0;
                    int newCost = cost[current.x][current.y] + tileCost;
                    if (newCost < cost[next.x][next.y])
                    {
                        cost[next.x][next.y] = newCost;
                        parent[next.x][next.y] = current;
                        queue.Add(next);
                        // Debug.Log($"[EnsurePathConnectivity] 갱신: 노드({next.x},{next.y}) cost: {newCost} (이전: {cost[next.x][next.y]}) 부모: ({current.x},{current.y})");
                    }
                }
            }
        }

        if (iterations >= MAX_ITERATIONS || cost[_posB.x][_posB.y] == int.MaxValue)
        {
            FallbackEnsureConnectivity();
            return;
        }

        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int node = _posB;
        while (node.x != -1 && node.y != -1)
        {
            path.Add(node);
            if (node == _posA)
                break;
            node = parent[node.x][node.y];
        }

        path.Reverse();
        // Debug.Log("[EnsurePathConnectivity] 복원된 경로: " + string.Join(" -> ", path.Select(p => $"({p.x},{p.y})")));
        foreach (var cell in path)
            if (cell.x >= oldWidth && Map[cell.x, cell.y] == TileType.Mountain)
            {
                Map[cell.x, cell.y] = TileType.Grass;
                ReassignTile(cell);
            }
    }

    // EnsurePathConnectivity에서 경로 복원이 실패한 경우
    // 모든 타일을 통과 가능하다고 가정하는 단순 BFS를 이용해 fallback 경로를 찾은 후
    // 그 경로 상의 Mountain 타일을 Grass로 변환하여 강제로 연결하는 메소드.
    private void FallbackEnsureConnectivity()
    {
        Debug.LogError("[EnsurePathConnectivity] 경로 복원 실패 - Fallback: 강제로 경로 복원 시도");
        List<Vector2Int> fallbackPath = FindFallbackPath(_posA, _posB);
        if (fallbackPath == null)
        {
            Debug.LogError("FallbackEnsureConnectivity: posA와 posB를 연결하는 경로를 찾을 수 없음");
            return;
        }

        // 경로 상의 산 타일을 Grass로 변경
        foreach (var tile in fallbackPath)
        {
            if (Map[tile.x, tile.y] == TileType.Mountain)
            {
                Map[tile.x, tile.y] = TileType.Grass;
                ReassignTile(tile);
            }
        }

        Debug.Log("FallbackEnsureConnectivity: 강제로 경로 연결 성공. 해당 경로 상의 산을 Grass로 변경함.");
    }

    // 모든 셀을 통과 가능하다고 가정하고, posA에서 posB까지 단순 BFS로 경로를 찾는 메소드.
    private List<Vector2Int> FindFallbackPath(Vector2Int start, Vector2Int end)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> parent = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        queue.Enqueue(start);
        visited.Add(start);
        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            if (cur == end)
            {
                List<Vector2Int> path = new List<Vector2Int>();
                Vector2Int node = end;
                while (!node.Equals(start))
                {
                    path.Add(node);
                    node = parent[node];
                }

                path.Add(start);
                path.Reverse();
                return path;
            }

            foreach (var neigh in GetCardinalNeighbors(cur))
            {
                if (IsInBounds(neigh) && visited.Add(neigh))
                {
                    parent[neigh] = cur;
                    queue.Enqueue(neigh);
                }
            }
        }

        return null;
    }

    #endregion

    #region Grass 클러스터 그룹 최종화 및 분할

    //ClusterGroup이 없는 Grass타일들을 그룹화 또는 분할
    private void FinalizeGrassClusters(int oldWidth = 0)
    {
        bool[][] visited = new bool[_curWidth][];
        for (int index = 0; index < _curWidth; index++)
            visited[index] = new bool[height];
        int newRegionWidth = _curWidth - oldWidth;
        for (int localX = 0; localX < newRegionWidth; localX++)
        {
            int x = localX + oldWidth;
            for (int y = 0; y < height; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (IsInSpecialRegion(pos))
                    visited[x][y] = true;
                else if (Map[x, y] == TileType.Grass && !_tileClusterGroupMap.ContainsKey(pos))
                    visited[x][y] = false;
                else
                    visited[x][y] = true;
            }
        }

        for (int localX = 0; localX < newRegionWidth; localX++)
        {
            int x = localX + oldWidth;
            for (int y = 0; y < height; y++)
            {
                if (!visited[x][y])
                {
                    List<Vector2Int> groupTiles = new List<Vector2Int>();
                    Queue<Vector2Int> queue = new Queue<Vector2Int>();
                    Vector2Int start = new Vector2Int(x, y);
                    queue.Enqueue(start);
                    visited[x][y] = true;
                    while (queue.Count > 0)
                    {
                        Vector2Int cur = queue.Dequeue();
                        groupTiles.Add(cur);
                        foreach (var nb in GetCardinalNeighbors(cur))
                        {
                            if (IsInBounds(nb) && nb.x >= oldWidth && !visited[nb.x][nb.y] &&
                                Map[nb.x, nb.y] == TileType.Grass)
                            {
                                visited[nb.x][nb.y] = true;
                                queue.Enqueue(nb);
                            }
                        }
                    }

                    ClusterGroup grassGroup = CreateClusterGroupFromTiles(groupTiles);
                    if (grassGroup.Tiles.Count >= 40)
                    {
                        List<ClusterGroup> splitGroups = SplitGrassClusterGroup(grassGroup);
                        foreach (var sg in splitGroups)
                            _grassClusterGroups.Add(sg);
                    }
                    else
                    {
                        _grassClusterGroups.Add(grassGroup);
                    }
                }
            }
        }
    }

    //Grass 외의 다른 미할당 타일들도 그룹화
    private void FinalizeUnassignedClusters(int oldWidth = 0)
    {
        bool[][] visited = new bool[_curWidth][];
        for (int i = 0; i < _curWidth; i++)
            visited[i] = new bool[height];

        int newRegionWidth = _curWidth - oldWidth;
        for (int localX = 0; localX < newRegionWidth; localX++)
        {
            int x = localX + oldWidth;
            for (int y = 0; y < height; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                if (!visited[x][y] && !_tileClusterGroupMap.ContainsKey(pos))
                {
                    List<Vector2Int> unassignedGroupTiles = new List<Vector2Int>();
                    Queue<Vector2Int> queue = new Queue<Vector2Int>();
                    queue.Enqueue(pos);
                    visited[x][y] = true;
                    while (queue.Count > 0)
                    {
                        Vector2Int current = queue.Dequeue();
                        unassignedGroupTiles.Add(current);
                        foreach (var neighbor in GetCardinalNeighbors(current))
                        {
                            if (IsInBounds(neighbor) && neighbor.x >= oldWidth && !visited[neighbor.x][neighbor.y] &&
                                !_tileClusterGroupMap.ContainsKey(neighbor))
                            {
                                visited[neighbor.x][neighbor.y] = true;
                                queue.Enqueue(neighbor);
                            }
                        }
                    }

                    ClusterGroup newGroup = CreateClusterGroupFromTiles(unassignedGroupTiles);
                    _grassClusterGroups.Add(newGroup);
                }
            }
        }
    }

    // 인자로 받아온 클러스터 그룹을 분할한다.
    private List<ClusterGroup> SplitGrassClusterGroup(ClusterGroup group)
    {
        List<ClusterGroup> result = new List<ClusterGroup>();
        int totalCount = group.Tiles.Count;

        int parts = totalCount / 20;
        if (parts < 2) parts = 1;

        // 남은 타일 집합
        HashSet<Vector2Int> remaining = new HashSet<Vector2Int>(group.Tiles);

        int baseSize = totalCount / parts;
        int remainder = totalCount % parts;

        for (int i = 0; i < parts; i++)
        {
            int targetSize = baseSize + (i < remainder ? 1 : 0);
            List<Vector2Int> subGroupTiles = new List<Vector2Int>();

            // 임의의 시작점 선택
            Vector2Int seed = Vector2Int.zero;
            foreach (var pos in remaining)
            {
                seed = pos;
                break;
            }

            if (seed == Vector2Int.zero) break;

            // BFS로 targetSize만큼의 연결된 타일 수집
            Queue<Vector2Int> q = new Queue<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            q.Enqueue(seed);
            visited.Add(seed);

            while (q.Count > 0 && subGroupTiles.Count < targetSize)
            {
                Vector2Int cur = q.Dequeue();
                if (remaining.Contains(cur))
                {
                    subGroupTiles.Add(cur);
                    remaining.Remove(cur);
                }

                foreach (var nb in GetCardinalNeighbors(cur))
                {
                    if (IsInBounds(nb) && !visited.Contains(nb) && remaining.Contains(nb))
                    {
                        visited.Add(nb);
                        q.Enqueue(nb);
                    }
                }
            }

            // 생성된 부분 그룹이 비어있지 않다면 클러스터 그룹 생성
            if (subGroupTiles.Count > 0)
            {
                ClusterGroup subGroup = CreateClusterGroupFromTiles(subGroupTiles);
                result.Add(subGroup);
            }
        }

        // 남은 타일이 있다면 하나의 그룹으로 추가
        if (remaining.Count > 0)
        {
            List<Vector2Int> leftover = new List<Vector2Int>(remaining);
            ClusterGroup subGroup = CreateClusterGroupFromTiles(leftover);
            result.Add(subGroup);
        }

        return result;
    }

    /// 모든 클러스터 그룹을 조사해서 연결되지 않은 그룹을 분할
    private void SplitDisconnectedClusterGroups()
    {
        List<ClusterGroup> allGroups = new List<ClusterGroup>();
        allGroups.AddRange(_grassClusterGroups);
        allGroups.AddRange(_mountainClusterGroups);
        allGroups.AddRange(_riverClusterGroups);
        allGroups.AddRange(_resourceClusterGroups);

        List<ClusterGroup> groupsToRemove = new List<ClusterGroup>();
        List<ClusterGroup> groupsToAdd = new List<ClusterGroup>();

        foreach (var group in allGroups)
        {
            List<ClusterGroup> splitGroups = SplitDisconnectedGroup(group);
            if (splitGroups.Count > 1)
            {
                groupsToRemove.Add(group);
                groupsToAdd.AddRange(splitGroups);
            }
        }

        foreach (var group in groupsToRemove)
            RemoveGroup(group);
        foreach (var newGroup in groupsToAdd)
        {
            TileType type = DetermineGroupType(newGroup);
            AddGroupToList(newGroup, type);
        }
    }

    // 그룹 내에서 연결되지 않은 타일을 다른 그룹으로 분할
    private List<ClusterGroup> SplitDisconnectedGroup(ClusterGroup group)
    {
        // 원래 그룹 내 타일 집합
        HashSet<Vector2Int> remaining = new HashSet<Vector2Int>(group.Tiles);
        List<ClusterGroup> newGroups = new List<ClusterGroup>();

        // 4방향으로 연결되어 있는 타일 단위로 flood fill
        while (remaining.Count > 0)
        {
            // 임의의 시작점 선택
            Vector2Int seed = remaining.First();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            List<Vector2Int> component = new List<Vector2Int>();

            queue.Enqueue(seed);
            remaining.Remove(seed);

            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                component.Add(cur);

                foreach (Vector2Int nb in GetCardinalNeighbors(cur))
                {
                    // 만약 인접 타일이 원래 그룹의 구성원이라면 같은 컴포넌트로 묶음
                    if (remaining.Contains(nb))
                    {
                        queue.Enqueue(nb);
                        remaining.Remove(nb);
                    }
                }
            }

            // component가 하나라도 있으면 새 그룹 생성
            if (component.Count > 0)
            {
                ClusterGroup newGroup = new ClusterGroup();
                newGroup.Tiles.AddRange(component);
                newGroup.RecalculateCenterAndDirection(height);
                // 각 타일의 그룹 매핑을 업데이트
                foreach (var tile in component)
                {
                    _tileClusterGroupMap[tile] = newGroup;
                }

                newGroups.Add(newGroup);
            }
        }

        return newGroups;
    }

    //그룹 내의 랜덤 타일 반환
    private TileType DetermineGroupType(ClusterGroup group)
    {
        if (group.Tiles.Count > 0)
        {
            Vector2Int tile = group.Tiles[0];
            return Map[tile.x, tile.y];
        }

        return TileType.None;
    }

    #endregion

    #region 공통 함수

    private bool IsInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < _curWidth && pos.y >= 0 && pos.y < height;
    }

    private int CountGrassTiles()
    {
        int count = 0;
        for (int x = 0; x < _curWidth; x++)
        for (int y = 0; y < height; y++)
            if (Map[x, y] == TileType.Grass)
                count++;
        return count;
    }

    // private int CountTiles(TileType type)
    // {
    //     int count = 0;
    //     for (int x = 0; x < _curWidth; x++)
    //     for (int y = 0; y < height; y++)
    //         if (Map[x, y] == type)
    //             count++;
    //     return count;
    // }

    private List<Vector2Int> GetCardinalNeighbors(Vector2Int pos)
    {
        return new List<Vector2Int>
        {
            new(pos.x + 1, pos.y),
            new(pos.x - 1, pos.y),
            new(pos.x, pos.y + 1),
            new(pos.x, pos.y - 1)
        };
    }

    private List<Vector2Int> GetNeighbors8(Vector2Int pos)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0)
                continue;
            neighbors.Add(new Vector2Int(pos.x + dx, pos.y + dy));
        }

        return neighbors;
    }

    private List<Vector2Int> GenerateRandomNonHoleyCluster(Vector2Int start, int targetCount, int maxIterations,
        System.Random rng,
        Func<Vector2Int, bool> traversePredicate, Func<Vector2Int, bool> includePredicate)
    {
        HashSet<Vector2Int> cluster = new HashSet<Vector2Int>();
        List<Vector2Int> frontier = new List<Vector2Int>();

        if (!traversePredicate(start))
            return new List<Vector2Int>();

        cluster.Add(start);

        foreach (var n in GetCardinalNeighbors(start))
        {
            if (IsInBounds(n) && traversePredicate(n))
                frontier.Add(n);
        }

        int iterations = 0;
        while (cluster.Count < targetCount && frontier.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            int index = rng.Next(frontier.Count);
            Vector2Int cell = frontier[index];
            frontier.RemoveAt(index);

            if (!traversePredicate(cell))
                continue;
            if (!includePredicate(cell))
                continue;

            cluster.Add(cell);

            List<Vector2Int> neighbors = GetCardinalNeighbors(cell);
            ShuffleList(neighbors, rng);
            foreach (var n in neighbors)
            {
                if (IsInBounds(n) && traversePredicate(n) && !cluster.Contains(n) && !frontier.Contains(n))
                    frontier.Add(n);
            }
        }

        if (iterations >= maxIterations)
            throw new MapGenerationException("GenerateRandomNonHoleyCluster: 반복 최대치를 초과했습니다.");
        cluster = FillClusterHoles(cluster);
        return new List<Vector2Int>(cluster);
    }

    private void ShuffleList(List<Vector2Int> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private HashSet<Vector2Int> FillClusterHoles(HashSet<Vector2Int> cluster)
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (var cell in cluster)
        {
            if (cell.x < minX) minX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y > maxY) maxY = cell.y;
        }

        minX -= 1;
        minY -= 1;
        maxX += 1;
        maxY += 1;

        int boxWidth = maxX - minX + 1;
        int boxHeight = maxY - minY + 1;
        bool[][] isCluster = new bool[boxWidth][];
        for (int x = 0; x < boxWidth; x++)
        {
            isCluster[x] = new bool[boxHeight];
        }

        foreach (var cell in cluster)
        {
            int bx = cell.x - minX;
            int by = cell.y - minY;
            isCluster[bx][by] = true;
        }

        bool[][] reachable = new bool[boxWidth][];
        for (int x = 0; x < boxWidth; x++)
        {
            reachable[x] = new bool[boxHeight];
        }

        Queue<(int, int)> q = new Queue<(int, int)>();
        for (int x = 0; x < boxWidth; x++)
        for (int y = 0; y < boxHeight; y++)
        {
            if (x == 0 || y == 0 || x == boxWidth - 1 || y == boxHeight - 1)
            {
                if (!isCluster[x][y])
                {
                    q.Enqueue((x, y));
                    reachable[x][y] = true;
                }
            }
        }

        int[] dx = new int[] { 1, -1, 0, 0 };
        int[] dy = new int[] { 0, 0, 1, -1 };

        while (q.Count > 0)
        {
            var (cx, cy) = q.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                int nx = cx + dx[i];
                int ny = cy + dy[i];
                if (nx >= 0 && ny >= 0 && nx < boxWidth && ny < boxHeight)
                {
                    if (!isCluster[nx][ny] && !reachable[nx][ny])
                    {
                        reachable[nx][ny] = true;
                        q.Enqueue((nx, ny));
                    }
                }
            }
        }

        for (int x = 0; x < boxWidth; x++)
        for (int y = 0; y < boxHeight; y++)
        {
            if (!reachable[x][y] && !isCluster[x][y])
            {
                int clusterX = x + minX;
                int clusterY = y + minY;
                cluster.Add(new Vector2Int(clusterX, clusterY));
            }
        }

        return cluster;
    }

    private bool IsCellNear(Vector2Int cell, Vector2Int point, int range)
    {
        return Mathf.Abs(cell.x - point.x) <= range && Mathf.Abs(cell.y - point.y) <= range;
    }

    // 가장 가까운 mountain이 아닌 타일 까지의 거리 계산
    private int GetDistanceToNonMountain(Vector2Int pos)
    {
        bool[][] visited = new bool[_curWidth][];
        for (int index = 0; index < _curWidth; index++)
        {
            visited[index] = new bool[height];
        }

        if (visited == null) throw new ArgumentNullException(nameof(visited));
        Queue<(Vector2Int pos, int dist)> q = new Queue<(Vector2Int, int)>();
        q.Enqueue((pos, 0));
        visited[pos.x][pos.y] = true;
        int[] dx = new int[] { 1, -1, 0, 0 };
        int[] dy = new int[] { 0, 0, 1, -1 };
        while (q.Count > 0)
        {
            var (current, dist) = q.Dequeue();
            // 현재 타일이 Mountain이 아니라면 바로 거리 반환
            if (Map[current.x, current.y] != TileType.Mountain)
            {
                return dist;
            }

            for (int i = 0; i < 4; i++)
            {
                Vector2Int next = new Vector2Int(current.x + dx[i], current.y + dy[i]);
                if (IsInBounds(next) && !visited[next.x][next.y])
                {
                    visited[next.x][next.y] = true;
                    q.Enqueue((next, dist + 1));
                }
            }
        }

        return 0; // 모든 타일이 Mountain인 경우(실제로 발생할 일은 드물다고 가정)
    }

    private bool IsInSpecialRegion(Vector2Int pos)
    {
        // posA 또는 posB 주변 5×5 영역 (중심에서 -2 ~ +2)
        return (Mathf.Abs(pos.x - _posA.x) <= 2 && Mathf.Abs(pos.y - _posA.y) <= 2) ||
               (Mathf.Abs(pos.x - _posB.x) <= 2 && Mathf.Abs(pos.y - _posB.y) <= 2);
    }

    private bool IsInNewRegion(Vector2Int localPos, int oldWidth)
    {
        return localPos.x >= 0 && localPos.x < (_curWidth - oldWidth) && localPos.y >= 0 && localPos.y < height;
    }

    #endregion

    #region 맵 오브젝트 생성

    private IEnumerator InstantiateMap(int oldWidth = 0)
    {
        // 마지막 보정 작업 실행
        EnsurePathConnectivity();

        //게임오버 생성
        if (oldWidth == 0)
        {
            gameOverObj = Instantiate(gameOverPrefab, new Vector3(-15f, 0f, 15f), Quaternion.identity);
            gameOverObj.GetComponent<NetworkObject>().Spawn();
            gameOverObj.SetActive(false);
        }

        //(디버그용) 경로 출력
        // CheckPath();

        int localSeed = _masterRng.Next();
        System.Random localRng = new System.Random(localSeed);

        //클러스터 그룹 수집
        List<ClusterGroup> allClusterGroups = _tileClusterGroupMap.Values.Distinct().ToList();
        allClusterGroups.RemoveAll(cg => cg.Tiles == null || cg.Tiles.Count == 0);

        // 클러스터 그룹별로 clusterPrefab 인스턴스 생성 및 Cluster 컴포넌트에 그룹 정보 할당
        Dictionary<ClusterGroup, GameObject> clusterGameObjects = new Dictionary<ClusterGroup, GameObject>();
        foreach (ClusterGroup cg in allClusterGroups)
        {
            GameObject clusterGo = Instantiate(clusterPrefab);
            clusterGo.name = $"Cluster_{cg.Direction}_{cg.CenterTile.x}_{cg.CenterTile.y}";
            clusterGameObjects.Add(cg, clusterGo);

            Cluster cluster = clusterGo.GetComponent<Cluster>();
            if (cluster)
            {
                cluster.NetworkObject.Spawn();
                RpcManager.Instance.SetParentRpc(_clusterParent.GetComponent<NetworkObject>().NetworkObjectId,
                    clusterGo.GetComponent<NetworkObject>().NetworkObjectId);
                cluster.SetOffset(SPAWN_OFFSET);
                cluster.ClusterGroup = cg;
                if (_specialClusterGroups.Contains(cg))
                {
                    cluster.isSpecial = true;
                }
                // Debug.Log($"클러스터 생성 완료: clusterSize: {cg.Tiles.Count}");
            }
            else
            {
                Debug.LogWarning($"Cluster 컴포넌트가 {clusterGo.name}에 없습니다.");
            }
        }

        // 클러스터 그룹들을 centerTile.x 기준 오름차순 정렬
        List<ClusterGroup> sortedClusters = allClusterGroups.OrderBy(cg => cg.CenterTile.x).ToList();
        for (int i = 0; i < sortedClusters.Count; i++)
        {
            clusterGameObjects[sortedClusters[i]].transform.SetSiblingIndex(i);
        }

        //클러스터마다 iron1의 스케일을 공유하기 위함
        Dictionary<ClusterGroup, float> clusterIron1Scales = new Dictionary<ClusterGroup, float>();
        foreach (ClusterGroup cg in allClusterGroups)
        {
            // 각 클러스터마다 0.7~1.0 사이의 랜덤 scale 값을 할당
            float scale = Random.Range(0.7f, 1.0f);
            clusterIron1Scales[cg] = scale;
        }

        int newRegionWidth = _curWidth - oldWidth;
        for (int localX = 0; localX < newRegionWidth; localX++)
        {
            int x = localX + oldWidth;
            for (int y = 0; y < height; y++)
            {
                Vector3 basePos = new Vector3(x, 0, y);
                if (_tileClusterGroupMap.TryGetValue(new Vector2Int(x, y), out ClusterGroup cg))
                {
                    basePos += cg.Direction == ClusterDirection.Upper
                        ? Vector3.up * SPAWN_OFFSET
                        : Vector3.down * SPAWN_OFFSET;
                }

                GameObject tileInstance = null;
                Vector2Int tilePos = new Vector2Int(x, y);

                if (tilePos == _posA)
                {
                    tileInstance = Instantiate(startPointPrefab, basePos, Quaternion.identity);
                }
                else if (tilePos == _posB)
                {
                    tileInstance = Instantiate(endPointPrefab, basePos, Quaternion.identity);
                }
                else
                {
                    switch (Map[x, y])
                    {
                        case TileType.Grass:
                            if (_tileClusterGroupMap.TryGetValue(tilePos, out ClusterGroup group) &&
                                _specialClusterGroups.Contains(group))
                                tileInstance = Instantiate(grass0Prefab, basePos, Quaternion.identity);

                            else
                                tileInstance = Instantiate(localRng.NextDouble() < 0.05f ? grass1Prefab : grass0Prefab,
                                    basePos, Quaternion.identity);
                            break;
                        case TileType.Wood:
                            tileInstance = Instantiate(localRng.NextDouble() < 0.5f ? wood1Prefab : wood0Prefab,
                                basePos,
                                Quaternion.identity);
                            break;
                        case TileType.Iron:
                            //주변 4방향이 모두 Iron이면 iron0, 아니면 iron1
                            bool allCardinalIron = true;
                            foreach (Vector2Int neighbor in GetCardinalNeighbors(tilePos))
                            {
                                if (IsInBounds(neighbor) && Map[neighbor.x, neighbor.y] != TileType.Iron)
                                {
                                    allCardinalIron = false;
                                    break;
                                }
                            }

                            tileInstance = allCardinalIron
                                ? Instantiate(iron0Prefab, basePos, Quaternion.identity)
                                : Instantiate(iron1Prefab, basePos, Quaternion.identity);
                            break;
                        case TileType.Mountain:
                            bool allCardinalMountain = true;
                            foreach (Vector2Int neighbor in GetCardinalNeighbors(tilePos))
                            {
                                if (IsInBounds(neighbor) && Map[neighbor.x, neighbor.y] != TileType.Mountain)
                                {
                                    allCardinalMountain = false;
                                    break;
                                }
                            }

                            tileInstance = allCardinalMountain
                                ? Instantiate(mountain0Prefab, basePos, Quaternion.identity)
                                : Instantiate(mountain1Prefab, basePos, Quaternion.identity);
                            break;
                        case TileType.River:
                            tileInstance = Instantiate(riverPrefab, basePos, Quaternion.identity);

                            break;
                    }
                }

                // 생성한 타일 설정 및 부모 및 클러스터 그룹 할당
                if (tileInstance)
                {
                    Blocks blocks = tileInstance.GetComponent<Blocks>();

                    if (_tileClusterGroupMap.TryGetValue(tilePos, out ClusterGroup group))
                    {
                        blocks.ClusterGroup = group;
                        blocks.desiredParent = clusterGameObjects.TryGetValue(group, out var o)
                            ? o.transform
                            : _clusterParent;
                    }
                    else
                    {
                        Debug.LogWarning($"{basePos.x}, {basePos.y}에 클러스터 그룹이 할당되어 있지 않음.");
                        blocks.ClusterGroup = null;
                        blocks.desiredParent = _clusterParent;
                        // tileInstance.transform.SetParent(clusterParent);
                    }

                    if (blocks) blocks.NetworkObject.Spawn();

                    if (blocks is Water water)
                    {
                        if (water)
                        {
                            if (tilePos.x == 0)
                                RpcManager.Instance.ToggleWaterFallRpc(water.NetworkObjectId, 1);
                            if (tilePos.y == 0)
                                RpcManager.Instance.ToggleWaterFallRpc(water.NetworkObjectId, 3);
                            if (tilePos.y == height - 1)
                                RpcManager.Instance.ToggleWaterFallRpc(water.NetworkObjectId, 7);
                        }
                    }
                }
            }

            yield return null;
        }

        yield return new WaitForSeconds(1.0f);

        Debug.Log("맵 타일 생성 완료, 스폰 애니메이션 시작");
        for (int i = 0; i < sortedClusters.Count; i++)
        {
            Cluster clusterComponent = clusterGameObjects[sortedClusters[i]].GetComponent<Cluster>();
            if (clusterComponent)
            {
                clusterComponent.PlaySpawnAnimation(i);
            }
        }

        yield return null;
    }

    //레일을 스폰(MapGenerator이 아닌 Cluster에서 호출)
    public void SpawnRails(int oldWidth = 0)
    {
        // Debug.Log($"SpawnRails called (oldWidth={oldWidth})");
        // if (railPrefab == null) Debug.LogError("railPrefab is NULL!");

        int railYAdjustment = -1;
        // List<RailController> posARails = new List<RailController>();
        // List<RailController> posBRails = new List<RailController>();
        RailController firstDestination = null;

        int startFirstRailX = -2;
        int endFirstRailX = -2;

        if (oldWidth == 0)
        {
            //첫 스폰 시
            // posA에 대한 Rail 생성 (왼쪽2칸 오른쪽4칸)
            for (int dx = startFirstRailX; dx <= 4; dx++)
            {
                Vector3 railPos = new Vector3(_posA.x + dx, 0, _posA.y + railYAdjustment) + _railSpawnOffset +
                                  Vector3.up * SPAWN_OFFSET;
                GameObject railGo = Instantiate(railPrefab, railPos, Quaternion.identity);
                railGo.name = $"Rail ({_posA.x + dx}:{_posA.y + railYAdjustment})";
                if (railGo)
                {
                    var rc = railGo.GetComponent<RailController>();
                    if (dx == startFirstRailX) rc.isStartFirstRail = true;
                    rc.NetworkObject.Spawn();
                    rc.SetRail();
                    rc.PlaySpawnAnimation(SPAWN_OFFSET);
                    // posARails.Add(rc);
                    if (dx == 1) firstDestination = rc;
                }
            }

            // posB에 대한 Rail 생성 (좌우 2칸씩, 총 5칸)
            for (int dx = endFirstRailX; dx <= 2; dx++)
            {
                Vector3 railPos = new Vector3(_posB.x + dx, 0, _posB.y + railYAdjustment) + _railSpawnOffset +
                                  Vector3.up * SPAWN_OFFSET;
                GameObject railGo = Instantiate(railPrefab, railPos, Quaternion.identity);
                railGo.name = $"Rail ({_posB.x + dx}:{_posB.y + railYAdjustment})";
                if (railGo)
                {
                    var rc = railGo.GetComponent<RailController>();
                    if (dx == endFirstRailX) rc.isEndFirstRail = true;
                    rc.NetworkObject.Spawn();
                    rc.SetRail();
                    rc.PlaySpawnAnimation(SPAWN_OFFSET);
                    // posBRails.Add(rc);
                }
            }
            
            _trainHead = SpawnTrain(firstDestination);
        }
        else
        {
            //확장 스폰 시
            RailManager.Instance.GetEndFirstRail().isEndFirstRail = false; //기존 플래그 초기ㅗ하
            for (int dx = endFirstRailX; dx <= 2; dx++)
            {
                Vector3 railPos = new Vector3(_posB.x + dx, 0, _posB.y + railYAdjustment) + _railSpawnOffset +
                                  Vector3.up * SPAWN_OFFSET;
                GameObject railGo = Instantiate(railPrefab, railPos, Quaternion.identity);
                railGo.name = $"Rail ({_posB.x + dx}:{_posB.y + railYAdjustment})";
                if (railGo)
                {
                    Debug.Log("확장레일스폰");
                    var rc = railGo.GetComponent<RailController>();
                    if (dx == endFirstRailX) rc.isEndFirstRail = true;
                    rc.NetworkObject.Spawn();
                    rc.SetRail();
                    rc.PlaySpawnAnimation(SPAWN_OFFSET);
                    // posBRails.Add(rc);
                }
            }
        }

        // RailManager.Instance.DebugLogAllChains();
    }

    //디버그용 레일 생성
    private void SpawnRail(int x, int y)
    {
        Vector3 railPos = new Vector3(_posA.x + x, 0, _posA.y + y);
        railPos += _railSpawnOffset;
        railPos.y += SPAWN_OFFSET;
        GameObject railGo = Instantiate(railPrefab, railPos, Quaternion.identity);


        if (railGo)
        {
            RailController rc = railGo.GetComponent<RailController>();
            rc.NetworkObject.Spawn();
            rc.PlaySpawnAnimation(SPAWN_OFFSET);
        }
    }

    //열차를 스폰
    private Train SpawnTrain(RailController firstDestination)
    {
        Vector3 trainPos = new Vector3(_posA.x + 1, 0, _posA.y + -1);
        trainPos += _trainSpawnOffset;
        trainPos.y += SPAWN_OFFSET;
        
        GameObject trainGo = Instantiate(trainCarHeadPrefab);
        trainGo.transform.position = trainPos;
        Train rc = trainGo.GetComponent<Train>();
        // rc.transform.rotation = Quaternion.Euler(0, 90, 0);
        rc.SetDestinationRail(firstDestination);
        
        return rc;
    }

    #endregion

    #region 외부 접근용 함수

    public void SetSeed(string seed)
    {
        // Debug.Log("시드설정");
        mapSeed = seed;
        int masterSeed = mapSeed.GetHashCode();
        _masterRng = new System.Random(masterSeed);
        Random.InitState(masterSeed);
    }

    // 확장(추가 생성) 여부를 반환 (extensionCount가 0이면 첫 생성)
    public bool IsInitialGeneration => _extensionCount == 0;

    public int GetOldWidth()
    {
        return _curWidth - width;
    }

    public Vector2Int GetPosA()
    {
        return _posA;
    }

    public Vector2Int GetPosB()
    {
        return _posB;
    }

    public string GetSeed()
    {
        return mapSeed;
    }

    #endregion

    #region 디버그 함수

    [Header("디버그용 변수")] public int visitX;
    public int visitY;
    private int _checkCount;
    private void CheckVisit()
    {
        if (visitX < 0 || visitX >= _curWidth || visitY < 0 || visitY >= height)
        {
            Debug.LogError($"CheckVisit: 방문 좌표 ({visitX}, {visitY})가 맵 범위를 벗어났습니다.");
            return;
        }

        Vector2Int target = new Vector2Int(visitX, visitY);
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> parent = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(_posA);
        visited.Add(_posA);

        bool found = false;
        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            if (cur == target)
            {
                found = true;
                break;
            }

            foreach (var d in new Vector2Int[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) })
            {
                Vector2Int next = cur + d;
                if (IsInBounds(next) && !visited.Contains(next))
                {
                    if (Map[next.x, next.y] != TileType.Mountain)
                    {
                        queue.Enqueue(next);
                        visited.Add(next);
                        parent[next] = cur;
                    }
                }
            }
        }

        if (found)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            Vector2Int cur = target;
            while (cur != _posA)
            {
                path.Add(cur);
                cur = parent[cur];
            }

            path.Add(_posA);
            path.Reverse();

            string pathStr = "최단 경로: ";
            foreach (var step in path)
                pathStr += $"({step.x},{step.y}) -> ";
            pathStr = pathStr.Substring(0, pathStr.Length - 4);
            Debug.Log(
                $"CheckVisit: [{_checkCount}] ({visitX},{visitY})에 도달 가능합니다. 해당 좌표의 타일 타입: {Map[visitX, visitY]}, {pathStr}");
        }
        else
        {
            Debug.Log($"CheckVisit: [{_checkCount}] 해당 좌표에 도달할 수 없습니다. 해당 좌표의 타일 타입: {Map[visitX, visitY]}");
        }

        _checkCount++;
    }

    //출발점에서 도착점까지 경로를 체크함.
    public void CheckPath()
    {
        // posA와 posB 좌표 출력
        Debug.Log($"CheckPath: posA = ({_posA.x}, {_posA.y}), posB = ({_posB.x}, {_posB.y})");

        // 플레이어는 산(Mountain)이 아닌 모든 타일을 통과할 수 있으므로,
        // allowedPredicate로 Map[pos.x, pos.y] != TileType.Mountain 조건을 사용합니다.
        List<Vector2Int> path = FindSimplePath(_posA, _posB, pos => { return Map[pos.x, pos.y] != TileType.Mountain; });

        if (path == null)
        {
            Debug.Log("CheckPath: posA에서 posB까지 도달할 수 없습니다.");
        }
        else
        {
            // 경로를 (x,y) 좌표 형식의 문자열로 변환
            string pathStr = "";
            foreach (Vector2Int step in path)
            {
                pathStr += $"({step.x},{step.y}) ";
            }

            Debug.Log("CheckPath: posA에서 posB까지 최단 경로: " + pathStr.Trim());
            // 최단 경로의 "거리"는 경로 상에서의 이동 횟수 (노드 개수 - 1)
            Debug.Log("최단 거리: " + (path.Count - 1));
        }
    }

    #endregion
}

#region 커스텀 예외 클래스

public class MapGenerationException : Exception
{
    public MapGenerationException(string message) : base(message)
    {
    }
}

#endregion

#region 클러스터 그룹 관련 enum 및 클래스

public enum ClusterDirection
{
    Upper,
    Under
}

public class ClusterGroup
{
    public readonly List<Vector2Int> Tiles = new();
    public ClusterDirection Direction;
    public Vector2Int CenterTile;


    //해당 클러스터 그룹의 방향(Under, Upper를 설정)
    public void RecalculateCenterAndDirection(int mapHeight)
    {
        if (Tiles.Count == 0)
            return;

        int minY = int.MaxValue;
        int maxY = int.MinValue;
        foreach (var cell in Tiles)
        {
            if (cell.y < minY) minY = cell.y;
            if (cell.y > maxY) maxY = cell.y;
        }

        float centerY = (minY + maxY) / 2f;

        // 중앙에 가장 가까운 타일 선택
        Vector2Int closest = Tiles[0];
        float bestDiff = Math.Abs(closest.y - centerY);
        foreach (var cell in Tiles)
        {
            float diff = Math.Abs(cell.y - centerY);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                closest = cell;
            }
        }

        CenterTile = closest;

        // 전체 맵 높이의 절반보다 크면 Upper, 그렇지 않으면 Under
        Direction = CenterTile.y > mapHeight / 2 ? ClusterDirection.Upper : ClusterDirection.Under;
    }
}

#endregion