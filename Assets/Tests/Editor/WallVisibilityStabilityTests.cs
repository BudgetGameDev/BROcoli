using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The properties that have to hold on any path at all: randomized walks
/// through generated rooms, and the two forms of determinism the decision
/// depends on - the same inputs give the same answer, and the order the world
/// hands back query results does not matter.
/// </summary>
public sealed class WallVisibilityStabilityTests
{
    private const int RandomWalkFrames = 260;

    /// <summary>Seeds crossed with random walks, so failures reproduce.</summary>
    private static IEnumerable<(int Seed, Vector2Int Center, int Walk)> Sweep()
    {
        foreach (int seed in DungeonGeometryModel.Seeds)
        foreach (int walk in new[] { 1, 2 })
            yield return (seed, Vector2Int.zero, walk);
    }

    /// <summary>
    /// Every invariant, on paths nobody chose: nothing lowers behind the
    /// player, pieces of a group move together, nothing lowers that was not
    /// asked for, and no group strobes.
    /// </summary>
    [Test]
    public void RandomWalksHoldEveryInvariant()
    {
        foreach ((int seed, Vector2Int center, int walk) in Sweep())
        {
            WallVisibilitySimulation.Result result = RandomRun(seed, center, walk);
            WallVisibilityInvariants.AssertAll(result);
        }
    }

    /// <summary>
    /// The same seed, path, camera, and previous state must produce the same
    /// transitions every time, or a failure cannot be reproduced from a report.
    /// </summary>
    [Test]
    public void TransitionsAreDeterministic()
    {
        foreach ((int seed, Vector2Int center, int walk) in Sweep())
        {
            WallVisibilitySimulation.Result first = RandomRun(seed, center, walk);
            WallVisibilitySimulation.Result second = RandomRun(seed, center, walk);
            AssertSameStates(first, second, "a repeated run");
        }
    }

    /// <summary>
    /// Which logical group is selected must not depend on the order colliders
    /// come back in, which physics gives no guarantee about.
    /// </summary>
    [Test]
    public void QueryOrderDoesNotChangeTheSelection()
    {
        foreach ((int seed, Vector2Int center, int walk) in Sweep())
        {
            var world = WallVisibilityFixtures.World(seed, center, 1);
            List<Vector3> path = WallVisibilityPaths.RandomWalk(world, walk, RandomWalkFrames);
            WallVisibilitySimulation.Result ordered = WallVisibilitySimulation.Run(
                world,
                path,
                WallVisibilitySimulation.CameraConfig.Dungeon
            );

            for (int permutation = 1; permutation <= 2; permutation++)
            {
                world.ShuffleQueryOrder(seed ^ (permutation * 7919));
                WallVisibilitySimulation.Result shuffled = WallVisibilitySimulation.Run(
                    world,
                    path,
                    WallVisibilitySimulation.CameraConfig.Dungeon
                );
                AssertSameStates(ordered, shuffled, $"query permutation {permutation}");
            }
            world.ResetOrder();
        }
    }

    /// <summary>
    /// A camera the player never moves under still has to settle: standing
    /// still may not produce any transition at all after the first one.
    /// </summary>
    [Test]
    public void StandingStillNeverTransitionsTwice()
    {
        Assert.That(
            WallVisibilityFixtures.TryFindClosedSouthWall(out int seed, out Vector2Int room),
            "no seed in the corpus has a room with an unbroken south wall"
        );

        var world = WallVisibilityFixtures.World(seed, room, 1);
        Vector2 center = DungeonLayout.RoomCenter(room);
        var position = new Vector3(
            center.x,
            0f,
            WallVisibilityFixtures.SouthBoundaryZ(room) + WallVisibilityFixtures.WallStandoff
        );
        WallVisibilitySimulation.Result result = WallVisibilitySimulation.Run(
            world,
            WallVisibilityPaths.Hold(position, 4f),
            WallVisibilitySimulation.CameraConfig.Dungeon
        );
        WallVisibilityInvariants.AssertAll(result);

        foreach (int groupId in result.TouchedGroups)
        {
            Assert.That(
                WallVisibilityEpisode.Of(result, groupId).Count,
                Is.EqualTo(1),
                WallVisibilityDiagnostics.Report(
                    result,
                    result.Frames.Count - 1,
                    $"group {groupId} changed state more than once while the player stood still",
                    new[] { groupId }
                )
            );
        }
    }

    private static WallVisibilitySimulation.Result RandomRun(int seed, Vector2Int center, int walk)
    {
        var world = WallVisibilityFixtures.World(seed, center, 1);
        return WallVisibilitySimulation.Run(
            world,
            WallVisibilityPaths.RandomWalk(world, walk, RandomWalkFrames),
            WallVisibilitySimulation.CameraConfig.Dungeon
        );
    }

    private static void AssertSameStates(
        WallVisibilitySimulation.Result expected,
        WallVisibilitySimulation.Result actual,
        string what
    )
    {
        Assert.That(actual.Frames.Count, Is.EqualTo(expected.Frames.Count));
        for (int index = 0; index < expected.Frames.Count; index++)
        {
            CollectionAssert.AreEqual(
                expected.Frames[index].LoweredGroups,
                actual.Frames[index].LoweredGroups,
                WallVisibilityDiagnostics.Report(
                    expected,
                    index,
                    $"{what} produced a different set of lowered wall groups; "
                        + $"it lowered {Describe(actual, index)}"
                )
            );
            CollectionAssert.AreEquivalent(
                expected.Frames[index].LoweredPieces,
                actual.Frames[index].LoweredPieces,
                WallVisibilityDiagnostics.Report(
                    expected,
                    index,
                    $"{what} faded a different set of wall pieces"
                )
            );
        }
    }

    private static string Describe(WallVisibilitySimulation.Result result, int index)
    {
        return string.Join(", ", result.Frames[index].LoweredGroups);
    }
}
