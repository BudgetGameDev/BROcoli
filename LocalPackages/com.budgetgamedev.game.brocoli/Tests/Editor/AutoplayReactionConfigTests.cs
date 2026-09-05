using System;
using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class AutoplayReactionConfigTests
    {
        [Test]
        public void BalanceUsesExplicitReferenceHypothesisWhileOtherTiersPreserveStress()
        {
            var balance = AutoplayConfig.FromArguments(new[] { "--tier=balance" }, _ => null);
            Assert.That(balance.ReactionProfile, Is.EqualTo("reference"));
            Assert.That(balance.ReactionDelaySeconds, Is.EqualTo(.2f));
            Assert.That(balance.ObservationIntervalSeconds, Is.EqualTo(.1f));
            var stress = AutoplayConfig.FromArguments(new[] { "--tier=coverage" }, _ => null);
            Assert.That(stress.ReactionProfile, Is.EqualTo("stress"));
            Assert.That(stress.ReactionDelaySeconds, Is.Zero);
            Assert.That(stress.ObservationIntervalSeconds, Is.Zero);
        }

        [Test]
        public void ExplicitTimingOverridesProfileRegardlessOfArgumentOrderAndEnvironmentWins()
        {
            var config = AutoplayConfig.FromArguments(
                new[]
                {
                    "--reaction-delay=0.4",
                    "--tier=balance",
                    "--reaction-profile=stress",
                    "--observation-interval=0.15",
                },
                key => key == "BROCOLI_REACTION_DELAY" ? "0.3" : null
            );
            Assert.That(config.ReactionProfile, Is.EqualTo("stress"));
            Assert.That(config.ReactionDelaySeconds, Is.EqualTo(.3f));
            Assert.That(config.ObservationIntervalSeconds, Is.EqualTo(.15f));
            var reference = AutoplayConfig.FromArguments(
                new[] { "--reaction-profile=stress" },
                key => key == "BROCOLI_REACTION_PROFILE" ? "reference" : null
            );
            Assert.That(reference.ReactionDelaySeconds, Is.EqualTo(.2f));
        }

        [TestCase("--reaction-delay=NaN")]
        [TestCase("--reaction-delay=-1")]
        [TestCase("--observation-interval=3")]
        [TestCase("--reaction-profile=typo")]
        public void InvalidReactionConfigurationCannotSilentlyChangeTheBalanceModel(string argument)
        {
            Assert.Throws<ArgumentException>(() =>
                AutoplayConfig.FromArguments(new[] { argument }, _ => null)
            );
        }

        [Test]
        public void EditorRequestForwardsAllReactionOptionsToThePlayer()
        {
            var request = Editor.AutoplayRunRequest.FromArguments(
                new[]
                {
                    "-reaction-profile",
                    "reference",
                    "-reaction-delay",
                    "0.2",
                    "-observation-interval",
                    "0.1",
                },
                () => "test"
            );
            Assert.That(
                request.Overrides,
                Is.EquivalentTo(
                    new[]
                    {
                        "--reaction-profile=reference",
                        "--reaction-delay=0.2",
                        "--observation-interval=0.1",
                    }
                )
            );
        }
    }
}
