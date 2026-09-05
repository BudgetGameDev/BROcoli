using System.Collections.Generic;
using System.IO;
using BudgetGameDev.Autoplay;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RunTelemetryTests
    {
        [Test]
        public void PausedCaptureFramesAdvanceAutomationButNotGameplayDurationOrSamples()
        {
            float capture = Time.captureDeltaTime;
            bool waiting = AutoplayTimeControl.WaitingForReadiness;
            try
            {
                SetConfigValue("Duration", 1000f);
                Time.captureDeltaTime = .1f;
                AutoplayTimeControl.WaitingForReadiness = false;
                Time.timeScale = 0f;
                for (int frame = 0; frame < 1000; frame++)
                    Invoke("Update");
                Assert.That(ReadFloat("_elapsed"), Is.Zero);
                Assert.That(File.ReadAllText(Path.Combine(output, "telemetry.jsonl")), Is.Empty);
                Assert.That(
                    AutoplayTimeControl.GameDelta,
                    Is.EqualTo(.1f).Within(.00001f),
                    "the independent menu/director clock keeps running"
                );

                Time.timeScale = 1f;
                Invoke("Update");
                Assert.That(ReadFloat("_elapsed"), Is.EqualTo(.1f).Within(.00001f));
                string beforePause = File.ReadAllText(Path.Combine(output, "telemetry.jsonl"));
                Assert.That(beforePause, Is.Not.Empty);
                float sampleAccumulator = ReadFloat("_sampleAcc");
                Time.timeScale = 0f;
                for (int frame = 0; frame < 100; frame++)
                    Invoke("Update");
                Assert.That(ReadFloat("_elapsed"), Is.EqualTo(.1f).Within(.00001f));
                Assert.That(ReadFloat("_sampleAcc"), Is.EqualTo(sampleAccumulator));
                Assert.That(
                    File.ReadAllText(Path.Combine(output, "telemetry.jsonl")),
                    Is.EqualTo(beforePause)
                );

                Time.timeScale = .5f;
                Invoke("Update");
                Assert.That(ReadFloat("_elapsed"), Is.EqualTo(.15f).Within(.00001f));
                AutoplayTimeControl.WaitingForReadiness = true;
                Invoke("Update");
                Assert.That(ReadFloat("_elapsed"), Is.EqualTo(.15f).Within(.00001f));
            }
            finally
            {
                Time.captureDeltaTime = capture;
                Time.timeScale = 1f;
                AutoplayTimeControl.WaitingForReadiness = waiting;
            }
        }

        [Test]
        public void PausedGameOverStillRestartsOnceAndRetainsTheDeathLedger()
        {
            Assert.That(GameOverOverlay.Active, Is.Null);
            SetConfigScenario("balance");
            SetConfigValue("Duration", 1000f);
            var pressed = new List<GameOverOverlay>();
            telemetry.PressRestart = pressed.Add;
            GameOverOverlay overlay = null;
            try
            {
                Invoke("OnGameOver");
                Assert.That(telemetry.Progression.Deaths, Is.EqualTo(1));
                overlay = GameOverOverlay.Show(0, 0, 0, 0);
                Assert.That(Time.timeScale, Is.Zero);
                Invoke("Update");
                Invoke("Update");
                Assert.That(pressed, Is.EqualTo(new[] { overlay }));
                Assert.That(telemetry.Progression.Deaths, Is.EqualTo(1));
                Assert.That(ReadFloat("_elapsed"), Is.Zero);
                Assert.That(File.ReadAllText(Path.Combine(output, "telemetry.jsonl")), Is.Empty);
            }
            finally
            {
                if (overlay != null)
                    Object.DestroyImmediate(overlay.gameObject);
                Time.timeScale = 1f;
            }
        }

        private float ReadFloat(string field) =>
            (float)typeof(RunTelemetry).GetField(field, Members).GetValue(telemetry);
    }
}
