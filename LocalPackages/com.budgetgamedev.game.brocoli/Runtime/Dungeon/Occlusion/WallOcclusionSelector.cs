using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
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
    /// Decides which occluders a single target needs out of the way, by measuring
    /// how much of that target they actually hide.
    ///
    /// The measurement is taken where it matters: sight lines are traced to a grid
    /// of points spread over the character's own body, and an occluder's coverage
    /// is the fraction of those points it stands in front of. Height and width
    /// therefore decide the answer by themselves. A knee-high crate blocks the
    /// lines to the character's feet and none of the lines to their head, so it
    /// scores low however broad it is. A narrow post blocks one column of lines
    /// whatever its height. Only something both tall enough and wide enough to
    /// stand across the body blocks most of them, and only that is worth lowering.
    ///
    /// This replaces comparing the screen rectangle of an occluder against the
    /// screen rectangle of a character, which answered a different question - how
    /// much the two boxes overlap - and got both of those cases wrong.
    ///
    /// Pure: it reads geometry and returns group ids, and knows nothing about
    /// renderers, materials, or animation.
    /// </summary>
    public sealed class WallOcclusionSelector
    {
        /// <summary>
        /// Sight lines across the body's width. Five is enough to tell something
        /// standing across the character from something standing beside them.
        /// </summary>
        public const int SampleColumns = 5;

        /// <summary>
        /// Sight lines up the body. The rows land near the feet, shins, waist,
        /// chest, and head, which is what lets a low object be recognised as one
        /// the character can be seen over.
        /// </summary>
        public const int SampleRows = 5;

        /// <summary>
        /// Where in a character's screen rectangle the sight lines are traced.
        /// Inset from the edges, because a line grazing the silhouette says more
        /// about the bounding box than about the body inside it.
        /// </summary>
        public static readonly Vector2[] TargetSamples = BuildSamples();

        private readonly List<OcclusionCandidate> candidates = new();
        private readonly Dictionary<int, int> blockedSamples = new();
        private readonly HashSet<int> blockedHere = new();

        /// <summary>How many sight lines a coverage fraction is measured over.</summary>
        public static int SampleCount => SampleColumns * SampleRows;

        public void Select(
            in OcclusionCameraModel camera,
            in OcclusionTarget target,
            IOcclusionCandidateSource source,
            IDictionary<int, OcclusionActivation> activations
        )
        {
            blockedSamples.Clear();

            // Geometry the target is standing under - an arch lintel, say - can sit
            // between them and the camera without any sight line reaching it, so it
            // is asked for by position rather than found by tracing.
            candidates.Clear();
            source.CollectEnclosing(target.Position, candidates);
            foreach (OcclusionCandidate candidate in candidates)
            {
                if (WallOcclusionMath.ContainsGroundPoint(candidate.Bounds, target.Position))
                    Activate(candidate.GroupId, target, 1f, activations);
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

                candidates.Clear();
                source.Collect(ray, maximumDepth / forwardAmount, candidates);

                // One sight line is either blocked by a group or it is not; a group
                // meeting it twice has still hidden one point of the body once.
                blockedHere.Clear();
                foreach (OcclusionCandidate candidate in candidates)
                {
                    // Only geometry in the gap between the camera and the target can
                    // be hiding it: something level with or past the target has
                    // already been walked by, and something behind the camera is not
                    // on screen at all.
                    if (
                        !WallOcclusionMath.IsBetweenCameraAndTarget(
                            candidate.Bounds,
                            camera,
                            target.Position
                        )
                    )
                        continue;
                    if (blockedHere.Add(candidate.GroupId))
                    {
                        blockedSamples.TryGetValue(candidate.GroupId, out int blocked);
                        blockedSamples[candidate.GroupId] = blocked + 1;
                    }
                }
            }

            foreach (KeyValuePair<int, int> blocked in blockedSamples)
                Activate(blocked.Key, target, blocked.Value / (float)SampleCount, activations);
        }

        /// <summary>
        /// How much of a character an occluder hides, as the fraction of sight
        /// lines to their body that it stands in front of. Exposed so the tests
        /// can measure the same thing the runtime does.
        /// </summary>
        public float CoverageOf(
            in OcclusionCameraModel camera,
            in OcclusionTarget target,
            IOcclusionCandidateSource source,
            int groupId
        )
        {
            var measured = new Dictionary<int, OcclusionActivation>();
            Select(camera, target, source, measured);
            return measured.TryGetValue(groupId, out OcclusionActivation activation)
                ? activation.Coverage
                : 0f;
        }

        private static Vector2[] BuildSamples()
        {
            var samples = new Vector2[SampleColumns * SampleRows];
            for (int row = 0; row < SampleRows; row++)
            for (int column = 0; column < SampleColumns; column++)
            {
                samples[row * SampleColumns + column] = new Vector2(
                    (column + 0.5f) / SampleColumns,
                    (row + 0.5f) / SampleRows
                );
            }
            return samples;
        }

        private static void Activate(
            int groupId,
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
            if (activations.TryGetValue(groupId, out OcclusionActivation existing))
            {
                cause = (OcclusionTargetKind)Mathf.Min((int)existing.Cause, (int)cause);
                strongest = Mathf.Max(existing.Coverage, coverage);
            }

            activations[groupId] = new OcclusionActivation(groupId, cause, strongest);
        }
    }
}
