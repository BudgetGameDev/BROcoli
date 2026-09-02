using System.Reflection;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class ExpGainDropTests
    {
        private const string ExperiencePrefabPath =
            "Brocoli/CursedDevolpmentStudioAss Assets/Exp_gain";

        [Test]
        public void ExperiencePrefabCanBeLoadedForPoolWarmup()
        {
            GameObject prefab = Resources.Load<GameObject>(ExperiencePrefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<ExpGain>(), Is.Not.Null);
        }

        [Test]
        public void DroppedOrbDisablesCollectionColliderWhileAirborne()
        {
            var pickup = new GameObject("Dropped XP test");
            try
            {
                Rigidbody body = pickup.AddComponent<Rigidbody>();
                SphereCollider collider = pickup.AddComponent<SphereCollider>();
                ExpGain gain = pickup.AddComponent<ExpGain>();
                typeof(ExpGain)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(gain, null);
                gain.SetPooled(true);

                gain.InitDropped(10, new Vector3(1f, 0.5f, 0f), ExpGain.DropStyle.Chest);

                Assert.That(collider.enabled, Is.False);
                Assert.That(body.isKinematic, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(pickup);
            }
        }

        [Test]
        public void DropArcStartsAndFinishesAtRequestedPositions()
        {
            Vector3 start = new Vector3(1f, 0.5f, 2f);
            Vector3 landing = new Vector3(4f, 0.5f, -1f);

            Assert.That(ExpGain.DropArcPosition(start, landing, 2f, 0f), Is.EqualTo(start));
            Assert.That(ExpGain.DropArcPosition(start, landing, 2f, 1f), Is.EqualTo(landing));
        }

        [Test]
        public void DropArcReachesConfiguredHeightAtItsMidpoint()
        {
            Vector3 start = new Vector3(0f, 0.5f, 0f);
            Vector3 landing = new Vector3(2f, 0.5f, 0f);

            Vector3 midpoint = ExpGain.DropArcPosition(start, landing, 2.2f, 0.5f);

            Assert.That(midpoint, Is.EqualTo(new Vector3(1f, 2.7f, 0f)));
        }

        [Test]
        public void AnUncollectedOrbIsNeverTakenBackFromTheFloor()
        {
            var pickup = new GameObject("Idle XP test");
            try
            {
                pickup.AddComponent<Rigidbody>();
                pickup.AddComponent<SphereCollider>();
                ExpGain gain = pickup.AddComponent<ExpGain>();
                typeof(ExpGain)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(gain, null);
                gain.SetPooled(true);
                gain.Init(10);

                MethodInfo fixedUpdate = typeof(ExpGain).GetMethod(
                    "FixedUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                for (int frame = 0; frame < 120; frame++)
                    fixedUpdate?.Invoke(gain, null);

                Assert.That(pickup.activeSelf, Is.True);
                Assert.That(
                    typeof(ExpGain).GetMethod(
                        "Update",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    ),
                    Is.Null,
                    "experience waits for the player to walk over it, never for a timer"
                );
            }
            finally
            {
                Object.DestroyImmediate(pickup);
            }
        }
    }
}
