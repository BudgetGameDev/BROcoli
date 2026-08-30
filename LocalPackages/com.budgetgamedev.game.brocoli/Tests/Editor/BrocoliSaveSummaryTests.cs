using System;
using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class BrocoliSaveSummaryTests
    {
        [Test]
        public void PlayTimeDropsTheHourFieldUntilThereIsOne()
        {
            Assert.That(BrocoliSaveSummary.PlayTime(0f), Is.EqualTo("00:00"));
            Assert.That(BrocoliSaveSummary.PlayTime(511f), Is.EqualTo("08:31"));
            Assert.That(BrocoliSaveSummary.PlayTime(3661f), Is.EqualTo("1:01:01"));
        }

        [Test]
        public void PlayTimeSurvivesAGarbledDuration()
        {
            Assert.That(BrocoliSaveSummary.PlayTime(float.NaN), Is.EqualTo("00:00"));
            Assert.That(BrocoliSaveSummary.PlayTime(-12f), Is.EqualTo("00:00"));
        }

        [Test]
        public void AgeReadsInTheCoarsestUnitThatStillSaysSomething()
        {
            DateTime now = new(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc);

            Assert.That(Age(now, now.AddSeconds(-20)), Is.EqualTo("JUST NOW"));
            Assert.That(Age(now, now.AddMinutes(-1)), Is.EqualTo("1 MINUTE AGO"));
            Assert.That(Age(now, now.AddMinutes(-42)), Is.EqualTo("42 MINUTES AGO"));
            Assert.That(Age(now, now.AddHours(-2)), Is.EqualTo("2 HOURS AGO"));
            Assert.That(Age(now, now.AddDays(-3)), Is.EqualTo("3 DAYS AGO"));
        }

        [Test]
        public void AClockThatMovedBackwardsStillReadsAsJustNow()
        {
            DateTime now = new(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc);

            Assert.That(Age(now, now.AddHours(3)), Is.EqualTo("JUST NOW"));
        }

        [Test]
        public void AnUndatedCheckpointSaysSo()
        {
            Assert.That(BrocoliSaveSummary.Age(0L, DateTime.UtcNow), Is.EqualTo("UNKNOWN"));
        }

        [Test]
        public void HeadlineReportsWhatTheRunReached()
        {
            var save = new BrocoliRunSave
            {
                player = new BrocoliPlayerSave { level = 7f },
                game = new BrocoliGameStateSave { score = 1240 },
                dungeon = new BrocoliDungeonSave { roomsVisited = 12 },
            };

            Assert.That(
                BrocoliSaveSummary.Headline(save),
                Is.EqualTo("LV 7   ·   12 ROOMS   ·   SCORE 1,240")
            );
        }

        [Test]
        public void HeadlineCountsASingleRoomInTheSingular()
        {
            var save = new BrocoliRunSave
            {
                player = new BrocoliPlayerSave { level = 1f },
                game = new BrocoliGameStateSave { score = 0 },
                dungeon = new BrocoliDungeonSave { roomsVisited = 1 },
            };

            Assert.That(
                BrocoliSaveSummary.Headline(save),
                Is.EqualTo("LV 1   ·   1 ROOM   ·   SCORE 0")
            );
        }

        private static string Age(DateTime now, DateTime savedAt) =>
            BrocoliSaveSummary.Age(savedAt.Ticks, now);
    }
}
