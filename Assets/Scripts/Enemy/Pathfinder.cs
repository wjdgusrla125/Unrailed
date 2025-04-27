using System.Collections.Generic;
using UnityEngine;

public class Pathfinder
{
    private static readonly Vector2Int[] Directions = 
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    public static bool IsWalkable(Vector2Int pos)
    {
        if (!IsInBounds(pos))
            return false;

        var tile = MapGenerator.Instance.Map[pos.x, pos.y];
        return tile == MapGenerator.TileType.Grass;
    }

    public static bool IsInBounds(Vector2Int pos)
    {
        var map = MapGenerator.Instance.Map;
        return pos.x >= 0 && pos.x < map.GetLength(0) &&
               pos.y >= 0 && pos.y < map.GetLength(1);
    }

    public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int target)
    {
        var openSet = new PriorityQueue<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, int>();
        var fScore = new Dictionary<Vector2Int, int>();

        openSet.Enqueue(start, 0);
        gScore[start] = 0;
        fScore[start] = Heuristic(start, target);

        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.Dequeue();

            if (current == target)
                return ReconstructPath(cameFrom, current);

            foreach (var dir in Directions)
            {
                Vector2Int neighbor = current + dir;
                if (!IsWalkable(neighbor))
                    continue;

                int tentativeG = gScore[current] + 1;
                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + Heuristic(neighbor, target);
                    if (!openSet.Contains(neighbor))
                        openSet.Enqueue(neighbor, fScore[neighbor]);
                }
            }
        }

        return null; // 경로 없음
    }

    private static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        List<Vector2Int> path = new List<Vector2Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }

    private static int Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); // Manhattan Distance
    }
}
