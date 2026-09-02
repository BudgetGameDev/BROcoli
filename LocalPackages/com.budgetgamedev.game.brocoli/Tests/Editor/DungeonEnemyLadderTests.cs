using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The ring ladder is the run's difficulty spine, and it is driven by prefab
    /// names, so a rename or a reordered check silently changes what the first room
    /// of a run contains. These pin the ladder to the enemies that actually ship.
    /// </summary>
    public sealed class DungeonEnemyLadderTests
    {
        [Test]
        public void TheFirstRingHoldsOnlyTheStartingArchetype()
        {
            Assert.That(DungeonEnemyPlacer.MinRingFor("EnemyEasy"), Is.EqualTo(1));

            foreach (
                string other in new[]
                {
                    "EnemyNormal",
                    "EnemySpider",
                    "EnemyEasyChunky",
                    "EnemyShooting",
                    "EnemyHard",
                    "EnemyHydraCorona",
                    "EnemyNormalChunky",
                    "EnemyShootingHard",
                    "EnemyHardChunky",
                }
            )
                Assert.That(DungeonEnemyPlacer.MinRingFor(other), Is.GreaterThan(1), other);
        }

        [Test]
        public void EachRingOutAddsEitherANewBehaviourOrABiggerHealthPool()
        {
            Assert.That(DungeonEnemyPlacer.MinRingFor("EnemyNormal"), Is.EqualTo(2));
            Assert.That(DungeonEnemyPlacer.MinRingFor("EnemySpider"), Is.EqualTo(2));
            Assert.That(DungeonEnemyPlacer.MinRingFor("EnemyEasyChunky"), Is.EqualTo(2));
            Assert.That(DungeonEnemyPlacer.MinRingFor("EnemyShooting"), Is.EqualTo(3));
            Assert.That(DungeonEnemyPlacer.MinRingFor("EnemyHard"), Is.EqualTo(3));
            Assert.That(DungeonEnemyPlacer.MinRingFor("EnemyHydraCorona"), Is.EqualTo(4));
            Assert.That(DungeonEnemyPlacer.MinRingFor("EnemyNormalChunky"), Is.EqualTo(4));
            Assert.That(DungeonEnemyPlacer.MinRingFor("EnemyShootingHard"), Is.EqualTo(5));
            Assert.That(DungeonEnemyPlacer.MinRingFor("EnemyHardChunky"), Is.EqualTo(5));
        }

        [Test]
        public void AChunkyVariantIsPlacedByItsHealthPoolRatherThanItsNameFamily()
        {
            Assert.That(
                DungeonEnemyPlacer.MinRingFor("EnemyEasyChunky"),
                Is.GreaterThan(DungeonEnemyPlacer.MinRingFor("EnemyEasy")),
                "a two-hundred-point bruiser does not belong beside the starting archetype"
            );
            Assert.That(
                DungeonEnemyPlacer.MinRingFor("EnemyNormalChunky"),
                Is.GreaterThan(DungeonEnemyPlacer.MinRingFor("EnemyNormal"))
            );
            Assert.That(
                DungeonEnemyPlacer.MinRingFor("EnemyHardChunky"),
                Is.GreaterThan(DungeonEnemyPlacer.MinRingFor("EnemyHard"))
            );
        }
    }
}
