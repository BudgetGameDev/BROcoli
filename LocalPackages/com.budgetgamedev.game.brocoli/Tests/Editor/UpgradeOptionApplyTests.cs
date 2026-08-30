using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Covers the per-stat amount, wording and application helpers, including the
    /// trade-off penalties. These are internal seams rather than reflection targets,
    /// so a test reaches them the same way the rest of the assembly does.
    /// </summary>
    public sealed class UpgradeOptionApplyTests
    {
        private static UpgradeOption.StatType[] AllStats => UpgradeOptionTests.AllStats;

        private const UpgradeOption.StatType UnknownStat = UpgradeOptionTests.UnknownStat;

        [Test]
        public void SetStatAmountScalesEveryStatWithTheMultiplier()
        {
            foreach (UpgradeOption.StatType stat in AllStats)
            {
                var small = new UpgradeOption { Type = stat };
                var large = new UpgradeOption { Type = stat };
                UpgradeOption.SetStatAmount(small, 1f);
                UpgradeOption.SetStatAmount(large, 8f);

                Assert.That(small.Amount, Is.GreaterThan(0f), $"{stat} awards nothing.");
                Assert.That(large.Amount, Is.GreaterThan(small.Amount), $"{stat} does not scale.");
            }
        }

        [Test]
        public void SetStatAmountLeavesAnUnknownStatAlone()
        {
            var option = new UpgradeOption { Type = UnknownStat, Amount = 42f };
            UpgradeOption.SetStatAmount(option, 5f);
            Assert.That(option.Amount, Is.EqualTo(42f));
        }

        [Test]
        public void EveryPenaltyAmountScalesWithTheMultiplier()
        {
            foreach (UpgradeOption.StatType stat in AllStats)
            {
                float small = UpgradeOption.GetPenaltyAmount(stat, 1f);
                float large = UpgradeOption.GetPenaltyAmount(stat, 8f);
                Assert.That(small, Is.GreaterThan(0f), $"{stat} penalises nothing.");
                Assert.That(large, Is.GreaterThan(small), $"{stat} penalty does not scale.");
            }
        }

        [Test]
        public void AnUnknownStatStillYieldsAScaledPenalty()
        {
            Assert.That(UpgradeOption.GetPenaltyAmount(UnknownStat, 3f), Is.EqualTo(3f));
        }

        [Test]
        public void EveryStatDescriptionCarriesTheCorrectSign()
        {
            foreach (UpgradeOption.StatType stat in AllStats)
            {
                Assert.That(UpgradeOption.GetStatDescription(stat, 5f, true), Does.StartWith("+"));
                Assert.That(UpgradeOption.GetStatDescription(stat, 5f, false), Does.StartWith("-"));
            }
        }

        [Test]
        public void AnUnknownStatDescriptionFallsBackToAPlainAmount()
        {
            Assert.That(UpgradeOption.GetStatDescription(UnknownStat, 5f, true), Is.EqualTo("+5"));
            Assert.That(UpgradeOption.GetStatDescription(UnknownStat, 5f, false), Is.EqualTo("-5"));
        }

        [Test]
        public void EveryStatHasADistinctShortName()
        {
            var seen = new List<string>();
            foreach (UpgradeOption.StatType stat in AllStats)
            {
                string name = UpgradeOption.GetStatShortName(stat);
                Assert.That(name, Is.Not.Null.And.Not.Empty);
                Assert.That(seen, Has.No.Member(name), $"{stat} reuses the short name {name}.");
                seen.Add(name);
            }
        }

        [Test]
        public void AnUnknownStatShortNameIsMarkedUnknown()
        {
            Assert.That(UpgradeOption.GetStatShortName(UnknownStat), Is.EqualTo("???"));
        }

        [Test]
        public void ApplyingABonusMovesEveryStat()
        {
            foreach (UpgradeOption.StatType stat in AllStats)
            {
                using var player = new TestPlayer();
                float before = player.Read(stat);

                var option = new UpgradeOption { Type = stat, Amount = 5f };
                option.ApplyTo(player.Stats);

                Assert.That(
                    player.Read(stat),
                    Is.Not.EqualTo(before),
                    $"Applying {stat} changed nothing."
                );
            }
        }

        [Test]
        public void ApplyingAnUnknownStatIsHarmless()
        {
            using var player = new TestPlayer();
            var option = new UpgradeOption { Type = UnknownStat, Amount = 5f };
            Assert.DoesNotThrow(() => option.ApplyTo(player.Stats));
        }

        [Test]
        public void ATrollUpgradeAppliesItsPenaltyAsWellAsItsBonus()
        {
            using var player = new TestPlayer();
            float armourBefore = player.Stats.CurrentArmor;
            float damageBefore = player.Stats.CurrentDamage;

            var option = new UpgradeOption
            {
                Type = UpgradeOption.StatType.Damage,
                Amount = 10f,
                IsTrollUpgrade = true,
                PenaltyType = UpgradeOption.StatType.Armor,
                PenaltyAmount = 4f,
            };
            option.ApplyTo(player.Stats);

            Assert.That(player.Stats.CurrentDamage, Is.EqualTo(damageBefore + 10f).Within(0.001f));
            Assert.That(player.Stats.CurrentArmor, Is.EqualTo(armourBefore - 4f).Within(0.001f));
        }

        [Test]
        public void AnAttackSpeedPenaltyIsGentlerThanTheEquivalentBonus()
        {
            // Attack speed is an interval, and the penalty deliberately applies only
            // half the magnitude, so a bonus and penalty of one size do not cancel.
            using var bonus = new TestPlayer();
            using var penalty = new TestPlayer();
            float start = bonus.Stats.CurrentAttackSpeed;

            var option = new UpgradeOption();
            option.ApplyStatChange(bonus.Stats, UpgradeOption.StatType.AttackSpeed, 0.4f, true);
            option.ApplyStatChange(penalty.Stats, UpgradeOption.StatType.AttackSpeed, 0.4f, false);

            Assert.That(bonus.Stats.CurrentAttackSpeed, Is.LessThan(start));
            Assert.That(penalty.Stats.CurrentAttackSpeed, Is.GreaterThan(start));
            Assert.That(
                start - bonus.Stats.CurrentAttackSpeed,
                Is.GreaterThan(penalty.Stats.CurrentAttackSpeed - start)
            );
        }

        /// <summary>Owns a PlayerStats component and destroys it with the test.</summary>
        private sealed class TestPlayer : IDisposable
        {
            private readonly GameObject _owner;

            internal TestPlayer()
            {
                _owner = new GameObject("UpgradeOption test player");
                Stats = _owner.AddComponent<PlayerStats>();
                // Start() seeds the base stats in play, and never runs in EditMode.
                // ResetStats is the public seam that does the same work.
                Stats.ResetStats();
            }

            internal PlayerStats Stats { get; }

            internal float Read(UpgradeOption.StatType stat)
            {
                return stat switch
                {
                    UpgradeOption.StatType.MaxHealth => Stats.CurrentMaxHealth,
                    UpgradeOption.StatType.Damage => Stats.CurrentDamage,
                    UpgradeOption.StatType.Speed => Stats.CurrentMovementSpeed,
                    UpgradeOption.StatType.AttackSpeed => Stats.CurrentAttackSpeed,
                    UpgradeOption.StatType.SprayRange => Stats.CurrentSprayRange,
                    UpgradeOption.StatType.SprayWidth => Stats.CurrentSprayWidth,
                    UpgradeOption.StatType.DetectionRadius => Stats.CurrentDetectionRadius,
                    UpgradeOption.StatType.CritChance => Stats.CurrentCritChance,
                    UpgradeOption.StatType.CritDamage => Stats.CurrentCritDamage,
                    UpgradeOption.StatType.Dodge => Stats.CurrentDodgeChance,
                    UpgradeOption.StatType.Armor => Stats.CurrentArmor,
                    UpgradeOption.StatType.HealthRegen => Stats.CurrentHealthRegen,
                    UpgradeOption.StatType.LifeSteal => Stats.CurrentLifeSteal,
                    _ => 0f,
                };
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_owner);
            }
        }
    }
}
