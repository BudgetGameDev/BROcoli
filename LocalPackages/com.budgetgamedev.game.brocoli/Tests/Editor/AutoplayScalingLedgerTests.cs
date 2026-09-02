using System.Reflection;
using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The scaling ledger is the only record of how hard the dungeon set each room,
    /// which is what makes a difficulty verdict actionable rather than a mood. It has
    /// to stay inert during ordinary play and add its rooms up honestly.
    /// </summary>
    public sealed class AutoplayScalingLedgerTests
    {
        [SetUp]
        [TearDown]
        public void ClearLedger()
        {
            SetAutoplayActive(false);
            AutoplayScalingLog.Reset();
        }

        private static void SetAutoplayActive(bool active) =>
            typeof(AutoplayController)
                .GetField("<IsActive>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, active);

        [Test]
        public void RecordingIsInertUntilARunIsDriving()
        {
            AutoplayScalingLog.Record(3, 2f, 1.5f, 1.2f, 4);
            Assert.That(AutoplayScalingLog.Count, Is.Zero);

            SetAutoplayActive(true);
            AutoplayScalingLog.Record(3, 2f, 1.5f, 1.2f, 4);

            Assert.That(AutoplayScalingLog.Count, Is.EqualTo(1));
        }

        [Test]
        public void ARoomThatSpawnedNothingIsNotARoomWorthScaling()
        {
            SetAutoplayActive(true);

            AutoplayScalingLog.Record(2, 1f, 1f, 1f, 0);

            Assert.That(AutoplayScalingLog.Count, Is.Zero);
        }

        [Test]
        public void TheSummaryTakesTheDeepestRingThePeakScalesAndTheFirstRoomsBaseline()
        {
            SetAutoplayActive(true);

            AutoplayScalingLog.Record(1, 1f, 1f, 1f, 3);
            AutoplayScalingLog.Record(4, 2.5f, 2.2f, 1.4f, 9);
            AutoplayScalingLog.Record(2, 1.8f, 1.6f, 1.2f, 5);

            ScalingSummary summary = AutoplayScalingLog.Summarize();

            Assert.That(summary.Rooms, Is.EqualTo(3));
            Assert.That(summary.MaxRing, Is.EqualTo(4));
            Assert.That(summary.Enemies, Is.EqualTo(17));
            Assert.That(summary.MostEnemiesInARoom, Is.EqualTo(9));
            Assert.That(summary.PeakPlayerPower, Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(summary.FirstHealthScale, Is.EqualTo(1f).Within(0.001f));
            Assert.That(summary.PeakHealthScale, Is.EqualTo(2.2f).Within(0.001f));
            Assert.That(summary.PeakDamageScale, Is.EqualTo(1.4f).Within(0.001f));
            Assert.That(summary.HealthScaleGrowth, Is.EqualTo(2.2f).Within(0.001f));
        }

        [Test]
        public void ARunThatSpawnedNoRoomsSummarisesAsUnscaledRatherThanAsZero()
        {
            ScalingSummary summary = AutoplayScalingLog.Summarize();

            Assert.That(summary.Rooms, Is.Zero);
            Assert.That(summary.MaxRing, Is.Zero);
            Assert.That(summary.FirstHealthScale, Is.EqualTo(1f));
            Assert.That(summary.PeakHealthScale, Is.EqualTo(1f));
            Assert.That(summary.HealthScaleGrowth, Is.EqualTo(1f));
        }
    }
}
