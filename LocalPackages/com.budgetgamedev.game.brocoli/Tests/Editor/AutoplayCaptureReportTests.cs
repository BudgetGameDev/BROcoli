using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The rest of the capture path: the loop that takes the picture, the option
    /// that asks for it, and the read-out that says what a run photographed and
    /// what it missed.
    /// </summary>
    public sealed class AutoplayCaptureReportTests
    {
        [SetUp]
        [TearDown]
        public void ClearTriggers() => AutoplayCaptureTriggers.Reset();

        [Test]
        public void TheCaptureLoopPhotographsAFiredTriggerBeforeTheNextIntervalFrame()
        {
            string outDir = Path.Combine(Path.GetTempPath(), "brocoli-capture-triggers");
            GameObject host = new("Capture trigger host");
            host.SetActive(false);
            try
            {
                Directory.Delete(outDir, true);
            }
            catch (DirectoryNotFoundException) { }

            try
            {
                Directory.CreateDirectory(outDir);
                File.WriteAllText(
                    Path.Combine(outDir, "events.jsonl"),
                    "{\"event\":\"from an earlier run\"}\n"
                );

                FrameCapture capture = host.AddComponent<FrameCapture>();
                AutoplayCaptureTriggers.Arm(new[] { "pickup.experience-dropped" });
                capture.Configure(new AutoplayConfig { OutDir = outDir, Interval = 0f });
                AutoplayCaptureTriggers.Notify("pickup.experience-dropped", 1);

                var taken = new List<string>();
                IEnumerator routine = capture.CaptureLoop(path => taken.Add(path));
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(routine.MoveNext(), Is.True);

                Assert.That(taken, Has.Count.EqualTo(2));
                Assert.That(
                    taken[0],
                    Does.EndWith("pickup.experience-dropped-001.png"),
                    "the trigger takes the frame it fired in"
                );
                Assert.That(
                    taken[1],
                    Does.EndWith("frame_00000.png"),
                    "and the interval capture lands on the next one"
                );
                string manifest = File.ReadAllText(Path.Combine(outDir, "events.jsonl"));
                Assert.That(
                    manifest,
                    Does.Contain("\"file\":\"events/pickup.experience-dropped-001.png\"")
                );
                Assert.That(
                    manifest,
                    Does.Not.Contain("from an earlier run"),
                    "a rerun into the same directory reads as one run"
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                try
                {
                    Directory.Delete(outDir, true);
                }
                catch (IOException) { }
            }
        }

        [Test]
        public void TheOptionCarriesTriggersFromEitherTheCommandLineOrTheEnvironment()
        {
            AutoplayConfig arguments = AutoplayConfig.FromArguments(
                new[]
                {
                    "--autoplay",
                    "--capture-on=pickup.experience-dropped+0.5, combat.enemy-killed",
                    "--capture-on=ui.map-opened",
                },
                _ => null
            );
            AutoplayConfig environment = AutoplayConfig.FromArguments(
                new[] { "--capture-on=ui.map-opened" },
                name => name == "BROCOLI_CAPTURE_ON" ? "gameover.shown" : null
            );

            Assert.That(
                arguments.CaptureOn,
                Is.EqualTo(
                    new[]
                    {
                        "pickup.experience-dropped+0.5",
                        "combat.enemy-killed",
                        "ui.map-opened",
                    }
                )
            );
            Assert.That(arguments.ToString(), Does.Contain("captureOn=pickup.experience-dropped"));
            Assert.That(
                environment.CaptureOn,
                Is.EqualTo(new[] { "gameover.shown" }),
                "the environment steers a run whose arguments it did not build"
            );
        }

        [Test]
        public void ARunWithoutTriggersSaysSoInTheReport()
        {
            string described = Editor.AutoplayRunner.DescribeCaptures(
                new Editor.AutoplayRunner.RunSummary(),
                CultureInfo.InvariantCulture
            );

            Assert.That(described, Is.EqualTo("none requested"));
        }

        [Test]
        public void TheReportNamesWhatWasPhotographedAndWhatWasMissed()
        {
            var summary = new Editor.AutoplayRunner.RunSummary
            {
                captures = new[]
                {
                    new Editor.AutoplayRunner.CaptureRecord
                    {
                        t = 12.5f,
                        @event = "pickup.experience-dropped",
                        occurrence = 1,
                        trigger = "pickup.experience-dropped",
                        file = "events/pickup.experience-dropped-001.png",
                    },
                },
                missingCaptures = new[] { "combat.elite-killed" },
            };

            string described = Editor.AutoplayRunner.DescribeCaptures(
                summary,
                CultureInfo.InvariantCulture
            );

            Assert.That(described, Does.Contain("pickup.experience-dropped#1 at 12.5s"));
            Assert.That(described, Does.Contain("events/pickup.experience-dropped-001.png"));
            Assert.That(described, Does.Contain("never fired: combat.elite-killed"));
        }

        [Test]
        public void TheRunnerForwardsTriggersToThePlayer()
        {
            Editor.AutoplayRunRequest request = Editor.AutoplayRunRequest.FromArguments(
                new[] { "-tier", "medium", "-capture-on", "pickup.experience-dropped+0.5" },
                () => "abc1234"
            );

            Assert.That(
                request.Overrides,
                Does.Contain("--capture-on=pickup.experience-dropped+0.5")
            );
        }
    }
}
