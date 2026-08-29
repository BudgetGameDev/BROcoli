using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The whole wall-visibility decision, in one place and free of renderers,
/// materials, and animation. Feed it a camera, the characters that must stay
/// readable, and something that can answer what geometry lies along a ray, and
/// it answers which logical wall groups are lowered and why.
///
/// The runtime fader and the property tests both drive this class, so a
/// simulated walk through a dungeon exercises the same rules as play does.
/// </summary>
public sealed class WallVisibilityResolver
{
    private readonly WallVisibilityStateMachine states;
    private readonly List<OcclusionTarget> targets = new();
    private readonly List<OcclusionCandidate> candidateBuffer = new();
    private readonly Dictionary<int, OcclusionActivation> activations = new();
    private OcclusionCameraModel camera;
    private Vector3 groundForward;
    private float deepestTargetDepth;

    public WallVisibilityResolver(WallVisibilityStateMachine.Settings settings)
    {
        states = new WallVisibilityStateMachine(settings);
    }

    public WallVisibilityResolver()
        : this(WallVisibilityStateMachine.Settings.Default) { }

    /// <summary>The groups lowered after the last <see cref="Resolve"/>.</summary>
    public IReadOnlyList<int> LoweredGroups => states.LoweredGroups;

    /// <summary>What the last <see cref="Resolve"/> selected, for diagnostics.</summary>
    public IReadOnlyDictionary<int, OcclusionActivation> Activations => activations;

    public IReadOnlyList<OcclusionTarget> Targets => targets;

    public void BeginFrame()
    {
        targets.Clear();
        activations.Clear();
    }

    public void AddTarget(in OcclusionTarget target)
    {
        targets.Add(target);
    }

    /// <summary>
    /// Selects the groups standing in front of the current targets and settles
    /// them through the hysteresis that keeps transitions from strobing.
    /// </summary>
    public void Resolve(in OcclusionCameraModel model, IOcclusionCandidateSource source, float time)
    {
        camera = model;
        groundForward = WallOcclusionMath.GroundForward(model);
        deepestTargetDepth = float.NegativeInfinity;
        foreach (OcclusionTarget target in targets)
        {
            deepestTargetDepth = Mathf.Max(
                deepestTargetDepth,
                WallOcclusionMath.GroundDepth(model, groundForward, target.Position)
            );
        }

        states.BeginFrame();
        foreach (OcclusionTarget target in targets)
            WallOcclusionSelector.Select(model, target, source, candidateBuffer, activations);

        foreach (KeyValuePair<int, OcclusionActivation> activation in activations)
            states.Select(activation.Key, activation.Value.Cause);
        states.EndFrame(time);
    }

    public WallVisibility VisibilityOf(int groupId)
    {
        return states.VisibilityOf(groupId);
    }

    public WallVisibilityReason ReasonFor(int groupId)
    {
        return states.ReasonFor(groupId);
    }

    public bool IsLowered(int groupId)
    {
        return states.VisibilityOf(groupId) == WallVisibility.Lowered;
    }

    /// <summary>
    /// Whether one piece of a lowered group actually fades, judged on what is
    /// solid about it rather than on the decoration around it.
    ///
    /// The group decides when a transition happens; this decides which pieces
    /// take part, so a run that passes the player fades only the part standing
    /// in the way. A piece opts out only when it is wholly out of the way,
    /// which is what keeps an arch frame - long enough to straddle the player
    /// as they walk through it - moving with the run it belongs to.
    /// </summary>
    public bool IsPieceInTheWay(Bounds structure)
    {
        if (groundForward == Vector3.zero)
            return AnyTargetIsBehind(structure);

        return WallOcclusionMath.OverlapsDepthRange(
            structure,
            camera,
            groundForward,
            deepestTargetDepth
        );
    }

    /// <summary>How deep the furthest target stands, for diagnostics.</summary>
    public float DeepestTargetDepth => deepestTargetDepth;

    public void Clear()
    {
        states.Clear();
        targets.Clear();
        activations.Clear();
    }

    private bool AnyTargetIsBehind(Bounds pieceBounds)
    {
        foreach (OcclusionTarget target in targets)
        {
            if (WallOcclusionMath.IsBetweenCameraAndTarget(pieceBounds, camera, target.Position))
                return true;
        }
        return false;
    }
}
