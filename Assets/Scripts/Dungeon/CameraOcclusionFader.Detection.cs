using System.Collections.Generic;
using UnityEngine;

public sealed partial class CameraOcclusionFader
{
    private const float FallbackPlayerWidth = 1.4f;
    private const float FallbackPlayerHeight = 2.2f;
    private const float EnemyTargetRefreshInterval = 0.25f;
    private static readonly Vector2[] TargetRaySamples =
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
    private EnemyBase[] enemyTargets = System.Array.Empty<EnemyBase>();
    private float nextEnemyTargetRefreshTime;

    public float MaximumDetectedCoverage { get; private set; }
    public int QualifyingColliderCount { get; private set; }
    public int VisibleEnemyTargetCount { get; private set; }

    private void FindOccludingGeometry()
    {
        if (gameplayCamera == null)
            gameplayCamera = GetComponent<Camera>();
        MaximumDetectedCoverage = 0f;
        QualifyingColliderCount = 0;
        VisibleEnemyTargetCount = 0;
        qualifyingColliders.Clear();
        qualifyingVolumes.Clear();

        GeometryUtility.CalculateFrustumPlanes(gameplayCamera, frustumPlanes);
        if (
            target != null
            && TryGetPlayerViewportRect(out Rect playerRect, out Bounds playerBounds)
        )
            ScanOcclusionTarget(playerRect, playerBounds, target.position);
        FindEnemyOccludingGeometry();
    }

    private void ScanOcclusionTarget(Rect targetRect, Bounds targetBounds, Vector3 targetPosition)
    {
        float targetDepth = Vector3.Dot(
            targetBounds.center - gameplayCamera.transform.position,
            gameplayCamera.transform.forward
        );
        float maximumDepth = Mathf.Max(gameplayCamera.nearClipPlane, targetDepth);

        foreach (Vector2 sample in TargetRaySamples)
        {
            Vector3 viewportPoint = new(
                Mathf.Lerp(targetRect.xMin, targetRect.xMax, sample.x),
                Mathf.Lerp(targetRect.yMin, targetRect.yMax, sample.y),
                0f
            );
            Ray ray = gameplayCamera.ViewportPointToRay(viewportPoint);
            float forwardAmount = Vector3.Dot(ray.direction, gameplayCamera.transform.forward);
            if (forwardAmount <= 0.0001f)
                continue;

            ScanTargetRay(ray, maximumDepth / forwardAmount, targetRect, targetPosition);
            ScanVisualVolumes(ray, maximumDepth / forwardAmount, targetRect, targetPosition);
        }
    }

    private void ScanVisualVolumes(Ray ray, float distance, Rect playerRect, Vector3 playerPosition)
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

            float coverage = playerInside ? 1f : TargetCoverage(bounds, playerRect);
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

    private void ScanTargetRay(Ray ray, float distance, Rect playerRect, Vector3 playerPosition)
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
            if (!IsVisibleCandidate(candidate, playerPosition) || !IsStructuralOccluder(candidate))
                continue;

            float coverage = TargetCoverage(candidate.bounds, playerRect);
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

    private void FindEnemyOccludingGeometry()
    {
        if (Time.unscaledTime >= nextEnemyTargetRefreshTime)
        {
            enemyTargets = Object.FindObjectsByType<EnemyBase>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );
            nextEnemyTargetRefreshTime = Time.unscaledTime + EnemyTargetRefreshInterval;
        }

        foreach (EnemyBase enemy in enemyTargets)
        {
            if (
                enemy == null
                || !enemy.gameObject.activeInHierarchy
                || enemy.IsDying
                || !TryGetEnemyViewportRect(enemy, out Rect enemyRect, out Bounds enemyBounds)
            )
                continue;

            VisibleEnemyTargetCount++;
            ScanOcclusionTarget(enemyRect, enemyBounds, enemy.transform.position);
        }
    }

    private bool TryGetEnemyViewportRect(
        EnemyBase enemy,
        out Rect viewportRect,
        out Bounds enemyBounds
    )
    {
        targetRenderers.Clear();
        enemy.GetComponentsInChildren(false, targetRenderers);
        bool hasBounds = false;
        enemyBounds = default;
        foreach (Renderer enemyRenderer in targetRenderers)
        {
            if (!IsPlayerBodyRenderer(enemyRenderer))
                continue;
            if (!hasBounds)
            {
                enemyBounds = enemyRenderer.bounds;
                hasBounds = true;
            }
            else
                enemyBounds.Encapsulate(enemyRenderer.bounds);
        }
        viewportRect = default;
        return hasBounds && TryProjectBounds(enemyBounds, out viewportRect);
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

    private float TargetCoverage(Bounds occluder, Rect playerRect)
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
}
