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
            public int seed;
            public bool captureEnabled = true;
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
            public ProgressionRecord progression;
            public ScalingRecord scaling;
            public string[] balanceFindings;
            public ReactionRecord reaction;
        }

        [Serializable]
        internal sealed class ReactionRecord
        {
            public string profile;
            public float observationIntervalSeconds;
            public float reactionDelaySeconds;
            public long observations;
            public long decisions;

            internal bool IsValid =>
                (profile == "stress" || profile == "reference")
                && ValidSeconds(observationIntervalSeconds)
                && ValidSeconds(reactionDelaySeconds)
                && observations >= 0
                && decisions >= 0;

            internal bool Matches(ReactionRecord other) =>
                other != null
                && profile == other.profile
                && observationIntervalSeconds == other.observationIntervalSeconds
                && reactionDelaySeconds == other.reactionDelaySeconds;

            private static bool ValidSeconds(float value) =>
                !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 2;
        }

        /// <summary>How the run levelled and how hard it was pressed doing it.</summary>
        [Serializable]
        internal sealed class ProgressionRecord
        {
            public int peakLevel;
            public int levels;
            public int lives;
            public int deaths;
            public int deepestRing;
            public int rings;
            public float deathsPerHour;
            public float secondsPerLevel;
            public float earlySecondsPerLevel;
            public float lateSecondsPerLevel;
            public float paceRatio;
            public float earlyKillsPerLevel;
            public float lateKillsPerLevel;
            public float secondsPerRing;
            public float meanHealth;
            public float lowestHealth;
            public float dangerShare;
            public float safeShare;
        }

        /// <summary>What the dungeon scaled the run's rooms to.</summary>
        [Serializable]
        internal sealed class ScalingRecord
        {
            public int rooms;
            public int maxRing;
            public int enemies;
            public int mostEnemiesInARoom;
            public float firstPlayerPower;
            public float peakPlayerPower;
            public float firstHealthScale;
            public float peakHealthScale;
            public float peakDamageScale;
            public float peakCountScale;
            public float peakSpeedScale;
            public float healthScaleGrowth;
            public float threatGrowth;
            public float powerThreatGrowth;
            public float powerGrowth;
            public float trackingRatio;
            public float saturatedShare;
            public float speedCappedShare;
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
            if (summary.captureEnabled)
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
                DescribeProgression(summary, invariant),
                DescribeScaling(summary, invariant),
                DescribeBalance(summary),
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
            if (!summary.captureEnabled)
                return "disabled (--no-capture); visual validation not performed";
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
