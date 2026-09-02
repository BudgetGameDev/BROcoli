using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Goal scoring decides what the agent spends a run doing, and two of the
    /// required features -- chests and boosts -- exist only because it deliberately
    /// walks to them. These pin the ordering that makes that happen.
    /// </summary>
    public sealed class BotDecisionPolicyTests
    {
        private static readonly BotTuning Tuning = new(2.5f, 5, 0.4f, 14f, 16f);

        private static BotSituation Calm(float chestDistance, float pickupDistance) =>
            new(false, float.PositiveInfinity, 0, 1f, false, false, chestDistance, pickupDistance);

        [Test]
        public void AChestAnywhereInSightBeatsWanderingOff()
        {
            BotSituation far = Calm(Tuning.ObjectiveRadius, float.PositiveInfinity);

            Assert.That(
                BotDecisionPolicy.ChooseIntent(far, Tuning, BotIntent.Waiting),
                Is.EqualTo(BotIntent.Loot)
            );
            Assert.That(
                BotDecisionPolicy.Utility(BotIntent.Loot, Calm(1f, float.PositiveInfinity), Tuning),
                Is.GreaterThan(BotDecisionPolicy.Utility(BotIntent.Loot, far, Tuning)),
                "a nearer chest is worth more"
            );
        }

        [Test]
        public void ADistantPickupIsNotWorthAbandoningExploration()
        {
            BotSituation far = Calm(float.PositiveInfinity, Tuning.ObjectiveRadius);
            BotSituation near = Calm(float.PositiveInfinity, 2f);

            Assert.That(
                BotDecisionPolicy.ChooseIntent(far, Tuning, BotIntent.Waiting),
                Is.EqualTo(BotIntent.Explore)
            );
            Assert.That(
                BotDecisionPolicy.ChooseIntent(near, Tuning, BotIntent.Waiting),
                Is.EqualTo(BotIntent.Collect)
            );
        }

        [Test]
        public void AHurtAgentValuesAPickupMoreHighly()
        {
            var healthy = new BotSituation(
                false,
                float.MaxValue,
                0,
                1f,
                false,
                false,
                float.PositiveInfinity,
                9f
            );
            var hurt = new BotSituation(
                false,
                float.MaxValue,
                0,
                0.2f,
                false,
                false,
                float.PositiveInfinity,
                9f
            );

            Assert.That(
                BotDecisionPolicy.Utility(BotIntent.Collect, hurt, Tuning),
                Is.GreaterThan(BotDecisionPolicy.Utility(BotIntent.Collect, healthy, Tuning))
            );
        }

        [Test]
        public void SomethingBitingOutranksAnythingOnTheFloor()
        {
            var cornered = new BotSituation(true, 1f, 1, 1f, false, false, 1f, 1f);

            Assert.That(
                BotDecisionPolicy.ChooseIntent(cornered, Tuning, BotIntent.Loot),
                Is.EqualTo(BotIntent.Retreat)
            );
        }

        [Test]
        public void RecoveryAndDodgingAreNotUpForDebate()
        {
            var recovering = new BotSituation(true, 1f, 9, 0.1f, true, true, 1f, 1f);
            var incoming = new BotSituation(true, 1f, 9, 0.1f, true, false, 1f, 1f);

            Assert.That(
                BotDecisionPolicy.ChooseIntent(recovering, Tuning, BotIntent.Waiting),
                Is.EqualTo(BotIntent.Recover)
            );
            Assert.That(
                BotDecisionPolicy.ChooseIntent(incoming, Tuning, BotIntent.Waiting),
                Is.EqualTo(BotIntent.Dodge)
            );
        }

        [Test]
        public void TheRunningGoalIsKeptUntilAnotherClearlyBeatsIt()
        {
            // Tuned so exploring and collecting sit within the hysteresis margin.
            BotSituation borderline = Calm(float.PositiveInfinity, 11.6f);

            Assert.That(
                BotDecisionPolicy.Utility(BotIntent.Collect, borderline, Tuning),
                Is.LessThan(BotDecisionPolicy.Utility(BotIntent.Explore, borderline, Tuning))
            );
            Assert.That(
                BotDecisionPolicy.Utility(BotIntent.Collect, borderline, Tuning)
                    + BotDecisionPolicy.Hysteresis,
                Is.GreaterThan(BotDecisionPolicy.Utility(BotIntent.Explore, borderline, Tuning))
            );
            Assert.That(
                BotDecisionPolicy.ChooseIntent(borderline, Tuning, BotIntent.Collect),
                Is.EqualTo(BotIntent.Collect)
            );
        }

        [Test]
        public void AFightThatIsAchievingNothingIsAbandoned()
        {
            var pinned = new BotSituation(
                true,
                5f,
                1,
                1f,
                false,
                false,
                float.PositiveInfinity,
                float.PositiveInfinity,
                true
            );

            Assert.That(
                BotDecisionPolicy.Utility(BotIntent.Engage, pinned, Tuning),
                Is.EqualTo(float.NegativeInfinity)
            );
            Assert.That(
                BotDecisionPolicy.ChooseIntent(pinned, Tuning, BotIntent.Engage),
                Is.EqualTo(BotIntent.Explore),
                "the agent must walk away rather than pace at an unreachable enemy"
            );
        }

        [Test]
        public void GoalsWithNothingToActOnScoreNothing()
        {
            BotSituation empty = Calm(float.PositiveInfinity, float.PositiveInfinity);

            Assert.That(
                BotDecisionPolicy.Utility(BotIntent.Engage, empty, Tuning),
                Is.EqualTo(float.NegativeInfinity)
            );
            Assert.That(
                BotDecisionPolicy.Utility(BotIntent.Retreat, empty, Tuning),
                Is.EqualTo(float.NegativeInfinity)
            );
            Assert.That(
                BotDecisionPolicy.Utility(BotIntent.Waiting, empty, Tuning),
                Is.EqualTo(float.NegativeInfinity)
            );
            Assert.That(BotDecisionPolicy.Proximity(1f, 0f), Is.EqualTo(0f));
            Assert.That(BotDecisionPolicy.Proximity(float.PositiveInfinity, 5f), Is.EqualTo(0f));
            Assert.That(BotDecisionPolicy.Proximity(0f, 5f), Is.EqualTo(1f));
        }

        [Test]
        public void ACrowdMakesRetreatingBeatAttacking()
        {
            var crowded = new BotSituation(
                true,
                6f,
                6,
                1f,
                false,
                false,
                float.PositiveInfinity,
                float.PositiveInfinity
            );

            Assert.That(
                BotDecisionPolicy.ChooseIntent(crowded, Tuning, BotIntent.Engage),
                Is.EqualTo(BotIntent.Retreat)
            );
        }
    }
}
