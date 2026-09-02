using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// How a run asks for the journey and what it is graded on afterwards. The tier
    /// is the only one that writes real save slots, so which tier turns it on is part
    /// of the harness's promise not to cost a player a run.
    /// </summary>
    public sealed partial class AutoplaySaveJourneyTests
    {
        [Test]
        public void TheJourneyTierDrivesTheMenusAndOwnsThem()
        {
            var config = new AutoplayConfig();

            AutoplayTiers.Apply(config, "journey");

            Assert.That(config.Scenario, Is.EqualTo(AutoplayFeatures.JourneyScenario));
            Assert.That(config.DriveMenus, Is.True, "a journey starts at the main menu");
            Assert.That(config.ExerciseSaveJourney, Is.True);
            Assert.That(
                config.ExerciseFeatures,
                Is.False,
                "the coverage sweep would be pressing the same pause menu"
            );
        }

        [Test]
        public void OnlyTheJourneyTierCheckpointsIntoARealSlot()
        {
            foreach (AutoplayTier tier in AutoplayTiers.All)
            {
                var config = new AutoplayConfig();
                AutoplayTiers.Apply(config, tier.Name);
                Assert.That(
                    config.ExerciseSaveJourney,
                    Is.EqualTo(tier.Name == "journey"),
                    tier.Name
                );
            }
        }

        [Test]
        public void TheJourneyIsGradedOnItsOwnStepsRatherThanOnCoverage()
        {
            Assert.That(
                AutoplayFeatures.RequiredFor(AutoplayFeatures.JourneyScenario),
                Is.EqualTo(AutoplayFeatures.SaveJourney)
            );
            Assert.That(
                AutoplayFeatures.RequiredFor("coverage"),
                Is.EqualTo(AutoplayFeatures.Required)
            );
            Assert.That(
                AutoplayFeatures.SaveJourney,
                Does.Contain(AutoplayFeatures.GameOverShown)
                    .And.Contains(AutoplayFeatures.GameOverRestart),
                "dying is something the journey drives, not something it hopes for"
            );
            Assert.That(
                AutoplayFeatureLog.Missing(AutoplayFeatures.SaveJourney),
                Is.EqualTo(AutoplayFeatures.SaveJourney)
            );
        }

        [Test]
        public void TheJourneyCanBeAskedForAndRefusedByName()
        {
            Assert.That(Parse("--tier=journey").ExerciseSaveJourney, Is.True);
            Assert.That(Parse("--tier=journey", "--no-journey").ExerciseSaveJourney, Is.False);
            Assert.That(Parse("--journey").ExerciseSaveJourney, Is.True);
            Assert.That(Parse("--tier=coverage").ExerciseSaveJourney, Is.False);
        }

        private static AutoplayConfig Parse(params string[] arguments) =>
            AutoplayConfig.FromArguments(arguments, _ => null);
    }
}
