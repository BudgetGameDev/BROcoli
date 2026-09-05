using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class ProjectileDamageWindowTests
    {
        private GameObject root;
        private PlayerStats stats;
        private PlayerDamageHandler handler;
        private Collider collider;

        [SetUp]
        public void CreatePlayer()
        {
            root = new GameObject("Projectile damage window");
            root.SetActive(false);
            collider = root.AddComponent<CapsuleCollider>();
            stats = root.AddComponent<PlayerStats>();
            stats.ResetStats();
            handler = root.AddComponent<PlayerDamageHandler>();
            Set("_playerStats", stats);
        }

        [TearDown]
        public void Cleanup() => Object.DestroyImmediate(root);

        [Test]
        public void SimultaneousProjectilesCannotBypassTheExistingHitWindow()
        {
            Assert.That(handler.TakeProjectileDamage(0f), Is.False);
            Assert.That(EnemyProjectile.DamagePlayer(collider, 20f), Is.True);
            for (int hit = 0; hit < 5; hit++)
                Assert.That(EnemyProjectile.DamagePlayer(collider, 20f), Is.False);
            Assert.That(stats.CurrentHealth, Is.EqualTo(80f));
            Set("_lastDamageTime", Time.time - 0.31f);
            Assert.That(EnemyProjectile.DamagePlayer(collider, 20f), Is.True);
            Assert.That(stats.CurrentHealth, Is.EqualTo(60f));
        }

        [Test]
        public void MeleeAndProjectileHitsShareOneRecoveryWindow()
        {
            Assert.That(handler.TakeMeleeDamage(15f), Is.True);
            Assert.That(EnemyProjectile.DamagePlayer(collider, 30f), Is.False);
            Set("_lastDamageTime", Time.time - 0.31f);
            Assert.That(EnemyProjectile.DamagePlayer(collider, 30f), Is.True);
            Assert.That(handler.TakeMeleeDamage(15f), Is.False);
            Assert.That(stats.CurrentHealth, Is.EqualTo(55f));
        }

        private void Set(string name, object value) =>
            typeof(PlayerDamageHandler)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(handler, value);
    }
}
