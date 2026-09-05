using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class AutoplayCohortReportTests
    {
        [Test]
        public void ExistingReportsUseTheSameCohortGateAndCannotHideAStalledSeed()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "brocoli-cohort-" + Guid.NewGuid().ToString("N")
            );
            int[] seeds = { 12345, 117074, 221803 };
            try
            {
                foreach (int seed in seeds)
                    WriteRun(root, seed, "duration", seed == 117074 ? 1 : 0);
                LogAssert.Expect(LogType.Log, new Regex("^\\[Autoplay\\] Balance cohort passed:"));
                Assert.That(Editor.AutoplayRunner.ReportBalanceCohort(root, seeds), Is.Zero);
                string report = File.ReadAllText(Path.Combine(root, "cohort.json"));
                Assert.That(report, Does.Contain("\"passed\": true"));
                Assert.That(report, Does.Contain("2700"));
                Assert.That(report, Does.Contain("seed-221803"));

                WriteRun(root, 221803, "stalled", 0);
                LogAssert.Expect(LogType.Log, new Regex("^\\[Autoplay\\] Balance cohort failed:"));
                Assert.That(Editor.AutoplayRunner.ReportBalanceCohort(root, seeds), Is.EqualTo(1));
                Assert.That(
                    File.ReadAllText(Path.Combine(root, "cohort.json")),
                    Does.Contain("seed 221803: run ended with stalled")
                );
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [TestCase("profile")]
        [TestCase("timing")]
        [TestCase("missing")]
        public void CohortsRejectDifferentReactionModelsEvenWhenEverySeedPassed(string mismatch)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "brocoli-reactions-" + Guid.NewGuid().ToString("N")
            );
            int[] seeds = { 1, 2, 3 };
            try
            {
                foreach (int seed in seeds)
                    WriteRun(root, seed, "duration", seed == 2 ? 1 : 0);
                string path = Path.Combine(root, "seed-3", "summary.json");
                var run = JsonUtility.FromJson<Editor.AutoplayRunner.RunSummary>(
                    File.ReadAllText(path)
                );
                if (mismatch == "profile")
                    run.reaction.profile = "stress";
                else if (mismatch == "timing")
                    run.reaction.reactionDelaySeconds = .3f;
                else
                    run.reaction = null;
                File.WriteAllText(path, JsonUtility.ToJson(run));
                LogAssert.Expect(LogType.Log, new Regex("^\\[Autoplay\\] Balance cohort failed:"));
                Assert.That(Editor.AutoplayRunner.ReportBalanceCohort(root, seeds), Is.EqualTo(1));
                Assert.That(
                    File.ReadAllText(Path.Combine(root, "cohort.json")),
                    Does.Contain("reaction")
                );
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        private static void WriteRun(string root, int seed, string reason, int deaths)
        {
            string directory = Path.Combine(root, $"seed-{seed}");
            Directory.CreateDirectory(directory);
            var summary = new Editor.AutoplayRunner.RunSummary
            {
                passed = reason == "duration",
                seed = seed,
                scenario = "balance",
                reason = reason,
                durationSeconds = 900f,
                progression = new Editor.AutoplayRunner.ProgressionRecord { deaths = deaths },
                scaling = new Editor.AutoplayRunner.ScalingRecord(),
                balanceFindings = Array.Empty<string>(),
                reaction = new Editor.AutoplayRunner.ReactionRecord
                {
                    profile = "reference",
                    observationIntervalSeconds = .1f,
                    reactionDelaySeconds = .2f,
                    observations = 9000,
                    decisions = 8998,
                },
            };
            File.WriteAllText(Path.Combine(directory, "summary.json"), JsonUtility.ToJson(summary));
        }
    }
}
