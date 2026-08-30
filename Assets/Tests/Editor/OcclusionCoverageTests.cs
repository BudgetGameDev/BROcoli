using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// What it takes for something to count as hiding the player.
///
/// The rule is not "these two screen rectangles overlap a lot" - that answers a
/// different question and gets the two interesting cases backwards. A crate
/// broad enough to fill the player's rectangle sideways still leaves their head
/// in plain view; a post tall enough to cover them top to bottom still leaves
/// their shoulders showing on either side. Only something both tall enough and
/// wide enough to stand across the body actually hides it.
///
/// So coverage is measured by tracing sight lines to a grid of points on the
/// character's own body and counting how many an occluder stands in front of.
/// The occluder's height and width, and the character's, then decide the answer
/// on their own, with nothing to tune per prop.
/// </summary>
public sealed class OcclusionCoverageTests
{
    // The Broccoli player and the gameplay camera as the Dungeon scene builds
    // them. The camera looks down steeply, which is exactly why height matters
    // so much here: sight lines to the player clear a low obstacle easily.
    private const float PlayerWidth = 1.36f;
    private const float PlayerHeight = 1f;
    private static readonly Vector3 CameraOffset = new(0f, 10.5f, -11.7f);

    /// <summary>How far in front of the player the obstacles are stood.</summary>
    private const float Standoff = 0.8f;

    private const int Group = 1;

    /// <summary>Boxes a sight line can be stopped by, and nothing else.</summary>
    private sealed class BoxWorld : IOcclusionCandidateSource
    {
        private readonly List<OcclusionCandidate> boxes = new();

        public void Add(Bounds bounds)
        {
            boxes.Add(new OcclusionCandidate(Group, bounds));
        }

        public void Collect(Ray ray, float maximumDistance, List<OcclusionCandidate> results)
        {
            foreach (OcclusionCandidate box in boxes)
            {
                if (box.Bounds.IntersectRay(ray, out float distance) && distance <= maximumDistance)
                    results.Add(box);
            }
        }

        public void CollectEnclosing(Vector3 targetPosition, List<OcclusionCandidate> results) { }
    }

    /// <summary>
    /// The case a rectangle comparison gets most wrong. A crate wider than the
    /// player covers their whole screen rectangle sideways, but the camera
    /// looks over it at everything above their knees, so it hides nothing worth
    /// lowering it for.
    /// </summary>
    [Test]
    public void AWideLowObstacleDoesNotCountAsHidingTheCharacter()
    {
        float coverage = CoverageOfObstacle(width: 3f, height: 0.6f);

        Assert.That(
            coverage,
            Is.LessThan(OcclusionTarget.PlayerCoverage),
            $"a 3.00-wide, 0.60-tall crate is treated as hiding a {PlayerHeight:0.00}-tall "
                + $"character ({coverage:P0} covered), but the camera sees the whole character "
                + "over the top of it"
        );
    }

    /// <summary>
    /// The other case it gets wrong, in the other direction. A post taller than
    /// the player covers them from head to foot down a narrow strip, and the
    /// player is still perfectly readable on either side of it. However tall it
    /// is, a pillar narrower than the character it stands in front of is not
    /// hiding them, and lowering it would be lowering scenery for nothing.
    /// </summary>
    [Test]
    public void ATallNarrowObstacleDoesNotCountAsHidingTheCharacter(
        [Values(0.2f, 0.3f, 0.5f, 0.7f)] float width
    )
    {
        float coverage = CoverageOfObstacle(width: width, height: 2.5f);

        Assert.That(
            coverage,
            Is.LessThan(OcclusionTarget.PlayerCoverage),
            $"a {width:0.00}-wide post is treated as hiding a {PlayerWidth:0.00}-wide character "
                + $"({coverage:P0} covered), but the character shows on both sides of it"
        );
    }

    /// <summary>
    /// Where the line sits. Something has to hide nearly all of the character -
    /// leaving not much more than the top of their head - before the room is
    /// rearranged around them. A character covered only to the shoulders is
    /// still a character the player can read.
    /// </summary>
    [Test]
    public void AnObstacleThatLeavesTheCharacterReadableIsLeftStanding()
    {
        // Tall enough to reach the chest, wide enough to span the body: the
        // head and shoulders are still clear above it.
        float coverage = CoverageOfObstacle(width: 3f, height: 1.15f);

        Assert.That(
            coverage,
            Is.LessThan(OcclusionTarget.PlayerCoverage),
            $"an obstacle the character's head and shoulders show over is treated as hiding "
                + $"them ({coverage:P0} covered)"
        );
    }

    /// <summary>
    /// And the case that is genuinely hiding somebody: tall enough to be seen
    /// through rather than over, wide enough to be seen through rather than
    /// around. This is what earns the lowering.
    /// </summary>
    [Test]
    public void SomethingTallAndWideEnoughToStandAcrossTheBodyHidesIt()
    {
        float coverage = CoverageOfObstacle(width: 3f, height: 2.5f);

        Assert.That(
            coverage,
            Is.GreaterThanOrEqualTo(OcclusionTarget.PlayerCoverage),
            $"a 3.00-wide, 2.50-tall obstacle standing across the character covers only "
                + $"{coverage:P0} of them, so it would be left standing in front of them"
        );
    }

    /// <summary>
    /// Height is read, not assumed: for a fixed width, growing an obstacle
    /// upward only ever hides more of the character.
    /// </summary>
    [Test]
    public void CoverageOnlyGrowsAsAnObstacleGetsTaller()
    {
        float previous = -1f;
        for (float height = 0.2f; height <= 3f; height += 0.2f)
        {
            float coverage = CoverageOfObstacle(width: 3f, height: height);
            Assert.That(
                coverage,
                Is.GreaterThanOrEqualTo(previous),
                $"raising the obstacle to {height:0.00} hid less of the character than the "
                    + "shorter one did"
            );
            previous = coverage;
        }

        Assert.That(previous, Is.GreaterThan(0.9f), "even a 3.00-tall wall never hid the player");
    }

    /// <summary>
    /// Width is read too: for a fixed height, widening an obstacle only ever
    /// hides more of the character.
    /// </summary>
    [Test]
    public void CoverageOnlyGrowsAsAnObstacleGetsWider()
    {
        float previous = -1f;
        for (float width = 0.2f; width <= 3f; width += 0.2f)
        {
            float coverage = CoverageOfObstacle(width: width, height: 2.5f);
            Assert.That(
                coverage,
                Is.GreaterThanOrEqualTo(previous),
                $"widening the obstacle to {width:0.00} hid less of the character than the "
                    + "narrower one did"
            );
            previous = coverage;
        }

        Assert.That(previous, Is.GreaterThan(0.9f), "even a 3.00-wide wall never hid the player");
    }

    /// <summary>
    /// A taller character is harder to hide behind the same obstacle. This is
    /// the character's own height entering the decision: swap the player model
    /// for a taller one and what counts as hiding them changes with it, with
    /// nothing to retune.
    /// </summary>
    [Test]
    public void ATallerCharacterIsHarderToHideBehindTheSameObstacle()
    {
        float shortCharacter = CoverageOfObstacle(width: 3f, height: 1.4f, characterHeight: 1f);
        float tallCharacter = CoverageOfObstacle(width: 3f, height: 1.4f, characterHeight: 2.6f);

        Assert.That(
            tallCharacter,
            Is.LessThan(shortCharacter),
            $"the same obstacle hides {tallCharacter:P0} of a 2.60-tall character and "
                + $"{shortCharacter:P0} of a 1.00-tall one, so the character's height is not "
                + "reaching the decision"
        );
    }

    /// <summary>Nothing in the way hides nothing.</summary>
    [Test]
    public void AnEmptyRoomCoversNothing()
    {
        var camera = Camera(Vector3.zero);
        Assert.That(
            new WallOcclusionSelector().CoverageOf(
                camera,
                Target(camera, Vector3.zero, PlayerHeight),
                new BoxWorld(),
                Group
            ),
            Is.Zero
        );
    }

    private static float CoverageOfObstacle(
        float width,
        float height,
        float characterHeight = PlayerHeight
    )
    {
        var playerPosition = Vector3.zero;
        OcclusionCameraModel camera = Camera(playerPosition);

        var world = new BoxWorld();
        world.Add(
            new Bounds(
                new Vector3(0f, height / 2f, playerPosition.z - Standoff),
                new Vector3(width, height, 0.4f)
            )
        );

        return new WallOcclusionSelector().CoverageOf(
            camera,
            Target(camera, playerPosition, characterHeight),
            world,
            Group
        );
    }

    private static OcclusionCameraModel Camera(Vector3 playerPosition)
    {
        return OcclusionCameraModel.Perspective(
            playerPosition + CameraOffset,
            Quaternion.LookRotation(-CameraOffset.normalized, Vector3.up),
            35f,
            16f / 9f,
            0.3f,
            1000f
        );
    }

    /// <summary>
    /// A character to be kept readable, with no threshold of its own, so the
    /// measured fraction comes back rather than a yes or no.
    /// </summary>
    private static OcclusionTarget Target(
        in OcclusionCameraModel camera,
        Vector3 position,
        float characterHeight
    )
    {
        var bounds = new Bounds(
            position + Vector3.up * (characterHeight / 2f),
            new Vector3(PlayerWidth, characterHeight, PlayerWidth)
        );
        Assert.That(
            OcclusionTarget.TryCreate(
                camera,
                OcclusionTargetKind.Player,
                position,
                bounds,
                0f,
                out OcclusionTarget target
            ),
            "the character is not on screen"
        );
        return target;
    }
}
