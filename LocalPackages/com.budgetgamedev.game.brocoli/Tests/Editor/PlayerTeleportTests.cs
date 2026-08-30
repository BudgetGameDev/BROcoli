using System.Reflection;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class PlayerTeleportTests
    {
        [Test]
        public void TeleportMovesTheRigidbodyWithTheTransform()
        {
            var player = new GameObject("Player teleport test");
            try
            {
                PlayerMovement movement = AwakenMovement(player);
                Rigidbody body = player.GetComponent<Rigidbody>();

                movement.SetPosition(new Vector2(12f, -7f));

                // A transform-only move leaves the body behind, and ProcessMovement
                // steps the player on from Rigidbody.position, so the next physics
                // frame would drag a restored run back to where it started.
                Assert.That(player.transform.position, Is.EqualTo(new Vector3(12f, 0f, -7f)));
                Assert.That(body.position, Is.EqualTo(new Vector3(12f, 0f, -7f)));
                Assert.That(movement.Position, Is.EqualTo(new Vector2(12f, -7f)));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void TeleportKeepsHeightAndRestoresInterpolation()
        {
            var player = new GameObject("Player teleport test");
            try
            {
                player.transform.position = new Vector3(0f, 0.5f, 0f);
                PlayerMovement movement = AwakenMovement(player);
                Rigidbody body = player.GetComponent<Rigidbody>();
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;

                movement.SetPosition(new Vector2(3f, 4f));

                Assert.That(player.transform.position.y, Is.EqualTo(0.5f));
                Assert.That(body.position.y, Is.EqualTo(0.5f));
                Assert.That(body.interpolation, Is.EqualTo(RigidbodyInterpolation.Interpolate));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static PlayerMovement AwakenMovement(GameObject player)
        {
            // Standing up the movement component alone is enough for a teleport.
            LogAssert.Expect(
                LogType.Warning,
                "PlayerMovement: No PlayerStats found - using default speed!"
            );

            // Added by hand: RequireComponent cannot add the abstract Collider type.
            player.AddComponent<Rigidbody>();
            player.AddComponent<CapsuleCollider>();
            PlayerMovement movement = player.AddComponent<PlayerMovement>();
            typeof(PlayerMovement)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(movement, null);
            return movement;
        }
    }
}
