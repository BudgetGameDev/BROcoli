using BudgetGameDev.Autoplay;
using NUnit.Framework;

namespace BudgetGameDev.Autoplay.Tests
{
    public sealed class AutoplayCoreTests
    {
        private readonly struct Scores : IUtilityPolicy<int>
        {
            private readonly float first;
            private readonly float second;

            internal Scores(float first, float second)
            {
                this.first = first;
                this.second = second;
            }

            public float Score(int action) => action == 0 ? first : second;
        }

        [Test]
        public void SharedSelectionKeepsPreviousActionUntilTheMarginIsBeaten()
        {
            var actions = new[] { 0, 1 };
            Assert.That(UtilitySelection.Choose(actions, new Scores(10, 14), 0, 6, -1), Is.Zero);
            Assert.That(
                UtilitySelection.Choose(actions, new Scores(10, 17), 0, 6, -1),
                Is.EqualTo(1)
            );
            Assert.That(
                UtilitySelection.Choose(actions, new Scores(float.NaN, 2), 0, 6, -1),
                Is.EqualTo(1)
            );
            Assert.That(
                UtilitySelection.Choose(
                    actions,
                    new Scores(float.NegativeInfinity, float.NaN),
                    0,
                    6,
                    -1
                ),
                Is.EqualTo(-1)
            );
        }

        [Test]
        public void SharedFeatureCoverageRecordsEventsAndPreservesRequiredOrder()
        {
            var ledger = new FeatureLedger();
            Assert.That(ledger.Record(null), Is.Zero);
            Assert.That(ledger.Record("jump"), Is.EqualTo(1));
            Assert.That(ledger.Record("jump"), Is.EqualTo(2));
            Assert.That(
                ledger.Missing(new[] { "jump", "land", "win" }),
                Is.EqualTo(new[] { "land", "win" })
            );
            ledger.Clear();
            Assert.That(ledger.Count("jump"), Is.Zero);
        }

        [Test]
        public void RareDeathsAreMeasuredAcrossSeedsWithoutDemandingEveryRunDies()
        {
            var cohort = new BalanceCohort();
            cohort.Add(1, 900f, 0, new string[0]);
            cohort.Add(2, 900f, 1, new string[0]);
            cohort.Add(3, 900f, 0, new string[0]);
            Assert.That(cohort.Evaluate(0.4f, 8f), Is.Empty);
            Assert.That(cohort.DeathsPerHour, Is.EqualTo(4f / 3f).Within(0.001f));
        }

        [Test]
        public void CohortCannotAverageAwayAnUnplayableSeed()
        {
            var cohort = new BalanceCohort();
            cohort.Add(1, 900f, 0, new string[0]);
            cohort.Add(2, 900f, 1, new[] { "level pace too high" });
            cohort.Add(3, 900f, 0, new string[0]);
            Assert.That(
                cohort.Evaluate(0.4f, 8f),
                Has.Some.Contains("seed 2: level pace too high")
            );
        }

        [Test]
        public void RepeatedSeedAndShortExposureCannotClaimBalance()
        {
            var cohort = new BalanceCohort();
            cohort.Add(1, 900f, 1, new string[0]);
            cohort.Add(1, 900f, 1, new string[0]);
            Assert.That(cohort.Runs, Is.EqualTo(1));
            Assert.That(cohort.Duration, Is.EqualTo(900f));
            Assert.That(cohort.Evaluate(0.4f, 8f), Has.Count.EqualTo(2));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void InvalidExposureDoesNotBecomeAPassingCohort(float duration)
        {
            var cohort = new BalanceCohort();
            cohort.Add(1, duration, 1, new string[0]);
            Assert.That(cohort.Duration, Is.Zero);
            Assert.That(cohort.Evaluate(0.4f, 8f), Has.Some.Contains("invalid duration"));
        }

        [Test]
        public void CompletelySafeCohortStillFailsTheLowerDifficultyBound()
        {
            var cohort = new BalanceCohort();
            for (int seed = 0; seed < 3; seed++)
                cohort.Add(seed, 900f, 0, new string[0]);
            Assert.That(cohort.Evaluate(0.4f, 8f), Has.Some.Contains("deaths out of band"));
        }
    }
}
