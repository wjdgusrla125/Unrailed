using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class MapGenerator : MonoBehaviour
{
    #region 변수 선언 및 직렬화 필드

    [SerializeField, Header("맵 시드값(비울 경우 랜덤)")]
    private string mapSeed;

    private System.Random _masterRng; // 랜덤 시드 지정

    private bool _isMapGenerating = false;
    public Action<bool> IsMapGenerated; // 맵 생성 성공 여부 전달

    [Header("맵 크기 설정")] [SerializeField] private int width; // 맵의 가로 길이
    [SerializeField] private int height; // 맵의 세로 길이
    [SerializeField] private int pathLength; // 출발지점과 도착지점 사이의 거리
    [SerializeField] private int minHorizontalDistance; // 출발지점과 도착지점 사이의 최소 가로 거리
    [SerializeField] private int maxGrassTileCount; // 풀 타일 최대치
    [SerializeField] private int minGrassTileCount; // 풀 타일 최소치

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
    [SerializeField] private int minRiverLength = 8; // 강의 최소 크기
    [SerializeField] private int maxRiverCellsAllowed = 35; // 강의 최대 셀 크기
    [SerializeField] private float lateralSpreadProbability = 0.4f; // 강 확산 확률
    [SerializeField] private int elongatedRiverMinWidth = 1; // 강 최소 폭
    [SerializeField] private int elongatedRiverMaxWidth = 2; // 강 최대 폭

    [Header("프리팹")] [SerializeField] private Transform blockParent; // 생성된 블럭들을 담을 오브젝트
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private GameObject woodPrefab;
    [SerializeField] private GameObject ironPrefab;
    [SerializeField] private GameObject riverPrefab;
    [SerializeField] private GameObject mountainPrefab;
    [SerializeField] private GameObject startPointPrefab;
    [SerializeField] private GameObject endPointPrefab;

    private enum TileType
    {
        None,
        Grass,
        Wood,
        Iron,
        Mountain,
        River
    }

    private TileType[,] _map;
    private Vector2Int _posA; // 시작점
    private Vector2Int _posB; // 도착점

    // 디버그용, Q키 쿨다운 (1초)
    private const float QCooldown = 1f;
    private float _lastQTime = -10f;

    // 반복 최대 횟수 상수
    private const int MAX_ITERATIONS = 10000;

    #endregion

    #region Event Function

    private void Start()
    {
        StartMapGeneration();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q) && Time.time - _lastQTime > QCooldown)
        {
            _lastQTime = Time.time;
            Debug.Log("Q입력, 시드 랜덤화 후 맵 생성");
            for (int i = blockParent.childCount - 1; i >= 0; i--)
                Destroy(blockParent.GetChild(i).gameObject);
            mapSeed = string.Empty;
            StartCoroutine(GenerateMapCoroutine());
        }

        if (Input.GetKeyDown(KeyCode.E) && Time.time - _lastQTime > QCooldown)
        {
            _lastQTime = Time.time;
            Debug.Log("E입력, 시드 유지 맵 생성");
            for (int i = blockParent.childCount - 1; i >= 0; i--)
                Destroy(blockParent.GetChild(i).gameObject);
            StartCoroutine(GenerateMapCoroutine());
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log($"R입력, ({visitX}, {visitY}) visit 확인");
            CheckVisit();
        }
    }

    #endregion

    #region 맵 생성 메인 코루틴

    public void StartMapGeneration()
    {
        StartCoroutine(GenerateMapCoroutine());
    }

    private IEnumerator GenerateMapCoroutine()
    {
        _isMapGenerating = true;
        try
        {
            InitializeSeed();
            InitializeMap();
            SetPath();
            GenerateValidPath();
            GenerateMountains();
            EnsureStartEndInnerClear();
            GenerateRivers();
            PlaceDestructibleObstacles();
            GenerateGrassToMountainClusters();
            EnsurePathConnectivity();
            EnsureStartEndInnerClear();
            EnsureWoodAccessibility();
            EnsureStartEndInnerClear();
            EnsureEndpointCorridor();
            EnsureReachability();
            FinalAdjustments();
            EnsureStartEndInnerClear();
            AdjustPathForRiverAndWood();
            InstantiateMap();

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

    #region 시드 및 맵 초기화

    private void InitializeSeed()
    {
        if (string.IsNullOrEmpty(mapSeed))
            mapSeed = DateTime.Now.Ticks.ToString();

        int masterSeed = mapSeed.GetHashCode();
        _masterRng = new System.Random(masterSeed);
        Random.InitState(masterSeed);
    }

    private void InitializeMap()
    {
        _map = new TileType[width, height];
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            _map[x, y] = TileType.Grass;
    }

    #endregion

    #region 출발지, 도착지 설정

    // 출발지/도착지 결정 (출발지에서 도착지까지 맨해튼 거리가 pathLength, 수평 거리 최소)
    void SetPath()
    {
        int minXb = width / 2;
        int minManhattanDistance = minXb - 1;

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

        _posA = new Vector2Int(1, height / 2);
        int attempts = 0;
        int maxAttempts = MAX_ITERATIONS;
        do
        {
            _posB = new Vector2Int(Random.Range(width / 2, width - 1), Random.Range(1, height - 1));
            attempts++;
            if (attempts > maxAttempts)
                throw new MapGenerationException("SetPath: 최대 시도 횟수를 초과했습니다.");
        } while ((Mathf.Abs(_posA.x - _posB.x) + Mathf.Abs(_posA.y - _posB.y) != pathLength) ||
                 (Mathf.Abs(_posA.x - _posB.x) < minHorizontalDistance));
    }

    // 시작점에서 도착점까지 경로를 Grass로 설정
    private void GenerateValidPath()
    {
        Vector2Int current = _posA;
        int iterations = 0;
        int maxIterations = MAX_ITERATIONS;
        while (current != _posB && iterations < maxIterations)
        {
            iterations++;
            if (current.x != _posB.x)
                current.x += (_posB.x > current.x) ? 1 : -1;
            else if (current.y != _posB.y)
                current.y += (_posB.y > current.y) ? 1 : -1;
            _map[current.x, current.y] = TileType.Grass;
        }

        if (iterations >= maxIterations)
            throw new MapGenerationException("GenerateValidPath: 최대 반복 횟수를 초과했습니다.");
    }

    #endregion

    #region 산 생성 (Mountain)

    private void GenerateMountains()
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
                    startX = localRng.Next(0, width);
                    startY = height - 1;
                    break;
                case 1:
                    startX = localRng.Next(0, width);
                    startY = 0;
                    break;
                case 2:
                    startX = 0;
                    startY = localRng.Next(0, height);
                    break;
                case 3:
                    startX = width - 1;
                    startY = localRng.Next(0, height);
                    break;
            }

            if ((startX == _posA.x && startY == _posA.y) ||
                (startX == _posB.x && startY == _posB.y))
            {
                continue;
            }

            int clusterSize = localRng.Next(mountainClusterSizeMin, mountainClusterSizeMax + 1);
            Queue<Vector2Int> mountainQueue = new Queue<Vector2Int>();
            mountainQueue.Enqueue(new Vector2Int(startX, startY));
            _map[startX, startY] = TileType.Mountain;
            int count = 1;
            int iterations = 0;
            int maxIterations = MAX_ITERATIONS;
            while (mountainQueue.Count > 0 && count < clusterSize && iterations < maxIterations)
            {
                iterations++;
                Vector2Int cur = mountainQueue.Dequeue();
                foreach (var n in GetNeighbors8(cur))
                {
                    if (IsInBounds(n) && _map[n.x, n.y] == TileType.Grass)
                    {
                        if (n == _posA || n == _posB)
                            continue;

                        float chance = 0.7f * (1f - (float)count / clusterSize);
                        if (localRng.NextDouble() < chance)
                        {
                            _map[n.x, n.y] = TileType.Mountain;
                            mountainQueue.Enqueue(n);
                            count++;
                        }
                    }
                }
            }

            if (iterations >= maxIterations)
                throw new MapGenerationException("GenerateMountains: 반복 최대치를 초과했습니다.");
        }

        ForceStartEndGrass();
    }

    #endregion

    #region 강 생성 (River)

    private void GenerateRivers()
    {
        int riverCount = Random.Range(minRiverCount, maxRiverCount + 1);
        int riversGenerated = 0;
        int outerAttempts = 0;

        while (riversGenerated < riverCount && outerAttempts < MAX_ITERATIONS)
        {
            outerAttempts++;
            List<Vector2Int> riverCells;
            if (riversGenerated == 0)
            {
                Debug.Log("길쭉한 River 생성 시도");
                riverCells = GenerateElongatedRiver();
            }
            else
            {
                Debug.Log("둥근 River 생성 시도");
                riverCells = GenerateRoundedRiver();
            }

            if (riverCells.Count >= minRiverLength)
            {
                if (riverCells.Count > maxRiverCellsAllowed)
                    riverCells = riverCells.GetRange(0, maxRiverCellsAllowed);

                foreach (var cell in riverCells)
                {
                    _map[cell.x, cell.y] = TileType.River;
                }

                riversGenerated++;
            }
            else
            {
                Debug.LogWarning("해당 시도에서 최소 길이를 만족하는 River를 생성하지 못함");
            }
        }

        if (riversGenerated < riverCount)
            throw new MapGenerationException("GenerateRivers: 일부 강 생성에 실패했습니다 (최대 시도 횟수 초과).");
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
                    startX = localRng.Next(0, width);
                    startY = height - 1;
                    direction = new Vector2Int(0, -1);
                    break;
                case 1:
                    startX = localRng.Next(0, width);
                    startY = 0;
                    direction = new Vector2Int(0, 1);
                    break;
                case 2:
                    startX = 0;
                    startY = localRng.Next(0, height);
                    direction = new Vector2Int(1, 0);
                    break;
                case 3:
                    startX = width - 1;
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
                Debug.Log($"강 생성 완료, 시도 횟수: {attempt}");
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
                    startX = localRng.Next(0, width);
                    startY = height - 1;
                    break;
                case 1:
                    startX = localRng.Next(0, width);
                    startY = 0;
                    break;
                case 2:
                    startX = 0;
                    startY = localRng.Next(0, height);
                    break;
                case 3:
                    startX = width - 1;
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
                    if (IsInBounds(n) && _map[n.x, n.y] == TileType.Grass && !candidateRiver.Contains(n))
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
                Debug.Log($"강 생성 완료, 시도 횟수: {attempt}");
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
                if (IsInBounds(n) && _map[n.x, n.y] == TileType.Grass && !candidateRiver.Contains(n))
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
                    if (IsInBounds(c) && _map[c.x, c.y] == TileType.Grass && !candidateRiver.Contains(c))
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

    private void PlaceDestructibleObstacles()
    {
        int localSeed = _masterRng.Next();
        System.Random localRng = new System.Random(localSeed);
        int totalWood = 0, totalIron = 0;
        bool assignWoodNext = true;

        for (int i = 0; i < destructibleClusterCount; i++)
        {
            int x = localRng.Next(0, width);
            int y = localRng.Next(0, height);
            if (_map[x, y] != TileType.Grass)
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
            {
                obstacleType = TileType.Wood;
            }
            else if (totalIron < minIronCount)
            {
                obstacleType = TileType.Iron;
            }
            else
            {
                continue;
            }

            foreach (var cell in cluster)
            {
                if (_map[cell.x, cell.y] == TileType.Grass)
                {
                    _map[cell.x, cell.y] = obstacleType;
                    if (obstacleType == TileType.Wood)
                        totalWood++;
                    else
                        totalIron++;
                }
            }
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
    private void GenerateGrassToMountainClusters()
    {
        int localSeed = _masterRng.Next();
        System.Random localRng = new System.Random(localSeed);
        int loopCounter = 0;
        while (CountGrassTiles() >= maxGrassTileCount && loopCounter < MAX_ITERATIONS)
        {
            loopCounter++;
            List<Vector2Int> randomGrassTiles = new List<Vector2Int>();
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (_map[x, y] == TileType.Grass)
                    randomGrassTiles.Add(new Vector2Int(x, y));

            if (randomGrassTiles.Count == 0)
            {
                Debug.Log("GenerateGrassToMountainClusters: 맵에 Grass 타일이 없음. 루프 탈출");
                break;
            }

            Vector2Int start = randomGrassTiles[localRng.Next(0, randomGrassTiles.Count)];
            int clusterSize = localRng.Next(mountainClusterSizeMin, mountainClusterSizeMax + 1);
            Queue<Vector2Int> mountainQueue = new Queue<Vector2Int>();
            mountainQueue.Enqueue(start);
            _map[start.x, start.y] = TileType.Mountain;
            int count = 1;
            int iterations = 0;
            int maxIterations = MAX_ITERATIONS;
            while (mountainQueue.Count > 0 && count < clusterSize && iterations < maxIterations)
            {
                iterations++;
                Vector2Int cur = mountainQueue.Dequeue();
                foreach (var n in GetNeighbors8(cur))
                {
                    if (IsInBounds(n) && _map[n.x, n.y] == TileType.Grass)
                    {
                        float chance = 0.7f * (1f - (float)count / clusterSize);
                        if (localRng.NextDouble() < chance)
                        {
                            _map[n.x, n.y] = TileType.Mountain;
                            mountainQueue.Enqueue(n);
                            count++;
                            if (count >= clusterSize)
                                break;
                        }
                    }
                }
            }

            if (iterations >= maxIterations)
                throw new MapGenerationException("GenerateGrassToMountainClusters: 반복 최대치를 초과했습니다.");
        }

        if (loopCounter >= MAX_ITERATIONS)
            throw new MapGenerationException("GenerateGrassToMountainClusters: 루프 제한에 도달했습니다.");
        ForceStartEndGrass();
    }

    // 출발/도착 주변 5*5 영역을 Grass로 설정
    private void EnsureStartEndInnerClear()
    {
        for (int dx = -2; dx <= 2; dx++)
        for (int dy = -2; dy <= 2; dy++)
        {
            int nx = _posA.x + dx, ny = _posA.y + dy;
            if (IsInBounds(new Vector2Int(nx, ny)))
                _map[nx, ny] = TileType.Grass;
        }

        for (int dx = -2; dx <= 2; dx++)
        for (int dy = -2; dy <= 2; dy++)
        {
            int nx = _posB.x + dx, ny = _posB.y + dy;
            if (IsInBounds(new Vector2Int(nx, ny)))
                _map[nx, ny] = TileType.Grass;
        }
    }

    // 부족한 Grass 타일 확보 (강 셀 제외)
    private void EnsureAdjacentGrassTiles()
    {
        int currentGrass = CountGrassTiles();
        int iterations = 0;
        int maxIterations = MAX_ITERATIONS;
        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (_map[x, y] == TileType.Mountain)
            {
                Vector2Int pos = new Vector2Int(x, y);
                foreach (Vector2Int n in GetCardinalNeighbors(pos))
                {
                    if (IsInBounds(n) &&
                        (_map[n.x, n.y] == TileType.Grass ||
                         _map[n.x, n.y] == TileType.Wood ||
                         _map[n.x, n.y] == TileType.Iron))
                    {
                        candidates.Add(pos);
                        break;
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
            if (_map[candidate.x, candidate.y] == TileType.Mountain)
            {
                _map[candidate.x, candidate.y] = TileType.Grass;
                currentGrass++;
                candidates.RemoveAt(index);
            }
        }

        if (iterations >= maxIterations)
            throw new MapGenerationException("EnsureAdjacentGrassTiles: 반복 최대치를 초과했습니다.");
    }

    // 도달 불가능한 영역을 Mountain으로 전환
    private void EnsureReachability()
    {
        bool[][] visited = new bool[width][];
        for (int i = 0; i < width; i++)
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
                    if (_map[next.x, next.y] != TileType.Mountain)
                    {
                        visited[next.x][next.y] = true;
                        queue.Enqueue(next);
                    }
                }
            }
        }

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (!visited[x][y])
                _map[x, y] = TileType.Mountain;
    }

    // 부족 자원 보충 및 인접 Grass 확보
    private void FinalAdjustments()
    {
        EnsureMinimumDestructibleObstacles();
        EnsureAdjacentGrassTiles();
    }

    private void EnsureMinimumDestructibleObstacles()
    {
        int localSeed = _masterRng.Next();
        System.Random localRng = new System.Random(localSeed);
        int currentWood = CountTiles(TileType.Wood);
        int currentIron = CountTiles(TileType.Iron);
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
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (_map[x, y] == TileType.Mountain)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    if (IsCellNear(pos, _posA, 2) || IsCellNear(pos, _posB, 2))
                        continue;

                    foreach (Vector2Int neighbor in GetCardinalNeighbors(pos))
                    {
                        if (IsInBounds(neighbor))
                        {
                            TileType neighborType = _map[neighbor.x, neighbor.y];
                            if (neighborType == TileType.Grass || neighborType == TileType.Wood ||
                                neighborType == TileType.Iron || neighborType == TileType.River)
                            {
                                candidates.Add(pos);
                                break;
                            }
                        }
                    }
                }
            }

            if (candidates.Count == 0)
                throw new MapGenerationException("EnsureMinimumDestructibleObstacles: 후보 Mountain 타일이 없습니다.");

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
                TileType cellType = _map[cell.x, cell.y];
                if (cellType == TileType.Mountain || cellType == TileType.Grass || cellType == TileType.River)
                    _map[cell.x, cell.y] = targetResource;
            }

            currentWood = CountTiles(TileType.Wood);
            currentIron = CountTiles(TileType.Iron);
        }

        if (currentWood < minWoodCount)
            Debug.LogWarning($"[EnsureMinDestrObs] Wood 최소치({minWoodCount}) 도달 실패. 최종 Wood = {currentWood}");
        if (currentIron < minIronCount)
            Debug.LogWarning($"[EnsureMinDestrObs] Iron 최소치({minIronCount}) 도달 실패. 최종 Iron = {currentIron}");
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
                TileType type = _map[pos.x, pos.y];
                return type is TileType.Grass or TileType.Mountain or TileType.River;
            },
            (pos) =>
            {
                TileType type = _map[pos.x, pos.y];
                return type is TileType.Grass or TileType.Mountain or TileType.River;
            }
        );
    }

    // 도착점에서 오른쪽 끝까지 통로 확보
    private void EnsureEndpointCorridor()
    {
        int row = _posB.y;
        for (int x = _posB.x; x < width; x++)
            _map[x, row] = TileType.Grass;

        if (row - 1 >= 0)
            for (int x = _posB.x; x < width; x++)
                _map[x, row - 1] = TileType.Grass;

        if (row + 1 < height)
            for (int x = _posB.x; x < width; x++)
                _map[x, row + 1] = TileType.Grass;
    }

    // 시작점에서 최소한 하나의 Wood 클러스터 접근 통로 생성
    private void EnsureWoodAccessibility()
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
            if (_map[cur.x, cur.y] == TileType.Wood)
            {
                targetWood = cur;
                foundWood = true;
                break;
            }

            foreach (var d in new Vector2Int[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) }) 
            {
                Vector2Int nxt = cur + d;
                if (IsInBounds(nxt) && !visited.Contains(nxt))
                {
                    if (_map[nxt.x, nxt.y] != TileType.Mountain && _map[nxt.x, nxt.y] != TileType.River)
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
            for (int i = 0; i < width; i++)
            for (int j = 0; j < height; j++)
            {
                if (_map[i, j] == TileType.Wood)
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
                Debug.LogWarning("EnsureWoodAccessibility: Wood 셀이 전혀 없음");
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

        foreach (var cell in path)
            _map[cell.x, cell.y] = TileType.Grass;

        Debug.Log("EnsureWoodAccessibility: Wood 통로 생성 완료");
    }

    // InstantiateMap() 호출 직전에 실행할 보정 함수
    private void AdjustPathForRiverAndWood()
    {
        List<Vector2Int> simplePath = FindSimplePath(_posA, _posB, pos =>
        {
            TileType type = _map[pos.x, pos.y];
            return type == TileType.Grass || type == TileType.Wood || type == TileType.Iron;
        });
        if (simplePath != null)
        {
            Debug.Log("경로가 Grass, Wood, Iron 만으로 구성되어 있으므로 추가 보정이 필요 없음.");
            return;
        }
        else
        {
            Debug.Log("Grass, Wood, Iron 만으로는 경로를 찾지 못했습니다. River를 포함한 경로를 보정합니다.");
        }

        Tuple<List<Vector2Int>, List<Vector2Int>> pathAndRivers = FindPathMinimizingRiver(_posA, _posB);
        List<Vector2Int> riverMinPath = pathAndRivers.Item1;
        List<Vector2Int> riverTilesOnPath = pathAndRivers.Item2;

        if (riverMinPath == null)
        {
            Debug.LogWarning("River를 포함한 경로도 찾을 수 없습니다.");
            return;
        }

        Debug.Log("최소 River 경로에서 River 타일 개수: " + riverTilesOnPath.Count +
                  ", 좌표: " + FormatVectorList(riverTilesOnPath));

        int counter = 0;
        List<Vector2Int> convertedRiverTiles = new List<Vector2Int>();
        while (riverTilesOnPath.Count > 2 && counter < MAX_ITERATIONS)
        {
            counter++;
            int randomIndex = _masterRng.Next(riverTilesOnPath.Count);
            Vector2Int tile = riverTilesOnPath[randomIndex];
            _map[tile.x, tile.y] = TileType.Grass;
            convertedRiverTiles.Add(tile);
            Debug.Log("River 타일을 Grass로 변환: " + tile);
            riverTilesOnPath.RemoveAt(randomIndex);
        }

        if (counter >= MAX_ITERATIONS)
            throw new MapGenerationException("AdjustPathForRiverAndWood: River 변환 반복 최대치를 초과했습니다.");

        int reachableWoodCount = CountReachableWoodWithoutRiver(_posA);
        Debug.Log("River 없이 도달 가능한 Wood 타일 개수: " + reachableWoodCount);

        if (reachableWoodCount >= 2)
        {
            Debug.Log("River 없이 도달 가능한 Wood 타일이 2개 이상이므로 추가 작업 없이 종료.");
        }
        else
        {
            foreach (Vector2Int tile in convertedRiverTiles)
            {
                _map[tile.x, tile.y] = TileType.Wood;
                Debug.Log("변환된 Grass 타일을 Wood로 전환: " + tile);
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
        int[][] cost = new int[width][];
        for (int i = 0; i < width; i++)
            cost[i] = new int[height];

        Vector2Int[][] parent = new Vector2Int[width][];
        for (int i = 0; i < width; i++)
            parent[i] = new Vector2Int[height];

        bool[][] visited = new bool[width][];
        for (int i = 0; i < width; i++)
            visited[i] = new bool[height];

        for (int x = 0; x < width; x++)
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
                if (IsInBounds(next) && _map[next.x, next.y] != TileType.Mountain)
                {
                    int tileCost = (_map[next.x, next.y] == TileType.River) ? 1 : 0;
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
        while (!(node.x == -1 && node.y == -1))
        {
            path.Add(node);
            if (node == start)
                break;
            node = parent[node.x][node.y];
        }

        path.Reverse();

        List<Vector2Int> riverTiles = new List<Vector2Int>();
        foreach (var pos in path)
            if (_map[pos.x, pos.y] == TileType.River)
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
            if (_map[cur.x, cur.y] == TileType.Wood)
                woodCount++;
            foreach (var d in directions)
            {
                Vector2Int next = cur + d;
                if (IsInBounds(next) && !visited.Contains(next))
                {
                    TileType type = _map[next.x, next.y];
                    if (type == TileType.Grass || type == TileType.Wood || type == TileType.Iron)
                    {
                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }
        }

        return woodCount;
    }

    #endregion

    #region 시작점, 도착점 보정

    private void ForceStartEndGrass()
    {
        _map[_posA.x, _posA.y] = TileType.Grass;
        _map[_posB.x, _posB.y] = TileType.Grass;
    }

    #endregion

    #region 경로 연결 보장 (Dijkstra)

    private void EnsurePathConnectivity()
    {
        int[][] cost = new int[width][];
        for (int i = 0; i < width; i++)
            cost[i] = new int[height];

        Vector2Int[][] parent = new Vector2Int[width][];
        for (int i = 0; i < width; i++)
            parent[i] = new Vector2Int[height];

        bool[][] visited = new bool[width][];
        for (int i = 0; i < width; i++)
            visited[i] = new bool[height];

        for (int x = 0; x < width; x++)
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
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

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
                break;

            foreach (var d in directions)
            {
                Vector2Int next = current + d;
                if (IsInBounds(next))
                {
                    int tileCost = (_map[next.x, next.y] == TileType.Mountain) ? 1 : 0;
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

        if (iterations >= MAX_ITERATIONS)
            throw new MapGenerationException("EnsurePathConnectivity: 반복 최대치를 초과했습니다.");
        if (cost[_posB.x][_posB.y] == int.MaxValue)
        {
            Debug.LogWarning("EnsurePathConnectivity: 경로 복원이 불가능함 (산 제거 후에도 경로 없음)");
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

        foreach (var cell in path)
            if (_map[cell.x, cell.y] == TileType.Mountain)
                _map[cell.x, cell.y] = TileType.Grass;
    }

    #endregion

    #region 공통 함수

    private bool IsInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    private int CountGrassTiles()
    {
        int count = 0;
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (_map[x, y] == TileType.Grass)
                count++;
        return count;
    }

    private int CountTiles(TileType type)
    {
        int count = 0;
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (_map[x, y] == type)
                count++;
        return count;
    }

    private List<Vector2Int> GetCardinalNeighbors(Vector2Int pos)
    {
        return new List<Vector2Int>
        {
            new Vector2Int(pos.x + 1, pos.y),
            new Vector2Int(pos.x - 1, pos.y),
            new Vector2Int(pos.x, pos.y + 1),
            new Vector2Int(pos.x, pos.y - 1)
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

    private string FormatVectorList(List<Vector2Int> list)
    {
        string s = "";
        foreach (var v in list)
            s += $"({v.x},{v.y}) ";
        return s;
    }

    #endregion

    #region 맵 오브젝트 생성

    private void InstantiateMap()
    {
        EnsurePathConnectivity();
        int grassCount = 0, woodCount = 0, ironCount = 0, mountainCount = 0, riverCount = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 pos = new Vector3(x, 0, y);
                if (x == _posA.x && y == _posA.y)
                    Instantiate(startPointPrefab, pos, Quaternion.identity, blockParent);
                else if (x == _posB.x && y == _posB.y)
                    Instantiate(endPointPrefab, pos, Quaternion.identity, blockParent);
                else
                {
                    switch (_map[x, y])
                    {
                        case TileType.Grass:
                            grassCount++;
                            Instantiate(tilePrefab, pos, Quaternion.identity, blockParent);
                            break;
                        case TileType.Wood:
                            woodCount++;
                            Instantiate(woodPrefab, pos, Quaternion.identity, blockParent);
                            break;
                        case TileType.Iron:
                            ironCount++;
                            Instantiate(ironPrefab, pos, Quaternion.identity, blockParent);
                            break;
                        case TileType.Mountain:
                            mountainCount++;
                            Instantiate(mountainPrefab, pos, Quaternion.identity, blockParent);
                            break;
                        case TileType.River:
                            riverCount++;
                            Instantiate(riverPrefab, pos, Quaternion.identity, blockParent);
                            break;
                    }
                }
            }
        }

        Debug.Log(
            $"맵 생성 완료, grassCount: {grassCount}, woodCount: {woodCount}, ironCount: {ironCount}, mountainCount: {mountainCount}, riverCount: {riverCount}\n" +
            "생성 파라미터\n" +
            $"minRiverCount: {minRiverCount}, maxRiverCount: {maxRiverCount}, minRiverLength: {minRiverLength}, maxRiverCellsAllowed: {maxRiverCellsAllowed}, lateralSpreadProbability: {lateralSpreadProbability}, elongatedRiverMinWidth: {elongatedRiverMinWidth}, elongatedRiverMaxWidth: {elongatedRiverMaxWidth}\n" +
            $"minGrassTileCount: {minGrassTileCount}, maxGrassTileCount: {maxGrassTileCount}, minWoodCount: {minWoodCount}, minIronCount: {minIronCount}"
        );
    }

    #endregion

    #region 디버그 함수

    public int visitX;
    public int visitY;
    private int _checkCount;

    private void CheckVisit()
    {
        if (visitX < 0 || visitX >= width || visitY < 0 || visitY >= height)
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
                    if (_map[next.x, next.y] != TileType.Mountain)
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
                $"CheckVisit: [{_checkCount}] ({visitX},{visitY})에 도달 가능합니다. 해당 좌표의 타일 타입: {_map[visitX, visitY]}, {pathStr}");
        }
        else
        {
            Debug.Log($"CheckVisit: [{_checkCount}] 해당 좌표에 도달할 수 없습니다. 해당 좌표의 타일 타입: {_map[visitX, visitY]}");
        }

        _checkCount++;
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