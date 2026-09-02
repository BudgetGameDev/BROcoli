using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// What a run is allowed to leave on disk. A picture every game-second over a
    /// twenty-minute sweep is a thousand full-screen PNGs, and a directory of them
    /// per run is the harness quietly filling a disk; these pin the budget that
    /// spreads the same coverage over a readable number of frames, and the cleanup
    /// that removes them once the report has read them back.
    /// </summary>
    public sealed class AutoplayFrameBudgetTests
    {
        [SetUp]
        [TearDown]
        public void ClearTriggers() => AutoplayCaptureTriggers.Reset();

        /// <summary>
        /// The budget coarsens the cadence rather than stopping the run early, so
        /// the frames still span the whole session instead of its first minute.
        /// </summary>
        [Test]
        public void TheBudgetSpreadsItsFramesOverTheWholeRun()
        {
            float coverage = FrameCapture.SpacedInterval(1f, 1200f, 120);
            float marathon = FrameCapture.SpacedInterval(60f, 10800f, 120);

            Assert.That(
                coverage,
                Is.EqualTo(1200f / 119f).Within(0.001f),
                "a twenty-minute sweep still asks for a frame a second"
            );
            Assert.That(
                marathon,
                Is.EqualTo(90.7563f).Within(0.001f),
                "three game-hours spread over the same budget"
            );
        }

        /// <summary>A tier already inside its budget keeps the cadence it asked for.</summary>
        [Test]
        public void AShortRunKeepsTheCadenceItsTierAskedFor()
        {
            Assert.That(
                FrameCapture.SpacedInterval(0.25f, 5f, 120),
                Is.EqualTo(0.25f),
                "the smoke tier's twenty frames are nowhere near the budget"
            );
            Assert.That(
                FrameCapture.SpacedInterval(0.5f, 30f, 120),
                Is.EqualTo(0.5f),
                "half a minute at a frame every half second fits inside the budget"
            );
        }

        /// <summary>
        /// A duration the spacing cannot be drawn from -- an open-ended run, or a
        /// budget of one -- falls back to the requested interval and its floor.
        /// </summary>
        [Test]
        public void AnUnknownDurationFallsBackToTheRequestedInterval()
        {
            Assert.That(FrameCapture.SpacedInterval(0.5f, 0f, 120), Is.EqualTo(0.5f));
            Assert.That(FrameCapture.SpacedInterval(0.5f, 600f, 1), Is.EqualTo(0.5f));
            Assert.That(FrameCapture.SpacedInterval(0f, 0f, 120), Is.EqualTo(0.02f));
        }

        /// <summary>
        /// A run that outlives the duration its spacing was computed for stops
        /// taking pictures rather than writing them until the disk runs out.
        /// </summary>
        [Test]
        public void TheLoopStopsPhotographingOnceTheBudgetIsSpent()
        {
            string outDir = Path.Combine(Path.GetTempPath(), "brocoli-frame-budget");
            GameObject host = new("Frame budget host");
            host.SetActive(false);
            try
            {
                Time.captureDeltaTime = 0.5f;
                FrameCapture capture = host.AddComponent<FrameCapture>();
                capture.Configure(
                    new AutoplayConfig
                    {
                        OutDir = outDir,
                        Interval = 0f,
                        Duration = 0f,
                        MaxFrames = 3,
                    }
                );

                var taken = new List<string>();
                IEnumerator routine = capture.CaptureLoop(path => taken.Add(path));
                for (int step = 0; step < 10; step++)
                    Assert.That(routine.MoveNext(), Is.True);

                Assert.That(taken, Has.Count.EqualTo(3), "the budget is a cap, not a suggestion");
                Assert.That(taken[2], Does.EndWith("frame_00002.png"));
            }
            finally
            {
                Time.captureDeltaTime = 0f;
                UnityEngine.Object.DestroyImmediate(host);
                try
                {
                    Directory.Delete(outDir, true);
                }
                catch (IOException) { }
            }
        }

        /// <summary>
        /// Between pictures the loop lets the frames go by. Photographing every
        /// rendered frame is how the budget gets spent in the run's first seconds.
        /// </summary>
        [Test]
        public void FramesAreTakenAtTheCadenceRatherThanEveryRenderedFrame()
        {
            GameObject host = new("Frame cadence host");
            host.SetActive(false);
            try
            {
                Time.captureDeltaTime = 0.5f;
                FrameCapture capture = host.AddComponent<FrameCapture>();
                capture.Configure(
                    new AutoplayConfig
                    {
                        OutDir = Path.Combine(Path.GetTempPath(), "brocoli-frame-cadence"),
                        Interval = 2f,
                        Duration = 0f,
                        MaxFrames = 120,
                    }
                );

                var taken = new List<string>();
                IEnumerator routine = capture.CaptureLoop(path => taken.Add(path));
                for (int step = 0; step < 6; step++)
                    Assert.That(routine.MoveNext(), Is.True);

                Assert.That(
                    taken,
                    Has.Count.EqualTo(2),
                    "one picture to open the run, and one two game-seconds later"
                );
            }
            finally
            {
                Time.captureDeltaTime = 0f;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// <summary>The option and the environment variable both reach the player.</summary>
        [Test]
        public void TheBudgetIsSteerableFromTheCommandLineAndTheEnvironment()
        {
            AutoplayConfig arguments = AutoplayConfig.FromArguments(
                new[] { "--autoplay", "--max-frames=40" },
                _ => null
            );
            AutoplayConfig environment = AutoplayConfig.FromArguments(
                new[] { "--autoplay" },
                name => name == "BROCOLI_MAX_FRAMES" ? "12" : null
            );

            Assert.That(arguments.MaxFrames, Is.EqualTo(40));
            Assert.That(environment.MaxFrames, Is.EqualTo(12));
            Assert.That(arguments.ToString(), Does.Contain("maxFrames=40"));
            Assert.That(
                Editor
                    .AutoplayRunRequest.FromArguments(
                        new[] { "-tier", "medium", "-max-frames", "40" },
                        () => ""
                    )
                    .Overrides,
                Does.Contain("--max-frames=40")
            );
        }

        /// <summary>
        /// The frames go once the report has read them; everything a run concluded
        /// stays behind. Asking to keep them keeps them.
        /// </summary>
        [Test]
        public void TheRunnerRemovesTheFramesButKeepsTheRunItself()
        {
            string outDir = Path.Combine(Path.GetTempPath(), "brocoli-frame-cleanup");
            try
            {
                Assert.That(SurvivingFrames(outDir, keepFrames: false), Is.False);
                Assert.That(SurvivingFrames(outDir, keepFrames: true), Is.True);
            }
            finally
            {
                try
                {
                    Directory.Delete(outDir, true);
                }
                catch (IOException) { }
            }
        }

        /// <summary>Writes a finished run, cleans it, and reports what is left.</summary>
        private static bool SurvivingFrames(string outDir, bool keepFrames)
        {
            string frames = Path.Combine(outDir, "frames");
            string events = Path.Combine(outDir, "events");
            Directory.CreateDirectory(frames);
            Directory.CreateDirectory(events);
            File.WriteAllText(Path.Combine(frames, "frame_00000.png"), "picture");
            File.WriteAllText(Path.Combine(events, "combat.enemy-killed-001.png"), "picture");
            File.WriteAllText(Path.Combine(outDir, "summary.json"), "{}");

            var request = Editor.AutoplayRunRequest.FromArguments(
                keepFrames ? new[] { "-keep-frames" } : System.Array.Empty<string>(),
                () => ""
            );
            request.OutDir = outDir;
            if (!keepFrames)
                LogAssert.Expect(LogType.Log, new Regex("Removed the interval frames"));
            Editor.AutoplayRunner.DiscardFrames(request);

            Assert.That(File.Exists(Path.Combine(outDir, "summary.json")), Is.True);
            Assert.That(
                File.Exists(Path.Combine(events, "combat.enemy-killed-001.png")),
                Is.True,
                "a triggered capture was asked for on purpose and stays"
            );
            return Directory.Exists(frames);
        }
    }
}
