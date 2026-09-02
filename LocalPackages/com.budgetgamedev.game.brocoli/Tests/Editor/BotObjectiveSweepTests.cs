using System.Collections.Generic;
using System.Reflection;
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
            {
                LootChest chest = item.GetComponent<LootChest>();
                if (chest != null)
                    Lifecycle(chest, "OnDisable");
                Object.DestroyImmediate(item);
            }
            created.Clear();
        }

        /// <summary>
        /// A chest standing where the register can see it. Unity only runs the
        /// enable and disable hooks itself while playing, so an edit-mode test
        /// drives them the same way the rest of the suite drives a component's
        /// lifecycle.
        /// </summary>
        private LootChest Chest(string name, Vector2 at)
        {
            GameObject item = new(name);
            item.transform.position = at.ToWorld();
            created.Add(item);
            LootChest chest = item.AddComponent<LootChest>();
            Lifecycle(chest, "OnEnable");
            return chest;
        }

        private static void Lifecycle(LootChest chest, string hook) =>
            typeof(LootChest)
                .GetMethod(hook, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(chest, System.Array.Empty<object>());

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
            var boost = new BotDriver.NearestTarget(Vector2.zero);
            var experience = new BotDriver.NearestTarget(Vector2.zero);

            BotDriver.Classify(null, ref boost, ref experience);
            BotDriver.Classify(
                Marker<BoxCollider>("Coverage scenery", Vector2.right),
                ref boost,
                ref experience
            );

            Assert.That(boost.Found, Is.False, "a destroyed or plain collider is not a reward");
            Assert.That(experience.Found, Is.False);

            BotDriver.Classify(
                Marker<HealthBoost>("Coverage boost", Vector2.right * 4f),
                ref boost,
                ref experience
            );
            BotDriver.Classify(
                Marker<ExpGain>("Coverage orb", Vector2.left * 5f),
                ref boost,
                ref experience
            );

            Assert.That(boost.Position, Is.EqualTo(Vector2.right * 4f));
            Assert.That(experience.Position, Is.EqualTo(Vector2.left * 5f));
        }

        /// <summary>
        /// Chests come from their own register rather than a physics sweep, so the
        /// agent finds the one three metres away instead of whichever slice of the
        /// wall layer an overlap query happened to hand back. A chest out of range,
        /// or one already opened, is not somewhere to walk.
        /// </summary>
        [Test]
        public void TheNearestStandingChestComesFromTheRegister()
        {
            // Somewhere no other test's chest can be standing, so the register is
            // read for what this test put in it.
            var here = new Vector2(500f, 500f);
            Assert.That(
                BotDriver.NearestChest(here, 16f).Found,
                Is.False,
                "an empty stretch of dungeon offers no chest"
            );

            Chest("Far chest", here + Vector2.right * 40f);
            Chest("Near chest", here + Vector2.up * 3f);
            LootChest unloaded = Chest("Unloaded chest", here + Vector2.left);
            Lifecycle(unloaded, "OnDisable");

            // Torn down without its disable hook, the way only an editor scene can.
            LootChest destroyed = Chest("Destroyed chest", here + Vector2.down);
            created.Remove(destroyed.gameObject);
            Object.DestroyImmediate(destroyed.gameObject);

            BotDriver.NearestTarget found = BotDriver.NearestChest(here, 16f);
            Assert.That(found.Found, Is.True, "the chest in the next room was not seen");
            Assert.That(
                found.Position,
                Is.EqualTo(here + Vector2.up * 3f),
                "a chest out of range, or no longer loaded, is not somewhere to walk"
            );
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
