using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class RunTelemetryTests
    {
        private const BindingFlags Members =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

        private static readonly List<string> NothingMissing = new();
        private static readonly List<string> SomethingMissing = new() { "ui.map-panned" };

        private string output;
        private GameObject host;
        private RunTelemetry telemetry;

        [SetUp]
        public void CreateTelemetry()
        {
            output = Path.Combine(Path.GetTempPath(), "BrocoliTelemetry-" + Guid.NewGuid());
            host = new GameObject("Telemetry");
            telemetry = host.AddComponent<RunTelemetry>();
            telemetry.Configure(
                new AutoplayConfig
                {
                    OutDir = output,
                    Interval = 0.1f,
                    Duration = 1f,
                    Scenario = "smoke",
                    Seed = 42,
                    Sha = "test-sha",
                }
            );
            Invoke("OnEnable");
            Invoke("Start");
        }

        [TearDown]
        public void DestroyTelemetry()
        {
            if (telemetry != null)
                Invoke("OnDisable");
            UnityEngine.Object.DestroyImmediate(host);
            if (Directory.Exists(output))
                Directory.Delete(output, true);
        }

        [Test]
        public void SamplesAndSummaryAreWrittenAsValidScenarioEvidence()
        {
            Invoke("WriteSample");
            Assert.That(
                File.ReadAllText(Path.Combine(output, "telemetry.jsonl")),
                Does.Contain("\"t\":")
            );

            LogAssert.Expect(
                LogType.Log,
                new System.Text.RegularExpressions.Regex(
                    "^\\[Autoplay\\] Run ended \\(duration\\).+passed=True"
                )
            );
            Invoke("EndRun", "duration");
            Invoke("EndRun", "duration");

            string summary = File.ReadAllText(Path.Combine(output, "summary.json"));
            Assert.That(summary, Does.Contain("\"passed\":true"));
            Assert.That(summary, Does.Contain("\"scenario\":\"smoke\""));
            Assert.That(summary, Does.Contain("\"seed\":42"));
            Assert.That(summary, Does.Contain("\"sha\":\"test-sha\""));
            Assert.That(summary, Does.Contain("\"speedup\":"));
            Assert.That(summary, Does.Contain("\"features\":{"));
            Assert.That(summary, Does.Contain("\"missingFeatures\":["));
        }

        [Test]
        public void WarningErrorAndExceptionLogsFailEveryScenario()
        {
            Invoke("OnLog", "warn", "", LogType.Warning);
            Invoke("OnLog", "error", "", LogType.Error);
            Invoke("OnLog", "assert", "", LogType.Assert);
            Invoke("OnLog", "first exception", "", LogType.Exception);
            Invoke("OnLog", "second exception", "", LogType.Exception);
            Invoke("OnLog", "ordinary", "", LogType.Log);

            Assert.That((bool)Invoke("EvaluateScenario", "duration", NothingMissing), Is.False);
            SetConfigScenario("survive");
            Assert.That((bool)Invoke("EvaluateScenario", "duration", NothingMissing), Is.False);
            SetConfigScenario("progress");
            Assert.That((bool)Invoke("EvaluateScenario", "gameover", NothingMissing), Is.False);

            LogAssert.Expect(
                LogType.Log,
                new System.Text.RegularExpressions.Regex(
                    "^\\[Autoplay\\] Run ended \\(gameover\\).+passed=False"
                )
            );
            Invoke("OnGameOver");

            string logs = File.ReadAllText(Path.Combine(output, "logs.txt"));
            Assert.That(logs, Does.Contain("Warning: warn"));
            Assert.That(logs, Does.Contain("Exception: first exception"));
            Assert.That(
                File.ReadAllText(Path.Combine(output, "summary.json")),
                Does.Contain("\"firstError\":\"first exception\"")
            );
        }

        [Test]
        public void MissingConfigurationDisablesTheComponentAndEscapingIsJsonSafe()
        {
            var bare = new GameObject("Bare Telemetry").AddComponent<RunTelemetry>();
            try
            {
                InvokeOn(bare, "Start");
                Assert.That(bare.enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bare.gameObject);
            }

            Assert.That(InvokeStatic("Escape", new object[] { null }), Is.EqualTo(""));
            Assert.That(InvokeStatic("Escape", "a\\b\"c\r\nd"), Is.EqualTo("a\\\\b\\\"c  d"));
        }

        [Test]
        public void PlayerResolutionSamplingAndDurationExerciseTheRuntimeLoop()
        {
            GameObject player = new("Coverage telemetry player");
            player.tag = "Player";
            player.AddComponent<CapsuleCollider>();
            player.AddComponent<Rigidbody>();
            PlayerStats stats = player.AddComponent<PlayerStats>();
            PlayerDamageHandler damage = player.AddComponent<PlayerDamageHandler>();
            GameObject enemyObject = new("Coverage telemetry enemy");
            enemyObject.AddComponent<BoxCollider>();
            enemyObject.AddComponent<Rigidbody>();
            EnemyScript enemy = enemyObject.AddComponent<EnemyScript>();
            try
            {
                stats.ResetStats();
                Invoke("ResolvePlayer");
                EnemySpatialHash hash = EnemySpatialHash.Instance;
                hash.Register(enemy);
                object[] nearest = { Vector2.zero, 0 };
                Assert.That(
                    (float)Invoke("NearestEnemyDistance", nearest),
                    Is.GreaterThanOrEqualTo(0f)
                );
                Assert.That((int)nearest[1], Is.GreaterThan(0));

                SetField("_ended", true);
                Invoke("Update");
                SetField("_ended", false);
                SetField("_sampleAcc", 100f);
                SetConfigValue("Duration", 100f);
                Invoke("Update");

                SetField("_damage", damage);
                SetConfigValue("Duration", -1f);
                LogAssert.Expect(
                    LogType.Log,
                    new System.Text.RegularExpressions.Regex(
                        "^\\[Autoplay\\] Run ended \\(duration\\).+passed=True"
                    )
                );
                Invoke("Update");
                Invoke("OnDisable");
            }
            finally
            {
                if (EnemySpatialHash.Instance != null)
                    EnemySpatialHash.Instance.Unregister(enemy);
                UnityEngine.Object.DestroyImmediate(enemyObject);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void ARunThatStopsGettingAnywhereIsFailedRatherThanRiddenOut()
        {
            Assert.That((bool)Invoke("EvaluateScenario", "stalled", NothingMissing), Is.False);
            SetConfigScenario("coverage");
            Assert.That(
                (bool)Invoke("EvaluateScenario", "stalled", NothingMissing),
                Is.False,
                "standing still long enough to reach every feature is not a pass"
            );

            Invoke("TrackProgress"); // nothing has moved yet
            SetField("_progressRooms", 5);
            Invoke("TrackProgress"); // the room count disagrees, so the clock resets
            Assert.That(telemetry, Is.Not.Null);

            SetField("_elapsed", 500f);
            SetField("_lastProgressTime", 0f);
            LogAssert.Expect(
                LogType.Log,
                new System.Text.RegularExpressions.Regex(
                    "^\\[Autoplay\\] Run ended \\(stalled\\).+passed=False"
                )
            );
            Invoke("Update");

            Assert.That(
                File.ReadAllText(Path.Combine(output, "summary.json")),
                Does.Contain("\"reason\":\"stalled\"")
            );
        }

        [Test]
        public void DeathDuringACoverageSweepStartsAnotherLifeInsteadOfEndingTheRun()
        {
            SetConfigScenario("coverage");
            SetConfigValue("Duration", 1000f);

            Invoke("OnGameOver");

            Assert.That(
                File.Exists(Path.Combine(output, "summary.json")),
                Is.False,
                "the run is not over, it is between lives"
            );
            Assert.That(
                (bool)Invoke("TryRestart"),
                Is.False,
                "nothing to press until the overlay is actually up"
            );
        }

        [Test]
        public void CleanScenarioPoliciesCoverSurvivalAndProgress()
        {
            Assert.That((bool)Invoke("EvaluateScenario", "duration", NothingMissing), Is.True);
            SetConfigScenario("survive");
            Assert.That((bool)Invoke("EvaluateScenario", "duration", NothingMissing), Is.True);
            Assert.That((bool)Invoke("EvaluateScenario", "gameover", NothingMissing), Is.False);
            SetConfigScenario("progress");
            SetConfigValue("MinLevel", 0);
            Assert.That((bool)Invoke("EvaluateScenario", "gameover", NothingMissing), Is.True);
            SetConfigValue("MinLevel", 1);
            Assert.That((bool)Invoke("EvaluateScenario", "gameover", NothingMissing), Is.False);

            SetConfigScenario("coverage");
            Assert.That(
                (bool)Invoke("EvaluateScenario", "gameover", NothingMissing),
                Is.True,
                "reaching every system passes even if the agent then died"
            );
            Assert.That(
                (bool)Invoke("EvaluateScenario", "duration", SomethingMissing),
                Is.False,
                "surviving is not enough when a system was never reached"
            );
        }

        private void SetConfigScenario(string scenario)
        {
            var config = (AutoplayConfig)
                telemetry.GetType().GetField("_cfg", Members).GetValue(telemetry);
            config.Scenario = scenario;
        }

        private void SetConfigValue(string name, object value)
        {
            var config = (AutoplayConfig)
                telemetry.GetType().GetField("_cfg", Members).GetValue(telemetry);
            config.GetType().GetField(name).SetValue(config, value);
        }

        private void SetField(string name, object value) =>
            telemetry.GetType().GetField(name, Members).SetValue(telemetry, value);

        private object Invoke(string method, params object[] arguments) =>
            InvokeOn(telemetry, method, arguments);

        private static object InvokeOn(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, Members).Invoke(target, arguments);

        private static object InvokeStatic(string method, params object[] arguments) =>
            typeof(RunTelemetry).GetMethod(method, Members).Invoke(null, arguments);
    }
}
