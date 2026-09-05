using System;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class EnemyDamageAggroTests
    {
        private GameObject enemyObject;
        private GameObject playerObject;

        private EnemyBase CreateEnemy(Type type, Vector2 position)
        {
            playerObject = new GameObject("Distant damage source");
            playerObject.transform.position = new Vector3(100f, 0f, 0f);
            enemyObject = new GameObject("Damage aggro enemy");
            enemyObject.transform.position = new Vector3(position.x, 0f, position.y);
            enemyObject.AddComponent<Rigidbody>();
            enemyObject.AddComponent<CapsuleCollider>();
            var enemy = (EnemyBase)enemyObject.AddComponent(type);
            enemy.player = playerObject.transform;
            enemy.SetLeashHome(Vector2.zero);
            return enemy;
        }

        [TearDown]
        public void TearDown()
        {
            if (enemyObject != null)
                UnityEngine.Object.DestroyImmediate(enemyObject);
            if (playerObject != null)
                UnityEngine.Object.DestroyImmediate(playerObject);
        }

        [TestCase(typeof(EnemyScript))]
        [TestCase(typeof(ShootingEnemyScript))]
        [TestCase(typeof(HydraEnemyScript))]
        public void LongRangeDamageWakesDormantEnemy(Type type)
        {
            var enemy = CreateEnemy(type, Vector2.zero);
            enemy.enabled = false;
            enemy.TakeDamage(1f);
            Assert.That(enemy.enabled, Is.True);
            Assert.That(enemy.IsPursuing, Is.True);
            Assert.That(enemy.ResolveChaseTarget(Time.time), Is.EqualTo(new Vector2(100f, 0f)));
            Assert.That(enemy.Health, Is.EqualTo(49f));
        }

        [Test]
        public void DamageOverridesReturnHomeButDoesNotRemoveLeash()
        {
            var enemy = CreateEnemy(typeof(EnemyScript), new Vector2(30f, 0f));
            Assert.That(enemy.ResolveChaseTarget(Time.time), Is.EqualTo(Vector2.zero));
            Assert.That(enemy.IsPursuing, Is.False);
            enemy.TakeDamage(1f, Vector2.zero, 0f);
            Assert.That(enemy.IsPursuing, Is.True);
            Assert.That(enemy.ResolveChaseTarget(Time.time), Is.EqualTo(new Vector2(100f, 0f)));
            Assert.That(
                enemy.ResolveChaseTarget(Time.time + EnemyBase.DamageAggroDuration + 1f),
                Is.EqualTo(Vector2.zero)
            );
            Assert.That(enemy.IsPursuing, Is.False);
        }

        [TestCase(0f)]
        [TestCase(-10f)]
        public void NonDamagingHitDoesNotWakeEnemy(float damage)
        {
            var enemy = CreateEnemy(typeof(EnemyScript), Vector2.zero);
            enemy.enabled = false;
            enemy.TakeDamage(damage);
            Assert.That(enemy.enabled, Is.False);
            Assert.That(enemy.Health, Is.EqualTo(50f));
        }

        [Test]
        public void PoolReuseForgetsDamageAggro()
        {
            var enemy = CreateEnemy(typeof(EnemyScript), new Vector2(30f, 0f));
            enemy.TakeDamage(1f);
            enemy.ResetForPool();
            enemy.SetLeashHome(Vector2.zero);
            Assert.That(enemy.ResolveChaseTarget(Time.time), Is.EqualTo(Vector2.zero));
            Assert.That(enemy.IsPursuing, Is.False);
        }

        [Test]
        public void KillingHitDoesNotWakeEnemy()
        {
            var enemy = (DamageAggroDeathProbe)CreateEnemy(
                typeof(DamageAggroDeathProbe),
                Vector2.zero
            );
            enemy.enabled = false;
            enemy.TakeDamage(50f);
            Assert.That(enemy.enabled, Is.False);
            Assert.That(enemy.DeathCalled, Is.True);
            Assert.That(enemy.Health, Is.Zero);
        }
    }

    // Isolate lethal-hit dispatch from death audio and animation, which need play mode.
    public sealed class DamageAggroDeathProbe : EnemyBase
    {
        public bool DeathCalled { get; private set; }

        public override void Die() => DeathCalled = true;
    }
}
