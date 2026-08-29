using System.Collections.Generic;
using UnityEngine;

public sealed partial class CameraOcclusionFader
{
    private const float FallbackPlayerWidth = 1.4f;
    private const float FallbackPlayerHeight = 2.2f;

    private readonly List<Renderer> targetRenderers = new();

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
        columnRoots.Clear();
        resolver?.Clear();
    }
}
