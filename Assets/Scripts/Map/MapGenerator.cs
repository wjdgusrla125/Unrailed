using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class MapGenerator : MonoBehaviour
{
    [SerializeField] private string mapSeed;
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private GameObject woodPrefab;
    [SerializeField] private GameObject ironPrefab;
    [SerializeField] private GameObject riverPrefab;
    [SerializeField] private GameObject mountainPrefab;
    [SerializeField] private GameObject startPointPrefab;
    [SerializeField] private GameObject endPointPrefab;
    [SerializeField] private Transform blockParent;

    [Header("맵 크기 설정")] [SerializeField] private int width = 15;
    [SerializeField] private int height = 9;
    [SerializeField] private int pathLength = 13;
    [SerializeField] private int minHorizontalDistance = 11;
    [SerializeField] private int maxNoneTileCount = 70;
    [SerializeField] private int minNoneTileCount = 20;

    [Header("나무, 철 관련 설정")] [SerializeField]
    private int destructibleClusterCount = 5;

    [SerializeField] private float destructibleClusterProbability = 0.9f;
    [SerializeField] private int minWoodCount = 30;
    [SerializeField] private int minIronCount = 30;
    [SerializeField] private int minDestructibleClusterSize = 4;

    [Header("산 관련 설정")] [SerializeField] private int mountainClusterCountMin = 4;
    [SerializeField] private int mountainClusterCountMax = 5;
    [SerializeField] private int mountainClusterSizeMin = 5;
    [SerializeField] private int mountainClusterSizeMax = 7;

    [Header("강 관련 설정")] [SerializeField] private int minRiverCount = 2;
    [SerializeField] private int maxRiverCount = 3;
    [SerializeField] private int minRiverLength = 8;

    private enum TileType
    {
        None,
        Wood,
        Iron,
        Mountain,
        River
    }

    private TileType[,] _map;
    private bool[,] _riverLocked; // 강 셀 보호

    private Vector2Int _posA; // 시작점
    private Vector2Int _posB; // 도착점

    // 디버그용, Q키 쿨다운 (1초)
    private float qCooldown = 1f;
    private float lastQTime = -10f;

    private void Start()
    {
        GenerateMap();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q) && Time.time - lastQTime > qCooldown)
        {
            lastQTime = Time.time;
            Debug.Log("Q입력, 시드 랜덤화 후 맵 생성");
            for (int i = blockParent.childCount - 1; i >= 0; i--)
                Destroy(blockParent.GetChild(i).gameObject);
            mapSeed = string.Empty;
            GenerateMap();
        }

        if (Input.GetKeyDown(KeyCode.E) && Time.time - lastQTime > qCooldown)
        {
            lastQTime = Time.time;
            Debug.Log("E입력, 시드 유지 맵 생성");
            for (int i = blockParent.childCount - 1; i >= 0; i--)
                Destroy(blockParent.GetChild(i).gameObject);
            GenerateMap();
        }

        // if (Input.GetKeyDown(KeyCode.R))
        // {
        //     Debug.Log($"R입력, ({visitX}, {visitY}) visit 확인");
        //     CheckVisit();
        // }
    }

    private void GenerateMap()
    {
        InitializeSeed();
        InitializeMap();
        SetPath();
        GenerateValidPath(); // 시작~도착 경로는 None
        GenerateMountains(); // 산 생성
        ForceStartEndNone();
        EnsureStartEndInnerClear();
        EnsurePathConnectivity(); // 시작점에서 도착점까지 도달 가능한지 확인, 불가능하면 경로 복원
        GenerateRivers(); // 강 생성
        EnsureReachability(); // 플레이어가 접근할 수 없는 영역은 mountain으로 전환
        PlaceDestructibleObstacles(); // 초기 Wood, Iron 생성 (산/강 제외)
        GenerateEdgeMountainClusters();
        EnsureReachability(); // 플레이어가 접근할 수 없는 영역은 mountain으로 전환
        ForceStartEndNone();
        EnsureStartEndInnerClear();
        FinalAdjustments(); // 부족한 Wood, Iron, None 보충 (아래 EnsureMinimumDestructibleObstacles() 사용)
        EnsureWoodAccessibility(); // 시작점에서 최소한 1개의 Wood 클러스터 접근 통로 생성
        EnsureStartEndInnerClear(); // 출발/도착 주변 3×3 영역은 None
        EnsureEndpointCorridor();
        InstantiateMap();
    }

    // 1. Seed 초기화
    private void InitializeSeed()
    {
        if (string.IsNullOrEmpty(mapSeed))
            mapSeed = DateTime.Now.Ticks.ToString();
        Random.InitState(mapSeed.GetHashCode());
    }

    // 맵 및 강 락 배열 초기화
    private void InitializeMap()
    {
        _map = new TileType[width, height];
        _riverLocked = new bool[width, height];
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            _map[x, y] = TileType.None;
            _riverLocked[x, y] = false;
        }
    }

    // 2. 출발지/도착지 결정 (출발지에서 도착지까지 맨해튼 거리가 pathLength, 수평 거리 최소)
    void SetPath()
    {
        _posA = new Vector2Int(1, height / 2);
        int attempts = 0;
        int maxAttempts = 1000;
        do
        {
            _posB = new Vector2Int(Random.Range(width / 2, width - 1), Random.Range(1, height - 1));
            attempts++;
            if (attempts > maxAttempts)
            {
                Debug.LogWarning("SetPath: 최대 시도 횟수 초과");
                break;
            }
        } while ((Mathf.Abs(_posA.x - _posB.x) + Mathf.Abs(_posA.y - _posB.y) != pathLength) ||
                 (Mathf.Abs(_posA.x - _posB.x) < minHorizontalDistance));
    }

    // 3. 시작점에서 도착점까지 경로를 None으로 설정
    private void GenerateValidPath()
    {
        Vector2Int current = _posA;
        int iterations = 0;
        int maxIterations = 1000;
        while (current != _posB && iterations < maxIterations)
        {
            iterations++;
            if (current.x != _posB.x)
                current.x += (_posB.x > current.x) ? 1 : -1;
            else if (current.y != _posB.y)
                current.y += (_posB.y > current.y) ? 1 : -1;
            _map[current.x, current.y] = TileType.None;
        }

        if (iterations >= maxIterations)
            Debug.LogWarning("GenerateValidPath: 최대 반복 횟수 초과");
    }

    // 4. 산 생성 (Flood Fill 방식, 8방향)
    private void GenerateMountains()
    {
        int mountainClusters = Random.Range(mountainClusterCountMin, mountainClusterCountMax + 1);
        for (int i = 0; i < mountainClusters; i++)
        {
            int edge = Random.Range(0, 4);
            int startX = 0, startY = 0;
            switch (edge)
            {
                case 0:
                    startX = Random.Range(0, width);
                    startY = height - 1;
                    break;
                case 1:
                    startX = Random.Range(0, width);
                    startY = 0;
                    break;
                case 2:
                    startX = 0;
                    startY = Random.Range(0, height);
                    break;
                case 3:
                    startX = width - 1;
                    startY = Random.Range(0, height);
                    break;
            }

            // 만약 이 위치가 시작점/도착점과 동일하다면 스킵
            if ((startX == _posA.x && startY == _posA.y) ||
                (startX == _posB.x && startY == _posB.y))
            {
                continue;
            }

            int clusterSize = Random.Range(mountainClusterSizeMin, mountainClusterSizeMax + 1);
            Queue<Vector2Int> mountainQueue = new Queue<Vector2Int>();
            mountainQueue.Enqueue(new Vector2Int(startX, startY));
            _map[startX, startY] = TileType.Mountain;
            int count = 1;
            int iterations = 0;
            int maxIterations = 10000;
            while (mountainQueue.Count > 0 && count < clusterSize && iterations < maxIterations)
            {
                iterations++;
                Vector2Int cur = mountainQueue.Dequeue();
                Vector2Int[] neighbors = new Vector2Int[]
                {
                    new Vector2Int(cur.x + 1, cur.y),
                    new Vector2Int(cur.x - 1, cur.y),
                    new Vector2Int(cur.x, cur.y + 1),
                    new Vector2Int(cur.x, cur.y - 1),
                    new Vector2Int(cur.x + 1, cur.y + 1),
                    new Vector2Int(cur.x - 1, cur.y - 1),
                    new Vector2Int(cur.x + 1, cur.y - 1),
                    new Vector2Int(cur.x - 1, cur.y + 1)
                };
                foreach (var n in neighbors)
                {
                    if (n.x >= 0 && n.x < width && n.y >= 0 && n.y < height &&
                        _map[n.x, n.y] == TileType.None)
                    {
                        // 이웃이 start/end라면 건너뜀
                        if (n == _posA || n == _posB)
                            continue;

                        if (Random.value < 0.5f)
                        {
                            _map[n.x, n.y] = TileType.Mountain;
                            mountainQueue.Enqueue(n);
                            count++;
                        }
                    }
                }
            }

            if (iterations >= maxIterations)
                Debug.LogWarning("GenerateMountains: 최대 반복 횟수 초과");
        }
    }


    // 5. 강 생성
    private void GenerateRivers()
    {
        int riverCount = Random.Range(minRiverCount, maxRiverCount + 1);
        int riversGenerated = 0;
        int outerAttempts = 0;
        // int totalRiverTiles = 0; // 전체 강 타일 수 누적

        while (riversGenerated < riverCount && outerAttempts < 100)
        {
            outerAttempts++;

            int edge = Random.Range(0, 4);
            int startX = 0, startY = 0;
            Vector2Int preferredDir;
            switch (edge)
            {
                case 0: // Top edge → 아래쪽
                    startX = Random.Range(0, width);
                    startY = height - 1;
                    preferredDir = new Vector2Int(0, -1);
                    break;
                case 1: // Bottom edge → 위쪽
                    startX = Random.Range(0, width);
                    startY = 0;
                    preferredDir = new Vector2Int(0, 1);
                    break;
                case 2: // Left edge → 오른쪽
                    startX = 0;
                    startY = Random.Range(0, height);
                    preferredDir = new Vector2Int(1, 0);
                    break;
                case 3: // Right edge → 왼쪽
                    startX = width - 1;
                    startY = Random.Range(0, height);
                    preferredDir = new Vector2Int(-1, 0);
                    break;
                default:
                    preferredDir = new Vector2Int(0, -1);
                    break;
            }

            Vector2Int current = new Vector2Int(startX, startY);
            List<Vector2Int> riverCells = new List<Vector2Int>();
            riverCells.Add(current);

            int safetyCounter = 0;
            int maxSafety = 1000;
            while (riverCells.Count < minRiverLength && safetyCounter < maxSafety)
            {
                safetyCounter++;

                Vector2Int[] candidateOffsets = new Vector2Int[]
                {
                    new Vector2Int(1, 0),
                    new Vector2Int(-1, 0),
                    new Vector2Int(0, 1),
                    new Vector2Int(0, -1)
                };

                List<Vector2Int> validOffsets = new List<Vector2Int>();
                foreach (var off in candidateOffsets)
                {
                    if (IsInBounds(current + off))
                        validOffsets.Add(off);
                }

                if (validOffsets.Count == 0)
                {
                    Debug.LogWarning("GenerateRivers: 확장 후보가 없음");
                    break;
                }

                float totalWeight = 0f;
                List<(Vector2Int offset, float weight)> weightedCandidates = new List<(Vector2Int, float)>();
                foreach (var off in validOffsets)
                {
                    float similarity = Vector2.Dot(off, preferredDir);
                    float weight = 1f + Mathf.Clamp(similarity, 0f, 1f);
                    weightedCandidates.Add((off, weight));
                    totalWeight += weight;
                }

                float r = Random.value * totalWeight;
                Vector2Int chosenOffset = weightedCandidates[0].offset;
                foreach (var candidate in weightedCandidates)
                {
                    r -= candidate.weight;
                    if (r <= 0f)
                    {
                        chosenOffset = candidate.offset;
                        break;
                    }
                }

                Vector2Int nextPos = current + chosenOffset;
                current = nextPos;
                riverCells.Add(current);

                if (Random.value < 0.6f)
                    preferredDir = chosenOffset;
            }

            if (safetyCounter >= maxSafety)
                Debug.LogWarning("GenerateRivers: 내부 안전 카운터 초과");

            if (riverCells.Count >= minRiverLength)
            {
                foreach (var cell in riverCells)
                {
                    _map[cell.x, cell.y] = TileType.River;
                    _riverLocked[cell.x, cell.y] = true;
                }

                riversGenerated++;
                // totalRiverTiles += riverCells.Count;
            }
            else
            {
                Debug.LogWarning("이번 시도에서는 강 생성 길이가 최소 길이에 미달함");
            }
        }

        if (riversGenerated < riverCount)
            Debug.LogWarning("GenerateRivers: 일부 강이 100번의 시도 내에 생성되지 않았습니다.");
        // else
        //     Debug.Log($"총 {riversGenerated}개의 강 생성 완료, 총 강 타일 수: {totalRiverTiles}");
    }

    // 좌표가 맵 내에 있는지 확인
    private bool IsInBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    // 6. 초기 Wood, Iron 생성: 산과 강이 아닌 셀에서 Flood Fill 클러스터 생성 (초기 할당)
    private void PlaceDestructibleObstacles()
    {
        int totalWood = 0, totalIron = 0;
        bool assignWoodNext = true;

        for (int i = 0; i < destructibleClusterCount; i++)
        {
            int x = Random.Range(0, width);
            int y = Random.Range(0, height);
            if (_map[x, y] != TileType.None)
                continue;

            Vector2Int startPos = new Vector2Int(x, y);
            List<Vector2Int> cluster = GenerateDestructibleCluster(startPos, true);
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
                if (_map[cell.x, cell.y] == TileType.None)
                {
                    _map[cell.x, cell.y] = obstacleType;
                    if (obstacleType == TileType.Wood)
                        totalWood++;
                    else if (obstacleType == TileType.Iron)
                        totalIron++;
                }
            }
        }
    }

    // none 타일이 너무 많을 경우 모서리에 존재하는 랜덤한 None 타일에서 산 클러스터 생성
    private void GenerateEdgeMountainClusters()
    {
        int loopCounter = 0;
        while (CountNoneTiles() >= maxNoneTileCount && loopCounter < 1000)
        {
            // Debug.Log($"[GenerateEdgeMountainClusters] none 타일 갯수: {CountNoneTiles()}");
            loopCounter++;

            List<Vector2Int> edgeNoneTiles = new List<Vector2Int>();
            for (int x = 0; x < width; x++)
            {
                if (_map[x, 0] == TileType.None)
                    edgeNoneTiles.Add(new Vector2Int(x, 0));
                if (_map[x, height - 1] == TileType.None)
                    edgeNoneTiles.Add(new Vector2Int(x, height - 1));
            }

            for (int y = 1; y < height - 1; y++)
            {
                if (_map[0, y] == TileType.None)
                    edgeNoneTiles.Add(new Vector2Int(0, y));
                if (_map[width - 1, y] == TileType.None)
                    edgeNoneTiles.Add(new Vector2Int(width - 1, y));
            }

            if (edgeNoneTiles.Count == 0)
            {
                Debug.Log("GenerateEdgeMountainClusters: 모든 모서리에 None 타일이 없음. 루프 탈출");
                break;
            }

            Vector2Int start = edgeNoneTiles[Random.Range(0, edgeNoneTiles.Count)];

            int clusterSize = Random.Range(mountainClusterSizeMin, mountainClusterSizeMax + 1);
            Queue<Vector2Int> mountainQueue = new Queue<Vector2Int>();
            mountainQueue.Enqueue(start);
            _map[start.x, start.y] = TileType.Mountain;
            int count = 1;
            int iterations = 0;
            int maxIterations = 10000;
            while (mountainQueue.Count > 0 && count < clusterSize && iterations < maxIterations)
            {
                iterations++;
                Vector2Int cur = mountainQueue.Dequeue();
                Vector2Int[] neighbors = new Vector2Int[]
                {
                    new Vector2Int(cur.x + 1, cur.y),
                    new Vector2Int(cur.x - 1, cur.y),
                    new Vector2Int(cur.x, cur.y + 1),
                    new Vector2Int(cur.x, cur.y - 1),
                    new Vector2Int(cur.x + 1, cur.y + 1),
                    new Vector2Int(cur.x - 1, cur.y - 1),
                    new Vector2Int(cur.x + 1, cur.y - 1),
                    new Vector2Int(cur.x - 1, cur.y + 1)
                };
                foreach (var n in neighbors)
                {
                    if (IsInBounds(n) && _map[n.x, n.y] == TileType.None)
                    {
                        if (Random.value < 0.5f)
                        {
                            _map[n.x, n.y] = TileType.Mountain;
                            mountainQueue.Enqueue(n);
                            count++;
                        }
                    }
                }
            }

            if (iterations >= maxIterations)
                Debug.LogWarning("GenerateEdgeMountainClusters: 최대 반복 횟수 초과");
        }

        if (loopCounter >= 1000)
            Debug.LogWarning("GenerateEdgeMountainClusters: 1000회 루프 제한 도달");
    }

    // 전체 맵에서 None 타일 개수 계산
    private int CountNoneTiles()
    {
        int count = 0;
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (_map[x, y] == TileType.None)
                count++;
        return count;
    }

    // Flood Fill 방식으로 최소 클러스터 크기의 셀 모음 생성
    private List<Vector2Int> GenerateDestructibleCluster(Vector2Int startPos, bool isWood)
    {
        List<Vector2Int> cluster = new List<Vector2Int>();
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        q.Enqueue(startPos);
        visited.Add(startPos);
        int iterations = 0;
        int maxIterations = 1000;
        while (q.Count > 0 && cluster.Count < minDestructibleClusterSize && iterations < maxIterations)
        {
            iterations++;
            Vector2Int cur = q.Dequeue();
            cluster.Add(cur);
            Vector2Int[] neigh = new Vector2Int[]
            {
                new Vector2Int(cur.x + 1, cur.y),
                new Vector2Int(cur.x - 1, cur.y),
                new Vector2Int(cur.x, cur.y + 1),
                new Vector2Int(cur.x, cur.y - 1)
            };
            foreach (var n in neigh)
            {
                if (n.x >= 0 && n.x < width && n.y >= 0 && n.y < height && !visited.Contains(n))
                {
                    if (Random.value < destructibleClusterProbability)
                    {
                        q.Enqueue(n);
                        visited.Add(n);
                    }
                }
            }
        }

        if (iterations >= maxIterations)
            Debug.LogWarning("GenerateDestructibleCluster: 최대 반복 횟수 초과");
        return cluster;
    }

    // 8. 출발/도착점 주변 3×3 영역을 None으로 설정
    private void EnsureStartEndInnerClear()
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            int nx = _posA.x + dx, ny = _posA.y + dy;
            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                _map[nx, ny] = TileType.None;
        }

        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            int nx = _posB.x + dx, ny = _posB.y + dy;
            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                _map[nx, ny] = TileType.None;
        }
    }

    // 9. EnsureNoneTiles: None 타일 최소치 확보 (강 셀 제외)
    private void EnsureNoneTiles()
    {
        int count = 0;
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (_map[x, y] == TileType.None)
                count++;
        int iterations = 0;
        int maxIterations = 10000;
        while (count < minNoneTileCount && iterations < maxIterations)
        {
            iterations++;
            int x = Random.Range(0, width);
            int y = Random.Range(0, height);
            if (_map[x, y] != TileType.None && _map[x, y] != TileType.River)
            {
                _map[x, y] = TileType.None;
                count++;
            }
        }

        if (iterations >= maxIterations)
            Debug.LogWarning("EnsureNoneTiles: 최대 반복 횟수 초과");
    }

    // 10. EnsureReachability: _posA에서 도달 불가능한 영역은 모두 Mountain으로 변경
    private void EnsureReachability()
    {
        // 1. _posA에서 상하좌우 이동으로 도달 가능한 영역을 계산 (BFS)
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
                    // player는 mountain 타일은 통과하지 못하므로 mountain이 아닌 타일만 방문 처리
                    if (_map[next.x, next.y] != TileType.Mountain)
                    {
                        visited[next.x][next.y] = true;
                        queue.Enqueue(next);
                    }
                }
            }
        }

        // 2. BFS에서 방문되지 않은 모든 타일을 mountain으로 전환
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!visited[x][y])
                {
                    _map[x, y] = TileType.Mountain;
                }
            }
        }

        // 3.  4면이 모두 mountain 또는 맵 밖인 타일을 반복적으로 mountain으로 변경
        bool changed;
        do
        {
            changed = false;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // 시작/도착점 주변 3×3 영역 등 예외 처리 필요하면 여기서 건너뛸 수 있음
                    if ((_posA.x == x && _posA.y == y) || (_posB.x == x && _posB.y == y))
                        continue;

                    // 이미 mountain이면 검사할 필요 없음
                    if (_map[x, y] == TileType.Mountain)
                        continue;

                    bool allBlocked = true;
                    foreach (var d in directions)
                    {
                        Vector2Int adj = new Vector2Int(x + d.x, y + d.y);
                        if (IsInBounds(adj))
                        {
                            if (_map[adj.x, adj.y] != TileType.Mountain)
                            {
                                allBlocked = false;
                                break;
                            }
                        }
                        // 맵 밖은 기본적으로 막힌 것으로 간주
                    }

                    if (allBlocked)
                    {
                        _map[x, y] = TileType.Mountain;
                        changed = true;
                    }
                }
            }
        } while (changed);
    }

    // 11. FinalAdjustments: 도달 가능한 영역에서 부족한 Wood, Iron, None 보충
    // EnsureMinimumDestructibleObstacles() 함수로 Wood, Iron 보충 후 EnsureNoneTiles() 호출
    private void FinalAdjustments()
    {
        EnsureMinimumDestructibleObstacles();
        EnsureNoneTiles();
    }

    // 플레이어가 도달 가능한 영역(시작점 기준 Flood Fill)에서, 시작/도착 주변 2셀 제외 후보들 중에서
    // 부족한 자원(wood 또는 iron)의 클러스터를 생성하여 최소치 달성
    private void EnsureMinimumDestructibleObstacles()
    {
        // 도달 가능한 타일 목록
        List<Vector2Int> reachable = GetReachableCells();
        // Debug.Log($"[EnsureMinDestrObs] reachable의 수: {reachable.Count}");
        // 시작점, 도착점 주변 2셀 범위 제외
        reachable = reachable.FindAll(cell => !IsCellNear(cell, _posA, 2) && !IsCellNear(cell, _posB, 2));
        // Debug.Log($"[EnsureMinDestrObs] reachable의 수: {reachable.Count}");

        int currentWood = CountTiles(TileType.Wood);
        int currentIron = CountTiles(TileType.Iron);

        // Debug.Log($"[EnsureMinDestrObs] 초기 Wood={currentWood}, Iron={currentIron}. (목표 Wood={minWoodCount}, Iron={minIronCount})");

        int attempts = 0;
        int maxAttempts = 10000;

        // ---- 1) Wood 부족분 보충 ----
        while (currentWood < minWoodCount && attempts < maxAttempts)
        {
            attempts++;

            // 현재 None인 셀 후보 목록 (reachable 중에서 None인 셀만)
            List<Vector2Int> candidates = reachable.FindAll(cell => _map[cell.x, cell.y] == TileType.None);
            if (candidates.Count == 0)
            {
                Debug.LogWarning(
                    $"[EnsureMinDestrObs] Wood 보충 불가: None 후보가 없습니다. (currentWood={currentWood}/{minWoodCount})");
                break;
            }

            // 랜덤 선택
            Vector2Int chosen = candidates[Random.Range(0, candidates.Count)];
            List<Vector2Int> cluster = GenerateDestructibleCluster(chosen, true);

            // 실제로 몇 개를 Wood로 만들었는지 기록
            int added = 0;
            foreach (var c in cluster)
            {
                if (_map[c.x, c.y] == TileType.None)
                {
                    _map[c.x, c.y] = TileType.Wood;
                    currentWood++;
                    added++;
                    if (currentWood >= minWoodCount)
                        break;
                }
            }

            // Debug.Log($"[EnsureMinDestrObs] Wood 생성 시도 #{attempts} - 시작점={chosen}, 클러스터크기={cluster.Count}, 실 Wood할당={added}, 현재 Wood={currentWood}");
        }

        if (currentWood < minWoodCount)
        {
            Debug.LogWarning($"[EnsureMinDestrObs] Wood 최소치({minWoodCount}) 도달 실패. 최종 Wood={currentWood}");
        }

        // ---- 2) Iron 부족분 보충 ----
        attempts = 0;
        while (currentIron < minIronCount && attempts < maxAttempts)
        {
            attempts++;

            // 현재 None인 셀 후보 목록 (reachable 중에서 None인 셀만)
            List<Vector2Int> candidates = reachable.FindAll(cell => _map[cell.x, cell.y] == TileType.None);
            if (candidates.Count == 0)
            {
                Debug.LogWarning(
                    $"[EnsureMinDestrObs] Iron 보충 불가: None 후보가 없습니다. (currentIron={currentIron}/{minIronCount})");
                break;
            }

            // 랜덤 선택
            Vector2Int chosen = candidates[Random.Range(0, candidates.Count)];
            List<Vector2Int> cluster = GenerateDestructibleCluster(chosen, false);

            // 실제로 몇 개를 Iron으로 만들었는지 기록
            int added = 0;
            foreach (var c in cluster)
            {
                if (_map[c.x, c.y] == TileType.None)
                {
                    _map[c.x, c.y] = TileType.Iron;
                    currentIron++;
                    added++;
                    if (currentIron >= minIronCount)
                        break;
                }
            }

            // Debug.Log($"[EnsureMinDestrObs] Iron 생성 시도 #{attempts} - 시작점={chosen}, 클러스터크기={cluster.Count}, 실 Iron할당={added}, 현재 Iron={currentIron}");
        }

        if (currentIron < minIronCount)
        {
            // Debug.LogWarning($"[EnsureMinDestrObs] Iron 최소치({minIronCount}) 도달 실패. 최종 Iron={currentIron}");
        }

        // 최종 결과 로그
        // Debug.Log($"[EnsureMinDestrObs] 최종 Wood={currentWood}, Iron={currentIron}");
    }

    // 플레이어가 도달 가능한 영역(시작점 기준 Flood Fill) 리스트 반환
    private List<Vector2Int> GetReachableCells()
    {
        bool[][] visited = new bool[width][];
        for (int x = 0; x < width; x++)
            visited[x] = new bool[height];

        List<Vector2Int> reachable = new List<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(_posA);
        visited[_posA.x][_posA.y] = true;

        // Debug.Log($"[GetReachableCells] Start BFS from {_posA} (Tile={_map[_posA.x,_posA.y]})");

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            reachable.Add(cur);

            // 디버그
            // Debug.Log($"[BFS] cur={cur}, map={_map[cur.x, cur.y]}, neighbors check...");

            Vector2Int[] neighbors =
            {
                new Vector2Int(cur.x + 1, cur.y),
                new Vector2Int(cur.x - 1, cur.y),
                new Vector2Int(cur.x, cur.y + 1),
                new Vector2Int(cur.x, cur.y - 1),
            };
            foreach (var n in neighbors)
            {
                if (IsInBounds(n) && !visited[n.x][n.y])
                {
                    // Debug.Log($"[BFS]   neighbor={n}, map={_map[n.x, n.y]}");
                    if (_map[n.x, n.y] != TileType.Mountain)
                    {
                        visited[n.x][n.y] = true;
                        queue.Enqueue(n);
                    }
                    else
                    {
                        // Debug.Log($"[BFS]   neighbor={n} is Mountain, skip.");
                    }
                }
            }
        }

        // Debug.Log($"[GetReachableCells] BFS result: reachable={reachable.Count} cells");
        return reachable;
    }

    // 두 점의 거리를 기준으로 범위 내인지 확인
    private bool IsCellNear(Vector2Int cell, Vector2Int point, int range)
    {
        return Mathf.Abs(cell.x - point.x) <= range && Mathf.Abs(cell.y - point.y) <= range;
    }

    // 특정 TileType의 전체 개수 반환
    private int CountTiles(TileType type)
    {
        int count = 0;
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (_map[x, y] == type)
                count++;
        return count;
    }

    // 12. EnsureEndpointCorridor: 도착점에서 오른쪽 끝까지 통로 확보
    private void EnsureEndpointCorridor()
    {
        int row = _posB.y;
        for (int x = _posB.x; x < width; x++)
            _map[x, row] = TileType.None;

        if (row - 1 >= 0)
        {
            for (int x = _posB.x; x < width; x++)
                _map[x, row - 1] = TileType.None;
        }

        if (row + 1 < height)
        {
            for (int x = _posB.x; x < width; x++)
                _map[x, row + 1] = TileType.None;
        }
    }

    // 13. EnsureWoodAccessibility: 시작점에서 최소한 하나의 Wood 클러스터 접근 통로 생성
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

            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };
            foreach (var d in directions)
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
        {
            _map[cell.x, cell.y] = TileType.None;
        }

        Debug.Log("EnsureWoodAccessibility: Wood 통로 생성 완료");
    }

    // 시작지점의 상태를 none으로 만든다.
    private void ForceStartEndNone()
    {
        // 시작점, 도착점은 절대 산 혹은 다른 장애물이 되지 않도록 강제로 None으로 설정
        _map[_posA.x, _posA.y] = TileType.None;
        _map[_posB.x, _posB.y] = TileType.None;
    }

    // 14. InstantiateMap: 맵 인스턴스화 및 최종 렌더링 전에 시작-도착 연결 보장
    private void InstantiateMap()
    {
        // 최종 연결 확인: 도달 불가능하면 수평/수직 경로로 mountain 제거
        EnsurePathConnectivity();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // if (x == visitX && y == visitY) Debug.Log($"({x}, {y}) TileType: {_map[x, y]}");

                Vector3 pos = new Vector3(x, 0, y);
                if (x == _posA.x && y == _posA.y)
                    Instantiate(startPointPrefab, pos, Quaternion.identity, blockParent);
                else if (x == _posB.x && y == _posB.y)
                    Instantiate(endPointPrefab, pos, Quaternion.identity, blockParent);
                else
                {
                    switch (_map[x, y])
                    {
                        case TileType.None:
                            Instantiate(tilePrefab, pos, Quaternion.identity, blockParent);
                            break;
                        case TileType.Wood:
                            Instantiate(woodPrefab, pos, Quaternion.identity, blockParent);
                            break;
                        case TileType.Iron:
                            Instantiate(ironPrefab, pos, Quaternion.identity, blockParent);
                            break;
                        case TileType.Mountain:
                            Instantiate(mountainPrefab, pos, Quaternion.identity, blockParent);
                            break;
                        case TileType.River:
                            Instantiate(riverPrefab, pos, Quaternion.identity, blockParent);
                            break;
                    }
                }
            }
        }
    }

    // 시작점에서 도착점까지 연결 가능한지 확인, 연결 불가능하면 수평/수직 경로로 mountain 제거
    private void EnsurePathConnectivity()
    {
        bool[][] visited = new bool[width][];
        for (int index = 0; index < width; index++)
        {
            visited[index] = new bool[height];
        }

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(_posA);
        visited[_posA.x][_posA.y] = true;
        bool reached = false;
        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();
            if (cur == _posB)
            {
                reached = true;
                break;
            }

            Vector2Int[] neighbors = new Vector2Int[]
            {
                new Vector2Int(cur.x + 1, cur.y),
                new Vector2Int(cur.x - 1, cur.y),
                new Vector2Int(cur.x, cur.y + 1),
                new Vector2Int(cur.x, cur.y - 1)
            };
            foreach (var n in neighbors)
            {
                if (IsInBounds(n) && !visited[n.x][n.y] && _map[n.x, n.y] != TileType.Mountain)
                {
                    visited[n.x][n.y] = true;
                    queue.Enqueue(n);
                }
            }
        }

        if (!reached)
        {
            Debug.LogWarning("EnsurePathConnectivity: 도착점에 도달 불가능 상태 발견, 경로 복원");
            Vector2Int cur = _posA;
            while (cur.x != _posB.x)
            {
                if (_map[cur.x, cur.y] == TileType.Mountain)
                {
                    _map[cur.x, cur.y] = TileType.None;
                }

                cur.x += (_posB.x > cur.x) ? 1 : -1;
            }

            while (cur.y != _posB.y)
            {
                if (_map[cur.x, cur.y] == TileType.Mountain)
                {
                    _map[cur.x, cur.y] = TileType.None;
                }

                cur.y += (_posB.y > cur.y) ? 1 : -1;
            }

            if (_map[_posB.x, _posB.y] == TileType.Mountain)
            {
                _map[_posB.x, _posB.y] = TileType.None;
            }
        }
    }
}