using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class DungeonNavigatorCoverageTests
    {
        [Test]
        public void PathCornerSelectionSkipsReachedIntermediateCorners()
        {
            Vector3[] corners =
            {
                Vector3.zero,
                Vector3.right * 0.25f,
                Vector3.right * 0.5f,
                Vector3.right * 5f,
            };
            Assert.That(
                DungeonEnemyNavigator.SelectPathCorner(corners, Vector3.zero),
                Is.EqualTo(corners[3])
            );
            Assert.That(
                DungeonEnemyNavigator.SelectPathCorner(corners, Vector3.left * 10f),
                Is.EqualTo(corners[1])
            );
            Vector3 slide = Vector3.forward * 3f;
            Assert.That(
                DungeonEnemyNavigator.SelectInitialSteeringTarget(
                    true,
                    slide,
                    corners,
                    Vector3.zero
                ),
                Is.EqualTo(slide)
            );
            Assert.That(
                DungeonEnemyNavigator.SelectInitialSteeringTarget(
                    false,
                    slide,
                    corners,
                    Vector3.zero
                ),
                Is.EqualTo(corners[3])
            );
        }

        [TestCase(0f, 1f, 1f, 0f, 1)]
        [TestCase(0f, 1f, 1f, 0f, -1)]
        [TestCase(0f, 0f, 1f, 0f, 1)]
        [TestCase(0f, 0f, 1f, 0f, -1)]
        [TestCase(1f, 0f, 0f, 1f, 1)]
        public void SlideDirectionCoversNormalFallbackAndSidePolicies(
            float normalX,
            float normalY,
            float directionX,
            float directionY,
            int side
        )
        {
            Vector2 result = DungeonEnemyNavigator.CalculateSlideDirection(
                new Vector2(normalX, normalY),
                new Vector2(directionX, directionY),
                side
            );
            Assert.That(result.sqrMagnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void PathApplicationRecoveryAndFixedUpdateCoverDeterministicRuntimeOutcomes()
        {
            GameObject host = new("Coverage deterministic navigator");
            GameObject player = new("Coverage navigator player");
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            host.SetActive(false);
            try
            {
                host.AddComponent<Rigidbody>();
                host.AddComponent<BoxCollider>();
                EnemyScript enemy = host.AddComponent<EnemyScript>();
                DungeonEnemyNavigator navigator = host.AddComponent<DungeonEnemyNavigator>();
                Set(navigator, "enemy", enemy);
                Set(navigator, "realPlayer", player.transform);
                Set(navigator, "path", new NavMeshPath());
                enemy.player = player.transform;

                DungeonEnemyNavigator.ObstacleSlide noSlide = (
                    Vector3 _,
                    Vector3 _,
                    Vector3 _,
                    out Vector3 target
                ) =>
                {
                    target = default;
                    return false;
                };
                Vector3[] direct = { Vector3.zero, Vector3.right };
                navigator.ApplyPath(
                    direct,
                    NavMeshPathStatus.PathComplete,
                    Vector3.zero,
                    Vector3.right,
                    Vector3.zero,
                    noSlide
                );
                Assert.That(enemy.player, Is.SameAs(player.transform));

                DungeonEnemyNavigator.ObstacleSlide slide = (
                    Vector3 _,
                    Vector3 _,
                    Vector3 _,
                    out Vector3 target
                ) =>
                {
                    target = Vector3.forward * 2f;
                    return true;
                };
                Vector3[] corners = { Vector3.zero, Vector3.right, Vector3.forward };
                navigator.ApplyPath(
                    corners,
                    NavMeshPathStatus.PathPartial,
                    Vector3.zero,
                    Vector3.forward,
                    Vector3.zero,
                    slide
                );
                navigator.SteerDirectlyOrSlide(Vector3.zero, Vector3.forward, slide);
                Assert.That(enemy.player, Is.Not.SameAs(player.transform));

                Assert.That(
                    DungeonEnemyNavigator.TryRecoveryDirections(
                        Vector2.right,
                        1,
                        (Vector2 _, out Vector3 target) =>
                        {
                            target = default;
                            return false;
                        },
                        out _
                    ),
                    Is.False
                );

                int attempts = 0;
                Assert.That(
                    DungeonEnemyNavigator.TryRecoveryDirections(
                        Vector2.right,
                        -1,
                        (Vector2 _, out Vector3 target) =>
                        {
                            target = Vector3.one;
                            return ++attempts == 2;
                        },
                        out Vector3 recovery
                    ),
                    Is.True
                );
                Assert.That(recovery, Is.EqualTo(Vector3.one));

                Set(navigator, "proxy", null);
                player.transform.position = host.transform.position;
                object[] recoveryArguments = { Vector3.zero };
                Invoke(navigator, "TryPickRecoveryTarget", recoveryArguments);
                player.transform.position = Vector3.right;
                host.transform.position = Vector3.up * 1000f;
                Invoke(navigator, "TryPickRecoveryTarget", recoveryArguments);
                host.transform.position = Vector3.zero;

                obstacle.transform.position = new Vector3(0.9f, 0.75f, 0f);
                Physics.SyncTransforms();
                object[] slideArguments =
                {
                    Vector3.zero,
                    Vector3.right * 3f,
                    Vector3.zero,
                    Vector3.zero,
                };
                Invoke(navigator, "TryGetObstacleSlide", slideArguments);
                Set(navigator, "nextProgressCheck", Time.time + 10f);
                Set(navigator, "recoveryUntil", 0f);
                Set(navigator, "nextRepath", 0f);
                Invoke(navigator, "FixedUpdate");
            }
            finally
            {
                Object.DestroyImmediate(obstacle);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(host);
            }
        }

        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        private static object Invoke(object target, string name, params object[] arguments) =>
            target.GetType().GetMethod(name, Hidden).Invoke(target, arguments);

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Hidden).SetValue(target, value);
    }
}
