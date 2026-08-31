using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class AutoplayRuntimeCoverageTests
    {
        private static readonly string[] Variables =
        {
            "BROCOLI_AUTOPLAY",
            "BROCOLI_SEED",
            "BROCOLI_DURATION",
            "BROCOLI_INTERVAL",
            "BROCOLI_TIMESTEP",
            "BROCOLI_OUT",
            "BROCOLI_SCENARIO",
        };

        [Test]
        public void EnvironmentConfigurationParsesEveryOverride()
        {
            string[] original = Array.ConvertAll(Variables, Environment.GetEnvironmentVariable);
            string output = Path.Combine(Path.GetTempPath(), "brocoli-autoplay-coverage");
            try
            {
                Environment.SetEnvironmentVariable("BROCOLI_AUTOPLAY", "true");
                Environment.SetEnvironmentVariable("BROCOLI_SEED", "73");
                Environment.SetEnvironmentVariable("BROCOLI_DURATION", "12.5");
                Environment.SetEnvironmentVariable("BROCOLI_INTERVAL", "0.25");
                Environment.SetEnvironmentVariable("BROCOLI_TIMESTEP", "0.05");
                Environment.SetEnvironmentVariable("BROCOLI_OUT", output);
                Environment.SetEnvironmentVariable("BROCOLI_SCENARIO", "progress");

                AutoplayConfig config = AutoplayConfig.FromCommandLine();

                Assert.That(config.Enabled, Is.True);
                Assert.That(config.Seed, Is.EqualTo(73));
                Assert.That(config.Duration, Is.EqualTo(12.5f));
                Assert.That(config.Interval, Is.EqualTo(0.25f));
                Assert.That(config.Timestep, Is.EqualTo(0.05f));
                Assert.That(config.OutDir, Is.EqualTo(output));
                Assert.That(config.Scenario, Is.EqualTo("progress"));
                Assert.That(config.ToString(), Does.Contain("seed=73"));
            }
            finally
            {
                for (int index = 0; index < Variables.Length; index++)
                    Environment.SetEnvironmentVariable(Variables[index], original[index]);
            }
        }

        [Test]
        public void InvalidNumericOverridesLeaveTargetsUnchanged()
        {
            MethodInfo tryInt = typeof(AutoplayConfig).GetMethod(
                "TryInt",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            MethodInfo tryFloat = typeof(AutoplayConfig).GetMethod(
                "TryFloat",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            object[] integer = { "bad", 9 };
            object[] number = { "bad", 2f };

            tryInt.Invoke(null, integer);
            tryFloat.Invoke(null, number);

            Assert.That(integer[1], Is.EqualTo(9));
            Assert.That(number[1], Is.EqualTo(2f));
        }

        [Test]
        public void CommandLineConfigurationParsesEveryOption()
        {
            string[] arguments =
            {
                "--autoplay",
                "--no-deterministic",
                "--deterministic",
                "--seed=17",
                "--duration=8.5",
                "--interval=0.2",
                "--timestep=0.04",
                "--minlevel=7",
                "--out=/tmp/brocoli-coverage",
                "--scenario=progress",
                "--sha=abc123",
            };

            AutoplayConfig config = AutoplayConfig.FromArguments(arguments, _ => null);

            Assert.That(config.Enabled, Is.True);
            Assert.That(config.Deterministic, Is.True);
            Assert.That(config.Seed, Is.EqualTo(17));
            Assert.That(config.Duration, Is.EqualTo(8.5f));
            Assert.That(config.Interval, Is.EqualTo(0.2f));
            Assert.That(config.Timestep, Is.EqualTo(0.04f));
            Assert.That(config.MinLevel, Is.EqualTo(7));
            Assert.That(config.OutDir, Is.EqualTo("/tmp/brocoli-coverage"));
            Assert.That(config.Scenario, Is.EqualTo("progress"));
            Assert.That(config.Sha, Is.EqualTo("abc123"));
        }

        [Test]
        public void UpgradeScoringCoversEveryStatAndCapPolicy()
        {
            var ordinary = new UpgradeDecisionContext(1f, 1, 0f, 0f, 0f);
            var pressure = new UpgradeDecisionContext(0.2f, 10, 75f, 100f, 100f);
            Assert.That(
                LevelUpAutoResolver.Score(null, ordinary),
                Is.EqualTo(float.NegativeInfinity)
            );

            foreach (UpgradeOption.StatType type in Enum.GetValues(typeof(UpgradeOption.StatType)))
            {
                var option = new UpgradeOption { Type = type, Amount = 10f };
                Assert.That(LevelUpAutoResolver.Score(option, ordinary), Is.GreaterThan(0f));
                Assert.That(LevelUpAutoResolver.Score(option, pressure), Is.GreaterThan(0f));
            }
        }
    }
}
