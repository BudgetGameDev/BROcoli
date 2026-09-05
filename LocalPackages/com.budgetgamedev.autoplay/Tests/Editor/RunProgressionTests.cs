using BudgetGameDev.Autoplay;
using NUnit.Framework;

namespace BudgetGameDev.Autoplay.Tests
{
    /// <summary>
    /// The progression ledger is what lets a run be graded on difficulty rather than
    /// on survival, so what it makes of a sequence of samples has to be exact: which
    /// level cost what, how much of the run was spent hurt, and where one life ends
    /// and the next begins.
    /// </summary>
    public sealed class RunProgressionTests
    {
        [Test]
        public void ItTimesEachLevelAndCountsTheKillsThatBoughtIt()
        {
            var progression = new RunProgression();
            progression.Sample(0f, 1f, 100f, 100f, 0, 0);
            progression.Sample(30f, 2f, 80f, 100f, 6, 0);
            progression.Sample(90f, 3f, 60f, 100f, 20, 0);

            Assert.That(progression.Steps.Count, Is.EqualTo(2));
            Assert.That(progression.Steps[0].Level, Is.EqualTo(2));
            Assert.That(progression.Steps[0].Time, Is.EqualTo(30f));
            Assert.That(progression.Steps[0].Seconds, Is.EqualTo(30f));
            Assert.That(progression.Steps[0].Kills, Is.EqualTo(6));
            Assert.That(progression.Steps[1].Seconds, Is.EqualTo(60f));
            Assert.That(progression.Steps[1].Kills, Is.EqualTo(14));
            Assert.That(progression.PeakLevel, Is.EqualTo(3));
        }

        [Test]
        public void LevelsThatLandInTheSameSampleShareTheGapRatherThanArrivingFree()
        {
            var progression = new RunProgression();
            progression.Sample(0f, 1f, 100f, 100f, 0, 0);
            progression.Sample(40f, 3f, 100f, 100f, 10, 0);

            Assert.That(progression.Steps.Count, Is.EqualTo(2));
            Assert.That(progression.Steps[0].Seconds, Is.EqualTo(20f));
            Assert.That(progression.Steps[1].Seconds, Is.EqualTo(20f));
            Assert.That(progression.Steps[0].Kills, Is.EqualTo(5));
            Assert.That(progression.Steps[1].Kills, Is.EqualTo(5));
        }

        [Test]
        public void ANewLifeRestartsTheLevelClockInsteadOfBillingItForTheLastOne()
        {
            var progression = new RunProgression();
            progression.Sample(0f, 1f, 100f, 100f, 0, 0);
            progression.Sample(60f, 2f, 20f, 100f, 8, 0);
            progression.NoteDeath();
            progression.Sample(120f, 0f, 0f, 0f, 8, 0);
            progression.Sample(150f, 2f, 90f, 100f, 12, 0);

            Assert.That(progression.Deaths, Is.EqualTo(1));
            Assert.That(progression.Lives, Is.EqualTo(2));
            Assert.That(progression.PeakLevel, Is.EqualTo(2));
            Assert.That(progression.Steps.Count, Is.EqualTo(2));
            Assert.That(
                progression.Steps[1].Seconds,
                Is.EqualTo(30f),
                "the second life's first level is timed from the restart"
            );
            Assert.That(progression.Steps[1].Kills, Is.EqualTo(4));
        }

        [Test]
        public void ItTimesEachRingTheRunPushedOutTo()
        {
            var progression = new RunProgression();
            progression.Sample(0f, 1f, 100f, 100f, 0, 0);
            progression.Sample(60f, 1f, 100f, 100f, 2, 1);
            progression.Sample(160f, 1f, 100f, 100f, 6, 2);

            ProgressionSummary summary = progression.Summarize(160f);

            Assert.That(summary.DeepestRing, Is.EqualTo(2));
            Assert.That(summary.Rings, Is.EqualTo(2));
            Assert.That(summary.SecondsPerRing, Is.EqualTo(80f).Within(0.001f));
        }

        [Test]
        public void WalkingBackThroughAShallowerRingDoesNotUndoReachingADeeperOne()
        {
            var progression = new RunProgression();
            progression.Sample(0f, 1f, 100f, 100f, 0, 0);
            progression.Sample(50f, 1f, 100f, 100f, 2, 2);
            progression.Sample(80f, 1f, 100f, 100f, 4, 1);
            progression.Sample(110f, 1f, 100f, 100f, 6, 2);

            ProgressionSummary summary = progression.Summarize(110f);

            Assert.That(summary.DeepestRing, Is.EqualTo(2));
            Assert.That(summary.Rings, Is.EqualTo(2), "the second visit to ring 2 costs nothing");
            Assert.That(summary.SecondsPerRing, Is.EqualTo(25f).Within(0.001f));
        }

        [Test]
        public void ANewLifeStartsItsOwnDescentRatherThanResumingTheLastOnes()
        {
            var progression = new RunProgression();
            progression.Sample(0f, 1f, 100f, 100f, 0, 0);
            progression.Sample(40f, 2f, 100f, 100f, 4, 1);
            progression.NoteDeath();
            progression.Sample(100f, 1f, 100f, 100f, 4, 0);
            progression.Sample(130f, 1f, 100f, 100f, 6, 1);

            ProgressionSummary summary = progression.Summarize(130f);

            Assert.That(summary.DeepestRing, Is.EqualTo(1));
            Assert.That(summary.Rings, Is.EqualTo(2));
            Assert.That(
                summary.SecondsPerRing,
                Is.EqualTo(35f).Within(0.001f),
                "40s in the first life and 30s in the second, not 40s and 130s"
            );
        }

        [Test]
        public void ItSummarisesPressureAsMeanHealthTheWorstMomentAndTimeInDanger()
        {
            var progression = new RunProgression();
            progression.Sample(0f, 1f, 100f, 100f, 0, 0);
            progression.Sample(10f, 1f, 20f, 100f, 1, 0);
            progression.Sample(20f, 1f, 60f, 100f, 2, 0);
            progression.Sample(30f, 1f, 100f, 100f, 3, 0);

            ProgressionSummary summary = progression.Summarize(30f);

            Assert.That(summary.MeanHealthFraction, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(summary.LowestHealthFraction, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(summary.DangerShare, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(summary.SafeShare, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(summary.Duration, Is.EqualTo(30f));
            Assert.That(summary.Levels, Is.Zero);
            Assert.That(summary.PaceRatio, Is.EqualTo(1f), "no levels means no shape to report");
        }

        [Test]
        public void ARunWithNoSamplesReportsFullHealthAndNoDanger()
        {
            ProgressionSummary summary = new RunProgression().Summarize(0f);

            Assert.That(summary.MeanHealthFraction, Is.EqualTo(1f));
            Assert.That(summary.LowestHealthFraction, Is.EqualTo(1f));
            Assert.That(summary.DangerShare, Is.Zero);
            Assert.That(summary.SafeShare, Is.EqualTo(1f));
            Assert.That(summary.PeakLevel, Is.EqualTo(1));
            Assert.That(summary.Lives, Is.EqualTo(1));
            Assert.That(summary.DeathsPerHour, Is.Zero);
        }

        [Test]
        public void EarlyAndLateLevelsAreAveragedSeparatelySoTheCurveShapeIsVisible()
        {
            const int earlyLevels = 4;
            var progression = new RunProgression(earlyLevels);
            progression.Sample(0f, 1f, 100f, 100f, 0, 0);

            float time = 0f;
            int kills = 0;
            for (int level = 2; level <= earlyLevels; level++)
            {
                time += 20f;
                kills += 4;
                progression.Sample(time, level, 100f, 100f, kills, 0);
            }
            for (int level = earlyLevels + 1; level <= 8; level++)
            {
                time += 80f;
                kills += 20;
                progression.Sample(time, level, 100f, 100f, kills, 0);
            }

            ProgressionSummary summary = progression.Summarize(time);

            Assert.That(summary.EarlySecondsPerLevel, Is.EqualTo(20f).Within(0.001f));
            Assert.That(summary.LateSecondsPerLevel, Is.EqualTo(80f).Within(0.001f));
            Assert.That(summary.EarlyKillsPerLevel, Is.EqualTo(4f).Within(0.001f));
            Assert.That(summary.LateKillsPerLevel, Is.EqualTo(20f).Within(0.001f));
            Assert.That(summary.PaceRatio, Is.EqualTo(4f).Within(0.001f));
            Assert.That(summary.PeakLevel, Is.EqualTo(8));
        }

        [Test]
        public void DeathsAreReportedAgainstTheClockRatherThanAsABareCount()
        {
            var progression = new RunProgression();
            progression.NoteDeath();
            progression.NoteDeath();

            Assert.That(progression.Summarize(1800f).DeathsPerHour, Is.EqualTo(4f).Within(0.001f));
        }
    }
}
