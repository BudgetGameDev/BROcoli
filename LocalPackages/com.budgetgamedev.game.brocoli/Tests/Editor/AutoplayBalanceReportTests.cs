using System.Globalization;
using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The pacing read-out is what a tuning pass reads, so it has to carry the
    /// numbers a decision needs and degrade legibly on a run that never got far
    /// enough to have them.
    /// </summary>
    public sealed class AutoplayBalanceReportTests
    {
        private static Editor.AutoplayRunner.RunSummary Measured() =>
            new()
            {
                progression = new Editor.AutoplayRunner.ProgressionRecord
                {
                    peakLevel = 11,
                    levels = 10,
                    lives = 3,
                    deaths = 2,
                    deepestRing = 6,
                    rings = 6,
                    deathsPerHour = 8f,
                    secondsPerLevel = 62.5f,
                    earlySecondsPerLevel = 35f,
                    lateSecondsPerLevel = 95f,
                    paceRatio = 2.7f,
                    earlyKillsPerLevel = 4f,
                    lateKillsPerLevel = 18f,
                    secondsPerRing = 110f,
                    meanHealth = 0.68f,
                    lowestHealth = 0.05f,
                    dangerShare = 0.12f,
                    safeShare = 0.3f,
                },
                scaling = new Editor.AutoplayRunner.ScalingRecord
                {
                    rooms = 26,
                    maxRing = 5,
                    enemies = 140,
                    mostEnemiesInARoom = 14,
                    firstPlayerPower = 1f,
                    peakPlayerPower = 3.4f,
                    firstHealthScale = 1f,
                    peakHealthScale = 2.6f,
                    peakDamageScale = 1.5f,
                    healthScaleGrowth = 2.6f,
                    threatGrowth = 3.9f,
                    powerThreatGrowth = 3.2f,
                    powerGrowth = 3.4f,
                    trackingRatio = 1.1f,
                    saturatedShare = 0.2f,
                },
            };

        [Test]
        public void ThePacingReadOutCarriesTheNumbersATuningPassNeeds()
        {
            string described = Editor.AutoplayRunner.DescribeProgression(
                Measured(),
                CultureInfo.InvariantCulture
            );

            Assert.That(described, Does.Contain("level 11 over 10 level-up(s)"));
            Assert.That(described, Does.Contain("62.5s each"));
            Assert.That(described, Does.Contain("35s early"));
            Assert.That(described, Does.Contain("95s late"));
            Assert.That(described, Does.Contain("2.7x"));
            Assert.That(described, Does.Contain("4 to 18 kills per level"));
            Assert.That(described, Does.Contain("68% mean health"));
            Assert.That(described, Does.Contain("5% at the worst"));
            Assert.That(described, Does.Contain("12% of the run in danger"));
            Assert.That(described, Does.Contain("2 death(s) over 3 life/lives"));
            Assert.That(described, Does.Contain("ring 6 over 6 ring(s), 110s each"));
        }

        [Test]
        public void TheScalingReadOutSaysHowDeepTheRunWentAndWhatItMet()
        {
            string described = Editor.AutoplayRunner.DescribeScaling(
                Measured(),
                CultureInfo.InvariantCulture
            );

            Assert.That(described, Does.Contain("26 room(s) out to ring 5"));
            Assert.That(described, Does.Contain("140 enemies (up to 14 at once)"));
            Assert.That(described, Does.Contain("player power 3.4x"));
            Assert.That(described, Does.Contain("enemy health 1x to 2.6x"));
            Assert.That(described, Does.Contain("3.2x threat answering 3.4x player power"));
            Assert.That(described, Does.Contain("exponent 1.1"));
            Assert.That(described, Does.Contain("3.9x counting depth"));
            Assert.That(described, Does.Contain("20% of rooms at a ceiling"));
        }

        [Test]
        public void ARunThatNeverLevelledOrSpawnedAnythingSaysSoRatherThanPrintingZeroes()
        {
            var empty = new Editor.AutoplayRunner.RunSummary();

            Assert.That(
                Editor.AutoplayRunner.DescribeProgression(empty, CultureInfo.InvariantCulture),
                Does.Contain("no level was reached")
            );
            Assert.That(
                Editor.AutoplayRunner.DescribeScaling(empty, CultureInfo.InvariantCulture),
                Does.Contain("no room spawned enemies")
            );
            Assert.That(Editor.AutoplayRunner.DescribeBalance(empty), Does.Contain("in band"));
        }

        [Test]
        public void EveryBandTheRunLeftIsPrintedOnItsOwnLine()
        {
            var summary = new Editor.AutoplayRunner.RunSummary
            {
                balanceFindings = new[] { "level pace too high", "deaths too low" },
            };

            string described = Editor.AutoplayRunner.DescribeBalance(summary);

            Assert.That(described, Does.Contain("level pace too high"));
            Assert.That(described, Does.Contain("deaths too low"));
            Assert.That(described.Split('\n'), Has.Length.EqualTo(2));
        }
    }
}
