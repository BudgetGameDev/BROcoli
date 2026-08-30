using System.Collections.Generic;
using UnityEngine;

public sealed partial class CameraOcclusionFader
{
    [Header("Occlusion Detection")]
    [SerializeField, Range(0.05f, 0.9f)]
    private float minimumPlayerCoverage = 0.5f;

    [Tooltip(
        "Minimum fraction of a visible enemy that a wall must cover before the wall is lowered. "
            + "This is intentionally lower than the player threshold so even a partly hidden enemy stays readable."
    )]
    [SerializeField, Range(0.01f, 0.5f)]
    private float minimumEnemyCoverage = 0.05f;

    [SerializeField, Min(0f)]
    private float releaseDelay = 0.2f;

    private readonly Plane[] frustumPlanes = new Plane[6];
    private OccluderQuery occluderQuery;
    private Camera gameplayCamera;
    private OcclusionCameraModel cameraModel;
    private DungeonManager dungeonManager;
    private Vector2Int playerRoom;

    public float MaximumDetectedCoverage { get; private set; }
    public int QualifyingGroupCount { get; private set; }

    /// <summary>
    /// The broad phase. Physics answers what stands along a sight line; the
    /// decision about what that means belongs to the resolver.
    /// </summary>
    private sealed class OccluderQuery : IOcclusionCandidateSource
    {
        private readonly CameraOcclusionFader owner;

        public OccluderQuery(CameraOcclusionFader owner)
        {
            this.owner = owner;
        }

        public void Collect(Ray ray, float maximumDistance, List<OcclusionCandidate> results)
        {
            owner.CollectAlongRay(ray, maximumDistance, results);
        }

        public void CollectEnclosing(Vector3 targetPosition, List<OcclusionCandidate> results)
        {
            owner.CollectEnclosingVolumes(targetPosition, results);
        }
    }

    private void UpdateOccludingGeometry()
    {
        if (gameplayCamera == null)
            gameplayCamera = GetComponent<Camera>();
        occluderQuery ??= new OccluderQuery(this);
        cameraModel = OcclusionCameraModel.FromCamera(gameplayCamera);
        cameraModel.CalculateFrustumPlanes(frustumPlanes);
        if (target != null)
            playerRoom = DungeonLayout.RoomAt(new Vector2(target.position.x, target.position.z));
        if (dungeonManager == null)
            dungeonManager = Object.FindAnyObjectByType<DungeonManager>();

        Resolver.BeginFrame();
        AddPlayerTarget();
        AddEnemyTargets();
        Resolver.Resolve(cameraModel, occluderQuery, Time.unscaledTime);

        MaximumDetectedCoverage = 0f;
        foreach (KeyValuePair<int, OcclusionActivation> activation in Resolver.Activations)
            MaximumDetectedCoverage = Mathf.Max(MaximumDetectedCoverage, activation.Value.Coverage);
        QualifyingGroupCount = Resolver.Activations.Count;

        CollectLoweredRenderers();
    }

    private void AddPlayerTarget()
    {
        if (target == null || !TryGetTargetBounds(target, out Bounds bounds))
            return;
        if (
            OcclusionTarget.TryCreate(
                cameraModel,
                OcclusionTargetKind.Player,
                target.position,
                bounds,
                minimumPlayerCoverage,
                out OcclusionTarget playerTarget
            )
        )
            Resolver.AddTarget(playerTarget);
    }

    /// <summary>
    /// Turns the lowered groups into the renderers that actually fade. A group
    /// decides when the transition happens; each of its pieces still has to be
    /// standing in the way to take part.
    /// </summary>
    private void CollectLoweredRenderers()
    {
        foreach (int groupId in Resolver.LoweredGroups)
        {
            DungeonOcclusionSection section = DungeonOcclusionSection.ForGroup(groupId);
            if (section != null && BelongsToPlayerRoom(section))
                section.CollectFadeRenderers(
                    Resolver,
                    frustumPlanes,
                    currentOccluders,
                    hitRenderers
                );
        }
    }

    private void CollectAlongRay(Ray ray, float maximumDistance, List<OcclusionCandidate> results)
    {
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            castHits,
            maximumDistance,
            occluderMask,
            QueryTriggerInteraction.Collide
        );
        for (int i = 0; i < hitCount; i++)
            AddColliderCandidate(castHits[i].collider, results);

        foreach (DungeonOcclusionVolume volume in DungeonOcclusionVolume.Active)
        {
            if (volume == null || !volume.gameObject.activeInHierarchy)
                continue;
            Bounds bounds = volume.WorldBounds;
            if (
                IsVisibleGeometry(volume.gameObject.layer, bounds)
                && bounds.IntersectRay(ray, out float distance)
                && distance <= maximumDistance
            )
                AddVolumeCandidate(volume, bounds, results);
        }
    }

    private void CollectEnclosingVolumes(Vector3 targetPosition, List<OcclusionCandidate> results)
    {
        foreach (DungeonOcclusionVolume volume in DungeonOcclusionVolume.Active)
        {
            if (volume == null || !volume.gameObject.activeInHierarchy)
                continue;
            Bounds bounds = volume.WorldBounds;
            if (
                IsVisibleGeometry(volume.gameObject.layer, bounds)
                && WallOcclusionMath.ContainsGroundPoint(bounds, targetPosition)
            )
                AddVolumeCandidate(volume, bounds, results);
        }
    }

    private void AddColliderCandidate(Collider candidate, List<OcclusionCandidate> results)
    {
        if (
            candidate == null
            || !candidate.enabled
            || !candidate.gameObject.activeInHierarchy
            || !IsVisibleGeometry(candidate.gameObject.layer, candidate.bounds)
        )
            return;

        // Objects on the Wall layer without a section - barrels, chests,
        // rocks - do not obscure a character enough to justify lowering
        // anything.
        DungeonOcclusionSection section = DungeonOcclusionSection.Owning(candidate);
        if (section != null && BelongsToPlayerRoom(section))
            results.Add(new OcclusionCandidate(section.GroupId, candidate.bounds));
    }

    private void AddVolumeCandidate(
        DungeonOcclusionVolume volume,
        Bounds bounds,
        List<OcclusionCandidate> results
    )
    {
        DungeonOcclusionSection section = volume.GetComponentInParent<DungeonOcclusionSection>();
        if (section != null && BelongsToPlayerRoom(section))
            results.Add(new OcclusionCandidate(section.GroupId, bounds));
    }

    private bool BelongsToPlayerRoom(DungeonOcclusionSection section)
    {
        if (target == null)
            return true;

        DungeonLayout layout = dungeonManager != null ? dungeonManager.Layout : null;
        return section.BelongsToRoom(playerRoom, layout);
    }

    private bool IsVisibleGeometry(int layer, Bounds bounds)
    {
        return (gameplayCamera.cullingMask & (1 << layer)) != 0
            && GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
    }
}
