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

        /// <summary>
        /// Experience anywhere in sight is worth walking to, and a nearer orb is worth
        /// more than a further one. Orbs wait on the floor rather than expiring, so a
        /// run that leaves the far corner of a room it just cleared is throwing away
        /// levels it has already earned.
        /// </summary>
        [Test]
        public void ExperienceAnywhereInSightBeatsWanderingOff()
        {
            BotSituation far = Calm(float.PositiveInfinity, Tuning.ObjectiveRadius);
            BotSituation near = Calm(float.PositiveInfinity, 2f);

            Assert.That(
                BotDecisionPolicy.ChooseIntent(far, Tuning, BotIntent.Waiting),
                Is.EqualTo(BotIntent.Collect)
            );
            Assert.That(
                BotDecisionPolicy.ChooseIntent(near, Tuning, BotIntent.Waiting),
                Is.EqualTo(BotIntent.Collect)
            );
            Assert.That(
                BotDecisionPolicy.Utility(BotIntent.Collect, near, Tuning),
                Is.GreaterThan(BotDecisionPolicy.Utility(BotIntent.Collect, far, Tuning))
            );
        }

        /// <summary>A chest still outranks an orb the same distance away.</summary>
        [Test]
        public void AChestStillOutranksExperienceTheSameDistanceAway()
        {
            Assert.That(
                BotDecisionPolicy.Utility(BotIntent.Loot, Calm(6f, 6f), Tuning),
                Is.GreaterThan(BotDecisionPolicy.Utility(BotIntent.Collect, Calm(6f, 6f), Tuning))
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
            // Equidistant chest and experience compete within the hysteresis margin.
            BotSituation borderline = Calm(6f, 6f);

            Assert.That(
                BotDecisionPolicy.Utility(BotIntent.Collect, borderline, Tuning),
                Is.LessThan(BotDecisionPolicy.Utility(BotIntent.Loot, borderline, Tuning))
            );
            Assert.That(
                BotDecisionPolicy.Utility(BotIntent.Collect, borderline, Tuning)
                    + BotDecisionPolicy.Hysteresis,
                Is.GreaterThan(BotDecisionPolicy.Utility(BotIntent.Loot, borderline, Tuning))
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

        /// <summary>
        /// A stalemate the agent is losing is still a stalemate, and leaving is what
        /// retreating is for. Refusing to retreat out of one is how a run ends up
        /// circling a crowd at weapon range while its health bar sits half empty.
        /// </summary>
        [Test]
        public void AStalemateIsWalkedAwayFromWhenClearAndRetreatedFromWhenCrowded()
        {
            var unhurt = new BotSituation(
                true,
                5f,
                0,
                1f,
                false,
                false,
                float.PositiveInfinity,
                float.PositiveInfinity,
                true
            );
            var bleeding = new BotSituation(
                true,
                5f,
                8,
                0.2f,
                false,
                false,
                float.PositiveInfinity,
                float.PositiveInfinity,
                true
            );

            Assert.That(
                BotDecisionPolicy.Utility(BotIntent.Retreat, unhurt, Tuning),
                Is.EqualTo(float.NegativeInfinity)
            );
            Assert.That(
                BotDecisionPolicy.ChooseIntent(unhurt, Tuning, BotIntent.Engage),
                Is.EqualTo(BotIntent.Explore)
            );
            Assert.That(
                BotDecisionPolicy.ChooseIntent(bleeding, Tuning, BotIntent.Engage),
                Is.EqualTo(BotIntent.Retreat),
                "a fight going nowhere while the agent bleeds is the one to back out of"
            );
        }

        /// <summary>
        /// Writing a fight off is not a reason to walk through it. Something already
        /// inside the danger radius is biting whatever the stall clock says, and an
        /// agent that ignores it face-tanks the crowd it just gave up on.
        /// </summary>
        [Test]
        public void SomethingBitingIsStillBackedAwayFromInAFightAlreadyWrittenOff()
        {
            var bitten = new BotSituation(
                true,
                Tuning.DangerRadius - 0.5f,
                3,
                1f,
                false,
                false,
                float.PositiveInfinity,
                float.PositiveInfinity,
                true
            );

            Assert.That(
                BotDecisionPolicy.ChooseIntent(bitten, Tuning, BotIntent.Engage),
                Is.EqualTo(BotIntent.Retreat)
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
