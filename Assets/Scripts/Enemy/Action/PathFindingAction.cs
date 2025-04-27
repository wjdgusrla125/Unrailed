using System;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "PathFinding", story: "[Agent] move to [Target]", category: "Action", id: "02340c598476873118c73a1ee5a1c958")]
public partial class PathFindingAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeField] public float MoveSpeed = 1f;
    [SerializeField] public float ArrivalThreshold = 0.1f;

    private List<Vector2Int> _path;
    private int _currentIndex;
    private Transform _agentTransform;
    private Vector2Int _agentTilePos;
    private Vector2Int _targetTilePos;
    private bool _pathFound = false;

    protected override Status OnStart()
    {
        if (Agent == null || Target == null)
            return Status.Failure;

        if (Agent.Value == null || Target.Value == null)
            return Status.Failure;

        _agentTransform = Agent.Value.transform;
        _agentTilePos = WorldToTile(_agentTransform.position);
        _targetTilePos = WorldToTile(Target.Value.transform.position);

        _path = Pathfinder.FindPath(_agentTilePos, _targetTilePos);
        _currentIndex = 0;
        _pathFound = _path != null && _path.Count > 0;

        if (!_pathFound)
            return Status.Failure;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (!_pathFound)
            return Status.Failure;

        if (_currentIndex >= _path.Count)
            return Status.Success;

        Vector3 targetWorldPos = TileToWorld(_path[_currentIndex]);
        targetWorldPos.y = _agentTransform.position.y;
        
        Vector3 moveDir = (targetWorldPos - _agentTransform.position).normalized;
        if (moveDir != Vector3.zero)
            _agentTransform.forward = new Vector3(moveDir.x, 0f, moveDir.z);

        _agentTransform.position = Vector3.MoveTowards(_agentTransform.position, targetWorldPos, MoveSpeed * Time.deltaTime);

        if (Vector3.Distance(new Vector3(_agentTransform.position.x, 0, _agentTransform.position.z), new Vector3(targetWorldPos.x, 0, targetWorldPos.z)) < ArrivalThreshold)
        {
            _currentIndex++;
        }

        return _currentIndex >= _path.Count ? Status.Success : Status.Running;
    }

    protected override void OnEnd()
    {
        _path = null;
        _currentIndex = 0;
        _pathFound = false;
    }

    private Vector2Int WorldToTile(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.z));
    }

    private Vector3 TileToWorld(Vector2Int tilePos)
    {
        return new Vector3(tilePos.x, 0f, tilePos.y);
    }
}

