using System.Collections.Generic;
using UnityEngine;

public partial class DungeonPropPlacer
{
    private static bool TryRandomSpot(
        DungeonLayout.RoomArchetype archetype,
        System.Random random,
        List<Vector2> occupied,
        float clearance,
        out Vector2 result
    )
    {
        for (int attempt = 0; attempt < 28; attempt++)
        {
            var candidate = new Vector2(
                Mathf.Lerp(-archetype.HalfWidth, archetype.HalfWidth, (float)random.NextDouble()),
                Mathf.Lerp(-archetype.HalfDepth, archetype.HalfDepth, (float)random.NextDouble())
            );
            if (Mathf.Abs(candidate.x) < 1.55f || Mathf.Abs(candidate.y) < 1.55f)
                continue;
            if (IsOnDivider(candidate, archetype))
                continue;
            bool clear = true;
            foreach (Vector2 other in occupied)
                clear &= (candidate - other).sqrMagnitude >= clearance * clearance;
            if (!clear)
                continue;
            result = candidate;
            return true;
        }
        result = default;
        return false;
    }

    private static bool IsOnDivider(Vector2 point, DungeonLayout.RoomArchetype archetype)
    {
        if (archetype.Shape != DungeonLayout.RoomShape.Divided)
            return false;
        if ((archetype.Variant & 1) == 0)
        {
            float nearest = Mathf.Min(Mathf.Abs(point.y - 4f), Mathf.Abs(point.y + 4f));
            return Mathf.Abs(point.x) < 1.5f && nearest < 2.6f;
        }

        float nearestHorizontal = Mathf.Min(
            Mathf.Abs(point.x),
            Mathf.Abs(point.x - 8f),
            Mathf.Abs(point.x + 8f)
        );
        return Mathf.Abs(point.y) < 1.5f && nearestHorizontal < 2.6f;
    }

    private static float Clearance(string prefabName)
    {
        if (prefabName.Contains("Rocks") || prefabName.Contains("Structure"))
            return 2.6f;
        if (prefabName.Contains("Table") || prefabName.Contains("Stairs"))
            return 2.2f;
        return 1.45f;
    }

    private static (Vector2 pos, float yaw)[] TorchSpots(DungeonLayout.RoomArchetype archetype)
    {
        if (archetype.Shape == DungeonLayout.RoomShape.Compact)
            return new[]
            {
                (new Vector2(-3.5f, PositiveWallFace(6f)), 180f),
                (new Vector2(3.5f, PositiveWallFace(6f)), 180f),
                (new Vector2(-3.5f, NegativeWallFace(-6f)), 0f),
                (new Vector2(3.5f, NegativeWallFace(-6f)), 0f),
                (new Vector2(PositiveWallFace(6f), -3.5f), -90f),
                (new Vector2(PositiveWallFace(6f), 3.5f), -90f),
                (new Vector2(NegativeWallFace(-6f), -3.5f), 90f),
                (new Vector2(NegativeWallFace(-6f), 3.5f), 90f),
            };
        if (archetype.Shape == DungeonLayout.RoomShape.LongHorizontal)
            return new[]
            {
                (new Vector2(-8f, PositiveWallFace(6f)), 180f),
                (new Vector2(8f, PositiveWallFace(6f)), 180f),
                (new Vector2(-8f, NegativeWallFace(-6f)), 0f),
                (new Vector2(8f, NegativeWallFace(-6f)), 0f),
            };
        if (archetype.Shape == DungeonLayout.RoomShape.LongVertical)
            return new[]
            {
                (new Vector2(PositiveWallFace(6f), -4f), -90f),
                (new Vector2(PositiveWallFace(6f), 4f), -90f),
                (new Vector2(NegativeWallFace(-6f), -4f), 90f),
                (new Vector2(NegativeWallFace(-6f), 4f), 90f),
            };
        if (archetype.Shape == DungeonLayout.RoomShape.LargeSquare)
            return new[]
            {
                (new Vector2(-6f, PositiveWallFace(HalfRoomDepth)), 180f),
                (new Vector2(6f, PositiveWallFace(HalfRoomDepth)), 180f),
                (new Vector2(-6f, NegativeWallFace(-HalfRoomDepth)), 0f),
                (new Vector2(6f, NegativeWallFace(-HalfRoomDepth)), 0f),
                (new Vector2(PositiveWallFace(10f), -4f), -90f),
                (new Vector2(PositiveWallFace(10f), 4f), -90f),
                (new Vector2(NegativeWallFace(-10f), -4f), 90f),
                (new Vector2(NegativeWallFace(-10f), 4f), 90f),
            };
        return new[]
        {
            (new Vector2(-8f, PositiveWallFace(HalfRoomDepth)), 180f),
            (new Vector2(8f, PositiveWallFace(HalfRoomDepth)), 180f),
            (new Vector2(-8f, NegativeWallFace(-HalfRoomDepth)), 0f),
            (new Vector2(8f, NegativeWallFace(-HalfRoomDepth)), 0f),
            (new Vector2(PositiveWallFace(HalfRoomWidth), -5f), -90f),
            (new Vector2(PositiveWallFace(HalfRoomWidth), 5f), -90f),
            (new Vector2(NegativeWallFace(-HalfRoomWidth), -5f), 90f),
            (new Vector2(NegativeWallFace(-HalfRoomWidth), 5f), 90f),
        };
    }

    private static float PositiveWallFace(float wallCoordinate)
    {
        return wallCoordinate + WallFrontFaceOffset;
    }

    private static float NegativeWallFace(float wallCoordinate)
    {
        return wallCoordinate + WallBackFaceOffset;
    }

    private static Vector2 PoolSpot(DungeonLayout.RoomArchetype archetype, System.Random random)
    {
        Vector2 corner = new Vector2(
            (archetype.Variant & 1) == 0 ? -1f : 1f,
            (archetype.Variant & 2) == 0 ? -1f : 1f
        );
        float x = Mathf.Lerp(
            archetype.HalfWidth * 0.2f,
            archetype.HalfWidth * 0.62f,
            (float)random.NextDouble()
        );
        float z = Mathf.Lerp(
            archetype.HalfDepth * 0.2f,
            archetype.HalfDepth * 0.58f,
            (float)random.NextDouble()
        );
        return new Vector2(x * corner.x, z * corner.y);
    }

    private static Vector2 RotateQuarterTurns(Vector2 point, int turns)
    {
        return ((turns % 4 + 4) % 4) switch
        {
            1 => new Vector2(point.y, -point.x),
            2 => -point,
            3 => new Vector2(-point.y, point.x),
            _ => point,
        };
    }

    private static void Shuffle<T>(T[] values, System.Random random)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
