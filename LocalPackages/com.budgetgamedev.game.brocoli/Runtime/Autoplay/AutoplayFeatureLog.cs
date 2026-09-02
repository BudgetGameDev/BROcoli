using System.Collections.Generic;
using System.Text;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Counts which game systems an autoplay run actually exercised. Gameplay code
    /// records into it from wherever the feature genuinely happens, so the ledger
    /// reflects the real game rather than the bot's intentions.
    ///
    /// The counters are static because the recording sites are spread across
    /// unrelated components and a run is a whole-process affair; <see cref="Reset"/>
    /// keeps that honest between runs and between tests.
    /// </summary>
    internal static class AutoplayFeatureLog
    {
        private static readonly Dictionary<string, int> Counts = new();

        internal static void Reset() => Counts.Clear();

        /// <summary>
        /// Records one occurrence. Inert unless autoplay is driving, so the recording
        /// calls can sit in ordinary gameplay code without costing a normal session.
        /// </summary>
        internal static void Record(string feature)
        {
            if (!AutoplayController.IsActive || string.IsNullOrEmpty(feature))
                return;

            Counts.TryGetValue(feature, out int seen);
            Counts[feature] = seen + 1;

            // Every named moment in the game passes through here, which makes this the
            // one place a screenshot trigger can watch without the recording sites
            // knowing anything about capture.
            AutoplayCaptureTriggers.Notify(feature, seen + 1);
        }

        /// <summary>Records only when the condition holds, keeping call sites one line.</summary>
        internal static void RecordIf(bool condition, string feature)
        {
            if (condition)
                Record(feature);
        }

        internal static int Count(string feature) =>
            Counts.TryGetValue(feature, out int seen) ? seen : 0;

        internal static bool Reached(string feature) => Count(feature) > 0;

        /// <summary>Required features this run never reached, in catalogue order.</summary>
        internal static List<string> Missing() => Missing(AutoplayFeatures.Required);

        /// <summary>
        /// The same question asked of one scenario's own list, because what a run
        /// had to reach depends on what it was asked to drive: a coverage sweep owes
        /// every system, and a journey owes the steps of the journey.
        /// </summary>
        internal static List<string> Missing(string[] required)
        {
            var missing = new List<string>();
            foreach (string feature in required)
                if (!Reached(feature))
                    missing.Add(feature);
            return missing;
        }

        /// <summary>Renders the whole ledger as a JSON object of feature counts.</summary>
        internal static string ToJson()
        {
            var json = new StringBuilder("{");
            AppendGroup(json, AutoplayFeatures.Required);
            AppendGroup(json, AutoplayFeatures.Optional);
            AppendGroup(json, AutoplayFeatures.Observed);
            return json.Append('}').ToString();
        }

        private static void AppendGroup(StringBuilder json, string[] features)
        {
            foreach (string feature in features)
            {
                if (json.Length > 1)
                    json.Append(',');
                json.Append('"').Append(feature).Append("\":").Append(Count(feature));
            }
        }
    }
}
