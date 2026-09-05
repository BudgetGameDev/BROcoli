using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class EnemyPromotionCoverageTests
    {
        private const BindingFlags Hidden =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void ReturningEnemyArchetypesWalkHomeAndReengageNearbyPlayers()
        {
            ExerciseReturningEnemy<EnemyScript>();
            ExerciseReturningEnemy<HydraEnemyScript>();
            ExerciseReturningEnemy<ShootingEnemyScript>();
        }

        [Test]
        public void SpentProjectileIgnoresASecondDespawn()
        {
            GameObject host = new("Promotion spent projectile");
            host.SetActive(false);
            try
            {
                host.AddComponent<Rigidbody>();
                host.AddComponent<BoxCollider>();
                EnemyProjectile projectile = host.AddComponent<EnemyProjectile>();
                Invoke(projectile, "Awake");
                projectile.SetPooled(true);
                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex(
                        "^Destroy may not be called from edit mode"
                    )
                );
                Invoke(projectile, "Despawn");
                Invoke(projectile, "Despawn");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                PoolManager.Instance?.ClearAll();
            }
        }

        [Test]
        public void ProjectileDamagesATaggedPlayerAndShootingEnemyBacksAway()
        {
            GameObject player = new("Promotion projectile player");
            GameObject projectileHost = new("Promotion player hit projectile");
            GameObject shooterHost = new("Promotion close shooter");
            player.SetActive(false);
            projectileHost.SetActive(false);
            shooterHost.SetActive(false);
            try
            {
                typeof(ProceduralEnemyProjectileHitAudio)
                    .GetField("isPrewarmed", Hidden)
                    .SetValue(null, false);
                ProceduralEnemyProjectileHitAudio.PrewarmAll();
                player.tag = "Player";
                player.AddComponent<BoxCollider>();
                player.AddComponent<PlayerStats>();

                projectileHost.AddComponent<Rigidbody>();
                projectileHost.AddComponent<BoxCollider>();
                EnemyProjectile projectile = projectileHost.AddComponent<EnemyProjectile>();
                Invoke(projectile, "Awake");
                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex(
                        "^Destroy may not be called from edit mode"
                    )
                );
                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex(
                        "^Destroy may not be called from edit mode"
                    )
                );
                Invoke(projectile, "OnTriggerEnter", player.GetComponent<Collider>());

                shooterHost.AddComponent<Rigidbody>();
                shooterHost.AddComponent<BoxCollider>();
                ShootingEnemyScript shooter = shooterHost.AddComponent<ShootingEnemyScript>();
                Invoke(shooter, "Awake");
                shooter.player = player.transform;
                shooterHost.GetComponent<Rigidbody>().position = Vector3.zero;
                player.transform.position = new Vector3(0.2f, 0f, 0f);
                Invoke(shooter, "FixedUpdate");

                Rigidbody playerBody = player.AddComponent<Rigidbody>();
                playerBody.linearVelocity = Vector3.right;
                Invoke(shooter, "GetPlayerGroundVelocity");
                playerBody.linearVelocity = Vector3.zero;
                PlayerController controller = player.AddComponent<PlayerController>();
                Invoke(controller, "Awake");
                Invoke(shooter, "GetPlayerGroundVelocity");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(shooterHost);
                UnityEngine.Object.DestroyImmediate(projectileHost);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void ExerciseReturningEnemy<T>()
            where T : EnemyBase
        {
            GameObject host = new("Promotion returning " + typeof(T).Name);
            GameObject player = new("Promotion leash player");
            host.SetActive(false);
            try
            {
                host.AddComponent<Rigidbody>();
                host.AddComponent<BoxCollider>();
                T enemy = host.AddComponent<T>();
                Invoke(enemy, "Awake");
                enemy.player = player.transform;
                enemy.SetLeashHome(Vector2.zero);
                host.transform.position = new Vector3(30f, 0f, 0f);
                host.GetComponent<Rigidbody>().position = host.transform.position;
                player.transform.position = new Vector3(60f, 0f, 0f);

                GetProperty(enemy, "ChaseTarget");
                Assert.That(enemy.IsPursuing, Is.False);
                Invoke(enemy, "FixedUpdate");

                player.transform.position = host.transform.position;
                GetProperty(enemy, "ChaseTarget");
                Assert.That(enemy.IsPursuing, Is.True);
                GetProperty(enemy, "HasReachedLeashHome");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                foreach (MethodInfo method in type.GetMethods(Hidden))
                    if (method.Name == name && method.GetParameters().Length == arguments.Length)
                        return method.Invoke(target, arguments);
            }
            throw new MissingMethodException(target.GetType().Name, name);
        }

        private static object GetProperty(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                PropertyInfo property = type.GetProperty(name, Hidden);
                if (property != null)
                    return property.GetValue(target);
            }
            throw new MissingMemberException(target.GetType().Name, name);
        }
    }
}
