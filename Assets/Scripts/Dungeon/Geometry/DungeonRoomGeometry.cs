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
    /// The shared boundary run between two rooms. Openings drop a wall piece;
    /// the two end pieces are trimmed and extended by half a slab depth so
    /// consecutive runs meet cleanly at the junction between them.
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
            if (edge.Horizontal)
            {
                // Pull the first piece back off the perpendicular run and push
                // the last one through it, so the seam lands inside the
                // neighbouring slab instead of exposing a bevelled end face.
                float adjustment = 0f;
                float end = 0f;
                if (slot == 0)
                {
                    adjustment = -DungeonWallPiece.SlabThickness / 2f;
                    end = -1f;
                }
                else if (slot == slotCount - 1)
                {
                    adjustment = DungeonWallPiece.SlabThickness / 2f;
                    end = 1f;
                }

                walls.Add(
                    new DungeonWallPiece(
                        new Vector2(roomCenter.x + offset, roomCenter.y + HalfRoomDepth),
                        true,
                        DungeonWallKind.Shell,
                        EdgeSection,
                        adjustment,
                        end
                    )
                );
            }
            else
            {
                walls.Add(
                    new DungeonWallPiece(
                        new Vector2(roomCenter.x + HalfRoomWidth, roomCenter.y + offset),
                        false,
                        DungeonWallKind.Shell,
                        EdgeSection
                    )
                );
            }
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

            // The gate assembly is centred on the wall slab rather than on the
            // boundary line, so it stays flush with the pieces beside it.
            float offset = DungeonPassage.SlotOffset(slot, slotCount);
            archways.Add(
                edge.Horizontal
                    ? new DungeonArchway(
                        new Vector2(
                            roomCenter.x + offset,
                            roomCenter.y + HalfRoomDepth + DungeonWallPiece.SlabCenterOffset
                        ),
                        0f
                    )
                    : new DungeonArchway(
                        new Vector2(
                            roomCenter.x + HalfRoomWidth + DungeonWallPiece.SlabCenterOffset,
                            roomCenter.y + offset
                        ),
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
