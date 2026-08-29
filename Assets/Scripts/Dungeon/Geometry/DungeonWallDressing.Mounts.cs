using UnityEngine;

public static partial class DungeonWallDressing
{
    private const float BannerMeshDepthOffset = 1.05f;

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
            new DungeonWallMount(new Vector2(-horizontalOffset, NearFace(wallZ)), 180f),
            new DungeonWallMount(new Vector2(horizontalOffset, NearFace(wallZ)), 180f),
            BottomWallTorch(-horizontalOffset, -wallZ),
            BottomWallTorch(horizontalOffset, -wallZ),
            new DungeonWallMount(new Vector2(NearFace(wallX), -verticalOffset), -90f),
            new DungeonWallMount(new Vector2(NearFace(wallX), verticalOffset), -90f),
            new DungeonWallMount(new Vector2(FarFace(-wallX), -verticalOffset), 90f),
            new DungeonWallMount(new Vector2(FarFace(-wallX), verticalOffset), 90f),
        };
    }

    private static DungeonWallMount[] HorizontalTorches(float wallZ, float offset)
    {
        return new[]
        {
            new DungeonWallMount(new Vector2(-offset, NearFace(wallZ)), 180f),
            new DungeonWallMount(new Vector2(offset, NearFace(wallZ)), 180f),
            BottomWallTorch(-offset, -wallZ),
            BottomWallTorch(offset, -wallZ),
        };
    }

    private static DungeonWallMount[] VerticalTorches(float wallX, float offset)
    {
        return new[]
        {
            new DungeonWallMount(new Vector2(NearFace(wallX), -offset), -90f),
            new DungeonWallMount(new Vector2(NearFace(wallX), offset), -90f),
            new DungeonWallMount(new Vector2(FarFace(-wallX), -offset), 90f),
            new DungeonWallMount(new Vector2(FarFace(-wallX), offset), 90f),
        };
    }

    private static DungeonWallMount BottomWallTorch(float x, float wallCoordinate)
    {
        // Bottom walls become half walls from the gameplay camera. Keep their
        // torches on the world-downward face so the bracket remains attached.
        return new DungeonWallMount(new Vector2(x, NearFace(wallCoordinate)), 180f);
    }

    private static DungeonWallMount BannerMount(DungeonLayout.RoomArchetype archetype, int side)
    {
        float wallX = archetype.Shape switch
        {
            DungeonLayout.RoomShape.Tiny => 4f,
            DungeonLayout.RoomShape.NarrowVertical => 4f,
            DungeonLayout.RoomShape.Compact => 6f,
            DungeonLayout.RoomShape.LongVertical => 6f,
            DungeonLayout.RoomShape.LargeSquare => 10f,
            _ => HalfRoomWidth,
        };
        float wallZ = archetype.Shape switch
        {
            DungeonLayout.RoomShape.Tiny => 4f,
            DungeonLayout.RoomShape.NarrowHorizontal => 4f,
            DungeonLayout.RoomShape.Compact => 6f,
            DungeonLayout.RoomShape.LongHorizontal => 6f,
            _ => HalfRoomDepth,
        };

        return ((side % 4) + 4) % 4 switch
        {
            0 => new DungeonWallMount(
                new Vector2(-3.5f, NearFace(wallZ) + BannerMeshDepthOffset),
                0f
            ),
            1 => new DungeonWallMount(
                new Vector2(NearFace(wallX) + BannerMeshDepthOffset, -3f),
                90f
            ),
            2 => new DungeonWallMount(
                new Vector2(3.5f, FarFace(-wallZ) - BannerMeshDepthOffset),
                180f
            ),
            _ => new DungeonWallMount(
                new Vector2(FarFace(-wallX) - BannerMeshDepthOffset, 3f),
                -90f
            ),
        };
    }
}
