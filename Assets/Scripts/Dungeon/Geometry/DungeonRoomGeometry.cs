using System.Collections.Generic;
using UnityEngine;

/// <summary>An archway frame standing in one doorway.</summary>
public readonly struct DungeonArchway
{
    // The gate prefab is centred on its own root and stands a little wider and
    // taller than the wall pieces beside it. Its two posts leave the doorway
    // itself clear, so nothing solid stands where the player walks through.
    public const float MeshHalfWidth = 2.2f;
    public const float MeshHalfDepth = 1f;
    public const float MeshHeight = 2.64f;
    public const float DoorwayHalfWidth = 1.32f;
    public const float PostOuterHalfWidth = 2.22f;
    public const float PostHalfDepth = 0.3f;

    // The crown spans the doorway with nothing solid under it, so it needs a
    // sight-line volume of its own: a character walking under the arch is
    // hidden by a lintel that no collider would ever report.
    public static readonly Vector3 OcclusionVolumeCenter = new(0f, 2.15f, 0f);
    public static readonly Vector3 OcclusionVolumeSize = new(3.1f, 1f, 2f);

    public readonly Vector2 Position;
    public readonly float Yaw;

    public DungeonArchway(Vector2 position, float yaw)
    {
        Position = position;
        Yaw = yaw;
    }

    /// <summary>True when the frame spans a boundary running along world X.</summary>
    public bool AlongX => Mathf.Approximately(Yaw, 0f);

    /// <summary>The ground rectangle the frame's mesh occupies.</summary>
    public Rect RenderFootprint => Footprint(MeshHalfWidth, MeshHalfDepth, 0f);

    /// <summary>The ground rectangle of the crown's sight-line volume.</summary>
    public Rect OcclusionFootprint =>
        Footprint(OcclusionVolumeSize.x / 2f, OcclusionVolumeSize.z / 2f, 0f);

    /// <summary>The ground rectangles of the two posts holding the frame up.</summary>
    public Rect PostFootprint(bool second)
    {
        float half = (PostOuterHalfWidth - DoorwayHalfWidth) / 2f;
        float offset = (PostOuterHalfWidth + DoorwayHalfWidth) / 2f;
        return Footprint(half, PostHalfDepth, second ? offset : -offset);
    }

    private Rect Footprint(float halfAlong, float halfAcross, float offsetAlong)
    {
        float halfX = AlongX ? halfAlong : halfAcross;
        float halfZ = AlongX ? halfAcross : halfAlong;
        float x = Position.x + (AlongX ? offsetAlong : 0f);
        float z = Position.y + (AlongX ? 0f : offsetAlong);
        return Rect.MinMaxRect(x - halfX, z - halfZ, x + halfX, z + halfZ);
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
