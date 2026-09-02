using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The objective sweep is what turns "a chest exists" into "the agent walked
    /// into it", so its sorting and its preference between reward kinds are checked
    /// directly rather than left to whatever a live dungeon happens to spawn.
    /// </summary>
    public sealed class BotObjectiveSweepTests
    {
        private readonly List<GameObject> created = new();

        [TearDown]
        public void DestroyMarkers()
        {
            foreach (GameObject item in created)
                Object.DestroyImmediate(item);
            created.Clear();
        }

        private Collider Marker<T>(string name, Vector2 at)
            where T : Component
        {
            GameObject item = new(name);
            item.transform.position = at.ToWorld();
            Collider collider = item.AddComponent<SphereCollider>();
            item.AddComponent<T>();
            created.Add(item);
            return collider;
        }

        [Test]
        public void EachRewardKindIsSortedIntoItsOwnTarget()
        {
            var chest = new BotDriver.NearestTarget(Vector2.zero);
            var boost = new BotDriver.NearestTarget(Vector2.zero);
            var experience = new BotDriver.NearestTarget(Vector2.zero);

            BotDriver.Classify(null, ref chest, ref boost, ref experience);
            BotDriver.Classify(
                Marker<BoxCollider>("Coverage scenery", Vector2.right),
                ref chest,
                ref boost,
                ref experience
            );

            Assert.That(chest.Found, Is.False, "a destroyed or plain collider is not a reward");
            Assert.That(boost.Found, Is.False);
            Assert.That(experience.Found, Is.False);

            BotDriver.Classify(
                Marker<LootChest>("Coverage chest", Vector2.up * 3f),
                ref chest,
                ref boost,
                ref experience
            );
            BotDriver.Classify(
                Marker<HealthBoost>("Coverage boost", Vector2.right * 4f),
                ref chest,
                ref boost,
                ref experience
            );
            BotDriver.Classify(
                Marker<ExpGain>("Coverage orb", Vector2.left * 5f),
                ref chest,
                ref boost,
                ref experience
            );

            Assert.That(chest.Position, Is.EqualTo(Vector2.up * 3f));
            Assert.That(boost.Position, Is.EqualTo(Vector2.right * 4f));
            Assert.That(experience.Position, Is.EqualTo(Vector2.left * 5f));
        }

        [Test]
        public void OnlyACloserCandidateDisplacesTheRunningBest()
        {
            var target = new BotDriver.NearestTarget(Vector2.zero);

            target.Offer(Vector2.right * 4f);
            target.Offer(Vector2.up * 4f);
            Assert.That(target.Position, Is.EqualTo(Vector2.right * 4f), "a tie changes nothing");

            target.Offer(Vector2.left * 2f);
            Assert.That(target.Position, Is.EqualTo(Vector2.left * 2f));
            Assert.That(target.Found, Is.True);
        }

        [Test]
        public void ABoostOutranksAnExperienceOrb()
        {
            var boost = new BotDriver.NearestTarget(Vector2.zero);
            var experience = new BotDriver.NearestTarget(Vector2.zero);
            experience.Offer(Vector2.right);

            Assert.That(
                BotDriver.PreferredPickup(boost, experience).Position,
                Is.EqualTo(Vector2.right),
                "without a boost the orb is the pickup"
            );

            boost.Offer(Vector2.left * 9f);
            Assert.That(
                BotDriver.PreferredPickup(boost, experience).Position,
                Is.EqualTo(Vector2.left * 9f),
                "a boost wins even from further away"
            );
        }

        [Test]
        public void DistancesAreInfiniteUntilThereIsSomethingToWalkTo()
        {
            BotDriver.ObjectiveObservation nothing = BotDriver.ObjectiveObservation.None;
            var something = new BotDriver.ObjectiveObservation(
                true,
                Vector2.right * 3f,
                true,
                Vector2.up * 4f
            );

            Assert.That(nothing.ChestDistance(Vector2.zero), Is.EqualTo(float.PositiveInfinity));
            Assert.That(nothing.PickupDistance(Vector2.zero), Is.EqualTo(float.PositiveInfinity));
            Assert.That(something.ChestDistance(Vector2.zero), Is.EqualTo(3f).Within(1e-4f));
            Assert.That(something.PickupDistance(Vector2.zero), Is.EqualTo(4f).Within(1e-4f));
        }
    }
}
