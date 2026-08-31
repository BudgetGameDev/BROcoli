using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class AutoplayDecisionTests
    {
        [Test]
        public void AgentExploresWhenNoThreatIsVisible()
        {
            var situation = new BotSituation(false, float.PositiveInfinity, 0, 1f, false, false);

            BotIntent intent = BotDecisionPolicy.ChooseIntent(situation, 2.5f, 5, 0.4f);

            Assert.That(intent, Is.EqualTo(BotIntent.Explore));
        }

        [Test]
        public void ProjectileDodgeTakesPriorityOverCombat()
        {
            var situation = new BotSituation(true, 4f, 2, 1f, true, false);

            BotIntent intent = BotDecisionPolicy.ChooseIntent(situation, 2.5f, 5, 0.4f);

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
            var situation = new BotSituation(
                true,
                nearestDistance,
                closeEnemies,
                healthFraction,
                false,
                false
            );

            BotIntent intent = BotDecisionPolicy.ChooseIntent(situation, 2.5f, 5, 0.4f);

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

            int selected = BotDecisionPolicy.ChooseExplorationDirection(
                layout,
                room,
                visited,
                1f,
                -1
            );

            Assert.That(expected, Is.GreaterThanOrEqualTo(0));
            Assert.That(selected, Is.EqualTo(expected));
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
