using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>What a wall group is doing right now.</summary>
    public enum WallVisibility
    {
        /// <summary>Standing at full height.</summary>
        Full,

        /// <summary>Faded down so the character behind it stays readable.</summary>
        Lowered,
    }

    /// <summary>Why a wall group holds its current state.</summary>
    public enum WallVisibilityReason
    {
        NotOccluding,
        OccludesPlayer,
        OccludesEnemy,
        ReleaseDelay,
        StabilityHold,
    }

    /// <summary>
    /// Turns a per-frame set of selected wall groups into a stable per-group
    /// visibility, one entry per logical group rather than per wall piece, so
    /// every piece of a run necessarily transitions with its neighbours.
    /// Deterministic: the same previous state, selection, and timestamp always
    /// produce the same result.
    /// </summary>
    public sealed class WallVisibilityStateMachine
    {
        /// <summary>The timings that decide when a lowered wall may stand back up.</summary>
        public readonly struct Settings
        {
            /// <summary>How long a released group stays lowered before restoring.</summary>
            public readonly float ReleaseDelay;

            /// <summary>A group reacquired within this window counts as jitter.</summary>
            public readonly float FlickerReacquireWindow;

            /// <summary>How long a jittering group is pinned lowered.</summary>
            public readonly float FlickerStabilityHold;

            public Settings(
                float releaseDelay,
                float flickerReacquireWindow,
                float flickerStabilityHold
            )
            {
                ReleaseDelay = releaseDelay;
                FlickerReacquireWindow = flickerReacquireWindow;
                FlickerStabilityHold = flickerStabilityHold;
            }

            public static Settings Default => new(0.2f, 0.45f, 0.65f);
        }

        private sealed class Entry
        {
            public bool SelectedLastFrame;
            public float LastSelectedTime = float.NegativeInfinity;
            public float SelectionLostTime = float.NegativeInfinity;
            public float StabilityHoldUntil = float.NegativeInfinity;
            public WallVisibility Visibility = WallVisibility.Full;
            public WallVisibilityReason Reason = WallVisibilityReason.NotOccluding;
            public OcclusionTargetKind Cause = OcclusionTargetKind.Player;
        }

        private readonly Settings settings;
        private readonly Dictionary<int, Entry> entries = new();
        private readonly Dictionary<int, OcclusionTargetKind> selection = new();
        private readonly List<int> loweredGroups = new();
        private readonly List<int> expired = new();

        public WallVisibilityStateMachine(Settings settings)
        {
            this.settings = settings;
        }

        /// <summary>The groups currently lowered, in ascending group order.</summary>
        public IReadOnlyList<int> LoweredGroups => loweredGroups;

        public void BeginFrame()
        {
            selection.Clear();
        }

        public void Select(int groupId, OcclusionTargetKind cause)
        {
            if (
                selection.TryGetValue(groupId, out OcclusionTargetKind existing)
                && existing <= cause
            )
                return;
            selection[groupId] = cause;
        }

        /// <summary>Settles every tracked group against this frame's selection.</summary>
        public void EndFrame(float time)
        {
            foreach (KeyValuePair<int, OcclusionTargetKind> selected in selection)
            {
                if (!entries.ContainsKey(selected.Key))
                    entries.Add(selected.Key, new Entry());
            }

            expired.Clear();
            loweredGroups.Clear();
            foreach (KeyValuePair<int, Entry> pair in entries)
            {
                Entry entry = pair.Value;
                bool selected = selection.TryGetValue(pair.Key, out OcclusionTargetKind cause);
                Advance(entry, selected, cause, time);
                if (entry.Visibility == WallVisibility.Lowered)
                    loweredGroups.Add(pair.Key);
                else if (
                    !selected
                    && time - entry.LastSelectedTime > settings.FlickerReacquireWindow
                )
                    expired.Add(pair.Key);
            }

            foreach (int groupId in expired)
                entries.Remove(groupId);
            loweredGroups.Sort();
        }

        public WallVisibility VisibilityOf(int groupId)
        {
            return entries.TryGetValue(groupId, out Entry entry)
                ? entry.Visibility
                : WallVisibility.Full;
        }

        public WallVisibilityReason ReasonFor(int groupId)
        {
            return entries.TryGetValue(groupId, out Entry entry)
                ? entry.Reason
                : WallVisibilityReason.NotOccluding;
        }

        public void Clear()
        {
            entries.Clear();
            selection.Clear();
            loweredGroups.Clear();
        }

        private void Advance(Entry entry, bool selected, OcclusionTargetKind cause, float time)
        {
            if (selected)
            {
                // A group that vanished and came straight back is boundary jitter,
                // not a decision. Pin it down so it cannot strobe.
                if (
                    !entry.SelectedLastFrame
                    && time - entry.SelectionLostTime <= settings.FlickerReacquireWindow
                )
                    entry.StabilityHoldUntil = Mathf.Max(
                        entry.StabilityHoldUntil,
                        time + settings.FlickerStabilityHold
                    );

                entry.LastSelectedTime = time;
                entry.Cause = cause;
            }
            else if (entry.SelectedLastFrame)
                entry.SelectionLostTime = time;

            entry.SelectedLastFrame = selected;
            bool holding = time <= entry.StabilityHoldUntil;
            bool releasing = time - entry.LastSelectedTime <= settings.ReleaseDelay;
            entry.Visibility =
                selected || holding || releasing ? WallVisibility.Lowered : WallVisibility.Full;
            entry.Reason = selected
                ? cause == OcclusionTargetKind.Player
                    ? WallVisibilityReason.OccludesPlayer
                    : WallVisibilityReason.OccludesEnemy
                : holding
                    ? WallVisibilityReason.StabilityHold
                    : releasing
                        ? WallVisibilityReason.ReleaseDelay
                        : WallVisibilityReason.NotOccluding;
        }
    }
}
