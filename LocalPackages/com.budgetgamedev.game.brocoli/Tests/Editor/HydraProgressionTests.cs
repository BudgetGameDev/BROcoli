using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class HydraProgressionTests
    {
        private const string HydraPrefabPath =
            "Packages/com.budgetgamedev.game.brocoli/Resources/Brocoli/CursedDevolpmentStudioAss Assets/Waves/EnemyHydraCorona.prefab";

        [TestCase(4, 0)]
        [TestCase(7, 0)]
        [TestCase(8, 1)]
        [TestCase(11, 1)]
        [TestCase(12, 2)]
        [TestCase(40, 2)]
        public void HigherDungeonRingsAddCappedSplitGenerations(int ring, int expected)
        {
            Assert.That(HydraEnemyScript.ExtraSplitGenerationsForRing(ring), Is.EqualTo(expected));
        }

        [Test]
        public void LargerHydraTiersKeepTheirFinalChildrenAtAPlayableSize()
        {
            const float childScale = 0.72f;
            const int baseSplitGenerations = 2;
            float baselineFinalScale = Mathf.Pow(childScale, baseSplitGenerations);

            for (int extraSplits = 0; extraSplits <= 2; extraSplits++)
            {
                float rootScale = HydraEnemyScript.RootScaleMultiplierForExtraSplits(
                    extraSplits,
                    childScale
                );
                float finalScale =
                    rootScale * Mathf.Pow(childScale, baseSplitGenerations + extraSplits);

                Assert.That(finalScale, Is.EqualTo(baselineFinalScale).Within(0.0001f));
                if (extraSplits > 0)
                    Assert.That(rootScale, Is.GreaterThan(1f));
            }
        }

        [Test]
        public void EverySmallerGenerationMovesFasterThanItsParent()
        {
            const float childScale = 0.72f;
            float speed = 2f;
            float scale = 1f;

            for (int generation = 1; generation <= 4; generation++)
            {
                float childSpeed = HydraEnemyScript.ChildSpeedForScale(speed, childScale, 1.1f);
                float nextScale = scale * childScale;

                Assert.That(childSpeed, Is.GreaterThan(speed), $"generation {generation}");
                Assert.That(nextScale, Is.LessThan(scale), $"generation {generation}");

                speed = childSpeed;
                scale = nextScale;
            }

            Assert.That(speed, Is.GreaterThan(3.9f));
        }

        [Test]
        public void DungeonTierConfiguresTheRootAndSurvivesIntoItsChildren()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HydraPrefabPath);
            Assert.That(prefab, Is.Not.Null, HydraPrefabPath);

            HydraEnemyScript root = Object.Instantiate(prefab).GetComponent<HydraEnemyScript>();
            HydraEnemyScript child = Object.Instantiate(prefab).GetComponent<HydraEnemyScript>();
            try
            {
                root.ConfigureForDungeonRing(12);
                child.InitAsChild(
                    1,
                    root.SplitGenerations,
                    root.HydraLevel,
                    root.MaxHealth,
                    root.Damage,
                    root.Speed,
                    0.9f,
                    root.transform.localScale
                );

                Assert.That(root.HydraLevel, Is.EqualTo(3));
                Assert.That(root.SplitGenerations, Is.EqualTo(4));
                Assert.That(root.transform.localScale.x, Is.GreaterThan(1.9f));
                Assert.That(child.HydraLevel, Is.EqualTo(root.HydraLevel));
                Assert.That(child.SplitGenerations, Is.EqualTo(root.SplitGenerations));
                Assert.That(child.CurrentGeneration, Is.EqualTo(1));
                Assert.That(child.transform.localScale.x, Is.LessThan(root.transform.localScale.x));
                Assert.That(child.Speed, Is.GreaterThan(root.Speed));
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
                Object.DestroyImmediate(child.gameObject);
            }
        }

        [Test]
        public void PoolResetRemovesThePreviousHydraTier()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HydraPrefabPath);
            HydraEnemyScript hydra = Object.Instantiate(prefab).GetComponent<HydraEnemyScript>();
            try
            {
                hydra.ConfigureForDungeonRing(12);
                hydra.ResetForPool();

                Assert.That(hydra.HydraLevel, Is.EqualTo(1));
                Assert.That(hydra.SplitGenerations, Is.EqualTo(2));
                Assert.That(hydra.CurrentGeneration, Is.Zero);
                Assert.That(hydra.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.DestroyImmediate(hydra.gameObject);
            }
        }
    }
}
