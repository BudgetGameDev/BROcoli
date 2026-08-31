using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class AutoplayRemainderCoverageTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void IntentNavigationAndPathCornersCoverEveryRoutingOutcome()
        {
            GameObject host = new("Coverage autoplay remainder");
            host.SetActive(false);
            try
            {
                BotDriver bot = host.AddComponent<BotDriver>();
                Invoke(bot, "Awake");
                Set(bot, "player", host.transform);
                Set(bot, "lastDodge", Vector2.right);
                Set(bot, "recoveryDirection", Vector2.up);
                var enemies = new BotDriver.EnemyObservation(
                    1,
                    1,
                    10f,
                    Vector2.right * 10f,
                    Vector2.right * 10f,
                    Vector2.left
                );

                foreach (BotIntent intent in Enum.GetValues(typeof(BotIntent)))
                    Invoke(bot, "NavigateIntent", intent, Vector2.zero, enemies);
                Assert.That(
                    (Vector2)Invoke(
                        bot,
                        "NavigateIntent",
                        (BotIntent)int.MaxValue,
                        Vector2.zero,
                        enemies
                    ),
                    Is.EqualTo(Vector2.zero)
                );

                Vector3[] corners =
                {
                    Vector3.zero,
                    new Vector3(0.1f, 0f, 0.1f),
                    Vector3.right * 3f,
                };
                Assert.That(
                    BotDriver.SelectPathDirection(corners, 3, Vector2.zero, Vector2.up),
                    Is.EqualTo(Vector2.right * 3f)
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ProjectilePerceptionAndObstacleFilteringCoverMissingBodies()
        {
            GameObject host = new("Coverage autoplay obstacle");
            GameObject projectile = new("Coverage projectile without body");
            host.SetActive(false);
            projectile.SetActive(false);
            try
            {
                BotDriver bot = host.AddComponent<BotDriver>();
                Invoke(bot, "Awake");
                Set(bot, "player", host.transform);

                projectile.AddComponent<BoxCollider>();
                projectile.AddComponent<EnemyProjectile>();
                projectile.SetActive(true);
                Physics.SyncTransforms();
                Collider collider = projectile.GetComponent<Collider>();
                Assert.That((bool)Invoke(bot, "IsNavigationObstacle", collider), Is.False);
                Assert.That(BotDriver.TryGetProjectileVelocity(null, out _), Is.False);

                Invoke(bot, "ComputeProjectileDodge", Vector2.zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(projectile);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EnemyPerceptionSkipsDyingAndCoincidentEnemies()
        {
            GameObject host = new("Coverage autoplay perception");
            GameObject dyingObject = new("Coverage dying enemy");
            GameObject coincidentObject = new("Coverage coincident enemy");
            host.SetActive(false);
            dyingObject.SetActive(false);
            coincidentObject.SetActive(false);
            try
            {
                BotDriver bot = host.AddComponent<BotDriver>();
                Invoke(bot, "Awake");
                Set(bot, "player", host.transform);
                dyingObject.AddComponent<Rigidbody>();
                dyingObject.AddComponent<BoxCollider>();
                coincidentObject.AddComponent<Rigidbody>();
                coincidentObject.AddComponent<BoxCollider>();
                EnemyScript dying = dyingObject.AddComponent<EnemyScript>();
                EnemyScript coincident = coincidentObject.AddComponent<EnemyScript>();
                typeof(EnemyBase).GetField("isDying", Hidden).SetValue(dying, true);
                EnemySpatialHash hash = EnemySpatialHash.Instance;
                hash.Clear();
                hash.Register(dying);
                hash.Register(coincident);
                Invoke(bot, "ObserveEnemies", Vector2.zero);
                hash.Clear();
            }
            finally
            {
                Object.DestroyImmediate(coincidentObject);
                Object.DestroyImmediate(dyingObject);
                Object.DestroyImmediate(host);
            }
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            foreach (MethodInfo method in target.GetType().GetMethods(Hidden))
                if (method.Name == name && method.GetParameters().Length == arguments.Length)
                    return method.Invoke(target, arguments);
            throw new MissingMethodException(target.GetType().Name, name);
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Hidden).SetValue(target, value);
    }
}
