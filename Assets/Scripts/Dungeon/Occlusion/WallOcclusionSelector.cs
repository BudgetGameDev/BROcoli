using System.Collections.Generic;
using UnityEngine;

/// <summary>Why a wall group was asked to lower.</summary>
public enum OcclusionTargetKind
{
    Player,
    Enemy,
}

/// <summary>One thing that might be standing in front of a target.</summary>
public readonly struct OcclusionCandidate
{
    /// <summary>The visibility group this piece of architecture fades with.</summary>
    public readonly int GroupId;
    public readonly Bounds Bounds;

    public OcclusionCandidate(int groupId, Bounds bounds)
    {
        GroupId = groupId;
        Bounds = bounds;
    }
}

/// <summary>A group the current frame wants lowered, and what asked for it.</summary>
public readonly struct OcclusionActivation
{
    public readonly int GroupId;
    public readonly OcclusionTargetKind Cause;
    public readonly float Coverage;

    public OcclusionActivation(int groupId, OcclusionTargetKind cause, float coverage)
    {
        GroupId = groupId;
        Cause = cause;
        Coverage = coverage;
    }
}

/// <summary>
/// The broad phase: whatever can answer "what geometry lies along this ray".
/// Physics answers it in play; the property tests answer it arithmetically.
/// </summary>
public interface IOcclusionCandidateSource
{
    void Collect(Ray ray, float maximumDistance, List<OcclusionCandidate> results);

    /// <summary>Candidates whose ground footprint the target stands inside.</summary>
    void CollectEnclosing(Vector3 targetPosition, List<OcclusionCandidate> results);
}

/// <summary>
/// Decides which wall groups a single target needs out of the way. Pure: it
/// reads geometry and returns group ids, and knows nothing about renderers,
/// materials, or animation.
/// </summary>
public static class WallOcclusionSelector
{
    /// <summary>Where inside a target's screen rectangle the probes are taken.</summary>
    public static readonly Vector2[] TargetSamples =
    {
        new(0.5f, 0.5f),
        new(0.025f, 0.2f),
        new(0.25f, 0.2f),
        new(0.5f, 0.2f),
        new(0.75f, 0.2f),
        new(0.975f, 0.2f),
        new(0.025f, 0.5f),
        new(0.25f, 0.5f),
        new(0.75f, 0.5f),
        new(0.975f, 0.5f),
        new(0.025f, 0.8f),
        new(0.25f, 0.8f),
        new(0.5f, 0.8f),
        new(0.75f, 0.8f),
        new(0.975f, 0.8f),
    };

    public static void Select(
        in OcclusionCameraModel camera,
        in OcclusionTarget target,
        IOcclusionCandidateSource source,
        List<OcclusionCandidate> buffer,
        IDictionary<int, OcclusionActivation> activations
    )
    {
        // Geometry the target is standing under - an arch lintel, say - can sit
        // between them and the camera without any sight-line ray reaching it,
        // so it is asked for by position rather than found by probing.
        buffer.Clear();
        source.CollectEnclosing(target.Position, buffer);
        foreach (OcclusionCandidate candidate in buffer)
        {
            if (WallOcclusionMath.ContainsGroundPoint(candidate.Bounds, target.Position))
                Activate(candidate, target, 1f, activations);
        }

        float targetDepth = Vector3.Dot(target.Bounds.center - camera.Position, camera.Forward);
        float maximumDepth = Mathf.Max(camera.NearClip, targetDepth);
        foreach (Vector2 sample in TargetSamples)
        {
            var viewportPoint = new Vector2(
                Mathf.Lerp(target.ViewportRect.xMin, target.ViewportRect.xMax, sample.x),
                Mathf.Lerp(target.ViewportRect.yMin, target.ViewportRect.yMax, sample.y)
            );
            Ray ray = camera.ViewportPointToRay(viewportPoint);
            float forwardAmount = Vector3.Dot(ray.direction, camera.Forward);
            if (forwardAmount <= 0.0001f)
                continue;

            buffer.Clear();
            source.Collect(ray, maximumDepth / forwardAmount, buffer);
            foreach (OcclusionCandidate candidate in buffer)
                Evaluate(camera, candidate, target, activations);
        }
    }

    private static void Evaluate(
        in OcclusionCameraModel camera,
        OcclusionCandidate candidate,
        in OcclusionTarget target,
        IDictionary<int, OcclusionActivation> activations
    )
    {
        // Whatever the ray hit, only geometry in the gap between the camera and
        // the target can be hiding it: a wall level with or past the target has
        // already been walked by, and one behind the camera is not on screen.
        if (!WallOcclusionMath.IsBetweenCameraAndTarget(candidate.Bounds, camera, target.Position))
            return;
        if (!WallOcclusionMath.TryProjectBounds(camera, candidate.Bounds, out Rect occluderRect))
            return;

        Activate(
            candidate,
            target,
            WallOcclusionMath.CoverageOf(target.ViewportRect, occluderRect),
            activations
        );
    }

    private static void Activate(
        OcclusionCandidate candidate,
        in OcclusionTarget target,
        float coverage,
        IDictionary<int, OcclusionActivation> activations
    )
    {
        if (coverage < target.MinimumCoverage)
            return;

        // Order-independent by construction: the merged cause is the lower of
        // the two kinds and the merged coverage the higher, so the same set of
        // hits produces the same activation whatever order they arrive in.
        OcclusionTargetKind cause = target.Kind;
        float strongest = coverage;
        if (activations.TryGetValue(candidate.GroupId, out OcclusionActivation existing))
        {
            cause = (OcclusionTargetKind)Mathf.Min((int)existing.Cause, (int)cause);
            strongest = Mathf.Max(existing.Coverage, coverage);
        }

        activations[candidate.GroupId] = new OcclusionActivation(
            candidate.GroupId,
            cause,
            strongest
        );
    }
}
