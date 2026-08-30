using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Where a lowered occluder stops being drawn, measured from the piece itself.
    ///
    /// Every occluder keeps the same fraction of its own height standing and fades
    /// what is above it: a wall run, an arch crown, and a barrel someone adds next
    /// year are all judged by the same rule against their own measurements. The
    /// character behind the piece reads, and the piece stays grounded in the room
    /// instead of vanishing whole. Nothing here knows what the piece is - only how
    /// tall it stands and where its base sits.
    ///
    /// This is the CPU twin of <c>GetOcclusionCoverage</c> in
    /// <c>Resources/Shaders/DungeonOcclusionFade</c>. The shader is what a player
    /// sees and this is what the tests read, so the two have to agree.
    /// </summary>
    public readonly struct OcclusionFadeProfile
    {
        /// <summary>
        /// The narrowest the fade band may be. A piece measuring no height at all
        /// would otherwise divide the transition by zero and pop.
        /// </summary>
        public const float MinimumFeather = 0.02f;

        /// <summary>The world height the piece starts giving way at.</summary>
        public readonly float StartY;

        /// <summary>How far above <see cref="StartY"/> the piece is fully gone.</summary>
        public readonly float Feather;

        public OcclusionFadeProfile(float startY, float feather)
        {
            StartY = startY;
            Feather = Mathf.Max(MinimumFeather, feather);
        }

        /// <summary>
        /// The cutoff for a piece standing <paramref name="height"/> tall on a base
        /// at <paramref name="baseY"/>, capped so that what is left standing can
        /// never be taller than the character it was lowered to reveal.
        ///
        /// The fraction alone is what shapes the look, and for anything about a
        /// character's size it is the whole rule: a wall keeps its own share of
        /// itself and the character reads over the top of it. The cap only bites
        /// on something far taller than the character, where a fraction of its own
        /// height would leave more standing than the character is tall - lowered,
        /// and still hiding them completely. Nothing the dungeon currently builds
        /// reaches that, which is the point: the rule holds for the prop that does.
        /// </summary>
        public static OcclusionFadeProfile For(
            float baseY,
            float height,
            float characterHeight,
            float visibleBaseFraction,
            float featherFraction
        )
        {
            float measured = Mathf.Max(0f, height);
            float standing = measured * Mathf.Clamp01(visibleBaseFraction);
            if (characterHeight > 0f)
                standing = Mathf.Min(standing, characterHeight);

            // The blend cannot be deeper than the piece it is blending away, or a
            // capped stub would be half gone before it was ever lowered.
            float feather = Mathf.Min(
                measured * Mathf.Max(0f, featherFraction),
                Mathf.Max(standing, MinimumFeather)
            );
            return new OcclusionFadeProfile(baseY + standing, feather);
        }

        /// <summary>The cutoff for whatever a box measures, however it got there.</summary>
        public static OcclusionFadeProfile For(
            Bounds bounds,
            float characterHeight,
            float visibleBaseFraction,
            float featherFraction
        )
        {
            return For(
                bounds.min.y,
                bounds.size.y,
                characterHeight,
                visibleBaseFraction,
                featherFraction
            );
        }

        /// <summary>
        /// How much of the piece survives at a world height while it is
        /// <paramref name="fade"/> lowered: 1 is solid and 0 is gone. At rest the
        /// piece is whole at every height, which is what lets one profile describe
        /// a piece through its entire transition.
        /// </summary>
        public float CoverageAt(float worldY, float fade)
        {
            return 1f - Mathf.Clamp01(fade) * HeightMask(worldY);
        }

        /// <summary>
        /// How much of the fade a height is subject to: 0 at and below the cutoff,
        /// rising to 1 across the feather. This is HLSL <c>smoothstep</c>, which
        /// Unity's <see cref="Mathf.SmoothStep"/> is not - that one interpolates
        /// between two values rather than returning the eased fraction.
        /// </summary>
        public float HeightMask(float worldY)
        {
            float span = Mathf.Max(Feather, 0.001f);
            float t = Mathf.Clamp01((worldY - StartY) / span);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Whether a height is still solid once the piece is fully lowered. The
        /// base of every occluder has to answer true and the top false; that is
        /// the whole behaviour, independent of what the piece happens to be.
        /// </summary>
        public bool IsSolidWhenLowered(float worldY)
        {
            return CoverageAt(worldY, 1f) >= 0.999f;
        }
    }
}
