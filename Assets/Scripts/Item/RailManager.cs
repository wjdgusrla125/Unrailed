
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class RailManager : SingletonManager<RailManager>
{
    private Dictionary<Vector2Int, RailController> _rails = new();

    public void RegisterRail(RailController rail, Vector2Int gridPos)
    {
        _rails[gridPos] = rail;

        var startHead = _rails.Values.FirstOrDefault(r => r.isStartHeadRail);
        var endFirst  = _rails.Values.FirstOrDefault(r => r.isEndFirstRail);

        bool adjToStart = startHead && IsAdjacent(startHead.GridPos, gridPos);
        bool adjToEnd   = endFirst && IsAdjacent(endFirst.GridPos,  gridPos);

        if (adjToStart && adjToEnd)
        {
            startHead.nextRail = rail.gameObject;
            rail.prevRail      = startHead.gameObject;
            rail.nextRail      = endFirst.gameObject;
            endFirst.prevRail  = rail.gameObject;

            startHead.UpdateRailAppearance();
            rail.UpdateRailAppearance();
            endFirst.UpdateRailAppearance();
        }
        else
        {
            bool extendedStart = adjToStart && TryExtendChain(rail, gridPos, r => r.isStartHeadRail);

            // 레일 첫 생성 시에는 아무 곳에도 붙지 않는다.
            if (!extendedStart)
                InitialConnectRail(rail, gridPos);
        }

        //헤드 플래그 재계산
        UpdateHeadRail();
    }

    //두 좌표가 맨해튼 거리가 1이면 true
    private bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }
    
    private bool TryExtendChain(RailController rail, Vector2Int pos, Func<RailController,bool> isHead)
    {
        var head = _rails.Values.FirstOrDefault(isHead);
        if (!head) return false;

        var headPos = head.GridPos;
        if (Math.Abs(pos.x - headPos.x) + Math.Abs(pos.y - headPos.y) == 1)
        {
            head.nextRail = rail.gameObject;
            rail.prevRail = head.gameObject;
            head.UpdateRailAppearance();
            rail.UpdateRailAppearance();
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

            if ((d == Vector2Int.left || d == Vector2Int.down) 
                && !rail.prevRail)
            {
                rail.prevRail       = neighbor.gameObject;
                neighbor.nextRail   = rail.gameObject;
                neighbor.UpdateRailAppearance();
            }
            
            else if ((d == Vector2Int.right || d == Vector2Int.up)
                     && !rail.nextRail)
            {
                rail.nextRail       = neighbor.gameObject;
                neighbor.prevRail   = rail.gameObject;
                neighbor.UpdateRailAppearance();
            }
        }

        rail.UpdateRailAppearance();;
    }

    private void UpdateHeadRail()
    {
        foreach (var r in _rails.Values)
        {
            r.isStartHeadRail = false;
            r.isEndHeadRail   = false;
        }

        foreach (var start in _rails.Values.Where(r => r.isStartFirstRail))
        {
            var current = start;
            var visited = new HashSet<RailController> { current };
            while (current.nextRail)
            {
                var next = current.nextRail.GetComponent<RailController>();
                if (!visited.Add(next))
                {
                    Debug.LogWarning("Start 체인 순환 감지");
                    break;
                }
                current = next;
            }
            current.isStartHeadRail = true;
        }

        foreach (var end in _rails.Values.Where(r => r.isEndFirstRail))
        {
            var current = end;
            var visited = new HashSet<RailController> { current };
            while (current.nextRail)
            {
                var next = current.nextRail.GetComponent<RailController>();
                if (!visited.Add(next))
                {
                    Debug.LogWarning("End 체인 순환 감지");
                    break;
                }
                current = next;
            }
            current.isEndHeadRail = true;
        }
    }

    public void DebugLogAllChains()
    {
        var sb = new StringBuilder();

        var startA = _rails.Values.First(r => r.isStartFirstRail);
        AppendChain(sb, "posA", startA);

        var startB = _rails.Values.First(r => r.isEndFirstRail);
        AppendChain(sb, "posB", startB);

        Debug.Log(sb.ToString());
    }
    
    //디버그용: 현재 연결된 레일을 출력
    private void AppendChain(StringBuilder sb, string label, RailController start)
    {
        sb.AppendLine(label);

        // 체인 순회
        var chain   = new List<RailController>();
        var current = start;
        var seen    = new HashSet<RailController>();
        while (current && seen.Add(current))
        {
            chain.Add(current);
            current = current.nextRail
                ? current.nextRail.GetComponent<RailController>()
                : null;
        }

        // 각 레일 출력 (X 좌표를 2칸 맞춤)
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

        sb.AppendLine($"FirstRail = Rail({start.GridPos.x}:{start.GridPos.y})");
        var last = chain.Last();
        sb.AppendLine($"HeadRail  = Rail({last.GridPos.x}:{last.GridPos.y})");
        sb.AppendLine();
    }

    public void AllRailsDespawn()
    {
        foreach (var kvp in _rails)
        {
            kvp.Value.NetworkObject.Despawn();
        }
        _rails.Clear();
    }

    //가장 앞의 레일을 반환
    public RailController GetStartHeadRail()
    {
        return _rails.Values.FirstOrDefault(r => r.isStartHeadRail);
    }

    public RailController GetEndFirstRail()
    {
        RailController rc = _rails.Values.FirstOrDefault(r => r.isEndFirstRail);
        return rc;
    }
}
