using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A grid of the space a player-sized capsule can stand in, given a set of
/// wall pieces. Walls are inflated by the capsule radius, so a passage the
/// player cannot physically fit through is not connected here either - which
/// makes reachability and minimum corridor width the same question.
/// </summary>
internal sealed class DungeonWalkableSpace
{
    private const float Step = 0.25f;

    private readonly Rect domain;
    private readonly int countX;
    private readonly int countZ;
    private readonly bool[] free;
    private readonly bool[] reached;

    public DungeonWalkableSpace(Rect domain, IEnumerable<DungeonWallPiece> walls, float inflate)
    {
        this.domain = domain;
        countX = Mathf.CeilToInt(domain.width / Step);
        countZ = Mathf.CeilToInt(domain.height / Step);
        free = new bool[countX * countZ];
        reached = new bool[countX * countZ];
        for (int i = 0; i < free.Length; i++)
            free[i] = true;

        foreach (DungeonWallPiece piece in walls)
            Block(piece.Footprint, inflate);
    }

    /// <summary>Floods the walkable space outward from a starting point.</summary>
    public void Flood(Vector2 start)
    {
        System.Array.Clear(reached, 0, reached.Length);
        if (!TryIndex(start, out int startIndex) || !free[startIndex])
            return;

        var queue = new Queue<int>();
        reached[startIndex] = true;
        queue.Enqueue(startIndex);
        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index / countZ;
            int z = index % countZ;
            Visit(x + 1, z, queue);
            Visit(x - 1, z, queue);
            Visit(x, z + 1, queue);
            Visit(x, z - 1, queue);
        }
    }

    public bool IsFree(Vector2 point)
    {
        return TryIndex(point, out int index) && free[index];
    }

    public bool IsReached(Vector2 point)
    {
        return TryIndex(point, out int index) && reached[index];
    }

    /// <summary>Every reached cell centre, for asserting where a fill escaped to.</summary>
    public IEnumerable<Vector2> ReachedPoints()
    {
        for (int x = 0; x < countX; x++)
        for (int z = 0; z < countZ; z++)
        {
            if (reached[x * countZ + z])
                yield return new Vector2(
                    domain.xMin + (x + 0.5f) * Step,
                    domain.yMin + (z + 0.5f) * Step
                );
        }
    }

    private void Visit(int x, int z, Queue<int> queue)
    {
        if (x < 0 || x >= countX || z < 0 || z >= countZ)
            return;
        int index = x * countZ + z;
        if (reached[index] || !free[index])
            return;
        reached[index] = true;
        queue.Enqueue(index);
    }

    private void Block(Rect footprint, float inflate)
    {
        int minX = Mathf.Max(0, CellIndex(footprint.xMin - inflate - domain.xMin));
        int maxX = Mathf.Min(countX - 1, CellIndex(footprint.xMax + inflate - domain.xMin));
        int minZ = Mathf.Max(0, CellIndex(footprint.yMin - inflate - domain.yMin));
        int maxZ = Mathf.Min(countZ - 1, CellIndex(footprint.yMax + inflate - domain.yMin));
        for (int x = minX; x <= maxX; x++)
        for (int z = minZ; z <= maxZ; z++)
        {
            Vector2 center = new Vector2(
                domain.xMin + (x + 0.5f) * Step,
                domain.yMin + (z + 0.5f) * Step
            );
            if (
                center.x >= footprint.xMin - inflate
                && center.x <= footprint.xMax + inflate
                && center.y >= footprint.yMin - inflate
                && center.y <= footprint.yMax + inflate
            )
                free[x * countZ + z] = false;
        }
    }

    private static int CellIndex(float offset)
    {
        return Mathf.FloorToInt(offset / Step);
    }

    private bool TryIndex(Vector2 point, out int index)
    {
        int x = CellIndex(point.x - domain.xMin);
        int z = CellIndex(point.y - domain.yMin);
        index = x * countZ + z;
        return x >= 0 && x < countX && z >= 0 && z < countZ;
    }
}
