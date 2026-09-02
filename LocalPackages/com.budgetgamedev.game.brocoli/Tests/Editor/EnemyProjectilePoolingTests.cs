using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Covers the recycling half of a pooled enemy shot. A spent projectile used to
    /// destroy itself, so its pool kept the destroyed reference on loan forever and
    /// a heavy fight wedged that pool at its cap after a hundred shots.
    /// </summary>
    public sealed class EnemyProjectilePoolingTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        // Comfortably past the 100-shot projectile pool cap.
        private const int ShotsPastTheCap = 150;

        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly HashSet<int> _awakened = new HashSet<int>();

        [SetUp]
        public void SetUp()
        {
            PoolManager.Instance.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            PoolManager.Instance.ClearAll();
            _awakened.Clear();
            foreach (GameObject created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void ShotsThatOutliveTheirLifetimeComeBackForReuse()
        {
            PoolManager pool = PoolManager.Instance;
            EnemyProjectile prefab = NewProjectilePrefab();

            for (int shot = 0; shot < ShotsPastTheCap; shot++)
            {
                EnemyProjectile live = Fire(pool, prefab);
                Assert.That(
                    live,
                    Is.Not.Null,
                    $"Shot {shot} found the pool empty, so spent shots are not coming back."
                );

                live.Init(Vector2.right);
                Expire(live);
                Invoke(live, "Update");

                Assert.That(
                    live.gameObject.activeSelf,
                    Is.False,
                    "An expired shot parks itself switched off."
                );
            }

            Assert.That(
                ProjectilePool().TotalCount,
                Is.EqualTo(1),
                "One projectile served every shot, so the pool never had to grow."
            );
        }

        [Test]
        public void AShotStoppedByAWallGoesBackToThePoolOnlyOnce()
        {
            PoolManager pool = PoolManager.Instance;
            EnemyProjectile prefab = NewProjectilePrefab();
            Collider wall = NewObject("PoolingTestWall").AddComponent<BoxCollider>();

            EnemyProjectile live = Fire(pool, prefab);
            live.Init(Vector2.right);

            Invoke(live, "OnTriggerEnter", wall);

            ObjectPool<EnemyProjectile> projectilePool = ProjectilePool();
            Assert.That(projectilePool.ActiveCount, Is.EqualTo(0));
            Assert.That(projectilePool.AvailableCount, Is.EqualTo(1));

            // A second contact in the same frame must not hand the same shot back
            // twice; the pool would then lend one projectile to two shooters.
            Invoke(live, "OnTriggerEnter", wall);

            Assert.That(projectilePool.AvailableCount, Is.EqualTo(1));
        }

        [Test]
        public void ARecycledShotFliesAgainAtFullSize()
        {
            PoolManager pool = PoolManager.Instance;
            EnemyProjectile prefab = NewProjectilePrefab();
            Transform visual = NewObject("PoolingTestVisual").transform;
            visual.SetParent(prefab.transform);
            SetField(prefab, "visualTransform", visual);

            EnemyProjectile live = Fire(pool, prefab);
            Transform liveVisual = live.transform.GetChild(0);
            Vector3 fullSize = liveVisual.localScale;

            live.Init(Vector2.right);

            // One frame inside the fizzle window shrinks the visual, then the shot
            // runs out and is recycled while still small.
            SetField(live, "spawnTime", Time.time - (live.lifeTime - 1f));
            Invoke(live, "Update");
            Assert.That(
                liveVisual.localScale,
                Is.Not.EqualTo(fullSize),
                "The fizzle must have shrunk the visual for this test to mean anything."
            );

            Expire(live);
            Invoke(live, "Update");

            EnemyProjectile reused = Fire(pool, prefab);
            reused.Init(Vector2.right);

            Assert.That(reused, Is.EqualTo(live), "The expired shot is the one handed back out.");
            Assert.That(
                liveVisual.localScale,
                Is.EqualTo(fullSize),
                "A reused shot must not inherit the shrunken scale its fizzle left behind."
            );
        }

        /// <summary>
        /// Takes a shot from the pool. Edit mode never runs Awake, so a freshly
        /// instantiated copy gets one by hand the first time it is handed out.
        /// </summary>
        private EnemyProjectile Fire(PoolManager pool, EnemyProjectile prefab)
        {
            EnemyProjectile live = pool.GetProjectile(prefab, Vector3.zero, Quaternion.identity);
            if (live != null && _awakened.Add(live.GetInstanceID()))
                Invoke(live, "Awake");
            return live;
        }

        private EnemyProjectile NewProjectilePrefab()
        {
            GameObject host = NewObject("PoolingTestProjectile");
            host.AddComponent<SphereCollider>().isTrigger = true;
            host.AddComponent<Rigidbody>().useGravity = false;
            EnemyProjectile projectile = host.AddComponent<EnemyProjectile>();
            projectile.lifeTime = 5f;
            projectile.speed = 5f;
            return projectile;
        }

        private GameObject NewObject(string objectName)
        {
            var host = new GameObject(objectName);
            _created.Add(host);
            return host;
        }

        private static ObjectPool<EnemyProjectile> ProjectilePool()
        {
            var pools =
                (Dictionary<int, ObjectPool<EnemyProjectile>>)
                    typeof(PoolManager)
                        .GetField("_projectilePools", PrivateInstance)
                        .GetValue(PoolManager.Instance);
            return pools.Values.Single();
        }

        /// <summary>Backdates a shot so its next frame counts as its last.</summary>
        private static void Expire(EnemyProjectile projectile)
        {
            SetField(projectile, "spawnTime", Time.time - projectile.lifeTime - 1f);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
        }

        private static void Invoke(object target, string name, params object[] arguments)
        {
            target.GetType().GetMethod(name, PrivateInstance).Invoke(target, arguments);
        }
    }
}
