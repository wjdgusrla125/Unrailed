using System;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "EdgePathFinding", story: "[Agent] move to Edge", category: "Action", id: "a3dbcd07bbec92de649ff8f576da50cf")]
public partial class EdgePathFindingAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;

    private List<Vector2Int> _path;
    private int _pathIndex;

    protected override Status OnStart()
    {
        if (Agent?.Value == null)
        {
            Debug.LogError("[EdgePathFindingAction] Agent가 설정되지 않았습니다.");
            return Status.Failure;
        }

        Vector2Int start = WorldToGrid(Agent.Value.transform.position);
        Debug.Log($"[EdgePathFindingAction] Agent 현재 위치 (Grid): {start}");

        if (!IsInBounds(start))
        {
            Debug.LogError($"[EdgePathFindingAction] Agent가 맵 범위 밖에 있습니다. ({start.x}, {start.y})");
            return Status.Failure;
        }

        _path = FindPathToClosestEdge(start);
        _pathIndex = 0;

        if (_path == null || _path.Count == 0)
        {
            Debug.LogWarning("[EdgePathFindingAction] 경로를 찾지 못했습니다.");
            return Status.Failure;
        }

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (_path == null || _pathIndex >= _path.Count)
            return Status.Success;

        Vector3 targetWorld = GridToWorld(_path[_pathIndex]);
        Vector3 agentPos = Agent.Value.transform.position;
        targetWorld.y = agentPos.y;

        float distance = Vector3.Distance(new Vector3(agentPos.x, 0, agentPos.z), new Vector3(targetWorld.x, 0, targetWorld.z));
        if (distance < 0.1f)
        {
            _pathIndex++;
        }
        else
        {
            Vector3 moveDir = (targetWorld - agentPos).normalized;
            if (moveDir != Vector3.zero)
                Agent.Value.transform.forward = new Vector3(moveDir.x, 0f, moveDir.z);
            
            Agent.Value.transform.position = Vector3.MoveTowards(agentPos, targetWorld, Time.deltaTime * 1f);
        }

        return (_pathIndex >= _path.Count) ? Status.Success : Status.Running;
    }

    private List<Vector2Int> FindPathToClosestEdge(Vector2Int start)
    {
        var map = MapGenerator.Instance.Map;
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            // Edge(가장자리) 도착했는지 확인
            if (IsEdge(current, width, height))
            {
                return ReconstructPath(cameFrom, current);
            }

            foreach (var dir in new Vector2Int[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                Vector2Int neighbor = current + dir;
                if (IsWalkable(neighbor) && !visited.Contains(neighbor))
                {
                    queue.Enqueue(neighbor);
                    visited.Add(neighbor);
                    cameFrom[neighbor] = current;
                }
            }
        }

        return null;
    }

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        List<Vector2Int> path = new List<Vector2Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }

    private bool IsEdge(Vector2Int pos, int width, int height)
    {
        return pos.x == 0 || pos.x == width - 1 || pos.y == 0 || pos.y == height - 1;
    }

    private bool IsWalkable(Vector2Int pos)
    {
        if (!IsInBounds(pos))
            return false;

        var tile = MapGenerator.Instance.Map[pos.x, pos.y];
        return tile == MapGenerator.TileType.Grass;
    }

    private bool IsInBounds(Vector2Int pos)
    {
        var map = MapGenerator.Instance.Map;
        return pos.x >= 0 && pos.x < map.GetLength(0) &&
               pos.y >= 0 && pos.y < map.GetLength(1);
    }

    private Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt(worldPos.x);
        int y = Mathf.RoundToInt(worldPos.z);
        return new Vector2Int(x, y);
    }

    private Vector3 GridToWorld(Vector2Int gridPos)
    {
        return new Vector3(gridPos.x, 0f, gridPos.y);
    }

    protected override void OnEnd()
    {
        // 종료 처리 필요시 작성
    }
}
