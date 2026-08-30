using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class CameraOcclusionFader
    {
        private const float FallbackPlayerWidth = 1.4f;
        private const float FallbackPlayerHeight = 2.2f;

        private readonly List<Renderer> targetRenderers = new();

        /// <summary>
        /// How tall the character the camera is keeping readable stands. An
        /// occluder's cut is measured against this as well as against itself, so
        /// something far taller than the player is cut low enough to actually
        /// reveal them rather than at a flattering fraction of its own height.
        /// </summary>
        public float TargetBodyHeight { get; private set; } = FallbackPlayerHeight;

        /// <summary>
        /// The world-space box a character fills. This is the only step of the
        /// visibility decision that has to look at renderers.
        /// </summary>
        private bool TryGetTargetBounds(Transform character, out Bounds bounds)
        {
            targetRenderers.Clear();
            character.GetComponentsInChildren(false, targetRenderers);
            bool hasBounds = false;
            bounds = default;
            foreach (Renderer characterRenderer in targetRenderers)
            {
                if (!IsCharacterBodyRenderer(characterRenderer))
                    continue;
                if (!hasBounds)
                {
                    bounds = characterRenderer.bounds;
                    hasBounds = true;
                }
                else
                    bounds.Encapsulate(characterRenderer.bounds);
            }

            if (!hasBounds && character == target)
            {
                bounds = new Bounds(
                    character.position + Vector3.up * targetHeight,
                    new Vector3(FallbackPlayerWidth, FallbackPlayerHeight, FallbackPlayerWidth)
                );
                hasBounds = true;
            }
            return hasBounds;
        }

        /// <summary>
        /// Records the height of the character the fades are being taken for.
        /// Measured from the body itself, so swapping the player model retunes
        /// every cut in the game without anyone editing a number.
        /// </summary>
        private void NoteTargetBodyHeight(Bounds bounds)
        {
            if (bounds.size.y > 0f)
                TargetBodyHeight = bounds.size.y;
        }

        private static bool IsCharacterBodyRenderer(Renderer characterRenderer)
        {
            return characterRenderer != null
                && characterRenderer.enabled
                && characterRenderer is not ParticleSystemRenderer
                && characterRenderer is not TrailRenderer
                && characterRenderer is not LineRenderer;
        }

        private void ResetDetection()
        {
            gameplayCamera = null;
            MaximumDetectedCoverage = 0f;
            QualifyingGroupCount = 0;
            VisibleEnemyTargetCount = 0;
            resolver?.Clear();
        }
    }
}
