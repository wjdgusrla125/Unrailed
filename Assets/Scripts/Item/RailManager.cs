using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEngine;

public class RailManager : NetworkSingletonManager<RailManager>
{
    private Dictionary<Vector2Int, RailController> _rails = new();
    
    public NetworkVariable<Vector2Int> startHeadPos = new NetworkVariable<Vector2Int>(
        new Vector2Int(-999, -999),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            UpdateHeadRail();
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        startHeadPos.Dispose();
    }

    public void RegisterRail(RailController rail, Vector2Int gridPos)
    {
        _rails[gridPos] = rail;

        var startHead = _rails.Values.FirstOrDefault(r => r.isStartHeadRail);
        var endFirst = _rails.Values.FirstOrDefault(r => r.isEndFirstRail);

        bool adjToStart = startHead && IsAdjacent(startHead.GridPos, gridPos);
        bool adjToEnd = endFirst && IsAdjacent(endFirst.GridPos, gridPos);

        if (adjToStart && adjToEnd)
        {
            startHead.nextRail = rail.gameObject;
            rail.prevRail = startHead.gameObject;
            rail.nextRail = endFirst.gameObject;
            endFirst.prevRail = rail.gameObject;

            startHead.UpdateRailAppearanceServer();
            rail.UpdateRailAppearanceServer();
            endFirst.UpdateRailAppearanceServer();

            OnChainsMerged(startHead, rail, endFirst);
        }
        else
        {
            bool extendedStart = adjToStart && TryExtendChain(rail, gridPos, r => r.isStartHeadRail);

            if (!extendedStart)
                InitialConnectRail(rail, gridPos);
        }

        // ✅ 새로 놓은 레일이 체인의 끝이라면 head로 지정
        if (rail.prevRail != null && rail.nextRail == null)
        {
            rail.isStartHeadRail = true;
            if (IsServer)
                startHeadPos.Value = rail.GridPos;
        }

        // ✅ 항상 전체 헤드 재검사 (혹시나 끊긴 체인이 있다면 복구)
        UpdateHeadRail();
    }
    
    public void UnregisterRail(Vector2Int gridPos)
    {
        if (_rails.ContainsKey(gridPos))
        {
            _rails.Remove(gridPos);
            UpdateHeadRail(); // 체인 재정렬
        }
    }

    private void OnChainsMerged(RailController startHead, RailController middleRail, RailController endFirst)
    {
        GameManager.Instance.trainManager.RailConnected();
    }

    private bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }

    private bool TryExtendChain(RailController rail, Vector2Int pos, Func<RailController, bool> isHead)
    {
        var head = _rails.Values.FirstOrDefault(isHead);
        if (!head) return false;

        var headPos = head.GridPos;
        if (Mathf.Abs(pos.x - headPos.x) + Mathf.Abs(pos.y - headPos.y) == 1)
        {
            head.nextRail = rail.gameObject;
            rail.prevRail = head.gameObject;

            head.UpdateRailAppearanceServer();
            rail.UpdateRailAppearanceServer();
            return true;
        }

        return false;
    }

    private void InitialConnectRail(RailController rail, Vector2Int pos)
    {
        rail.prevRail = null;
        rail.nextRail = null;

        foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
        {
            if (!_rails.TryGetValue(pos + d, out var neighbor))
                continue;

            if ((d == Vector2Int.left || d == Vector2Int.down) && !rail.prevRail)
            {
                rail.prevRail = neighbor.gameObject;
                neighbor.nextRail = rail.gameObject;
                neighbor.UpdateRailAppearanceServer();
            }
            else if ((d == Vector2Int.right || d == Vector2Int.up) && !rail.nextRail)
            {
                rail.nextRail = neighbor.gameObject;
                neighbor.prevRail = rail.gameObject;
                neighbor.UpdateRailAppearanceServer();
            }
        }

        rail.UpdateRailAppearanceServer();
    }

    public void UpdateHeadRail()
    {
        foreach (var r in _rails.Values)
        {
            Debug.Log($"[UpdateHeadRail] registered rail: {r.GridPos} | startFirst: {r.isStartFirstRail}");
            r.isStartHeadRail = false;
            r.isEndHeadRail = false;
        }

        foreach (var start in _rails.Values.Where(r => r.isStartFirstRail))
        {
            Debug.Log($"[UpdateHeadRail] starting from: {start.GridPos}");

            var current = start;
            var visited = new HashSet<RailController> { current };
            while (current.nextRail)
            {
                var next = current.nextRail.GetComponent<RailController>();
                if (!visited.Add(next))
                {
                    break;
                }
                current = next;
            }
            current.isStartHeadRail = true;

            if (IsServer)
                startHeadPos.Value = current.GridPos;
        }
    }

    public void AllRailsDespawn()
    {
        foreach (var kvp in _rails)
        {
            kvp.Value.NetworkObject.Despawn();
        }
        _rails.Clear();
    }

    public void DebugLogAllChains()
    {
        var sb = new StringBuilder();

        var startA = _rails.Values.FirstOrDefault(r => r.isStartFirstRail);
        if (startA != null)
            AppendChain(sb, "Start Chain", startA);

        var startB = _rails.Values.FirstOrDefault(r => r.isEndFirstRail);
        if (startB != null)
            AppendChain(sb, "End Chain", startB);

        Debug.Log(sb.ToString());
    }

    private void AppendChain(StringBuilder sb, string label, RailController start)
    {
        sb.AppendLine(label);

        var chain = new List<RailController>();
        var current = start;
        var seen = new HashSet<RailController>();
        while (current && seen.Add(current))
        {
            chain.Add(current);
            current = current.nextRail
                ? current.nextRail.GetComponent<RailController>()
                : null;
        }

        foreach (var rail in chain)
        {
            string p = rail.prevRail
                ? $"Rail({rail.prevRail.GetComponent<RailController>().GridPos.x,2}:{rail.prevRail.GetComponent<RailController>().GridPos.y})"
                : "null";
            string n = rail.nextRail
                ? $"Rail({rail.nextRail.GetComponent<RailController>().GridPos.x,2}:{rail.nextRail.GetComponent<RailController>().GridPos.y})"
                : "null";
            sb.AppendLine($"Rail({rail.GridPos.x,2}:{rail.GridPos.y}): P = {p}, N = {n}");
        }

        var last = chain.Last();
        sb.AppendLine($"FirstRail = Rail({start.GridPos.x}:{start.GridPos.y})");
        sb.AppendLine($"HeadRail  = Rail({last.GridPos.x}:{last.GridPos.y})");
        sb.AppendLine();
    }

    public RailController GetStartHeadRail()
    {
        return _rails.Values.FirstOrDefault(r => r.isStartHeadRail);
    }

    public RailController GetEndFirstRail()
    {
        return _rails.Values.FirstOrDefault(r => r.isEndFirstRail);
    }
    
    public bool IsRailRegistered(Vector2Int pos)
    {
        return _rails.ContainsKey(pos);
    }
}