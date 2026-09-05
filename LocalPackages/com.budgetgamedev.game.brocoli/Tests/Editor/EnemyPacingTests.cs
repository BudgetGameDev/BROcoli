using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class EnemyPacingTests
    {
        [Test]
        public void LateMeleePressuresWalkingPlayersWhileOpeningRemainsForgiving()
        {
            float opening = EnemyScaling.Speed(2f, EnemyScaling.SpeedScale(1, 1f), "EnemyNormal");
            float late = EnemyScaling.Speed(2f, EnemyScaling.SpeedScale(14, 7f), "EnemyNormal");
            Assert.That(opening, Is.LessThan(2.5f));
            Assert.That(late, Is.GreaterThan(3.7f));
            Assert.That(late, Is.LessThan(4f));
        }

        [TestCase("EnemyNormal")]
        [TestCase("EnemyHard")]
        [TestCase("EnemyEasyChunky")]
        [TestCase("EnemyHydra")]
        public void ExtremeDepthCannotMakeOrdinaryMeleeFasterThanBasePlayer(string archetype)
        {
            float speed = EnemyScaling.Speed(2.5f, EnemyScaling.SpeedScale(1000, 1000f), archetype);
            Assert.That(speed, Is.LessThan(4f));
        }
    }
}
