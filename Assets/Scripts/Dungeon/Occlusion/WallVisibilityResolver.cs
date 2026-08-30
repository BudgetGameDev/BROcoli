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
    private readonly WallVisibilityStateMachine pieceStates;
    private readonly List<OcclusionTarget> targets = new();
    private readonly WallOcclusionSelector selector = new();
    private readonly Dictionary<int, OcclusionActivation> activations = new();
    private OcclusionCameraModel camera;
    private Vector3 groundForward;
    private float deepestTargetDepth;
    private float pieceQueryTime;

    public WallVisibilityResolver(WallVisibilityStateMachine.Settings settings)
    {
        states = new WallVisibilityStateMachine(settings);
        pieceStates = new WallVisibilityStateMachine(settings);
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
            selector.Select(model, target, source, activations);

        foreach (KeyValuePair<int, OcclusionActivation> activation in activations)
            states.Select(activation.Key, activation.Value.Cause);
        states.EndFrame(time);

        // The per-piece answers recorded since the previous resolve settle
        // through the same hysteresis as the groups, so a piece sitting on the
        // depth boundary cannot strobe while its group stays lowered.
        pieceStates.EndFrame(pieceQueryTime);
        pieceStates.BeginFrame();
        pieceQueryTime = time;
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
    ///
    /// The raw geometric answer wobbles when the depth boundary sits right on
    /// a piece - a walk cycle, a target flickering on the edge of detection -
    /// so releases settle through the same hysteresis the groups use, keyed by
    /// <paramref name="pieceId"/>. A piece entering the gap still fades on the
    /// same frame; only standing back up waits out the release delay, and a
    /// piece that jitters is pinned down. Ask once per piece per resolve.
    /// </summary>
    public bool IsPieceInTheWay(int pieceId, Bounds structure)
    {
        if (IsPieceInTheGap(structure))
        {
            pieceStates.Select(pieceId, OcclusionTargetKind.Player);
            return true;
        }
        return pieceStates.VisibilityOf(pieceId) == WallVisibility.Lowered;
    }

    /// <summary>The raw geometric answer under the settling: is the piece in
    /// the gap between the camera and the deepest target right now?</summary>
    public bool IsPieceInTheGap(Bounds structure)
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
        pieceStates.Clear();
        pieceQueryTime = 0f;
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
