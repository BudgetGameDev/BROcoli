using UnityEngine;

public static partial class DungeonWallDressing
{
    /// <summary>
    /// The mounting points a room shape offers, before doorways are consulted.
    /// Compact shapes hang their torches on interior runs; open ones use the
    /// outer shell.
    /// </summary>
    private static DungeonWallMount[] ShapeTorchMounts(DungeonLayout.RoomArchetype archetype)
    {
        return archetype.Shape switch
        {
            DungeonLayout.RoomShape.Tiny => FourWallTorches(4f, 4f, 2.7f, 2.7f),
            DungeonLayout.RoomShape.Compact => FourWallTorches(6f, 6f, 3.5f, 3.5f),
            DungeonLayout.RoomShape.NarrowHorizontal => HorizontalTorches(4f, 8f),
            DungeonLayout.RoomShape.LongHorizontal => HorizontalTorches(6f, 8f),
            DungeonLayout.RoomShape.NarrowVertical => VerticalTorches(4f, 4f),
            DungeonLayout.RoomShape.LongVertical => VerticalTorches(6f, 4f),
            DungeonLayout.RoomShape.LargeSquare => FourWallTorches(10f, HalfRoomDepth, 6f, 4f),
            _ => FourWallTorches(HalfRoomWidth, HalfRoomDepth, 8f, 5f),
        };
    }

    private static DungeonWallMount[] FourWallTorches(
        float wallX,
        float wallZ,
        float horizontalOffset,
        float verticalOffset
    )
    {
        return new[]
        {
            new DungeonWallMount(new Vector2(-horizontalOffset, InnerFace(wallZ)), 180f),
            new DungeonWallMount(new Vector2(horizontalOffset, InnerFace(wallZ)), 180f),
            BottomWallTorch(-horizontalOffset, -wallZ),
            BottomWallTorch(horizontalOffset, -wallZ),
            new DungeonWallMount(new Vector2(InnerFace(wallX), -verticalOffset), -90f),
            new DungeonWallMount(new Vector2(InnerFace(wallX), verticalOffset), -90f),
            new DungeonWallMount(new Vector2(InnerFace(-wallX), -verticalOffset), 90f),
            new DungeonWallMount(new Vector2(InnerFace(-wallX), verticalOffset), 90f),
        };
    }

    private static DungeonWallMount[] HorizontalTorches(float wallZ, float offset)
    {
        return new[]
        {
            new DungeonWallMount(new Vector2(-offset, InnerFace(wallZ)), 180f),
            new DungeonWallMount(new Vector2(offset, InnerFace(wallZ)), 180f),
            BottomWallTorch(-offset, -wallZ),
            BottomWallTorch(offset, -wallZ),
        };
    }

    private static DungeonWallMount[] VerticalTorches(float wallX, float offset)
    {
        return new[]
        {
            new DungeonWallMount(new Vector2(InnerFace(wallX), -offset), -90f),
            new DungeonWallMount(new Vector2(InnerFace(wallX), offset), -90f),
            new DungeonWallMount(new Vector2(InnerFace(-wallX), -offset), 90f),
            new DungeonWallMount(new Vector2(InnerFace(-wallX), offset), 90f),
        };
    }

    private static DungeonWallMount BottomWallTorch(float x, float wallCoordinate)
    {
        // Bottom walls become half walls from the gameplay camera. Keep their
        // torches on the outward face so the bracket remains attached.
        return new DungeonWallMount(new Vector2(x, OuterFace(wallCoordinate)), 180f);
    }
}
