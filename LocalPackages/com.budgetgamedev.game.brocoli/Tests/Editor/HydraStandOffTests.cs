using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// A hydra holds a fixed gap from the player: it closes when it is too far,
    /// backs off when the player has walked into it, and stands still in between.
    /// The backing-off band is the one worth pinning deliberately -- a hydra that
    /// only ever closes ends up inside the player, where its strike has no reach
    /// and the player cannot see what is hitting them.
    /// </summary>
    public sealed class HydraStandOffTests
    {
        private const float StandOff = 0.4f;

        private GameObject hydraObject;
        private GameObject playerObject;
        private HydraEnemyScript hydra;

        [SetUp]
        public void BuildTheStandOff()
        {
            hydraObject = new GameObject("Stand-off hydra");
            hydraObject.SetActive(false);
            playerObject = new GameObject("Stand-off player");
            playerObject.SetActive(false);

            var playerBody = playerObject.AddComponent<SphereCollider>();
            playerBody.radius = 0.5f;

            var body = hydraObject.AddComponent<SphereCollider>();
            body.radius = 0.5f;
            Rigidbody rigidbody = hydraObject.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.isKinematic = false;

            hydra = hydraObject.AddComponent<HydraEnemyScript>();
            hydra.player = playerObject.transform;
            hydra.Speed = 2f;
            Set("rb", rigidbody);
            Set("bodyCollider", body);
            Set("playerCollider", playerBody);
            Set("playerStandOffGap", StandOff);
            // Crowd separation is a different rule with its own tests; a lone
            // hydra must not pick up a shove from whatever is left in the hash.
            Set("separationRadius", 0f);
            Set("separationForce", 0f);

            hydraObject.SetActive(true);
            playerObject.SetActive(true);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void DestroyTheStandOff()
        {
            Object.DestroyImmediate(hydraObject);
            Object.DestroyImmediate(playerObject);
        }

        /// <summary>Well outside the gap, the hydra comes at the player.</summary>
        [Test]
        public void AHydraTooFarAwayClosesOnThePlayer()
        {
            Assert.That(GroundVelocityAt(6f).x, Is.GreaterThan(0.01f));
        }

        /// <summary>
        /// Inside the gap it backs off rather than pressing on into the player.
        /// </summary>
        [Test]
        public void AHydraTooCloseBacksOff()
        {
            Assert.That(GroundVelocityAt(1.01f).x, Is.LessThan(-0.01f));
        }

        /// <summary>Inside the dead zone it holds the gap and stands still.</summary>
        [Test]
        public void AHydraAtItsGapHoldsStill()
        {
            float reachableGap = EnemyBase.StandOffInsideReach(StandOff, 0.42f);
            Assert.That(GroundVelocityAt(1f + reachableGap).magnitude, Is.LessThan(0.01f));
        }

        /// <summary>
        /// Drives one physics step with the player that far along +X from the
        /// hydra, and reports the velocity the hydra asked for.
        /// </summary>
        private Vector2 GroundVelocityAt(float distance)
        {
            hydraObject.transform.position = Vector3.zero;
            playerObject.transform.position = new Vector3(distance, 0f, 0f);
            Physics.SyncTransforms();
            hydra.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;

            typeof(HydraEnemyScript)
                .GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(hydra, System.Array.Empty<object>());

            return hydra.GetComponent<Rigidbody>().linearVelocity.ToGround();
        }

        private void Set(string name, object value)
        {
            for (System.Type type = typeof(HydraEnemyScript); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
                if (field == null)
                    continue;
                field.SetValue(hydra, value);
                return;
            }

            throw new System.MissingFieldException(nameof(HydraEnemyScript), name);
        }
    }
}
