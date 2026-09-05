using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// One <c>--capture-on</c> request: which recorded event to watch, which
    /// occurrence of it to photograph, and how long to wait afterwards.
    ///
    /// The spec is <c>event[#occurrence|*][+delay]</c>. Bare <c>event</c> means the
    /// first occurrence, <c>event#3</c> the third, and <c>event*</c> every one of
    /// them. A <c>+delay</c> in game-seconds photographs the aftermath rather than
    /// the instant -- a dropped orb has landed half a second later.
    /// </summary>
    internal readonly struct AutoplayCaptureTrigger
    {
        /// <summary>Occurrence value meaning "every time this event happens".</summary>
        internal const int EveryOccurrence = 0;

        internal readonly string Spec;
        internal readonly string Event;
        internal readonly int Occurrence;
        internal readonly float Delay;

        private AutoplayCaptureTrigger(string spec, string name, int occurrence, float delay)
        {
            Spec = spec;
            Event = name;
            Occurrence = occurrence;
            Delay = delay;
        }

        internal bool Matches(string name, int occurrence) =>
            Event == name && (Occurrence == EveryOccurrence || Occurrence == occurrence);

        internal static bool TryParse(string spec, out AutoplayCaptureTrigger trigger)
        {
            trigger = default;
            if (string.IsNullOrWhiteSpace(spec))
                return false;

            string text = spec.Trim();
            if (!TrySplitDelay(ref text, out float delay))
                return false;
            if (!TrySplitOccurrence(ref text, out int occurrence))
                return false;
            if (text.Length == 0)
                return false;

            trigger = new AutoplayCaptureTrigger(spec.Trim(), text, occurrence, delay);
            return true;
        }

        private static bool TrySplitDelay(ref string text, out float delay)
        {
            delay = 0f;
            int plus = text.IndexOf('+');
            if (plus < 0)
                return true;

            bool parsed = float.TryParse(
                text.Substring(plus + 1),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out delay
            );
            text = text.Substring(0, plus);
            return parsed && delay >= 0f;
        }

        private static bool TrySplitOccurrence(ref string text, out int occurrence)
        {
            occurrence = 1;
            if (text.EndsWith("*", System.StringComparison.Ordinal))
            {
                occurrence = EveryOccurrence;
                text = text.Substring(0, text.Length - 1);
                return true;
            }

            int hash = text.IndexOf('#');
            if (hash < 0)
                return true;

            bool parsed = int.TryParse(
                text.Substring(hash + 1),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out occurrence
            );
            text = text.Substring(0, hash);
            return parsed && occurrence >= 1;
        }
    }

    /// <summary>
    /// Turns recorded gameplay events into screenshot requests, so an agent can ask
    /// a batch run for the frame where something specific happened rather than
    /// hunting through the interval captures for it.
    ///
    /// Static for the same reason the feature ledger is: the events are recorded
    /// from unrelated components all over the game, and a run is a whole-process
    /// affair. <see cref="Arm"/> clears the previous run's state.
    /// </summary>
    internal static class AutoplayCaptureTriggers
    {
        /// <summary>
        /// Frames one <c>event*</c> spec may claim. A marathon run kills thousands of
        /// enemies, and an agent that asked to watch every kill wants a sample of
        /// them rather than a directory it cannot read.
        /// </summary>
        internal const int EveryLimit = 40;

        internal readonly struct Request
        {
            internal readonly string Spec;
            internal readonly string Event;
            internal readonly int Occurrence;
            internal readonly float Remaining;

            internal Request(string spec, string name, int occurrence, float remaining)
            {
                Spec = spec;
                Event = name;
                Occurrence = occurrence;
                Remaining = remaining;
            }

            internal Request After(float delta) => new(Spec, Event, Occurrence, Remaining - delta);
        }

        private static readonly List<AutoplayCaptureTrigger> Armed = new();
        private static readonly List<Request> Pending = new();
        private static readonly List<string> Captured = new();
        private static readonly Dictionary<string, int> CapturedPerSpec = new();
        private static readonly Dictionary<string, int> ClaimedPerSpec = new();
        private static float _elapsed;

        internal static bool Any => Armed.Count > 0;

        /// <summary>Arms a run's triggers, reporting any spec it could not read.</summary>
        internal static void Arm(IEnumerable<string> specs)
        {
            Reset();
            if (specs == null)
                return;

            foreach (string spec in specs)
            {
                if (AutoplayCaptureTrigger.TryParse(spec, out AutoplayCaptureTrigger trigger))
                    Armed.Add(trigger);
                else
                    Debug.LogWarning(
                        $"[Autoplay] Ignoring capture trigger '{spec}': expected "
                            + "event[#occurrence|*][+delay], as in "
                            + "pickup.experience-dropped+0.5."
                    );
            }
        }

        internal static void Reset()
        {
            Armed.Clear();
            Pending.Clear();
            Captured.Clear();
            CapturedPerSpec.Clear();
            ClaimedPerSpec.Clear();
            _elapsed = 0f;
        }

        /// <summary>Queues a capture for every armed trigger this occurrence answers.</summary>
        internal static void Notify(string name, int occurrence)
        {
            foreach (AutoplayCaptureTrigger trigger in Armed)
            {
                if (!trigger.Matches(name, occurrence) || AtLimit(trigger))
                    continue;
                ClaimedPerSpec.TryGetValue(trigger.Spec, out int claimed);
                ClaimedPerSpec[trigger.Spec] = claimed + 1;
                Pending.Add(new Request(trigger.Spec, name, occurrence, trigger.Delay));
            }
        }

        /// <summary>Advances the run clock the captures are stamped against.</summary>
        internal static void Tick(float delta)
        {
            _elapsed += delta;
            for (int index = 0; index < Pending.Count; index++)
                Pending[index] = Pending[index].After(delta);
        }

        /// <summary>
        /// Takes one request whose delay has elapsed. One per call because Unity keeps
        /// only the last screenshot requested in a frame, so the caller has to spread
        /// simultaneous requests over consecutive frames.
        /// </summary>
        internal static bool TryTakeReady(out Request request)
        {
            for (int index = 0; index < Pending.Count; index++)
            {
                if (Pending[index].Remaining > 0f)
                    continue;
                request = Pending[index];
                Pending.RemoveAt(index);
                return true;
            }

            request = default;
            return false;
        }

        /// <summary>Records a taken capture and renders its manifest line.</summary>
        internal static string Record(Request request, string file)
        {
            CapturedPerSpec.TryGetValue(request.Spec, out int taken);
            CapturedPerSpec[request.Spec] = taken + 1;
            string entry =
                $"{{\"t\":{_elapsed.ToString("0.###", CultureInfo.InvariantCulture)},"
                + $"\"event\":\"{request.Event}\",\"occurrence\":{request.Occurrence},"
                + $"\"trigger\":\"{request.Spec}\",\"file\":\"{file}\"}}";
            Captured.Add(entry);
            return entry;
        }

        /// <summary>Triggers that were asked for and never fired, in the armed order.</summary>
        internal static List<string> Unfired()
        {
            var unfired = new List<string>();
            foreach (AutoplayCaptureTrigger trigger in Armed)
                if (!CapturedPerSpec.ContainsKey(trigger.Spec) && !unfired.Contains(trigger.Spec))
                    unfired.Add(trigger.Spec);
            return unfired;
        }

        internal static string ToJson()
        {
            var json = new StringBuilder("[");
            for (int index = 0; index < Captured.Count; index++)
            {
                if (index > 0)
                    json.Append(',');
                json.Append(Captured[index]);
            }
            return json.Append(']').ToString();
        }

        /// <summary>
        /// Counts what a spec has already claimed, not what it has photographed, so a
        /// burst of events cannot queue past the limit while the captures drain.
        /// </summary>
        private static bool AtLimit(AutoplayCaptureTrigger trigger) =>
            trigger.Occurrence == AutoplayCaptureTrigger.EveryOccurrence
            && ClaimedPerSpec.TryGetValue(trigger.Spec, out int claimed)
            && claimed >= EveryLimit;
    }
}
