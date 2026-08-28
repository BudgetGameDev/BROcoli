using UnityEngine;

public partial class DungeonPropPlacer
{
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
