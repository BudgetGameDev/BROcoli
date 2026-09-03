using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class AutoplayDecisionTests
    {
        private static readonly BotTuning Tuning = new(2.5f, 5, 0.4f, 14f, 16f);

        private static BotSituation Situation(
            bool hasEnemies,
            float nearest,
            int close,
            float health,
            bool projectile
        ) =>
            new(
                hasEnemies,
                nearest,
                close,
                health,
                projectile,
                false,
                float.PositiveInfinity,
                float.PositiveInfinity
            );

        [Test]
        public void AgentExploresWhenNoThreatIsVisible()
        {
            var situation = Situation(false, float.PositiveInfinity, 0, 1f, false);

            BotIntent intent = BotDecisionPolicy.ChooseIntent(situation, Tuning, BotIntent.Waiting);

            Assert.That(intent, Is.EqualTo(BotIntent.Explore));
        }

        [Test]
        public void ProjectileDodgeTakesPriorityOverCombat()
        {
            var situation = Situation(true, 4f, 2, 1f, true);

            BotIntent intent = BotDecisionPolicy.ChooseIntent(situation, Tuning, BotIntent.Waiting);

            Assert.That(intent, Is.EqualTo(BotIntent.Dodge));
        }

        [TestCase(2.4f, 1, 1f)]
        [TestCase(4f, 5, 1f)]
        [TestCase(4f, 1, 0.3f)]
        public void ImmediateDangerMakesTheAgentRetreat(
            float nearestDistance,
            int closeEnemies,
            float healthFraction
        )
        {
            var situation = Situation(true, nearestDistance, closeEnemies, healthFraction, false);

            BotIntent intent = BotDecisionPolicy.ChooseIntent(situation, Tuning, BotIntent.Waiting);

            Assert.That(intent, Is.EqualTo(BotIntent.Retreat));
        }

        [Test]
        public void ExplorationPrefersTheOnlyUnvisitedOpenRoom()
        {
            var layout = new DungeonLayout(314159);
            Vector2Int room = layout.ClampToPlayableBand(Vector2Int.zero);
            var visited = new HashSet<Vector2Int> { room };
            int expected = -1;

            for (int direction = 0; direction < DungeonLayout.DirectionOffsets.Length; direction++)
            {
                if (!layout.IsPlayableDoorOpen(room, direction))
                    continue;
                if (expected < 0)
                    expected = direction;
                else
                    visited.Add(room + DungeonLayout.DirectionOffsets[direction]);
            }

            int selected = BotExplorationPolicy.ChooseDirection(layout, room, visited, 1f, -1);

            Assert.That(expected, Is.GreaterThanOrEqualTo(0));
            Assert.That(selected, Is.EqualTo(expected));
        }

        /// <summary>
        /// The frontier is wherever the run has not been, however far that is. Ranking
        /// the four rooms next door cannot reach it: once the rooms around the agent
        /// are all visited they all score alike, and the run is spent hill-climbing
        /// around a corner of the dungeon it has already seen.
        /// </summary>
        [Test]
        public void ExplorationWalksAcrossKnownRoomsToReachTheNearestUnseenOne()
        {
            var layout = new DungeonLayout(271828);
            Vector2Int start = layout.ClampToPlayableBand(Vector2Int.zero);

            // Everything within two doorways has been seen, so nothing next door is
            // worth choosing and the target has to lie beyond that.
            var visited = new HashSet<Vector2Int>();
            foreach (Vector2Int room in Reachable(layout, start, 2))
                visited.Add(room);

            Assert.That(
                BotExplorationPolicy.TryFindFrontier(
                    layout,
                    start,
                    visited,
                    1f,
                    out Vector2Int frontier
                ),
                Is.True
            );
            Assert.That(visited.Contains(frontier), Is.False);
            Assert.That(
                DungeonLayout.Ring(frontier),
                Is.GreaterThan(0),
                "the agent settled for somewhere it had already been"
            );
        }

        [Test]
        public void AnAgentWithNowhereUnseenLeftToReachSaysSoRatherThanGuessing()
        {
            var layout = new DungeonLayout(271828);
            Vector2Int start = layout.ClampToPlayableBand(Vector2Int.zero);
            var visited = new HashSet<Vector2Int>(
                Reachable(layout, start, BotExplorationPolicy.SearchLimit)
            );

            Assert.That(
                BotExplorationPolicy.TryFindFrontier(layout, start, visited, 1f, out _),
                Is.False
            );
        }

        /// <summary>Every room within <paramref name="depth"/> doorways of a start.</summary>
        private static HashSet<Vector2Int> Reachable(
            DungeonLayout layout,
            Vector2Int start,
            int depth
        )
        {
            var seen = new HashSet<Vector2Int> { start };
            var frontier = new List<Vector2Int> { start };
            for (int step = 0; step < depth && frontier.Count > 0; step++)
            {
                var next = new List<Vector2Int>();
                foreach (Vector2Int room in frontier)
                {
                    for (int d = 0; d < DungeonLayout.DirectionOffsets.Length; d++)
                    {
                        if (!layout.IsPlayableDoorOpen(room, d))
                            continue;
                        Vector2Int candidate = room + DungeonLayout.DirectionOffsets[d];
                        if (seen.Add(candidate))
                            next.Add(candidate);
                    }
                }
                frontier = next;
            }

            return seen;
        }

        [Test]
        public void LowHealthMakesSustainMoreValuableThanEqualTierDamage()
        {
            var context = new UpgradeDecisionContext(0.25f, 2, 0f, 5f, 0f);
            var health = new UpgradeOption
            {
                Type = UpgradeOption.StatType.MaxHealth,
                Amount = 15f,
            };
            var damage = new UpgradeOption { Type = UpgradeOption.StatType.Damage, Amount = 8f };

            Assert.That(
                LevelUpAutoResolver.Score(health, context),
                Is.GreaterThan(LevelUpAutoResolver.Score(damage, context))
            );
        }

        [Test]
        public void UpgradeScoringUsesActualTradeOffPenaltyMagnitude()
        {
            var context = new UpgradeDecisionContext(1f, 1, 0f, 5f, 0f);
            var safeSpeed = new UpgradeOption
            {
                Type = UpgradeOption.StatType.Speed,
                Amount = 0.2f,
            };
            var riskySpeed = new UpgradeOption
            {
                Type = UpgradeOption.StatType.Speed,
                Amount = 0.2f,
                IsTrollUpgrade = true,
                PenaltyType = UpgradeOption.StatType.MaxHealth,
                PenaltyAmount = 30f,
            };

            Assert.That(
                LevelUpAutoResolver.Score(safeSpeed, context),
                Is.GreaterThan(LevelUpAutoResolver.Score(riskySpeed, context))
            );
        }

        [Test]
        public void AttackSpeedUpgradeShortensTheAttackInterval()
        {
            var player = new GameObject("Autoplay upgrade test");
            player.SetActive(false);
            try
            {
                PlayerStats playerStats = player.AddComponent<PlayerStats>();
                playerStats.ResetStats();
                float originalInterval = playerStats.CurrentAttackSpeed;

                playerStats.AddAttackSpeedPublic(0.2f);

                Assert.That(playerStats.CurrentAttackSpeed, Is.LessThan(originalInterval));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
