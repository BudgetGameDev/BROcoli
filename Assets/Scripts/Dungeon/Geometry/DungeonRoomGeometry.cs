using System.Collections.Generic;
using UnityEngine;

/// <summary>An archway frame standing in one doorway.</summary>
public readonly struct DungeonArchway
{
    public readonly Vector2 Position;
    public readonly float Yaw;

    public DungeonArchway(Vector2 position, float yaw)
    {
        Position = position;
        Yaw = yaw;
    }
}

/// <summary>
/// Where every wall piece, archway, and junction of the dungeon goes, as pure
/// ground-plane arithmetic. <see cref="DungeonRoomBuilder"/> is the only thing
/// that turns these placements into GameObjects, so the geometry the tests
/// reason about is exactly the geometry the game builds.
/// </summary>
public static partial class DungeonRoomGeometry
{
    private const float Tile = DungeonLayout.TileSize;
    private const float HalfRoomWidth = DungeonLayout.RoomWidth / 2f;
    private const float HalfRoomDepth = DungeonLayout.RoomDepth / 2f;

    private const string EdgeSection = "Wall Run";

    /// <summary>
    /// The shared boundary run between two rooms. Openings drop a wall piece.
    /// Because every slab straddles its own boundary line, a run reaches
    /// exactly to the room's corner and overlaps the perpendicular run there by
    /// half a slab: junctions close themselves, with nothing to trim.
    /// </summary>
    public static void AppendEdgeWalls(
        List<DungeonWallPiece> walls,
        DungeonEdge edge,
        DungeonPassage passage
    )
    {
        Vector2 roomCenter = DungeonLayout.RoomCenter(new Vector2Int(edge.X, edge.Y));
        int slotCount = edge.Horizontal ? DungeonLayout.RoomTilesX : DungeonLayout.RoomTilesZ;
        for (int slot = 0; slot < slotCount; slot++)
        {
            if (passage.HasOpening(slot))
                continue;

            float offset = DungeonPassage.SlotOffset(slot, slotCount);
            walls.Add(
                new DungeonWallPiece(
                    edge.Horizontal
                        ? new Vector2(roomCenter.x + offset, roomCenter.y + HalfRoomDepth)
                        : new Vector2(roomCenter.x + HalfRoomWidth, roomCenter.y + offset),
                    edge.Horizontal,
                    DungeonWallKind.Shell,
                    EdgeSection
                )
            );
        }
    }

    /// <summary>The archway frames standing in this edge's openings.</summary>
    public static void AppendEdgeArchways(
        List<DungeonArchway> archways,
        DungeonEdge edge,
        DungeonPassage passage
    )
    {
        Vector2 roomCenter = DungeonLayout.RoomCenter(new Vector2Int(edge.X, edge.Y));
        int slotCount = edge.Horizontal ? DungeonLayout.RoomTilesX : DungeonLayout.RoomTilesZ;
        for (int slot = 0; slot < slotCount; slot++)
        {
            if (!passage.HasArchway(slot))
                continue;

            // The gate prefab is already centred on its own root, so it goes
            // straight onto the boundary line beside the wall slabs.
            float offset = DungeonPassage.SlotOffset(slot, slotCount);
            archways.Add(
                edge.Horizontal
                    ? new DungeonArchway(
                        new Vector2(roomCenter.x + offset, roomCenter.y + HalfRoomDepth),
                        0f
                    )
                    : new DungeonArchway(
                        new Vector2(roomCenter.x + HalfRoomWidth, roomCenter.y + offset),
                        90f
                    )
            );
        }
    }

    /// <summary>The endpoints of an edge's centre line, for occlusion grouping.</summary>
    public static (Vector2 From, Vector2 To) EdgeSpan(DungeonEdge edge)
    {
        Vector2 roomCenter = DungeonLayout.RoomCenter(new Vector2Int(edge.X, edge.Y));
        return edge.Horizontal
            ? (
                new Vector2(roomCenter.x - HalfRoomWidth, roomCenter.y + HalfRoomDepth),
                new Vector2(roomCenter.x + HalfRoomWidth, roomCenter.y + HalfRoomDepth)
            )
            : (
                new Vector2(roomCenter.x + HalfRoomWidth, roomCenter.y - HalfRoomDepth),
                new Vector2(roomCenter.x + HalfRoomWidth, roomCenter.y + HalfRoomDepth)
            );
    }

    /// <summary>
    /// The grid corner where four rooms meet. Vertex (x, y) is the north-east
    /// corner of room (x, y).
    /// </summary>
    public static Vector2 JunctionPoint(Vector2Int vertex)
    {
        return new Vector2(
            vertex.x * DungeonLayout.RoomWidth + HalfRoomWidth,
            vertex.y * DungeonLayout.RoomDepth + HalfRoomDepth
        );
    }

    /// <summary>The floor rectangle a room's geometry is expected to stay within.</summary>
    public static Rect RoomFloorBounds(Vector2Int room)
    {
        Vector2 center = DungeonLayout.RoomCenter(room);
        return Rect.MinMaxRect(
            center.x - HalfRoomWidth,
            center.y - HalfRoomDepth,
            center.x + HalfRoomWidth,
            center.y + HalfRoomDepth
        );
    }
}
