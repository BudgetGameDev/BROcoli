using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Covers how an upgrade is rolled and presented. UpgradeOption is plain
    /// serialisable logic rather than a component, so it is exercised directly.
    /// The generators roll UnityEngine.Random, seeded here so a failure reproduces.
    /// </summary>
    public sealed class UpgradeOptionTests
    {
        internal const int Seed = 20260830;

        // An enum value no switch arm names, which is how the default arms are reached.
        internal const UpgradeOption.StatType UnknownStat = (UpgradeOption.StatType)999;
        private const UpgradeOption.Rarity UnknownRarity = (UpgradeOption.Rarity)999;

        internal static UpgradeOption.StatType[] AllStats =>
            (UpgradeOption.StatType[])Enum.GetValues(typeof(UpgradeOption.StatType));

        private static UpgradeOption.Rarity[] AllRarities =>
            (UpgradeOption.Rarity[])Enum.GetValues(typeof(UpgradeOption.Rarity));

        [Test]
        public void EveryRarityHasItsOwnColour()
        {
            var seen = new List<Color>();
            foreach (UpgradeOption.Rarity rarity in AllRarities)
            {
                var option = new UpgradeOption { RarityLevel = rarity };
                Color colour = option.GetRarityColor();
                Assert.That(seen, Has.No.Member(colour), $"{rarity} reuses another colour.");
                seen.Add(colour);
            }
        }

        [Test]
        public void AnUnknownRarityFallsBackToTheCommonColour()
        {
            var option = new UpgradeOption { RarityLevel = UnknownRarity };
            Assert.That(option.GetRarityColor(), Is.EqualTo(UpgradeOption.CommonColor));
        }

        [Test]
        public void RarityNameIsTheUppercasedEnumName()
        {
            var option = new UpgradeOption { RarityLevel = UpgradeOption.Rarity.Legendary };
            Assert.That(option.GetRarityName(), Is.EqualTo("LEGENDARY"));
        }

        [Test]
        public void GeneratingEnoughOptionsProducesEveryRarityAndEveryStat()
        {
            var rarities = new HashSet<UpgradeOption.Rarity>();
            var stats = new HashSet<UpgradeOption.StatType>();

            UnityEngine.Random.InitState(Seed);
            for (int index = 0; index < 20000; index++)
            {
                // A high level maximises the legendary and epic odds, so the rarest
                // branches are reached in a run of this length.
                UpgradeOption option = UpgradeOption.GenerateRandom(100);
                rarities.Add(option.RarityLevel);
                stats.Add(option.Type);

                Assert.That(option.IsTrollUpgrade, Is.False);
                Assert.That(option.DisplayName, Is.Not.Null.And.Not.Empty);
                Assert.That(option.Description, Is.Not.Null.And.Not.Empty);
            }

            Assert.That(rarities, Is.EquivalentTo(AllRarities));
            Assert.That(stats, Is.EquivalentTo(AllStats));
        }

        [Test]
        public void ARarerRollAwardsAtLeastAsMuchOfTheSameStat()
        {
            // The rarity multiplier is monotonic, so for one stat the amount must
            // never fall as rarity rises.
            var best =
                new Dictionary<UpgradeOption.StatType, Dictionary<UpgradeOption.Rarity, float>>();

            UnityEngine.Random.InitState(Seed);
            for (int index = 0; index < 20000; index++)
            {
                UpgradeOption option = UpgradeOption.GenerateRandom(100);
                if (
                    !best.TryGetValue(
                        option.Type,
                        out Dictionary<UpgradeOption.Rarity, float> byRarity
                    )
                )
                {
                    byRarity = new Dictionary<UpgradeOption.Rarity, float>();
                    best[option.Type] = byRarity;
                }

                byRarity[option.RarityLevel] = option.Amount;
            }

            foreach (UpgradeOption.StatType stat in AllStats)
            {
                Dictionary<UpgradeOption.Rarity, float> byRarity = best[stat];
                float previous = float.NegativeInfinity;
                foreach (UpgradeOption.Rarity rarity in AllRarities)
                {
                    Assert.That(
                        byRarity[rarity],
                        Is.GreaterThanOrEqualTo(previous),
                        $"{stat} at {rarity} is worth less than at the rarity below it."
                    );
                    previous = byRarity[rarity];
                }
            }
        }

        [Test]
        public void TrollUpgradesAlwaysTradeOneStatAgainstADifferentOne()
        {
            var rarities = new HashSet<UpgradeOption.Rarity>();
            var stats = new HashSet<UpgradeOption.StatType>();

            UnityEngine.Random.InitState(Seed);
            for (int index = 0; index < 5000; index++)
            {
                UpgradeOption option = UpgradeOption.GenerateTrollUpgrade(10);
                rarities.Add(option.RarityLevel);
                stats.Add(option.Type);

                Assert.That(option.IsTrollUpgrade, Is.True);
                Assert.That(option.PenaltyType, Is.Not.EqualTo(option.Type));
                Assert.That(option.PenaltyAmount, Is.GreaterThan(0f));
                Assert.That(option.Description, Does.Contain("<color=#4CFF4C>"));
                Assert.That(option.Description, Does.Contain("<color=#FF4C4C>"));
                Assert.That(option.DisplayName, Does.EndWith(" Trade"));
            }

            // Troll upgrades are documented as Rare or better.
            Assert.That(
                rarities,
                Is.EquivalentTo(
                    new[]
                    {
                        UpgradeOption.Rarity.Rare,
                        UpgradeOption.Rarity.Epic,
                        UpgradeOption.Rarity.Legendary,
                    }
                )
            );
            Assert.That(stats, Is.EquivalentTo(AllStats));
        }

        [Test]
        public void RarityMultipliersRiseWithRarity()
        {
            float previous = 0f;
            foreach (UpgradeOption.Rarity rarity in AllRarities)
            {
                float multiplier = UpgradeOption.RarityMultiplier(rarity);
                Assert.That(multiplier, Is.GreaterThan(previous), $"{rarity} does not scale up.");
                previous = multiplier;
            }
        }

        [Test]
        public void AnUnknownRarityScalesLikeCommon()
        {
            Assert.That(
                UpgradeOption.RarityMultiplier(UnknownRarity),
                Is.EqualTo(UpgradeOption.RarityMultiplier(UpgradeOption.Rarity.Common))
            );
        }

        [Test]
        public void TrollRarityMultipliersRiseWithRarityAndBeatTheNormalOnes()
        {
            float rare = UpgradeOption.TrollRarityMultiplier(UpgradeOption.Rarity.Rare);
            float epic = UpgradeOption.TrollRarityMultiplier(UpgradeOption.Rarity.Epic);
            float legendary = UpgradeOption.TrollRarityMultiplier(UpgradeOption.Rarity.Legendary);

            Assert.That(rare, Is.LessThan(epic));
            Assert.That(epic, Is.LessThan(legendary));
            Assert.That(
                rare,
                Is.GreaterThan(UpgradeOption.RarityMultiplier(UpgradeOption.Rarity.Rare)),
                "Trade-off upgrades are meant to be the higher-reward option."
            );
        }

        [Test]
        public void AnUnknownTrollRarityScalesLikeRare()
        {
            Assert.That(
                UpgradeOption.TrollRarityMultiplier(UnknownRarity),
                Is.EqualTo(UpgradeOption.TrollRarityMultiplier(UpgradeOption.Rarity.Rare))
            );
        }
    }
}
