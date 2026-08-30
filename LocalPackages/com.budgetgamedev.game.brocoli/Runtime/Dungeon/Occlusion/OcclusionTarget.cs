using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// A character the camera has to keep readable, described by where it is and
    /// what rectangle of the screen it fills. Building one is the only step that
    /// needs renderers; every decision after it is arithmetic on these values.
    /// </summary>
    public readonly struct OcclusionTarget
    {
        /// <summary>
        /// How much of the player has to be hidden before anything is lowered for
        /// them: nearly all of it, so a wall gives way at the point where only the
        /// top of their head is still showing. Anything less is a character the
        /// player can still read - standing behind a post, or with their legs
        /// behind a crate - and lowering the room around them for that is more
        /// distracting than the obstruction was.
        /// </summary>
        public const float PlayerCoverage = 0.8f;

        /// <summary>
        /// The same for an enemy, and deliberately far lower. A partly hidden
        /// enemy is a threat that cannot be read or aimed at, so it is worth
        /// clearing sight of one long before it is worth doing for the player.
        /// </summary>
        public const float EnemyCoverage = 0.05f;

        public readonly OcclusionTargetKind Kind;
        public readonly Vector3 Position;
        public readonly Bounds Bounds;
        public readonly Rect ViewportRect;

        /// <summary>
        /// How much of this target a wall must cover before it is worth lowering.
        /// The enemy threshold is deliberately lower than the player's, so even a
        /// partly hidden enemy stays readable.
        /// </summary>
        public readonly float MinimumCoverage;

        public OcclusionTarget(
            OcclusionTargetKind kind,
            Vector3 position,
            Bounds bounds,
            Rect viewportRect,
            float minimumCoverage
        )
        {
            Kind = kind;
            Position = position;
            Bounds = bounds;
            ViewportRect = viewportRect;
            MinimumCoverage = minimumCoverage;
        }

        public static bool TryCreate(
            in OcclusionCameraModel camera,
            OcclusionTargetKind kind,
            Vector3 position,
            Bounds bounds,
            float minimumCoverage,
            out OcclusionTarget target
        )
        {
            if (!WallOcclusionMath.TryProjectBounds(camera, bounds, out Rect viewportRect))
            {
                target = default;
                return false;
            }

            target = new OcclusionTarget(kind, position, bounds, viewportRect, minimumCoverage);
            return true;
        }
    }
}
