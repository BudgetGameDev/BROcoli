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
            public string firstError;
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
                $"  first error:{Blank(summary.firstError)}",
                $"  results:    {outDir}"
            );
        }

        private static string Number(float value, IFormatProvider invariant) =>
            value.ToString("0.#", invariant);

        private static string Blank(string value) =>
            string.IsNullOrEmpty(value) ? " none" : $" {value}";
    }
}
