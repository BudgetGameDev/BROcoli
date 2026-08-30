using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The behaviour a player actually sees when something is lowered: its base
/// stays and its top goes. Nothing here names a prop or a wall, and nothing
/// here uses a dimension taken from one - every case is generated, so a prop
/// added to the game years from now is already covered by these rules rather
/// than needing a case of its own.
///
/// This was the gap that let the fade rule be prop-specific for so long: the
/// rest of the suite models fading as a boolean per group, so it could not
/// tell a wall that keeps its base from one that vanishes whole.
/// </summary>
public sealed class OcclusionFadeProfileTests
{
    private const float BaseFraction = 0.45f;
    private const float FeatherFraction = 0.12f;

    /// <summary>A player-sized character for the cut to be measured against.</summary>
    private const float CharacterHeight = 2.2f;

    /// <summary>
    /// Sizes spanning everything an occluder could plausibly be, from a coin
    /// on the floor to a structure taller than the room, at heights above and
    /// below the ground plane. None of them is a dimension the game currently
    /// ships, which is the point.
    /// </summary>
    private static readonly (float BaseY, float Height)[] Shapes =
    {
        (0f, 0.18f),
        (0f, 0.7f),
        (0f, 1.053f),
        (0f, 2.283f),
        (0f, 6.4f),
        (-1.75f, 3.2f),
        (2.4f, 0.95f),
        (0.006f, 2.64f),
    };

    /// <summary>
    /// The whole promise, for any shape: stand a piece up, lower it fully, and
    /// its foot is still drawn while its head is not.
    /// </summary>
    [Test]
    public void EveryOccluderKeepsItsBaseAndLosesItsTop([ValueSource(nameof(Shapes))] object shape)
    {
        (float baseY, float height) = ((float, float))shape;
        OcclusionFadeProfile profile = Profile(baseY, height);

        Assert.That(
            profile.IsSolidWhenLowered(baseY),
            $"a piece {height:0.00} tall standing at {baseY:0.00} disappears at its own base, "
                + "so it would read as floating rather than as lowered"
        );
        Assert.That(
            profile.CoverageAt(baseY + height, 1f),
            Is.LessThan(0.05f),
            $"a piece {height:0.00} tall standing at {baseY:0.00} is still drawn at its top, "
                + "so it would keep hiding whoever is behind it"
        );
    }

    /// <summary>
    /// The cut is a fraction of what the piece measures - so a piece twice as
    /// tall gives way twice as far up, and one rule serves every prop - capped
    /// at the height of the character it was lowered for. For anything about a
    /// character's size, which is nearly everything a dungeon is built from,
    /// the cap never binds and the fraction is the whole rule.
    /// </summary>
    [Test]
    public void TheCutIsAFractionOfTheMeasuredHeightCappedAtTheCharacter()
    {
        foreach ((float baseY, float height) in Shapes)
        {
            OcclusionFadeProfile profile = Profile(baseY, height);
            float expected = Mathf.Min(height * BaseFraction, CharacterHeight);
            Assert.That(
                profile.StartY - baseY,
                Is.EqualTo(expected).Within(0.0001f),
                $"a piece {height:0.00} tall keeps {profile.StartY - baseY:0.00} standing "
                    + $"rather than the expected {expected:0.00}"
            );
        }
    }

    /// <summary>
    /// A piece no taller than the character is cut exactly where it always was.
    /// The cap exists for the pathological case and must not quietly restyle
    /// every wall in the game on its way past.
    /// </summary>
    [Test]
    public void TheCapDoesNotDisturbAnythingAboutACharactersSize(
        [Values(0.7f, 1.053f, 2.283f, 2.64f)] float height
    )
    {
        Assert.That(
            Profile(0f, height).StartY,
            Is.EqualTo(height * BaseFraction).Within(0.0001f),
            $"the cap moved the cut on a {height:0.00}-tall piece, which is about the size of "
                + "the things the dungeon is already built from"
        );
    }

    /// <summary>
    /// The point of the rule above. Something much taller than the player, cut
    /// at a fraction of its own height, would leave more than a player's height
    /// of itself standing and go on hiding them - lowered, and still in the
    /// way. Whatever the occluder measures, what is left has to be short enough
    /// for the character behind it to be seen over.
    /// </summary>
    [Test]
    public void WhatIsLeftStandingIsNeverTallEnoughToStillHideTheCharacter(
        [Values(2.4f, 3.5f, 6.4f, 12f)] float height
    )
    {
        const float floor = 0f;
        OcclusionFadeProfile profile = Profile(floor, height);

        // The character's readable mass sits above the cut: their head has to
        // come out from behind whatever is left.
        float standing = profile.StartY - floor;
        Assert.That(
            standing,
            Is.LessThanOrEqualTo(CharacterHeight),
            $"a piece {height:0.00} tall keeps {standing:0.00} standing when lowered, which is "
                + $"taller than the {CharacterHeight:0.00} character it was lowered to reveal, "
                + "so lowering it achieved nothing"
        );
        Assert.That(
            profile.CoverageAt(floor + standing + profile.Feather + 0.01f, 1f),
            Is.LessThan(0.05f),
            $"a piece {height:0.00} tall is still drawn above its own blend band after being "
                + "lowered, so the cap left it hiding the character anyway"
        );
    }

    /// <summary>An occluder nobody has asked to lower is whole everywhere.</summary>
    [Test]
    public void APieceAtRestIsDrawnAtEveryHeight()
    {
        foreach ((float baseY, float height) in Shapes)
        {
            OcclusionFadeProfile profile = Profile(baseY, height);
            for (int step = 0; step <= 10; step++)
            {
                Assert.That(
                    profile.CoverageAt(baseY + height * step / 10f, 0f),
                    Is.EqualTo(1f).Within(0.0001f),
                    $"a piece {height:0.00} tall is already fading before it is lowered"
                );
            }
        }
    }

    /// <summary>
    /// The transition only ever removes geometry: as a piece lowers, no height
    /// on it comes back. Without this a prop could flicker back into view
    /// midway through fading.
    /// </summary>
    [Test]
    public void LoweringOnlyEverRemovesGeometry()
    {
        foreach ((float baseY, float height) in Shapes)
        {
            OcclusionFadeProfile profile = Profile(baseY, height);
            for (int step = 0; step <= 8; step++)
            {
                float worldY = baseY + height * step / 8f;
                float previous = 1f;
                for (int f = 0; f <= 10; f++)
                {
                    float coverage = profile.CoverageAt(worldY, f / 10f);
                    Assert.That(
                        coverage,
                        Is.LessThanOrEqualTo(previous + 0.0001f),
                        $"a piece {height:0.00} tall becomes more solid at {worldY:0.00} as it "
                            + "lowers further"
                    );
                    previous = coverage;
                }
            }
        }
    }

    /// <summary>
    /// A piece measuring no height at all still has to produce a usable band.
    /// Geometry with degenerate bounds turns up in imported art, and a zero
    /// feather would divide by nothing and pop.
    /// </summary>
    [Test]
    public void ADegenerateOccluderStillGetsAUsableBand()
    {
        OcclusionFadeProfile profile = Profile(1.2f, 0f);
        Assert.That(profile.Feather, Is.GreaterThanOrEqualTo(OcclusionFadeProfile.MinimumFeather));
        Assert.That(profile.CoverageAt(1.2f + 10f, 1f), Is.LessThan(0.05f));
    }

    /// <summary>
    /// The shipped camera has to actually keep a base. The fraction is a
    /// serialized field, so nothing else in the suite would notice it being
    /// tuned to zero - at which point every occluder would vanish whole and
    /// every other test here would still pass.
    /// </summary>
    [Test]
    public void TheShippedFaderKeepsAVisibleBase()
    {
        var host = new GameObject("FaderHost");
        try
        {
            host.AddComponent<Camera>();
            var serialized = new SerializedObject(host.AddComponent<CameraOcclusionFader>());
            float shipped = serialized.FindProperty("visibleBaseFraction").floatValue;
            Assert.That(
                shipped,
                Is.GreaterThan(0.05f),
                "the camera is configured to fade occluders away entirely rather than to "
                    + "lower them, so nothing would stay grounded in the room"
            );
            Assert.That(
                serialized.FindProperty("fadeFeatherFraction").floatValue,
                Is.GreaterThan(0f),
                "the camera is configured to cut occluders off with a hard edge"
            );
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    private static OcclusionFadeProfile Profile(float baseY, float height)
    {
        return OcclusionFadeProfile.For(
            baseY,
            height,
            CharacterHeight,
            BaseFraction,
            FeatherFraction
        );
    }
}
