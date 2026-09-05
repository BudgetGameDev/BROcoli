using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class BotNaturalMovementTests
    {
        private static readonly BotTuning Tuning = new(2.5f, 5, 0.4f, 14f, 16f);

        [Test]
        public void DoorwayEntryDefersLootUntilThePlayerIsSafelyInside()
        {
            var doorway = new BotDoorwayCommitment();
            Vector2 before = new(12.5f, 0f);
            var calm = new BotSituation(false, float.PositiveInfinity, 0, 1, false, false, 2, 1);
            doorway.Begin(before, Vector2Int.zero, Vector2Int.right, 0);
            Assert.That(
                doorway.Resolve(BotIntent.Collect, calm, Tuning, before, 0.1f),
                Is.EqualTo(BotIntent.Explore)
            );
            Assert.That(doorway.TryGoal(new Vector2(14.2f, 0), 0.5f, out Vector2 goal), Is.True);
            Assert.That(goal, Is.EqualTo(new Vector2(28, 0)));
            Assert.That(
                doorway.Resolve(BotIntent.Loot, calm, Tuning, new Vector2(16.1f, 0), 1f),
                Is.EqualTo(BotIntent.Loot)
            );
        }

        [Test]
        public void DangerAndDelayedDodgeCanInterruptAnEntryCommitment()
        {
            var doorway = new BotDoorwayCommitment();
            Vector2 before = new(12.5f, 0);
            doorway.Begin(before, Vector2Int.zero, Vector2Int.right, 0);
            var danger = new BotSituation(true, 1f, 1, 1, false, false, 1, 1);
            var projectile = new BotSituation(
                false,
                float.PositiveInfinity,
                0,
                1,
                true,
                false,
                1,
                1
            );
            Assert.That(
                doorway.Resolve(BotIntent.Retreat, danger, Tuning, before, 0.1f),
                Is.EqualTo(BotIntent.Retreat)
            );
            Assert.That(
                doorway.Resolve(BotIntent.Dodge, projectile, Tuning, before, 0.2f),
                Is.EqualTo(BotIntent.Dodge)
            );
        }

        [Test]
        public void ABlockedDoorwayAttemptExpiresWithoutRearmingItsOwnDeadline()
        {
            var doorway = new BotDoorwayCommitment();
            Vector2 before = new(12.5f, 0);
            doorway.Begin(before, Vector2Int.zero, Vector2Int.right, 0);
            Assert.That(doorway.TryGoal(before, 2.1f, out _), Is.False);
            doorway.Begin(before, Vector2Int.zero, Vector2Int.right, 2.2f);
            Assert.That(doorway.TryGoal(before, 2.3f, out _), Is.False);
            doorway.Clear();
            doorway.Begin(before, Vector2Int.zero, Vector2Int.right, 3f);
            Assert.That(
                doorway.TryGoal(before, 3.1f, out _),
                Is.True,
                "new lives start without inherited doorway state"
            );
        }
    }
}
