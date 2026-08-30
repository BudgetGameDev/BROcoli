using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The invariants every simulated walk has to satisfy, whatever path it took.
/// Each one is checked against values re-derived in the test rather than by
/// asking the production predicate the same question twice.
/// </summary>
internal static class WallVisibilityInvariants
{
    /// <summary>Ordinary release, in seconds, before a wall may stand back up.</summary>
    public const float ReleaseDelay = 0.2f;

    /// <summary>How long a jittering group is pinned lowered.</summary>
    public const float StabilityHold = 0.65f;

    private const float DepthTolerance = 0.01f;

    public static void AssertAll(WallVisibilitySimulation.Result result)
    {
        AssertNothingBehindTheTargetsIsLowered(result);
        AssertLoweredPiecesAgreeWithTheirGroup(result);
        AssertLoweredGroupsWereAskedFor(result);
        AssertLoweredPiecesStandInTheGap(result);
        AssertHeldPiecesWereRecentlyInTheGap(result);
        AssertNoStrobing(result);
    }

    /// <summary>
    /// A wall the player has already walked past must not drop. Lowering it
    /// reveals what is beyond it and hides nothing the player needed to see.
    /// A piece straddling the player still counts as in the way - part of it
    /// is - so what this forbids is a piece lying wholly beyond them. Judged on
    /// the raw geometric answer; a piece held through a release is bounded by
    /// <see cref="AssertHeldPiecesWereRecentlyInTheGap"/> instead.
    /// </summary>
    public static void AssertNothingBehindTheTargetsIsLowered(
        WallVisibilitySimulation.Result result
    )
    {
        foreach (WallVisibilitySimulation.Frame frame in result.Frames)
        foreach (int pieceId in frame.GapPieces)
        {
            float depth = FrontDepth(result, frame, pieceId);
            Assert.That(
                depth,
                Is.LessThanOrEqualTo(frame.DeepestTargetDepth + DepthTolerance),
                WallVisibilityDiagnostics.Report(
                    result,
                    frame.Index,
                    $"piece {pieceId} is lowered although its nearest solid corner stands "
                        + $"{depth:0.00} deep, past the deepest target at "
                        + $"{frame.DeepestTargetDepth:0.00}"
                )
            );
        }
    }

    /// <summary>
    /// Pieces of one group never transition independently: every piece of a
    /// lowered group that is standing in the way is lowered with it.
    /// </summary>
    public static void AssertLoweredPiecesAgreeWithTheirGroup(
        WallVisibilitySimulation.Result result
    )
    {
        foreach (WallVisibilitySimulation.Frame frame in result.Frames)
        foreach (WallVisibilityWorld.Group group in result.World.Groups)
        {
            bool lowered = frame.IsLowered(group.Id);
            foreach (int pieceId in group.Pieces)
            {
                bool inTheWay =
                    FrontDepth(result, frame, pieceId) <= frame.DeepestTargetDepth - DepthTolerance
                    && RearDepth(result, frame, pieceId) >= DepthTolerance;
                bool pieceLowered = frame.LoweredPieces.Contains(pieceId);
                if (lowered && inTheWay)
                    Assert.That(
                        pieceLowered,
                        WallVisibilityDiagnostics.Report(
                            result,
                            frame.Index,
                            $"piece {pieceId} stands in the way but did not lower with its group",
                            new[] { group.Id }
                        )
                    );
                if (!lowered)
                    Assert.That(
                        pieceLowered,
                        Is.False,
                        WallVisibilityDiagnostics.Report(
                            result,
                            frame.Index,
                            $"piece {pieceId} lowered while its group stands",
                            new[] { group.Id }
                        )
                    );
            }
        }
    }

    /// <summary>
    /// Nothing lowers on its own account, and nothing lowers on a neighbour's:
    /// a lowered group was selected itself, inside the hysteresis window. This
    /// is what keeps a fade from running on into the next room's wall.
    /// </summary>
    public static void AssertLoweredGroupsWereAskedFor(WallVisibilitySimulation.Result result)
    {
        int window = Mathf.CeilToInt(
            (ReleaseDelay + StabilityHold) / WallVisibilitySimulation.FrameStep
        );
        for (int index = 0; index < result.Frames.Count; index++)
        {
            WallVisibilitySimulation.Frame frame = result.Frames[index];
            foreach (int groupId in frame.LoweredGroups)
            {
                Assert.That(
                    WasAskedFor(result, index, window, groupId),
                    WallVisibilityDiagnostics.Report(
                        result,
                        index,
                        $"group {groupId} is lowered although it was not selected "
                            + "inside the hysteresis window",
                        new[] { groupId }
                    )
                );
            }
        }
    }

    /// <summary>
    /// A faded wall reaches into the gap between the camera and someone who
    /// has to stay visible. Never wholly behind the camera, where it hides
    /// nothing, and never further from the target than the camera itself is.
    /// Judged on the raw geometric answer, as above.
    /// </summary>
    public static void AssertLoweredPiecesStandInTheGap(WallVisibilitySimulation.Result result)
    {
        float reach = result.Camera.Offset.magnitude + DungeonLayout.TileSize;
        foreach (WallVisibilitySimulation.Frame frame in result.Frames)
        foreach (int pieceId in frame.GapPieces)
        {
            Assert.That(
                RearDepth(result, frame, pieceId),
                Is.GreaterThanOrEqualTo(-DepthTolerance),
                WallVisibilityDiagnostics.Report(
                    result,
                    frame.Index,
                    $"piece {pieceId} faded although it stands behind the camera, where it "
                        + "cannot be hiding anything"
                )
            );

            float behind = frame.DeepestTargetDepth - RearDepth(result, frame, pieceId);
            Assert.That(
                behind,
                Is.LessThanOrEqualTo(reach),
                WallVisibilityDiagnostics.Report(
                    result,
                    frame.Index,
                    $"piece {pieceId} faded although it is {behind:0.00} in front of the "
                        + $"deepest target, beyond the camera's own reach of {reach:0.00}"
                )
            );
        }
    }

    /// <summary>
    /// The per-piece hysteresis fades a piece the frame it enters the gap, and
    /// may hold it through a release - but only briefly. Every gap piece is
    /// lowered, and every lowered piece stood in the gap inside the window the
    /// release delay plus the stability hold guarantee.
    /// </summary>
    public static void AssertHeldPiecesWereRecentlyInTheGap(
        WallVisibilitySimulation.Result result
    )
    {
        int window = Mathf.CeilToInt(
            (ReleaseDelay + StabilityHold) / WallVisibilitySimulation.FrameStep
        );
        for (int index = 0; index < result.Frames.Count; index++)
        {
            WallVisibilitySimulation.Frame frame = result.Frames[index];
            foreach (int pieceId in frame.GapPieces)
                Assert.That(
                    frame.LoweredPieces.Contains(pieceId),
                    WallVisibilityDiagnostics.Report(
                        result,
                        index,
                        $"piece {pieceId} stands in the gap but its fade was delayed"
                    )
                );

            foreach (int pieceId in frame.LoweredPieces)
            {
                if (frame.GapPieces.Contains(pieceId))
                    continue;
                Assert.That(
                    WasInTheGap(result, index, window, pieceId),
                    WallVisibilityDiagnostics.Report(
                        result,
                        index,
                        $"piece {pieceId} is held lowered although it has not stood in the "
                            + "gap inside the hysteresis window"
                    )
                );
            }
        }
    }

    /// <summary>
    /// The anti-popping rule. No lowered episode is shorter than the configured
    /// release, and no group can start three episodes inside the interval the
    /// release plus the stability hold guarantee.
    /// </summary>
    public static void AssertNoStrobing(WallVisibilitySimulation.Result result)
    {
        float floor = ReleaseDelay - WallVisibilitySimulation.FrameStep;
        foreach (int groupId in result.TouchedGroups)
        {
            List<WallVisibilityEpisode> episodes = WallVisibilityEpisode.Of(result, groupId);
            for (int i = 0; i < episodes.Count; i++)
            {
                WallVisibilityEpisode episode = episodes[i];
                if (episode.EndFrame < result.Frames.Count - 1)
                    Assert.That(
                        episode.Duration,
                        Is.GreaterThanOrEqualTo(floor),
                        WallVisibilityDiagnostics.Report(
                            result,
                            episode.StartFrame,
                            $"group {groupId} flashed for only {episode.Duration:0.000}s",
                            new[] { groupId }
                        )
                    );

                if (i < 2)
                    continue;
                float span = episode.Start - episodes[i - 2].Start;
                Assert.That(
                    span,
                    Is.GreaterThanOrEqualTo(ReleaseDelay + StabilityHold - 0.05f),
                    WallVisibilityDiagnostics.Report(
                        result,
                        episode.StartFrame,
                        $"group {groupId} alternated three times in {span:0.000}s",
                        new[] { groupId }
                    )
                );
            }
        }
    }

    private static bool WasInTheGap(
        WallVisibilitySimulation.Result result,
        int index,
        int window,
        int pieceId
    )
    {
        for (int i = Mathf.Max(0, index - window); i <= index; i++)
        {
            if (result.Frames[i].GapPieces.Contains(pieceId))
                return true;
        }
        return false;
    }

    private static bool WasAskedFor(
        WallVisibilitySimulation.Result result,
        int index,
        int window,
        int groupId
    )
    {
        for (int i = Mathf.Max(0, index - window); i <= index; i++)
        {
            WallVisibilitySimulation.Frame frame = result.Frames[i];
            if (frame.Activated.Contains(groupId))
                return true;
        }
        return false;
    }

    /// <summary>How deep the nearest solid corner of a piece stands.</summary>
    private static float FrontDepth(
        WallVisibilitySimulation.Result result,
        WallVisibilitySimulation.Frame frame,
        int pieceId
    )
    {
        return Depth(result, frame, pieceId, nearest: true);
    }

    /// <summary>How deep the rear-most solid corner of a piece stands.</summary>
    private static float RearDepth(
        WallVisibilitySimulation.Result result,
        WallVisibilitySimulation.Frame frame,
        int pieceId
    )
    {
        return Depth(result, frame, pieceId, nearest: false);
    }

    private static float Depth(
        WallVisibilitySimulation.Result result,
        WallVisibilitySimulation.Frame frame,
        int pieceId,
        bool nearest
    )
    {
        Bounds bounds = result.World.PieceOf(pieceId).Structure;
        float depth = nearest ? float.PositiveInfinity : float.NegativeInfinity;
        for (int x = 0; x <= 1; x++)
        for (int z = 0; z <= 1; z++)
        {
            var corner = new Vector3(
                x == 0 ? bounds.min.x : bounds.max.x,
                0f,
                z == 0 ? bounds.min.z : bounds.max.z
            );
            float cornerDepth = Vector3.Dot(corner - frame.CameraPosition, frame.GroundForward);
            depth = nearest ? Mathf.Min(depth, cornerDepth) : Mathf.Max(depth, cornerDepth);
        }
        return depth;
    }
}
