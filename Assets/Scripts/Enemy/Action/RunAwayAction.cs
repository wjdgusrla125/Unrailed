using System;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "RunAway", story: "[Agent] Run away from [Target]", category: "Action", id: "f1c2be74-c8b7-4d26-9c25-8d60a68e8a0c")]
public partial class RunAwayAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeField] public float MoveSpeed = 3f;
    [SerializeField] public float TriggerDistance = 5f;
    [SerializeField] public float SafeDistance = 8f;

    private Transform _agentTransform;
    private Transform _targetTransform;
    private List<Vector2Int> _path;
    private int _currentIndex;
    private bool _isRunningAway = false;

    protected override Status OnStart()
    {
        if (Agent?.Value == null || Target?.Value == null)
            return Status.Failure;

        _agentTransform = Agent.Value.transform;
        _targetTransform = Target.Value.transform;
        _isRunningAway = false;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (_agentTransform == null || _targetTransform == null)
            return Status.Failure;

        float currentDistance = Vector3.Distance(_agentTransform.position, _targetTransform.position);

        if (!_isRunningAway)
        {
            if (currentDistance > TriggerDistance)
                return Status.Success; // 아직 충분히 멀다 = 도망 필요 없음

            bool pathFound = FindFleePath();
            if (!pathFound)
            {
                Debug.LogWarning("[RunAwayAction] 도망 경로를 찾지 못했습니다.");
                return Status.Failure;
            }

            _isRunningAway = true;
        }

        if (_path == null || _currentIndex >= _path.Count)
        {
            // 도망 완료
            float updatedDistance = Vector3.Distance(_agentTransform.position, _targetTransform.position);
            return updatedDistance >= SafeDistance ? Status.Success : Status.Failure;
        }

        Vector3 targetWorldPos = TileToWorld(_path[_currentIndex]);
        targetWorldPos.y = _agentTransform.position.y;

        _agentTransform.position = Vector3.MoveTowards(_agentTransform.position, targetWorldPos, MoveSpeed * Time.deltaTime);

        if (Vector3.Distance(new Vector3(_agentTransform.position.x, 0, _agentTransform.position.z),
                             new Vector3(targetWorldPos.x, 0, targetWorldPos.z)) < 0.1f)
        {
            _currentIndex++;
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
        _path = null;
        _currentIndex = 0;
        _isRunningAway = false;
    }

    private bool FindFleePath()
    {
        Vector2Int agentTile = WorldToTile(_agentTransform.position);
        Vector2Int targetTile = WorldToTile(_targetTransform.position);

        Vector2Int fleeDir = (agentTile - targetTile);
        if (fleeDir == Vector2Int.zero)
            fleeDir = Vector2Int.right; // 같은 위치일 경우

        fleeDir = new Vector2Int(
            Math.Sign(fleeDir.x),
            Math.Sign(fleeDir.y)
        );

        List<Vector2Int> fleeTargets = new List<Vector2Int>();

        // 기본 도망 방향
        Vector2Int mainTarget = agentTile + fleeDir * Mathf.RoundToInt(SafeDistance);
        fleeTargets.Add(mainTarget);

        // 대체 방향들도 추가
        fleeTargets.Add(agentTile + new Vector2Int(fleeDir.x, 0) * Mathf.RoundToInt(SafeDistance));
        fleeTargets.Add(agentTile + new Vector2Int(0, fleeDir.y) * Mathf.RoundToInt(SafeDistance));
        fleeTargets.Add(agentTile + new Vector2Int(-fleeDir.x, -fleeDir.y) * Mathf.RoundToInt(SafeDistance));

        foreach (var dest in fleeTargets)
        {
            Vector2Int clampedDest = ClampToMap(dest);
            _path = Pathfinder.FindPath(agentTile, clampedDest);
            if (_path != null && _path.Count > 0)
            {
                _currentIndex = 0;
                return true;
            }
        }

        return false;
    }

    private Vector2Int WorldToTile(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.z));
    }

    private Vector3 TileToWorld(Vector2Int tilePos)
    {
        return new Vector3(tilePos.x, 0f, tilePos.y);
    }

    private Vector2Int ClampToMap(Vector2Int pos)
    {
        var map = MapGenerator.Instance.Map;
        int maxX = map.GetLength(0) - 1;
        int maxY = map.GetLength(1) - 1;

        return new Vector2Int(
            Mathf.Clamp(pos.x, 0, maxX),
            Mathf.Clamp(pos.y, 0, maxY)
        );
    }
}
