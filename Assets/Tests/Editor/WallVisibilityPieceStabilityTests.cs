using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The per-piece participation answer has to be as stable as the group
/// decision it refines. A target flickering on the edge of detection - an
/// enemy crossing the reveal radius, a walk cycle bobbing over the depth
/// boundary - toggles which pieces stand in the gap every frame; the settled
/// answer must not strobe with it, must not delay the initial fade, and must
/// still stand the piece back up once the flicker is really over.
/// </summary>
public sealed class WallVisibilityPieceStabilityTests
{
    private const int GroupId = 1;
    private const int FrontPieceId = 11;
    private const int DeepPieceId = 12;

    /// <summary>Between the camera and the player: always in the gap.</summary>
    private static readonly Bounds FrontWall = new(
        new Vector3(0f, 1.25f, -1.5f),
        new Vector3(8f, 2.5f, 0.4f)
    );

    /// <summary>
    /// Beyond the player but short of the enemy: in the gap exactly while the
    /// enemy target exists, so a flickering enemy toggles its raw answer.
    /// </summary>
    private static readonly Bounds DeepWall = new(
        new Vector3(0f, 1.25f, 2.5f),
        new Vector3(8f, 2.5f, 0.4f)
    );

    private static readonly Vector3 Player = Vector3.zero;
    private static readonly Vector3 Enemy = new(0f, 0f, 4.5f);

    /// <summary>One run of two pieces; the selector filters what it returns.</summary>
    private sealed class RunSource : IOcclusionCandidateSource
    {
        public void Collect(Ray ray, float maximumDistance, List<OcclusionCandidate> results)
        {
            results.Add(new OcclusionCandidate(GroupId, FrontWall));
            results.Add(new OcclusionCandidate(GroupId, DeepWall));
        }

        public void CollectEnclosing(Vector3 targetPosition, List<OcclusionCandidate> results) { }
    }

    [Test]
    public void APieceOnTheEdgeOfTheGapDoesNotStrobeWithAFlickeringTarget()
    {
        var resolver = new WallVisibilityResolver();
        var source = new RunSource();
        int frame = 0;

        // The player alone: the group lowers and the deep piece stays out.
        (bool deep, bool front) = Step(resolver, source, frame++, withEnemy: false);
        Assert.That(resolver.IsLowered(GroupId), "the front wall did not lower the group");
        Assert.That(front, "the piece in front of the player did not fade");
        Assert.That(deep, Is.False, "the piece beyond the player faded without a deep target");

        // The enemy appears: the deep piece joins the fade on the same frame.
        (deep, _) = Step(resolver, source, frame++, withEnemy: true);
        Assert.That(deep, "the deep piece did not fade the frame its target appeared");

        // The enemy flickers on the edge of detection for two seconds. The raw
        // answer toggles every frame; the settled answer must not.
        for (int i = 0; i < 60; i++)
        {
            (deep, front) = Step(resolver, source, frame++, withEnemy: i % 2 == 0);
            Assert.That(deep, $"the deep piece strobed on flicker frame {i}");
            Assert.That(front, $"the front piece strobed on flicker frame {i}");
        }

        // The enemy is really gone: the deep piece stands back up once the
        // release delay and the stability hold run out, and stays up.
        int recoveredAt = -1;
        for (int i = 0; i < 60; i++)
        {
            (deep, _) = Step(resolver, source, frame++, withEnemy: false);
            if (!deep)
            {
                recoveredAt = i;
                break;
            }
        }
        Assert.That(recoveredAt, Is.GreaterThanOrEqualTo(0), "the deep piece never stood back up");
        for (int i = 0; i < 30; i++)
        {
            (deep, front) = Step(resolver, source, frame++, withEnemy: false);
            Assert.That(deep, Is.False, "the deep piece fell again with no target behind it");
            Assert.That(front, "the front piece stood up although it still hides the player");
        }
    }

    /// <summary>
    /// One simulated frame: resolve with the player - and optionally the enemy
    /// - as targets, then ask what each piece is doing, the way the fader asks
    /// for the pieces of a lowered group.
    /// </summary>
    private static (bool Deep, bool Front) Step(
        WallVisibilityResolver resolver,
        RunSource source,
        int frame,
        bool withEnemy
    )
    {
        float time = frame * WallVisibilitySimulation.FrameStep;
        OcclusionCameraModel camera = WallVisibilitySimulation.CameraConfig.Dungeon.At(Player);
        resolver.BeginFrame();
        Assert.That(
            OcclusionTarget.TryCreate(
                camera,
                OcclusionTargetKind.Player,
                Player,
                WallVisibilitySimulation.BodyBounds(
                    Player,
                    WallVisibilitySimulation.PlayerWidth,
                    WallVisibilitySimulation.PlayerHeight
                ),
                0.5f,
                out OcclusionTarget playerTarget
            ),
            "the player projects on screen"
        );
        resolver.AddTarget(playerTarget);

        if (withEnemy)
        {
            Assert.That(
                OcclusionTarget.TryCreate(
                    camera,
                    OcclusionTargetKind.Enemy,
                    Enemy,
                    WallVisibilitySimulation.BodyBounds(
                        Enemy,
                        WallVisibilitySimulation.EnemyWidth,
                        WallVisibilitySimulation.EnemyHeight
                    ),
                    0.05f,
                    out OcclusionTarget enemyTarget
                ),
                "the enemy projects on screen"
            );
            resolver.AddTarget(enemyTarget);
        }

        resolver.Resolve(camera, source, time);
        return (
            resolver.IsPieceInTheWay(DeepPieceId, DeepWall),
            resolver.IsPieceInTheWay(FrontPieceId, FrontWall)
        );
    }
}
