using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Targeted walks over the transitions that are easiest to get wrong: an
/// inside corner, the wall the player has already passed, the release after
/// they walk away, and a doorway into the next room.
/// </summary>
public sealed class WallVisibilityTransitionTests
{
    /// <summary>
    /// Walking into a corner must not pull the perpendicular wall down with
    /// the one in front of the player. Side walls stay standing.
    /// </summary>
    [Test]
    public void WalkingIntoACornerLeavesThePerpendicularWallStanding()
    {
        Assert.That(
            WallVisibilityFixtures.TryFindClosedSouthWestCorner(out int seed, out Vector2Int room),
            "no seed in the corpus has a room walled shut on both south and west"
        );

        var world = WallVisibilityFixtures.World(seed, room, 1);
        int south = WallVisibilityFixtures.SouthEdgeGroup(world, room);
        int west = WallVisibilityFixtures.GroupNamed(world, $"Edge ({room.x - 1}, {room.y}, V)");
        Assert.That(south, Is.Not.EqualTo(-1));
        Assert.That(west, Is.Not.EqualTo(-1));

        Vector2 center = DungeonLayout.RoomCenter(room);
        float southZ =
            WallVisibilityFixtures.SouthBoundaryZ(room) + WallVisibilityFixtures.WallStandoff;
        float westX = center.x - DungeonLayout.RoomWidth / 2f + WallVisibilityFixtures.WallStandoff;
        WallVisibilitySimulation.Result result = WallVisibilitySimulation.Run(
            world,
            WallVisibilityPaths.Polyline(
                new Vector3(center.x, 0f, center.y),
                new Vector3(westX, 0f, center.y),
                new Vector3(westX, 0f, southZ),
                new Vector3(center.x, 0f, southZ)
            ),
            WallVisibilitySimulation.CameraConfig.Dungeon
        );
        WallVisibilityInvariants.AssertAll(result);

        foreach (WallVisibilitySimulation.Frame frame in result.Frames)
        {
            Assert.That(
                frame.IsLowered(west),
                Is.False,
                WallVisibilityDiagnostics.Report(
                    result,
                    frame.Index,
                    "the side wall lowered along with the wall in front of the player",
                    new[] { south, west }
                )
            );
        }
    }

    /// <summary>
    /// The wall the player has walked past is behind them. Lowering it would
    /// show the room they just left instead of the one they are in.
    /// </summary>
    [Test]
    public void TheWallBehindThePlayerNeverLowers()
    {
        Assert.That(
            WallVisibilityFixtures.TryFindClosedSouthWall(out int seed, out Vector2Int room),
            "no seed in the corpus has a room with an unbroken south wall"
        );

        var world = WallVisibilityFixtures.World(seed, room, 1);
        int north = WallVisibilityFixtures.GroupNamed(world, $"Edge ({room.x}, {room.y}, H)");
        Assert.That(north, Is.Not.EqualTo(-1));

        Vector2 center = DungeonLayout.RoomCenter(room);
        float southZ =
            WallVisibilityFixtures.SouthBoundaryZ(room) + WallVisibilityFixtures.WallStandoff;
        float northZ = southZ + DungeonLayout.RoomDepth - 2f * WallVisibilityFixtures.WallStandoff;
        WallVisibilitySimulation.Result result = WallVisibilitySimulation.Run(
            world,
            WallVisibilityPaths.Polyline(
                new Vector3(center.x, 0f, southZ),
                new Vector3(center.x, 0f, northZ)
            ),
            WallVisibilitySimulation.CameraConfig.Dungeon
        );
        WallVisibilityInvariants.AssertAll(result);

        foreach (WallVisibilitySimulation.Frame frame in result.Frames)
        {
            Assert.That(
                frame.IsLowered(north),
                Is.False,
                WallVisibilityDiagnostics.Report(
                    result,
                    frame.Index,
                    "the wall beyond the player lowered, revealing the room ahead of them",
                    new[] { north }
                )
            );
        }
    }

    /// <summary>
    /// A wall stays down for the configured release once the player stops
    /// needing it out of the way, then restores - it does not snap back the
    /// instant the last sight line clears.
    /// </summary>
    [Test]
    public void LeavingAWallRestoresItOnlyAfterTheReleaseDelay()
    {
        Assert.That(
            WallVisibilityFixtures.TryFindClosedSouthWall(out int seed, out Vector2Int room),
            "no seed in the corpus has a room with an unbroken south wall"
        );

        var world = WallVisibilityFixtures.World(seed, room, 1);
        int south = WallVisibilityFixtures.SouthEdgeGroup(world, room);
        Vector2 center = DungeonLayout.RoomCenter(room);
        float southZ =
            WallVisibilityFixtures.SouthBoundaryZ(room) + WallVisibilityFixtures.WallStandoff;
        WallVisibilitySimulation.Result result = WallVisibilitySimulation.Run(
            world,
            WallVisibilityPaths.Concat(
                WallVisibilityPaths.Polyline(
                    new Vector3(center.x, 0f, southZ),
                    new Vector3(center.x, 0f, southZ + DungeonLayout.RoomDepth - 3f)
                ),
                WallVisibilityPaths.Hold(
                    new Vector3(center.x, 0f, southZ + DungeonLayout.RoomDepth - 3f),
                    1.5f
                )
            ),
            WallVisibilitySimulation.CameraConfig.Dungeon
        );
        WallVisibilityInvariants.AssertAll(result);

        int lastSelected = -1;
        int restored = -1;
        for (int index = 0; index < result.Frames.Count; index++)
        {
            if (result.Frames[index].Activated.Contains(south))
                lastSelected = index;
            else if (lastSelected >= 0 && restored < 0 && !result.Frames[index].IsLowered(south))
                restored = index;
        }

        Assert.That(lastSelected, Is.GreaterThanOrEqualTo(0), "the wall was never selected");
        Assert.That(
            restored,
            Is.GreaterThan(lastSelected),
            WallVisibilityDiagnostics.Report(
                result,
                result.Frames.Count - 1,
                "the wall never stood back up after the player walked away from it",
                new[] { south }
            )
        );
        float held = result.Frames[restored].Time - result.Frames[lastSelected].Time;
        Assert.That(
            held,
            Is.GreaterThanOrEqualTo(
                WallVisibilityInvariants.ReleaseDelay - WallVisibilitySimulation.FrameStep
            ),
            WallVisibilityDiagnostics.Report(
                result,
                restored,
                $"the wall snapped back up {held:0.000}s after the last sight line cleared, "
                    + $"inside the {WallVisibilityInvariants.ReleaseDelay:0.00}s release",
                new[] { south }
            )
        );
    }

    /// <summary>Crossing a doorway into the next room stays stable.</summary>
    [Test]
    public void CrossingADoorwayIntoTheNextRoomIsStable()
    {
        Assert.That(
            WallVisibilityFixtures.TryFindSouthDoorway(out int seed, out Vector2Int room),
            "no seed in the corpus has a room with a doorway in its south wall"
        );

        var layout = new DungeonLayout(seed);
        Assert.That(WallVisibilityFixtures.TryGetSouthDoorwayX(layout, room, out float doorX));

        var world = WallVisibilityFixtures.World(seed, room, 2);
        float southZ = WallVisibilityFixtures.SouthBoundaryZ(room);
        WallVisibilitySimulation.Result result = WallVisibilitySimulation.Run(
            world,
            WallVisibilityPaths.Polyline(
                new Vector3(doorX, 0f, southZ + 8f),
                new Vector3(doorX, 0f, southZ - 8f)
            ),
            WallVisibilitySimulation.CameraConfig.Dungeon
        );
        WallVisibilityInvariants.AssertAll(result);
    }

    /// <summary>
    /// An arch is part of the wall it stands in. Walking through one, the frame
    /// and the run around it have to reach their new state on the same frame:
    /// the arch dropping a moment ahead of the wall reads as two separate
    /// things happening where the player sees one wall.
    /// </summary>
    [Test]
    public void AnArchwayLowersOnTheSameFrameAsTheRunItStandsIn()
    {
        Assert.That(
            WallVisibilityFixtures.TryFindSouthArchway(
                out int seed,
                out Vector2Int room,
                out float doorX
            ),
            "no seed in the corpus frames a south doorway with an arch"
        );

        var world = WallVisibilityFixtures.World(seed, room, 1);
        int group = WallVisibilityFixtures.SouthEdgeGroup(world, room);
        Assert.That(group, Is.Not.EqualTo(-1));
        (int archway, int wall) = ArchAndWallOf(world, group);

        float southZ = WallVisibilityFixtures.SouthBoundaryZ(room);
        WallVisibilitySimulation.Result result = WallVisibilitySimulation.Run(
            world,
            WallVisibilityPaths.Polyline(
                new Vector3(doorX, 0f, southZ - 6f),
                new Vector3(doorX, 0f, southZ + 6f)
            ),
            WallVisibilitySimulation.CameraConfig.Dungeon
        );
        WallVisibilityInvariants.AssertAll(result);

        bool sawLowered = false;
        foreach (WallVisibilitySimulation.Frame frame in result.Frames)
        {
            bool archLowered = frame.LoweredPieces.Contains(archway);
            bool wallLowered = frame.LoweredPieces.Contains(wall);
            sawLowered |= archLowered;
            Assert.That(
                archLowered,
                Is.EqualTo(wallLowered),
                WallVisibilityDiagnostics.Report(
                    result,
                    frame.Index,
                    $"the archway is {(archLowered ? "down" : "up")} while the wall run it "
                        + $"stands in is {(wallLowered ? "down" : "up")}",
                    new[] { group }
                )
            );
        }

        Assert.That(sawLowered, "the walk never lowered the archway, so it tested nothing");
    }

    /// <summary>The archway piece of a run, and one wall piece beside it.</summary>
    private static (int Archway, int Wall) ArchAndWallOf(WallVisibilityWorld world, int group)
    {
        int archway = -1;
        int wall = -1;
        foreach (int pieceId in world.GroupOf(group).Pieces)
        {
            if (world.PieceOf(pieceId).IsGateway)
                archway = pieceId;
            else
                wall = pieceId;
        }

        Assert.That(archway, Is.Not.EqualTo(-1), "the run has no archway");
        Assert.That(wall, Is.Not.EqualTo(-1), "the run has no wall pieces");
        return (archway, wall);
    }
}
