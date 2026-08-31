using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class LegacyRemainderCoverageTests
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        [TestMustExpectAllLogs(false)]
        public void CombatAssetsAndEmptyTargetCandidatesCoverFailurePaths()
        {
            LogAssert.ignoreFailingMessages = true;
            GameObject host = new("Coverage combat remainder");
            host.SetActive(false);
            try
            {
                PlayerCombat combat = host.AddComponent<PlayerCombat>();
                combat.ConfigureCombatAssets(null, 0);
                combat.HandleCombat(new Collider[] { null }, Vector2.zero, 5f);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        [TestMustExpectAllLogs(false)]
        public void DamageInputAudioAndAnimatorCoverRemainingGuards()
        {
            LogAssert.ignoreFailingMessages = true;
            GameObject host = new("Coverage legacy remainder");
            GameObject utilityHost = new("Coverage legacy utilities");
            host.SetActive(false);
            utilityHost.SetActive(false);
            GameObject collision = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                PlayerStats stats = host.AddComponent<PlayerStats>();
                stats.ResetStats();
                PlayerDamageHandler damage = host.AddComponent<PlayerDamageHandler>();
                Invoke(damage, "Awake");

                float savedMaximum = Get<float>(stats, "_currentMaxHealth");
                Set(stats, "_currentMaxHealth", 0f);
                Assert.That(
                    (float)Invoke(damage, "CalculateDamageIntensity", 2f),
                    Is.EqualTo(0.5f)
                );
                Set(stats, "_currentMaxHealth", savedMaximum);

                collision.tag = "Enemy";
                Assert.That(
                    SprayDamageHandler.TryGetEnemy(collision.GetComponent<Collider>(), out _),
                    Is.False
                );
                damage.HandleCollision(collision.GetComponent<Collider>());
                collision.tag = "Projectile";
                damage.HandleCollision(collision.GetComponent<Collider>());

                Set(damage, "_gameOver", true);
                Assert.That(damage.TakeMeleeDamage(1f), Is.False);
                damage.HandleCollision(collision.GetComponent<Collider>());
                damage.CheckForDeath();
                damage.TriggerGameOver();
                Set(damage, "_gameOver", false);

                PlayerInputHandler input = host.AddComponent<PlayerInputHandler>();
                input.ApplyResolvedInput(Vector2.right, 0f);
                Assert.That(input.LastNonZeroInput, Is.EqualTo(Vector2.right));

                utilityHost.AddComponent<BoxCollider>();
                PlayerMovement movement = utilityHost.AddComponent<PlayerMovement>();
                Invoke(movement, "UpdateAnimator", Vector2.one);
                Assert.That(
                    PlayerMovement.TryNormalizeContact(Vector2.zero, out Vector2 normal),
                    Is.False
                );
                Assert.That(normal, Is.EqualTo(Vector2.zero));

                PlayerAudioHandler audio = utilityHost.AddComponent<PlayerAudioHandler>();
                Invoke(audio, "LoadClip", "Brocoli/Coverage/MissingClip");

                Set(damage, "_deathVisual", null);
                IEnumerator death = (IEnumerator)Invoke(damage, "PlayDeathSequence", 0, 0, 0, 0f);
                Assert.That(death.MoveNext(), Is.True);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                UnityEngine.Object.DestroyImmediate(collision);
                UnityEngine.Object.DestroyImmediate(utilityHost);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        [TestMustExpectAllLogs(false)]
        public void ProjectilePairTargetAndAudioCoverHitAndClippingPaths()
        {
            GameObject projectileObject = new("Coverage legacy projectile");
            GameObject enemyObject = new("Coverage projectile enemy");
            GameObject first = new("Coverage first pair");
            GameObject second = new("Coverage second pair");
            projectileObject.SetActive(false);
            enemyObject.SetActive(false);
            try
            {
                float bestDamage = 0f;
                Transform bestTarget = null;
                Assert.That(
                    PlayerCombat.PreferPairTarget(
                        1f,
                        first.transform,
                        2f,
                        second.transform,
                        1f,
                        ref bestDamage,
                        ref bestTarget
                    ),
                    Is.True
                );
                Assert.That(bestTarget, Is.SameAs(second.transform));
                Assert.That(
                    PlayerCombat.PreferPairTarget(
                        0.5f,
                        first.transform,
                        1f,
                        second.transform,
                        2f,
                        ref bestDamage,
                        ref bestTarget
                    ),
                    Is.False
                );

                ProceduralEnemyProjectileHitAudio enemyAudio =
                    projectileObject.AddComponent<ProceduralEnemyProjectileHitAudio>();
                Assert.That((float)Invoke(enemyAudio, "SoftClip", 2f), Is.LessThan(2f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(enemyObject);
                UnityEngine.Object.DestroyImmediate(projectileObject);
            }
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                foreach (MethodInfo method in type.GetMethods(PrivateInstance))
                    if (method.Name == name && method.GetParameters().Length == arguments.Length)
                        return method.Invoke(target, arguments);
            }
            throw new MissingMethodException(target.GetType().Name, name);
        }

        private static void Set(object target, string name, object value)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, PrivateInstance);
                if (field == null)
                    continue;
                field.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, name);
        }

        private static T Get<T>(object target, string name)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, PrivateInstance);
                if (field != null)
                    return (T)field.GetValue(target);
            }
            throw new MissingFieldException(target.GetType().Name, name);
        }

        private static void Drain(IEnumerator routine)
        {
            for (int step = 0; routine.MoveNext() && step < 16; step++)
                if (routine.Current is IEnumerator nested)
                    Drain(nested);
        }
    }
}
