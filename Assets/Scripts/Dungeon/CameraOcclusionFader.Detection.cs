using System.Collections.Generic;
using UnityEngine;

public sealed partial class CameraOcclusionFader
{
    private const float FallbackPlayerWidth = 1.4f;
    private const float FallbackPlayerHeight = 2.2f;
    private static readonly Vector2[] PlayerRaySamples =
    {
        new(0.5f, 0.5f),
        new(0.2f, 0.25f),
        new(0.5f, 0.25f),
        new(0.8f, 0.25f),
        new(0.2f, 0.55f),
        new(0.8f, 0.55f),
        new(0.2f, 0.82f),
        new(0.5f, 0.82f),
        new(0.8f, 0.82f),
    };

    [Header("Occlusion Detection")]
    [SerializeField, Range(0.05f, 0.9f)]
    private float minimumPlayerCoverage = 0.5f;

    [SerializeField, Min(0f)]
    private float releaseDelay = 0.2f;

    private readonly Plane[] frustumPlanes = new Plane[6];
    private readonly List<Renderer> targetRenderers = new();
    private readonly HashSet<Collider> qualifyingColliders = new();
    private readonly HashSet<DungeonOcclusionVolume> qualifyingVolumes = new();
    private Camera gameplayCamera;

    public float MaximumDetectedCoverage { get; private set; }
    public int QualifyingColliderCount { get; private set; }

    private void FindOccludingGeometry()
    {
        if (gameplayCamera == null)
            gameplayCamera = GetComponent<Camera>();
        MaximumDetectedCoverage = 0f;
        QualifyingColliderCount = 0;
        qualifyingColliders.Clear();
        qualifyingVolumes.Clear();

        if (!TryGetPlayerViewportRect(out Rect playerRect, out Bounds playerBounds))
            return;

        Vector3 playerPosition = target.position;
        GeometryUtility.CalculateFrustumPlanes(gameplayCamera, frustumPlanes);
        float playerDepth = Vector3.Dot(
            playerBounds.center - gameplayCamera.transform.position,
            gameplayCamera.transform.forward
        );
        float maximumDepth = Mathf.Max(gameplayCamera.nearClipPlane, playerDepth);

        foreach (Vector2 sample in PlayerRaySamples)
        {
            Vector3 viewportPoint = new(
                Mathf.Lerp(playerRect.xMin, playerRect.xMax, sample.x),
                Mathf.Lerp(playerRect.yMin, playerRect.yMax, sample.y),
                0f
            );
            Ray ray = gameplayCamera.ViewportPointToRay(viewportPoint);
            float forwardAmount = Vector3.Dot(ray.direction, gameplayCamera.transform.forward);
            if (forwardAmount <= 0.0001f)
                continue;

            ScanPlayerRay(ray, maximumDepth / forwardAmount, playerRect, playerPosition);
            ScanVisualVolumes(ray, maximumDepth / forwardAmount, playerRect, playerPosition);
        }
    }

    private void ScanVisualVolumes(
        Ray ray,
        float distance,
        Rect playerRect,
        Vector3 playerPosition
    )
    {
        foreach (DungeonOcclusionVolume volume in DungeonOcclusionVolume.Active)
        {
            if (volume == null || !volume.gameObject.activeInHierarchy)
                continue;

            int layerMask = 1 << volume.gameObject.layer;
            Bounds bounds = volume.WorldBounds;
            bool playerInside = IsInsideGroundFootprint(bounds, playerPosition);
            if (
                (gameplayCamera.cullingMask & layerMask) == 0
                || !GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)
                || (
                    !playerInside
                    && (
                        !DungeonOcclusionGeometry.IsFullyOnCameraSideOfPlayer(
                            bounds,
                            gameplayCamera,
                            playerPosition
                        )
                        || !bounds.IntersectRay(ray, out float hitDistance)
                        || hitDistance > distance
                    )
                )
            )
                continue;

            float coverage = playerInside ? 1f : PlayerCoverage(bounds, playerRect);
            MaximumDetectedCoverage = Mathf.Max(MaximumDetectedCoverage, coverage);
            if (coverage < minimumPlayerCoverage)
                continue;

            if (qualifyingVolumes.Add(volume))
                QualifyingColliderCount++;
            DungeonOcclusionSection.CollectForVolume(
                volume,
                gameplayCamera,
                frustumPlanes,
                playerPosition,
                currentSections,
                currentOccluders,
                hitRenderers
            );
        }
    }

    private static bool IsInsideGroundFootprint(Bounds bounds, Vector3 playerPosition)
    {
        return playerPosition.x >= bounds.min.x
            && playerPosition.x <= bounds.max.x
            && playerPosition.z >= bounds.min.z
            && playerPosition.z <= bounds.max.z;
    }

    private void ScanPlayerRay(
        Ray ray,
        float distance,
        Rect playerRect,
        Vector3 playerPosition
    )
    {
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            castHits,
            distance,
            occluderMask,
            QueryTriggerInteraction.Collide
        );
        for (int i = 0; i < hitCount; i++)
        {
            Collider candidate = castHits[i].collider;
            if (
                !IsVisibleCandidate(candidate, playerPosition)
                || !IsStructuralOccluder(candidate)
            )
                continue;

            float coverage = PlayerCoverage(candidate.bounds, playerRect);
            MaximumDetectedCoverage = Mathf.Max(MaximumDetectedCoverage, coverage);
            if (coverage < minimumPlayerCoverage)
                continue;

            if (qualifyingColliders.Add(candidate))
                QualifyingColliderCount++;
            if (
                DungeonOcclusionSection.TryCollectForHit(
                    candidate,
                    gameplayCamera,
                    frustumPlanes,
                    playerPosition,
                    currentSections,
                    currentOccluders,
                    hitRenderers
                )
            )
                continue;

            DungeonOcclusionSection.CollectVisibleRenderers(
                candidate.transform,
                gameplayCamera,
                frustumPlanes,
                playerPosition,
                currentOccluders,
                hitRenderers
            );
        }
    }

    private static bool IsStructuralOccluder(Collider candidate)
    {
        if (candidate.GetComponentInParent<DungeonOcclusionSection>() != null)
            return true;

        // Freestanding columns are full-height architecture. Other objects on
        // the Wall layer (barrels, chests, rocks, and similar low props) do not
        // obscure the player enough to justify fading.
        return IsFreestandingColumn(candidate);
    }

    private static bool IsFreestandingColumn(Component candidate)
    {
        Transform current = candidate != null ? candidate.transform : null;
        while (current != null)
        {
            if (current.name.StartsWith("DungeonColumn", System.StringComparison.Ordinal))
                return true;
            current = current.parent;
        }
        return false;
    }

    private bool IsVisibleCandidate(Collider candidate, Vector3 playerPosition)
    {
        if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy)
            return false;
        int layerMask = 1 << candidate.gameObject.layer;
        return (gameplayCamera.cullingMask & layerMask) != 0
            && GeometryUtility.TestPlanesAABB(frustumPlanes, candidate.bounds)
            && DungeonOcclusionGeometry.IsFullyOnCameraSideOfPlayer(
                candidate.bounds,
                gameplayCamera,
                playerPosition
            );
    }

    private float PlayerCoverage(Bounds occluder, Rect playerRect)
    {
        if (!TryProjectBounds(occluder, out Rect occluderRect))
            return 0f;

        float overlapWidth = Mathf.Max(
            0f,
            Mathf.Min(playerRect.xMax, occluderRect.xMax)
                - Mathf.Max(playerRect.xMin, occluderRect.xMin)
        );
        float overlapHeight = Mathf.Max(
            0f,
            Mathf.Min(playerRect.yMax, occluderRect.yMax)
                - Mathf.Max(playerRect.yMin, occluderRect.yMin)
        );
        float playerArea = Mathf.Max(0.000001f, playerRect.width * playerRect.height);
        return overlapWidth * overlapHeight / playerArea;
    }

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
