using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Finds the situations the targeted property tests need - an unbroken wall to
    /// walk beside, a doorway to cross, a room with a freestanding structure in it
    /// - by searching the generated dungeon rather than hard-coding coordinates
    /// that a layout change would silently invalidate.
    /// </summary>
    internal static class WallVisibilityFixtures
    {
        /// <summary>
        /// How far inside the room the player walks when following a wall. Close
        /// enough that the wall covers them; clear of the slab and its collider.
        /// </summary>
        public const float WallStandoff = 1.2f;

        /// <summary>Rooms the fixture search sweeps, for one seed.</summary>
        public static IEnumerable<Vector2Int> SweepRooms()
        {
            for (int x = -2; x <= 2; x++)
            for (int y = -2; y <= 2; y++)
                yield return new Vector2Int(x, y);
        }

        public static WallVisibilityWorld World(int seed, Vector2Int center, int radius = 1)
        {
            return new WallVisibilityWorld(new DungeonGeometryModel(seed, center, radius));
        }

        /// <summary>The world-space Z of a room's southern boundary line.</summary>
        public static float SouthBoundaryZ(Vector2Int room)
        {
            return DungeonLayout.RoomCenter(room).y - DungeonLayout.RoomDepth / 2f;
        }

        /// <summary>
        /// The group of the run on a room's south boundary - the side the camera
        /// looks over, so the only side that can stand between it and the player.
        /// </summary>
        public static int SouthEdgeGroup(WallVisibilityWorld world, Vector2Int room)
        {
            return GroupNamed(world, $"Edge ({room.x}, {room.y - 1}, H)");
        }

        public static int GroupNamed(WallVisibilityWorld world, string name)
        {
            foreach (WallVisibilityWorld.Group group in world.Groups)
            {
                if (group.Name == name)
                    return group.Id;
            }
            return -1;
        }

        /// <summary>The first seed and room whose south boundary has no opening.</summary>
        public static bool TryFindClosedSouthWall(out int seed, out Vector2Int room)
        {
            return TryFindRoom(IsSouthWallClosed, out seed, out room);
        }

        /// <summary>
        /// A room whose south wall continues unbroken into its eastern neighbour's,
        /// so a walk along it crosses the seam between two built runs.
        /// </summary>
        public static bool TryFindClosedSouthWallRun(out int seed, out Vector2Int room)
        {
            return TryFindRoom(
                (layout, candidate) =>
                    IsSouthWallClosed(layout, candidate)
                    && IsSouthWallClosed(layout, candidate + Vector2Int.right),
                out seed,
                out room
            );
        }

        /// <summary>A room walled shut on both its south and west boundaries.</summary>
        public static bool TryFindClosedSouthWestCorner(out int seed, out Vector2Int room)
        {
            return TryFindRoom(
                (layout, candidate) =>
                    IsSouthWallClosed(layout, candidate)
                    && Passage(layout, candidate, DungeonLayout.West).OpeningCount == 0,
                out seed,
                out room
            );
        }

        /// <summary>The world X of the first doorway in a room's south boundary.</summary>
        public static bool TryGetSouthDoorwayX(DungeonLayout layout, Vector2Int room, out float x)
        {
            DungeonPassage passage = Passage(layout, room, DungeonLayout.South);
            for (int slot = 0; slot < DungeonLayout.RoomTilesX; slot++)
            {
                if (!passage.HasOpening(slot))
                    continue;
                x =
                    DungeonLayout.RoomCenter(room).x
                    + DungeonPassage.SlotOffset(slot, DungeonLayout.RoomTilesX);
                return true;
            }

            x = 0f;
            return false;
        }

        /// <summary>
        /// A room with an interior run across the player's view, and the piece of
        /// it to stand behind. An interior wall is the only place a character can
        /// be hidden from the camera without the player being hidden too: the
        /// player stands in front of it and whoever is behind it is not.
        /// </summary>
        public static bool TryFindInteriorScreen(
            out int seed,
            out Vector2Int room,
            out Vector2 anchor
        )
        {
            Vector2 found = Vector2.zero;
            bool located = TryFindRoom(
                (layout, candidate) =>
                {
                    var walls = new List<DungeonWallPiece>();
                    DungeonRoomGeometry.AppendInteriorWalls(
                        walls,
                        candidate,
                        layout.Archetype(candidate)
                    );
                    Vector2 center = DungeonLayout.RoomCenter(candidate);
                    foreach (DungeonWallPiece piece in walls)
                    {
                        if (
                            !piece.AlongX
                            || piece.Anchor.y <= center.y
                            || Mathf.Abs(piece.Anchor.x - center.x) < 0.1f
                        )
                            continue;
                        found = piece.Anchor;
                        return true;
                    }
                    return false;
                },
                out seed,
                out room
            );
            anchor = found;
            return located;
        }

        /// <summary>The first seed and room whose south boundary is framed by an arch.</summary>
        public static bool TryFindSouthArchway(out int seed, out Vector2Int room, out float x)
        {
            float found = 0f;
            bool located = TryFindRoom(
                (layout, candidate) =>
                {
                    DungeonPassage passage = Passage(layout, candidate, DungeonLayout.South);
                    for (int slot = 0; slot < DungeonLayout.RoomTilesX; slot++)
                    {
                        if (!passage.HasArchway(slot))
                            continue;
                        found =
                            DungeonLayout.RoomCenter(candidate).x
                            + DungeonPassage.SlotOffset(slot, DungeonLayout.RoomTilesX);
                        return true;
                    }
                    return false;
                },
                out seed,
                out room
            );
            x = found;
            return located;
        }

        public static bool IsSouthWallClosed(DungeonLayout layout, Vector2Int room)
        {
            return Passage(layout, room, DungeonLayout.South).OpeningCount == 0;
        }

        /// <summary>The first seed and room whose south boundary has a doorway.</summary>
        public static bool TryFindSouthDoorway(out int seed, out Vector2Int room)
        {
            return TryFindRoom(
                (layout, candidate) =>
                    Passage(layout, candidate, DungeonLayout.South).OpeningCount > 0,
                out seed,
                out room
            );
        }

        /// <summary>The first seed and room built with a freestanding interior.</summary>
        public static bool TryFindInteriorStructure(out int seed, out Vector2Int room)
        {
            return TryFindRoom(
                (layout, candidate) =>
                {
                    var walls = new List<DungeonWallPiece>();
                    DungeonRoomGeometry.AppendInteriorWalls(
                        walls,
                        candidate,
                        layout.Archetype(candidate)
                    );
                    return HasCrossing(walls);
                },
                out seed,
                out room
            );
        }

        /// <summary>True when two interior runs meet, forming a cross or a T.</summary>
        public static bool HasCrossing(IReadOnlyList<DungeonWallPiece> walls)
        {
            for (int i = 0; i < walls.Count; i++)
            for (int j = i + 1; j < walls.Count; j++)
            {
                if (
                    walls[i].Section != walls[j].Section
                    && walls[i].AlongX != walls[j].AlongX
                    && DungeonWallGrouping.AreInContact(walls[i], walls[j])
                )
                    return true;
            }
            return false;
        }

        public static DungeonPassage Passage(DungeonLayout layout, Vector2Int room, int direction)
        {
            DungeonEdge edge = DungeonLayout.EdgeBetween(room, direction);
            return layout.Passage(edge, layout.IsDoorOpen(room, direction));
        }

        private static bool TryFindRoom(
            System.Func<DungeonLayout, Vector2Int, bool> predicate,
            out int seed,
            out Vector2Int room
        )
        {
            foreach (int candidateSeed in DungeonGeometryModel.Seeds)
            {
                var layout = new DungeonLayout(candidateSeed);
                foreach (Vector2Int candidate in SweepRooms())
                {
                    if (!predicate(layout, candidate))
                        continue;
                    seed = candidateSeed;
                    room = candidate;
                    return true;
                }
            }

            seed = 0;
            room = Vector2Int.zero;
            return false;
        }
    }
}
