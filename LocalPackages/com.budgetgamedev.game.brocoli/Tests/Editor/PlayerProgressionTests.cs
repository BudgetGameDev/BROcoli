using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The experience curve decides whether a run keeps moving, so its shape is
    /// worth pinning: every level dearer than the last, the growth easing rather
    /// than compounding, and a tenth level a session can actually reach.
    /// </summary>
    public sealed class PlayerProgressionTests
    {
        [Test]
        public void TheFirstLevelCostsTheBaseAndEveryLevelAfterCostsMore()
        {
            Assert.That(
                PlayerProgression.ExperienceForLevel(1),
                Is.EqualTo(PlayerProgression.BaseExperience)
            );
            Assert.That(
                PlayerProgression.ExperienceForLevel(0),
                Is.EqualTo(PlayerProgression.BaseExperience),
                "a level below the first is clamped rather than free"
            );

            float previous = 0f;
            for (int level = 1; level <= 30; level++)
            {
                float required = PlayerProgression.ExperienceForLevel(level);
                Assert.That(required, Is.GreaterThan(previous), $"level {level}");
                previous = required;
            }
        }

        [Test]
        public void GrowthEasesFromItsOpeningRateDownTowardTheFloor()
        {
            Assert.That(PlayerProgression.GrowthAt(1), Is.EqualTo(PlayerProgression.GrowthStart));
            Assert.That(
                PlayerProgression.GrowthAt(0),
                Is.EqualTo(PlayerProgression.GrowthStart),
                "the opening growth is clamped, not extrapolated backwards"
            );

            float previous = PlayerProgression.GrowthAt(1);
            for (int level = 2; level <= 40; level++)
            {
                float growth = PlayerProgression.GrowthAt(level);
                Assert.That(growth, Is.LessThan(previous), $"level {level}");
                Assert.That(
                    growth,
                    Is.GreaterThan(PlayerProgression.GrowthFloor),
                    $"level {level}"
                );
                previous = growth;
            }

            Assert.That(previous, Is.EqualTo(PlayerProgression.GrowthFloor).Within(0.01f));
            Assert.That(
                PlayerProgression.GrowthStart,
                Is.LessThan(2f),
                "the curve is a slope: even its steepest level costs less than double"
            );
        }

        [Test]
        public void ReachingTheTenthLevelCostsFarLessThanADoublingCurveAsked()
        {
            float doubling = 0f;
            float step = PlayerProgression.BaseExperience;
            for (int level = 1; level < 10; level++)
            {
                doubling += step;
                step *= 2f;
            }

            Assert.That(PlayerProgression.ExperienceToReach(1), Is.Zero);
            Assert.That(PlayerProgression.ExperienceToReach(10), Is.LessThan(doubling / 4f));
            Assert.That(
                PlayerProgression.ExperienceToReach(10),
                Is.GreaterThan(PlayerProgression.ExperienceForLevel(9)),
                "the total is the sum of the levels below it"
            );
        }
    }
}
