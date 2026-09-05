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

        private static ScalingSample Room(
            int ring,
            float power,
            float depthScale,
            float healthPowerScale,
            float damageScale,
            int enemies,
            float countScale = 1f,
            float speedScale = 1f
        ) =>
            new(
                ring,
                power,
                depthScale,
                healthPowerScale,
                damageScale,
                countScale,
                speedScale,
                enemies
            );

        [Test]
        public void RecordingIsInertUntilARunIsDriving()
        {
            AutoplayScalingLog.Record(Room(3, 2f, 1.2f, 1.5f, 1.2f, 4));
            Assert.That(AutoplayScalingLog.Count, Is.Zero);

            SetAutoplayActive(true);
            AutoplayScalingLog.Record(Room(3, 2f, 1.2f, 1.5f, 1.2f, 4));

            Assert.That(AutoplayScalingLog.Count, Is.EqualTo(1));
        }

        [Test]
        public void ARoomThatSpawnedNothingIsNotARoomWorthScaling()
        {
            SetAutoplayActive(true);

            AutoplayScalingLog.Record(Room(2, 1f, 1f, 1f, 1f, 0));

            Assert.That(AutoplayScalingLog.Count, Is.Zero);
        }

        [Test]
        public void TheSummaryTakesTheDeepestRingThePeakScalesAndTheFirstRoomsBaseline()
        {
            SetAutoplayActive(true);

            AutoplayScalingLog.Record(Room(1, 1f, 1f, 1f, 1f, 3));
            AutoplayScalingLog.Record(Room(4, 2.5f, 1.1f, 2f, 1.4f, 9));
            AutoplayScalingLog.Record(Room(2, 1.8f, 1.05f, 1.5f, 1.2f, 5));

            ScalingSummary summary = AutoplayScalingLog.Summarize();

            Assert.That(summary.Rooms, Is.EqualTo(3));
            Assert.That(summary.MaxRing, Is.EqualTo(4));
            Assert.That(summary.Enemies, Is.EqualTo(17));
            Assert.That(summary.MostEnemiesInARoom, Is.EqualTo(9));
            Assert.That(summary.FirstPlayerPower, Is.EqualTo(1f).Within(0.001f));
            Assert.That(summary.PeakPlayerPower, Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(summary.FirstHealthScale, Is.EqualTo(1f).Within(0.001f));
            Assert.That(summary.PeakHealthScale, Is.EqualTo(2.2f).Within(0.001f));
            Assert.That(summary.PeakDamageScale, Is.EqualTo(1.4f).Within(0.001f));
            Assert.That(summary.HealthScaleGrowth, Is.EqualTo(2.2f).Within(0.001f));
        }

        /// <summary>
        /// Threat is health times damage, because health alone measures how long a
        /// fight lasts rather than how dangerous it is.
        /// </summary>
        [Test]
        public void ThreatGrowthReadsDamageAndHealthTogetherAgainstTheFirstRoom()
        {
            SetAutoplayActive(true);

            AutoplayScalingLog.Record(Room(1, 1f, 1f, 1f, 1f, 3));
            AutoplayScalingLog.Record(Room(5, 4f, 1.5f, 2f, 1.5f, 8));

            ScalingSummary summary = AutoplayScalingLog.Summarize();

            Assert.That(summary.ThreatGrowth, Is.EqualTo(4.5f).Within(0.001f));
            Assert.That(summary.PowerGrowth, Is.EqualTo(4f).Within(0.001f));
            Assert.That(
                summary.PowerThreatGrowth,
                Is.EqualTo(3f).Within(0.001f),
                "the ring the room sits in is not the player's own growth being answered"
            );
            Assert.That(
                summary.TrackingRatio,
                Is.EqualTo(0.792f).Within(0.01f),
                "threat grew as power to roughly the 0.79th, so upgrades stayed ahead"
            );
        }

        [Test]
        public void APlayerWhoNeverGrewLeavesTrackingAtOneRatherThanDividingByNothing()
        {
            SetAutoplayActive(true);

            AutoplayScalingLog.Record(Room(1, 1f, 1f, 1f, 1f, 3));
            AutoplayScalingLog.Record(Room(3, 1f, 1.2f, 1f, 1f, 4));

            Assert.That(AutoplayScalingLog.Summarize().TrackingRatio, Is.EqualTo(1f));
        }

        [Test]
        public void RoomsBuiltAgainstACeilingAreCountedSoLostHeadroomIsVisible()
        {
            SetAutoplayActive(true);

            AutoplayScalingLog.Record(Room(1, 1f, 1f, 1f, 1f, 3));
            AutoplayScalingLog.Record(Room(6, 40f, 1.5f, EnemyScaling.MaxHealthPowerScale, 2f, 8));
            AutoplayScalingLog.Record(Room(7, 40f, 1.6f, 3f, EnemyScaling.MaxDamagePowerScale, 8));
            AutoplayScalingLog.Record(
                Room(8, 40f, 1.7f, 3f, 2f, 8, EnemyScaling.MaxCountPowerScale)
            );
            AutoplayScalingLog.Record(
                Room(9, 40f, 1.8f, 3f, 2f, 8, 1f, EnemyScaling.MaxSpeedScale)
            );

            Assert.That(
                AutoplayScalingLog.Summarize().SaturatedShare,
                Is.EqualTo(0.6f).Within(0.001f)
            );
        }

        [Test]
        public void MovementSafetyCapDoesNotMeanHealthDamageAndCountStoppedScaling()
        {
            ScalingSummary summary = ScalingSummary.Of(
                new[]
                {
                    Room(1, 1f, 1f, 1f, 1f, 3),
                    Room(4, 4f, 1.5f, 2f, 1.5f, 8, 1.2f, EnemyScaling.MaxSpeedScale),
                }
            );
            Assert.That(summary.SaturatedShare, Is.Zero);
            Assert.That(summary.SpeedCappedShare, Is.EqualTo(0.5f));
            Assert.That(summary.ThreatGrowth, Is.GreaterThan(1f));
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
            Assert.That(summary.ThreatGrowth, Is.EqualTo(1f));
            Assert.That(summary.PowerThreatGrowth, Is.EqualTo(1f));
            Assert.That(summary.PowerGrowth, Is.EqualTo(1f));
            Assert.That(summary.SaturatedShare, Is.Zero);
        }
    }
}
