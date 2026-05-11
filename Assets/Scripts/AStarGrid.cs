using System.Collections.Generic;
using UnityEngine;

public sealed class AStarGrid : MonoBehaviour
{
    [SerializeField] private List<Transform> nodes = new List<Transform>();
    public IReadOnlyList<Transform> Nodes => nodes;

    public void SetNodes(List<Transform> trackNodes)
    {
        nodes = trackNodes;
    }

    public Transform FindClosestNode(Vector3 position)
    {
        Transform closest = null;
        var bestSqrDistance = float.PositiveInfinity;

        foreach (var node in nodes)
        {
            if (node == null)
            {
                continue;
            }

            var sqrDistance = (node.position - position).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                closest = node;
            }
        }

        return closest;
    }

    public List<Transform> FindPath(Transform start, Transform goal)
    {
        var path = new List<Transform>();
        if (start == null || goal == null || nodes.Count == 0)
        {
            return path;
        }

        var openSet = new List<Transform> { start };
        var cameFrom = new Dictionary<Transform, Transform>();
        var gScore = new Dictionary<Transform, float>();
        var fScore = new Dictionary<Transform, float>();

        foreach (var node in nodes)
        {
            if (node == null)
            {
                continue;
            }

            gScore[node] = float.PositiveInfinity;
            fScore[node] = float.PositiveInfinity;
        }

        gScore[start] = 0f;
        fScore[start] = Heuristic(start, goal);

        while (openSet.Count > 0)
        {
            var current = GetLowestScore(openSet, fScore);
            if (current == goal)
            {
                return ReconstructPath(cameFrom, current);
            }

            openSet.Remove(current);

            foreach (var neighbour in GetNeighbours(current))
            {
                var tentativeScore = gScore[current] + Vector3.Distance(current.position, neighbour.position);
                if (tentativeScore >= gScore[neighbour])
                {
                    continue;
                }

                cameFrom[neighbour] = current;
                gScore[neighbour] = tentativeScore;
                fScore[neighbour] = tentativeScore + Heuristic(neighbour, goal);

                if (!openSet.Contains(neighbour))
                {
                    openSet.Add(neighbour);
                }
            }
        }

        return path;
    }

    public int IndexOf(Transform node)
    {
        return nodes.IndexOf(node);
    }

    private IEnumerable<Transform> GetNeighbours(Transform current)
    {
        var index = nodes.IndexOf(current);
        if (index < 0 || nodes.Count == 0)
        {
            yield break;
        }

        yield return nodes[(index + 1) % nodes.Count];
    }

    private static float Heuristic(Transform a, Transform b)
    {
        return Vector3.Distance(a.position, b.position);
    }

    private static Transform GetLowestScore(List<Transform> openSet, Dictionary<Transform, float> score)
    {
        var bestNode = openSet[0];
        var bestScore = score.TryGetValue(bestNode, out var firstScore) ? firstScore : float.PositiveInfinity;

        for (var i = 1; i < openSet.Count; i++)
        {
            var candidate = openSet[i];
            var candidateScore = score.TryGetValue(candidate, out var value) ? value : float.PositiveInfinity;
            if (candidateScore < bestScore)
            {
                bestNode = candidate;
                bestScore = candidateScore;
            }
        }

        return bestNode;
    }

    private static List<Transform> ReconstructPath(Dictionary<Transform, Transform> cameFrom, Transform current)
    {
        var path = new List<Transform> { current };
        while (cameFrom.TryGetValue(current, out var previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
