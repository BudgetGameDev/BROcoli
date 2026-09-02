using System;
using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The tiers are the harness's whole command surface now that no launcher
    /// scripts stand in front of the player, so their presets and the option
    /// precedence around them are worth pinning down.
    /// </summary>
    public sealed class AutoplayTierTests
    {
        [Test]
        public void EveryTierIsFindableByNameAndListed()
        {
            string listed = AutoplayTiers.Names();

            foreach (string name in listed.Split(new[] { ", " }, StringSplitOptions.None))
            {
                Assert.That(AutoplayTiers.TryFind(name, out AutoplayTier tier), Is.True, name);
                Assert.That(tier.Name, Is.EqualTo(name));
                Assert.That(tier.Description, Is.Not.Empty, name);
                Assert.That(tier.Duration, Is.GreaterThan(0f), name);
                Assert.That(tier.Interval, Is.GreaterThan(0f), name);
                Assert.That(tier.Timestep, Is.GreaterThan(0f), name);
                Assert.That(tier.Scenario, Is.Not.Empty, name);
            }

            Assert.That(AutoplayTiers.TryFind("no-such-tier", out _), Is.False);
            Assert.That(AutoplayTiers.TryFind(AutoplayTiers.Default, out _), Is.True);
        }

        [Test]
        public void AnAbsentOrUnknownTierLeavesTheConfigAlone()
        {
            var config = new AutoplayConfig();
            float duration = config.Duration;

            AutoplayTiers.Apply(config, null);
            AutoplayTiers.Apply(config, string.Empty);
            AutoplayTiers.Apply(config, "no-such-tier");

            Assert.That(config.Tier, Is.Empty);
            Assert.That(config.Duration, Is.EqualTo(duration));
            Assert.That(config.ToString(), Does.Contain("tier=custom"));
        }

        [Test]
        public void ATierShapesTheRunAndExplicitOptionsStillWin()
        {
            AutoplayConfig config = AutoplayConfig.FromArguments(
                new[] { "--autoplay", "--tier=marathon", "--duration=12" },
                _ => null
            );

            Assert.That(config.Tier, Is.EqualTo("marathon"));
            Assert.That(config.Duration, Is.EqualTo(12f), "an explicit option beats its preset");
            Assert.That(config.Interval, Is.EqualTo(60f));
            Assert.That(config.Scenario, Is.EqualTo("survive"));
            Assert.That(config.ToString(), Does.Contain("tier=marathon"));
        }

        [Test]
        public void TheCoverageTierEntersThroughTheMenuAndAssertsFeatures()
        {
            AutoplayConfig config = AutoplayConfig.FromArguments(
                new[] { "--autoplay", "--tier=coverage" },
                _ => null
            );

            Assert.That(config.Scenario, Is.EqualTo("coverage"));
            Assert.That(config.DriveMenus, Is.True);
            Assert.That(config.ExerciseFeatures, Is.True);
        }

        [Test]
        public void ATierCanBeSelectedFromTheEnvironment()
        {
            AutoplayConfig config = AutoplayConfig.FromArguments(
                Array.Empty<string>(),
                name => name == "BROCOLI_TIER" ? "smoke" : null
            );

            Assert.That(config.Tier, Is.EqualTo("smoke"));
            Assert.That(config.Duration, Is.EqualTo(5f));
            Assert.That(
                config.ExerciseFeatures,
                Is.False,
                "a five-second run has no time to probe"
            );
            Assert.That(config.OutDir, Does.Contain("AutoplayRuns"));
        }

        [Test]
        public void MenuAndFeatureDrivingToggleBothWays()
        {
            AutoplayConfig on = AutoplayConfig.FromArguments(
                new[] { "--menus", "--features" },
                _ => null
            );
            AutoplayConfig off = AutoplayConfig.FromArguments(
                new[] { "--no-menus", "--no-features" },
                _ => null
            );

            Assert.That(on.DriveMenus, Is.True);
            Assert.That(on.ExerciseFeatures, Is.True);
            Assert.That(off.DriveMenus, Is.False);
            Assert.That(off.ExerciseFeatures, Is.False);
            Assert.That(off.Enabled, Is.False, "no option here implies a run was requested");
        }

        [Test]
        public void TheCaptureStepStaysWithinThePhysicsSubStepBudget()
        {
            const float fixedStep = 0.02f;
            float ceiling = fixedStep * AutoplayTimeControl.MaximumPhysicsStepsPerFrame;

            Assert.That(
                AutoplayTimeControl.ResolveCaptureStep(1f, fixedStep),
                Is.EqualTo(ceiling).Within(1e-5f),
                "an absurd timestep is clamped rather than left to outrun physics"
            );
            Assert.That(
                AutoplayTimeControl.ResolveCaptureStep(0.03f, fixedStep),
                Is.EqualTo(0.03f).Within(1e-6f)
            );
            Assert.That(
                AutoplayTimeControl.ResolveCaptureStep(0f, fixedStep),
                Is.EqualTo(AutoplayTimeControl.MinimumStep).Within(1e-6f)
            );
            Assert.That(
                AutoplayTimeControl.ResolveCaptureStep(1f, 0f),
                Is.EqualTo(AutoplayTimeControl.MinimumStep).Within(1e-6f)
            );
        }

        [Test]
        public void TheRunIsMeasuredOnTheGameClockNotTheWallClock()
        {
            float original = UnityEngine.Time.captureDeltaTime;
            try
            {
                UnityEngine.Time.captureDeltaTime = 0.05f;
                Assert.That(
                    AutoplayTimeControl.GameDelta,
                    Is.EqualTo(0.05f).Within(1e-6f),
                    "a fast-forwarded frame advances the capture step, not wall time"
                );

                UnityEngine.Time.captureDeltaTime = 0f;
                Assert.That(
                    AutoplayTimeControl.GameDelta,
                    Is.EqualTo(UnityEngine.Time.unscaledDeltaTime).Within(1e-6f)
                );
            }
            finally
            {
                UnityEngine.Time.captureDeltaTime = original;
            }
        }

        [Test]
        public void SpeedupNeedsMeasurableRealTime()
        {
            Assert.That(AutoplayTimeControl.Speedup(100f, 0f), Is.EqualTo(0f));
            Assert.That(AutoplayTimeControl.Speedup(100f, 10f), Is.EqualTo(10f).Within(1e-4f));
        }
    }
}
