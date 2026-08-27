using UnityEngine;

public sealed partial class CameraOcclusionFader
{
    private bool TryGetPlayerViewportRect(out Rect viewportRect, out Bounds playerBounds)
    {
        targetRenderers.Clear();
        target.GetComponentsInChildren(false, targetRenderers);
        bool hasBounds = false;
        playerBounds = default;
        foreach (Renderer targetRenderer in targetRenderers)
        {
            if (!IsPlayerBodyRenderer(targetRenderer))
                continue;
            if (!hasBounds)
            {
                playerBounds = targetRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                playerBounds.Encapsulate(targetRenderer.bounds);
            }
        }

        if (!hasBounds)
        {
            playerBounds = new Bounds(
                target.position + Vector3.up * targetHeight,
                new Vector3(FallbackPlayerWidth, FallbackPlayerHeight, FallbackPlayerWidth)
            );
        }
        return TryProjectBounds(playerBounds, out viewportRect);
    }

    private static bool IsPlayerBodyRenderer(Renderer targetRenderer)
    {
        return targetRenderer != null
            && targetRenderer.enabled
            && targetRenderer is not ParticleSystemRenderer
            && targetRenderer is not TrailRenderer
            && targetRenderer is not LineRenderer;
    }

    private bool TryProjectBounds(Bounds bounds, out Rect viewportRect)
    {
        Vector3 minimum = bounds.min;
        Vector3 maximum = bounds.max;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        bool hasVisiblePoint = false;

        for (int x = 0; x <= 1; x++)
        for (int y = 0; y <= 1; y++)
        for (int z = 0; z <= 1; z++)
        {
            var corner = new Vector3(
                x == 0 ? minimum.x : maximum.x,
                y == 0 ? minimum.y : maximum.y,
                z == 0 ? minimum.z : maximum.z
            );
            Vector3 viewport = gameplayCamera.WorldToViewportPoint(corner);
            if (viewport.z <= gameplayCamera.nearClipPlane)
                continue;

            hasVisiblePoint = true;
            minX = Mathf.Min(minX, viewport.x);
            minY = Mathf.Min(minY, viewport.y);
            maxX = Mathf.Max(maxX, viewport.x);
            maxY = Mathf.Max(maxY, viewport.y);
        }

        viewportRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return hasVisiblePoint && maxX > 0f && minX < 1f && maxY > 0f && minY < 1f;
    }

    private void ResetDetection()
    {
        gameplayCamera = null;
        MaximumDetectedCoverage = 0f;
        QualifyingColliderCount = 0;
        qualifyingColliders.Clear();
        qualifyingVolumes.Clear();
    }
}
