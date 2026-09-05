using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class BotStallRecoveryTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void FailedCenterRecoveryIsRetiredInsteadOfExtendingItsOwnDeadline()
        {
            var host = new GameObject("Unreachable room center regression");
            host.SetActive(false);
            try
            {
                BotDriver bot = host.AddComponent<BotDriver>();
                var position = new Vector2(-26f, -2f);
                Vector2Int room = DungeonLayout.RoomAt(position);
                Set(bot, "unwedgeUntil", Time.time + 5f);
                Set(bot, "hasExplorationRoom", false);
                Assert.That(
                    bot.RetireUnreachableCenter(position, DungeonLayout.RoomCenter(room)),
                    Is.True
                );
                Assert.That(Get<float>(bot, "unwedgeUntil"), Is.Zero);
                Assert.That(Get<HashSet<Vector2Int>>(bot, "stagedRooms"), Does.Contain(room));
                Assert.That(Get<HashSet<Vector2Int>>(bot, "visitedRooms"), Is.Empty);
                Assert.That(bot.RetireUnreachableCenter(position, Vector2.zero), Is.False);
                Assert.That(Get<float>(bot, "unwedgeUntil"), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ExplorationTimeoutCannotRestartAnUnproductiveFight()
        {
            var host = new GameObject("Combat clock regression");
            host.SetActive(false);
            try
            {
                BotDriver bot = host.AddComponent<BotDriver>();
                bot.ResetLifeNavigation();
                Set(bot, "lastProgress", Time.time - 1000f);
                Set(bot, "lastCombatProgress", Time.time - 1000f);
                typeof(BotDriver)
                    .GetMethod("GetExplorationTarget", Hidden)
                    .Invoke(bot, new object[] { Vector2.zero });
                Assert.That(Get<float>(bot, "lastProgress"), Is.EqualTo(Time.time));

                var enemy = new BotDriver.EnemyObservation(
                    1,
                    0,
                    9f,
                    Vector2.right * 9f,
                    Vector2.right * 9f,
                    Vector2.right * 9f,
                    Vector2.zero
                );
                var situation = (BotSituation)
                    typeof(BotDriver)
                        .GetMethod("Observe", Hidden)
                        .Invoke(bot, new object[] { Vector2.zero, enemy });
                Assert.That(
                    situation.EngagementStalled,
                    Is.True,
                    "retrying a doorway is not evidence that a stalled fight started making progress"
                );
                Assert.That(
                    BotDecisionPolicy.ChooseIntent(
                        situation,
                        new BotTuning(2.5f, 5, 0.4f, 14f, 16f),
                        BotIntent.Engage
                    ),
                    Is.EqualTo(BotIntent.Explore)
                );
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NewLifeForgetsThePreviousCharactersRoomsAndRecoveryTargets()
        {
            var host = new GameObject("Respawn navigation regression");
            host.SetActive(false);
            try
            {
                BotDriver bot = host.AddComponent<BotDriver>();
                Get<HashSet<Vector2Int>>(bot, "visitedRooms").Add(Vector2Int.one);
                Get<HashSet<Vector2Int>>(bot, "stagedRooms").Add(Vector2Int.one);
                Set(bot, "hasOccupiedRoom", true);
                Set(bot, "hasExplorationRoom", true);
                Set(bot, "recoveryUntil", Time.time + 20f);
                Set(bot, "lastDodge", Vector2.one);

                bot.ResetLifeNavigation();

                Assert.That(Get<HashSet<Vector2Int>>(bot, "visitedRooms"), Is.Empty);
                Assert.That(Get<HashSet<Vector2Int>>(bot, "stagedRooms"), Is.Empty);
                Assert.That(Get<bool>(bot, "hasOccupiedRoom"), Is.False);
                Assert.That(Get<bool>(bot, "hasExplorationRoom"), Is.False);
                Assert.That(Get<float>(bot, "recoveryUntil"), Is.Zero);
                Assert.That(Get<Vector2>(bot, "lastDodge"), Is.EqualTo(Vector2.zero));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [TestCase(13.8f, 0.35f)]
        [TestCase(6.7f, 0.076f)]
        public void AStalledLowHealthBotLeavesOnceClearOfImmediateDanger(
            float distance,
            float health
        )
        {
            var tuning = new BotTuning(2.5f, 5, 0.4f, 14f, 16f);
            var distant = new BotSituation(
                true,
                distance,
                0,
                health,
                false,
                false,
                float.PositiveInfinity,
                float.PositiveInfinity,
                true
            );
            Assert.That(
                BotDecisionPolicy.ChooseIntent(distant, tuning, BotIntent.Retreat),
                Is.EqualTo(BotIntent.Explore)
            );
            var dangerous = new BotSituation(
                true,
                2f,
                1,
                0.35f,
                false,
                false,
                float.PositiveInfinity,
                float.PositiveInfinity,
                true
            );
            Assert.That(
                BotDecisionPolicy.ChooseIntent(dangerous, tuning, BotIntent.Explore),
                Is.EqualTo(BotIntent.Retreat)
            );
        }

        [Test]
        public void AStalledFightStillEscapesEncirclement()
        {
            var situation = new BotSituation(
                true,
                6.7f,
                0,
                0.076f,
                false,
                false,
                float.PositiveInfinity,
                float.PositiveInfinity,
                true,
                0.6f
            );
            Assert.That(
                BotDecisionPolicy.ChooseIntent(
                    situation,
                    new BotTuning(2.5f, 5, 0.4f, 14f, 16f),
                    BotIntent.Explore
                ),
                Is.EqualTo(BotIntent.Retreat)
            );
        }

        [TestCase((int)BotIntent.Explore)]
        [TestCase((int)BotIntent.Loot)]
        [TestCase((int)BotIntent.Collect)]
        public void JourneyRoutesKeepDoorwayPriorityWhenDistantEnemiesAreSensed(int intentValue)
        {
            var intent = (BotIntent)intentValue;
            float weight = BotDriver.OpennessWeight(intent, 1);
            Assert.That(weight, Is.EqualTo(BotDriver.OpennessWeight(intent, 0)));
            Vector2 desired = Vector2.right;
            Vector2 openDetour = new Vector2(0.5f, Mathf.Sqrt(0.75f));
            Assert.That(
                BotDriver.ScoreHeading(desired, desired, Vector2.zero, 0.1f, weight),
                Is.GreaterThan(
                    BotDriver.ScoreHeading(openDetour, desired, Vector2.zero, 1f, weight)
                ),
                "open ground must not outvote the complete route to the next doorway"
            );
        }

        [TestCase((int)BotIntent.Engage)]
        [TestCase((int)BotIntent.Retreat)]
        [TestCase((int)BotIntent.Dodge)]
        public void ActiveCombatStillPrefersMoreOpenGround(int intentValue)
        {
            var intent = (BotIntent)intentValue;
            Assert.That(
                BotDriver.OpennessWeight(intent, 1),
                Is.GreaterThan(BotDriver.OpennessWeight(intent, 0))
            );
        }

        [Test]
        public void RejectedDoorIsNotSelectedAgainOrFalselyCountedAsVisited()
        {
            var layout = new DungeonLayout(209500);
            Vector2Int from = default;
            bool found = false;
            for (int x = -5; x <= 5 && !found; x++)
            {
                from = layout.ClampToPlayableBand(new Vector2Int(x, 0));
                int exits = 0;
                for (int d = 0; d < 4; d++)
                    if (layout.IsPlayableDoorOpen(from, d))
                        exits++;
                found = exits > 1;
            }
            Assert.That(found, Is.True, "fixture needs a junction");
            var visited = new HashSet<Vector2Int> { from };
            Assert.That(
                BotExplorationPolicy.TryFindFrontier(
                    layout,
                    from,
                    visited,
                    1f,
                    out _,
                    out Vector2Int first
                ),
                Is.True
            );
            var rejected = new HashSet<Vector2Int> { first };
            Assert.That(
                BotExplorationPolicy.TryFindFrontier(
                    layout,
                    from,
                    visited,
                    1f,
                    out _,
                    out Vector2Int alternative,
                    rejected
                ),
                Is.True
            );
            Assert.That(alternative, Is.Not.EqualTo(first));
            Assert.That(
                visited.SetEquals(new[] { from }),
                Is.True,
                "an unreachable exit is not a visited room"
            );
            int direction = BotExplorationPolicy.ChooseDirection(
                layout,
                from,
                visited,
                1f,
                -1,
                rejected
            );
            Assert.That(from + DungeonLayout.DirectionOffsets[direction], Is.Not.EqualTo(first));
        }

        private static T Get<T>(BotDriver bot, string name) =>
            (T)typeof(BotDriver).GetField(name, Hidden).GetValue(bot);

        private static void Set(BotDriver bot, string name, object value) =>
            typeof(BotDriver).GetField(name, Hidden).SetValue(bot, value);
    }
}
