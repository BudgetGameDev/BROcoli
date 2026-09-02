using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BudgetGameDev.Games.Brocoli.Editor
{
    public static partial class AutoplayRunner
    {
        /// <summary>
        /// The parts of <c>summary.json</c> worth printing. Fields not listed here are
        /// simply not read: the file stays the record, and this is the read-out.
        /// </summary>
        [Serializable]
        internal sealed class RunSummary
        {
            public bool passed;
            public string tier;
            public string scenario;
            public string reason;
            public float durationSeconds;
            public float realSeconds;
            public float speedup;
            public float finalLevel;
            public int roomsVisited;
            public float distanceTravelled;
            public int stuckRecoveries;
            public int maxEnemies;
            public int warnings;
            public int errors;
            public int exceptions;
            public string[] missingFeatures;
            public CaptureRecord[] captures;
            public string[] missingCaptures;
            public string firstError;
        }

        /// <summary>One screenshot a <c>--capture-on</c> trigger asked for.</summary>
        [Serializable]
        internal sealed class CaptureRecord
        {
            public float t;
            public string @event;
            public int occurrence;
            public string trigger;
            public string file;
        }

        private static void ReportRun(AutoplayRunRequest request, int exitCode)
        {
            string path = Path.Combine(request.OutDir, "summary.json");
            if (!File.Exists(path))
            {
                Debug.LogError(
                    $"[Autoplay] The player exited {exitCode} without writing {path}. "
                        + $"See {Path.Combine(request.OutDir, "player.log")}."
                );
                return;
            }

            RunSummary summary = JsonUtility.FromJson<RunSummary>(File.ReadAllText(path));
            Debug.Log(Describe(summary, request.OutDir, exitCode));
            Debug.Log(AutoplayFrameReport.Describe(Path.Combine(request.OutDir, "frames")));
        }

        internal static string Describe(RunSummary summary, string outDir, int exitCode)
        {
            var invariant = CultureInfo.InvariantCulture;
            string missing =
                summary.missingFeatures == null || summary.missingFeatures.Length == 0
                    ? "none"
                    : string.Join(", ", summary.missingFeatures);

            return string.Join(
                Environment.NewLine,
                $"[Autoplay] {(summary.passed ? "PASS" : "FAIL")} tier={summary.tier} "
                    + $"scenario={summary.scenario} exit={exitCode}",
                $"  ended:      {summary.reason}",
                $"  simulated:  {Number(summary.durationSeconds, invariant)}s of game time in "
                    + $"{Number(summary.realSeconds, invariant)}s real "
                    + $"({Number(summary.speedup, invariant)}x)",
                $"  progress:   level {Number(summary.finalLevel, invariant)}, "
                    + $"{summary.roomsVisited} room(s), "
                    + $"{summary.distanceTravelled.ToString("0", invariant)} travelled, "
                    + $"peak {summary.maxEnemies} enemies",
                $"  navigation: {summary.stuckRecoveries} stuck recovery/recoveries",
                $"  logs:       {summary.warnings} warning(s), {summary.errors} error(s), "
                    + $"{summary.exceptions} exception(s)",
                $"  unused:     {missing}",
                $"  captures:   {DescribeCaptures(summary, invariant)}",
                $"  first error:{Blank(summary.firstError)}",
                $"  results:    {outDir}"
            );
        }

        /// <summary>
        /// Names each captured event and the frame it landed in, then the triggers
        /// that never fired -- the run a trigger was the whole point of is worth
        /// reading at a glance.
        /// </summary>
        internal static string DescribeCaptures(RunSummary summary, IFormatProvider invariant)
        {
            bool none = summary.captures == null || summary.captures.Length == 0;
            bool nothingAsked =
                none && (summary.missingCaptures == null || summary.missingCaptures.Length == 0);
            if (nothingAsked)
                return "none requested";

            var parts = new System.Collections.Generic.List<string>();
            foreach (CaptureRecord capture in summary.captures ?? Array.Empty<CaptureRecord>())
                parts.Add(
                    $"{capture.@event}#{capture.occurrence} at "
                        + $"{Number(capture.t, invariant)}s -> {capture.file}"
                );
            if (summary.missingCaptures is { Length: > 0 })
                parts.Add($"never fired: {string.Join(", ", summary.missingCaptures)}");
            return string.Join("; ", parts);
        }

        private static string Number(float value, IFormatProvider invariant) =>
            value.ToString("0.#", invariant);

        private static string Blank(string value) =>
            string.IsNullOrEmpty(value) ? " none" : $" {value}";
    }
}
