using System;
using System.Collections.Generic;
using System.IO;
using BudgetGameDev.Autoplay;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Editor
{
    public static partial class AutoplayRunner
    {
        /// <summary>Builds once, runs three independent balance seeds, and writes cohort.json.</summary>
        public static void RunBalanceCohort()
        {
            AutoplayRunRequest request = AutoplayRunRequest.FromCommandLine();
            request.Tier = "balance";
            int code = ExecuteCohort(request);
            if (Application.isBatchMode)
                EditorApplication.Exit(code);
        }

        public static void ReportExistingBalanceCohort()
        {
            AutoplayRunRequest request = AutoplayRunRequest.FromCommandLine();
            int first = request.Seed;
            int code = ReportBalanceCohort(
                request.OutDir,
                new[] { first, unchecked(first + 104729), unchecked(first + 209458) }
            );
            if (Application.isBatchMode)
                EditorApplication.Exit(code);
        }

        private static int ExecuteCohort(AutoplayRunRequest request)
        {
            if (ResolvePlayer(request) == null)
                return 2;
            string root = request.OutDir;
            int firstSeed = request.Seed;
            request.Build = false;
            var seeds = new int[3];
            var exits = new Dictionary<int, int>();
            for (int index = 0; index < seeds.Length; index++)
            {
                request.Seed = unchecked(firstSeed + index * 104729);
                seeds[index] = request.Seed;
                request.OutDir = Path.Combine(root, $"seed-{request.Seed}");
                string path = Path.Combine(request.OutDir, "summary.json");
                // A process that fails before writing must never reuse an old success.
                if (File.Exists(path))
                    File.Delete(path);
                exits[request.Seed] = Execute(request);
            }
            return ReportBalanceCohort(root, seeds, exits);
        }

        /// <summary>
        /// Grades existing seed-{seed}/summary.json files without launching players.
        /// Returns zero on success and writes the same cohort.json as RunBalanceCohort.
        /// </summary>
        public static int ReportBalanceCohort(string root, int[] seeds) =>
            ReportBalanceCohort(root, seeds, null);

        private static int ReportBalanceCohort(
            string root,
            int[] seeds,
            IReadOnlyDictionary<int, int> exits
        )
        {
            if (seeds == null)
                throw new ArgumentNullException(nameof(seeds));
            root = Path.GetFullPath(root);
            Directory.CreateDirectory(root);
            var cohort = new BalanceCohort();
            var reports = new List<string>();
            ReactionRecord reaction = null;
            foreach (int seed in seeds)
            {
                string path = Path.Combine(root, $"seed-{seed}", "summary.json");
                var findings = new List<string>();
                RunSummary run = ReadCohortRun(path, findings);
                if (run?.reaction == null || !run.reaction.IsValid)
                    findings.Add("missing or invalid reaction profile metadata");
                else if (reaction != null && !reaction.Matches(run.reaction))
                    findings.Add("reaction configuration differs across cohort seeds");
                else
                {
                    reaction ??= new ReactionRecord
                    {
                        profile = run.reaction.profile,
                        observationIntervalSeconds = run.reaction.observationIntervalSeconds,
                        reactionDelaySeconds = run.reaction.reactionDelaySeconds,
                    };
                    reaction.observations += run.reaction.observations;
                    reaction.decisions += run.reaction.decisions;
                }
                if (run == null || run.progression == null || run.scaling == null)
                    findings.Add("missing progression/scaling report");
                else
                {
                    if (run.balanceFindings != null)
                        findings.AddRange(run.balanceFindings);
                    if (run.reason != "duration" || run.warnings + run.errors + run.exceptions > 0)
                        findings.Add($"run ended with {run.reason}; logs must be clean");
                    if (run.seed != seed || run.scenario != "balance")
                        findings.Add("summary does not match the requested balance seed");
                    if (!run.passed && findings.Count == 0)
                        findings.Add("player reported a failed run");
                }
                if (
                    exits != null
                    && exits.TryGetValue(seed, out int exit)
                    && exit != 0
                    && findings.Count == 0
                )
                    findings.Add($"player exited {exit}");
                cohort.Add(
                    seed,
                    run?.durationSeconds ?? 0f,
                    run?.progression?.deaths ?? 0,
                    findings
                );
                reports.Add(path);
            }
            List<string> verdict = cohort.Evaluate(
                ProgressionBalance.MinDeathsPerHour,
                ProgressionBalance.MaxDeathsPerHour
            );
            var report = new CohortReport
            {
                passed = verdict.Count == 0,
                seeds = cohort.Runs,
                durationSeconds = cohort.Duration,
                deaths = cohort.Deaths,
                deathsPerHour = cohort.DeathsPerHour,
                findings = verdict.ToArray(),
                summaries = reports.ToArray(),
                reaction = reaction,
            };
            File.WriteAllText(Path.Combine(root, "cohort.json"), JsonUtility.ToJson(report, true));
            Debug.Log(
                $"[Autoplay] Balance cohort {(report.passed ? "passed" : "failed")}: {cohort.Runs} seeds, {cohort.DeathsPerHour:0.##} deaths/hour. {string.Join("; ", verdict)}"
            );
            return report.passed ? 0 : 1;
        }

        private static RunSummary ReadCohortRun(string path, List<string> findings)
        {
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonUtility.FromJson<RunSummary>(File.ReadAllText(path));
            }
            catch (Exception error) when (error is IOException || error is ArgumentException)
            {
                findings.Add($"unreadable summary: {error.Message}");
                return null;
            }
        }

        [Serializable]
        private sealed class CohortReport
        {
            public bool passed;
            public int seeds;
            public float durationSeconds;
            public int deaths;
            public float deathsPerHour;
            public string[] findings;
            public string[] summaries;
            public ReactionRecord reaction;
        }
    }
}
