using System.Collections.Generic;
using UnityEngine;

public partial class DungeonPropPlacer
{
    // Half width of a torch bracket plus a little breathing room. Anything
    // closer than this to an opening's centre line reads as hanging in the
    // doorway rather than being mounted on the wall beside it.
    private const float TorchDoorwayClearance = 0.9f;
    private const float TorchSpacing = 3f;

    /// <summary>
    /// The room's torch mounting points, minus any that would sit inside a
    /// doorway. When the shape's preferred spots are mostly doorways, the
    /// remaining solid outer-wall pieces make up the difference so a room is
    /// never left unlit.
    /// </summary>
    private static List<(Vector2 pos, float yaw)> AvailableTorchSpots(
        DungeonLayout.RoomArchetype archetype,
        DungeonLayout.RoomDoorways doorways,
        int wanted,
        System.Random random
    )
    {
        var spots = new List<(Vector2 pos, float yaw)>();
        foreach ((Vector2 pos, float yaw) spot in TorchSpots(archetype))
        {
            if (!doorways.BlocksDoorway(spot.pos, TorchDoorwayClearance))
                spots.Add(spot);
        }
        Shuffle(spots, random);
        if (spots.Count >= wanted)
            return spots;

        List<(Vector2 pos, float yaw)> fallback = SolidOuterWallTorchSpots(doorways);
        Shuffle(fallback, random);
        foreach ((Vector2 pos, float yaw) spot in fallback)
        {
            if (spots.Count >= wanted)
                break;
            if (!IsClearOfSpots(spots, spot.pos))
                continue;
            spots.Add(spot);
        }
        return spots;
    }

    /// <summary>
    /// One mounting point per outer-wall piece that survived the doorway cuts.
    /// </summary>
    private static List<(Vector2 pos, float yaw)> SolidOuterWallTorchSpots(
        DungeonLayout.RoomDoorways doorways
    )
    {
        var spots = new List<(Vector2 pos, float yaw)>();
        for (int slot = 1; slot < DungeonLayout.RoomTilesX - 1; slot++)
        {
            float x = DungeonPassage.SlotOffset(slot, DungeonLayout.RoomTilesX);
            if (!doorways.North.HasOpening(slot))
                spots.Add((new Vector2(x, PositiveWallFace(HalfRoomDepth)), 180f));
            if (!doorways.South.HasOpening(slot))
                spots.Add(BottomWallTorch(x, -HalfRoomDepth));
        }

        for (int slot = 1; slot < DungeonLayout.RoomTilesZ - 1; slot++)
        {
            float z = DungeonPassage.SlotOffset(slot, DungeonLayout.RoomTilesZ);
            if (!doorways.East.HasOpening(slot))
                spots.Add((new Vector2(PositiveWallFace(HalfRoomWidth), z), -90f));
            if (!doorways.West.HasOpening(slot))
                spots.Add((new Vector2(NegativeWallFace(-HalfRoomWidth), z), 90f));
        }
        return spots;
    }

    private static bool IsClearOfSpots(List<(Vector2 pos, float yaw)> spots, Vector2 candidate)
    {
        foreach ((Vector2 pos, float yaw) spot in spots)
        {
            if ((spot.pos - candidate).sqrMagnitude < TorchSpacing * TorchSpacing)
                return false;
        }
        return true;
    }

    private static (Vector2 pos, float yaw)[] TorchSpots(DungeonLayout.RoomArchetype archetype)
    {
        return archetype.Shape switch
        {
            DungeonLayout.RoomShape.Tiny => FourWallTorchSpots(4f, 4f, 2.7f, 2.7f),
            DungeonLayout.RoomShape.Compact => FourWallTorchSpots(6f, 6f, 3.5f, 3.5f),
            DungeonLayout.RoomShape.NarrowHorizontal => HorizontalTorchSpots(4f, 8f),
            DungeonLayout.RoomShape.LongHorizontal => HorizontalTorchSpots(6f, 8f),
            DungeonLayout.RoomShape.NarrowVertical => VerticalTorchSpots(4f, 4f),
            DungeonLayout.RoomShape.LongVertical => VerticalTorchSpots(6f, 4f),
            DungeonLayout.RoomShape.LargeSquare => FourWallTorchSpots(10f, HalfRoomDepth, 6f, 4f),
            _ => FourWallTorchSpots(HalfRoomWidth, HalfRoomDepth, 8f, 5f),
        };
    }

    private static (Vector2 pos, float yaw)[] FourWallTorchSpots(
        float wallX,
        float wallZ,
        float horizontalOffset,
        float verticalOffset
    )
    {
        return new[]
        {
            (new Vector2(-horizontalOffset, PositiveWallFace(wallZ)), 180f),
            (new Vector2(horizontalOffset, PositiveWallFace(wallZ)), 180f),
            BottomWallTorch(-horizontalOffset, -wallZ),
            BottomWallTorch(horizontalOffset, -wallZ),
            (new Vector2(PositiveWallFace(wallX), -verticalOffset), -90f),
            (new Vector2(PositiveWallFace(wallX), verticalOffset), -90f),
            (new Vector2(NegativeWallFace(-wallX), -verticalOffset), 90f),
            (new Vector2(NegativeWallFace(-wallX), verticalOffset), 90f),
        };
    }

    private static (Vector2 pos, float yaw)[] HorizontalTorchSpots(float wallZ, float offset)
    {
        return new[]
        {
            (new Vector2(-offset, PositiveWallFace(wallZ)), 180f),
            (new Vector2(offset, PositiveWallFace(wallZ)), 180f),
            BottomWallTorch(-offset, -wallZ),
            BottomWallTorch(offset, -wallZ),
        };
    }

    private static (Vector2 pos, float yaw)[] VerticalTorchSpots(float wallX, float offset)
    {
        return new[]
        {
            (new Vector2(PositiveWallFace(wallX), -offset), -90f),
            (new Vector2(PositiveWallFace(wallX), offset), -90f),
            (new Vector2(NegativeWallFace(-wallX), -offset), 90f),
            (new Vector2(NegativeWallFace(-wallX), offset), 90f),
        };
    }

    private static (Vector2 pos, float yaw) BottomWallTorch(float x, float wallCoordinate)
    {
        // Bottom walls become half walls from the gameplay camera. Keep their
        // torches on the world-downward face so the bracket remains attached.
        return (new Vector2(x, PositiveWallFace(wallCoordinate)), 180f);
    }
}
