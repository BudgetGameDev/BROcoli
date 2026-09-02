using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Covers the recycling contract of the shared pool: what it hands out, what
    /// it refuses, and what it destroys. Transform stands in for a pooled prefab
    /// because every GameObject already has one.
    /// </summary>
    public sealed partial class ObjectPoolTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();
        private Transform _prefab;
        private Transform _parent;

        [SetUp]
        public void SetUp()
        {
            _prefab = NewObject("PoolPrefab").transform;
            _parent = NewObject("PoolParent").transform;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void PreWarmParksInactiveCopiesAndStopsAtTheCap()
        {
            var pool = new ObjectPool<Transform>(
                _prefab,
                initialSize: 5,
                maxSize: 2,
                parent: _parent
            );

            Assert.That(pool.AvailableCount, Is.EqualTo(2));
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(pool.TotalCount, Is.EqualTo(2));
            Assert.That(_parent.childCount, Is.EqualTo(2));
            for (int i = 0; i < _parent.childCount; i++)
            {
                Assert.That(
                    _parent.GetChild(i).gameObject.activeSelf,
                    Is.False,
                    "A pre-warmed copy waits switched off."
                );
            }

            pool.Clear();
        }

        [Test]
        public void GetDrainsThePoolBeforeInstantiatingAnythingNew()
        {
            var pool = new ObjectPool<Transform>(_prefab, initialSize: 1, parent: _parent);

            Transform first = pool.Get();
            Assert.That(first.gameObject.activeSelf, Is.True);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.AvailableCount, Is.EqualTo(0));
            Assert.That(pool.TotalCount, Is.EqualTo(1));

            Transform second = pool.Get();
            Assert.That(second, Is.Not.SameAs(first), "An empty pool has to grow.");
            Assert.That(second.name, Is.EqualTo("PoolPrefab (Pooled)"));
            Assert.That(pool.TotalCount, Is.EqualTo(2));

            pool.Return(first);
            Assert.That(first.gameObject.activeSelf, Is.False);
            Assert.That(pool.AvailableCount, Is.EqualTo(1));
            Assert.That(pool.Get(), Is.SameAs(first), "A returned object is the next one out.");

            pool.Clear();
        }

        [Test]
        public void GetWithAPoseMovesFreshAndRecycledObjectsBeforeTheyWakeUp()
        {
            var pool = new ObjectPool<Transform>(_prefab, parent: _parent);
            var spawn = new Vector3(1f, 2f, 3f);
            Quaternion facing = Quaternion.Euler(0f, 90f, 0f);

            Transform fresh = pool.Get(spawn, facing);
            Assert.That(Vector3.Distance(fresh.position, spawn), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(fresh.rotation, facing), Is.LessThan(0.01f));

            pool.Return(fresh);
            var respawn = new Vector3(-4f, 5f, 6f);
            Transform recycled = pool.Get(respawn, Quaternion.identity);

            Assert.That(recycled, Is.SameAs(fresh));
            Assert.That(
                Vector3.Distance(recycled.position, respawn),
                Is.LessThan(0.0001f),
                "A recycled object must be moved before it is switched on."
            );

            pool.Clear();
        }

        [Test]
        public void ACappedPoolRefusesToGrowAndSaysSo()
        {
            var pool = new ObjectPool<Transform>(_prefab, maxSize: 1, parent: _parent);
            Transform only = pool.Get();
            Assert.That(only, Is.Not.Null);

            LogAssert.Expect(
                LogType.Warning,
                "[ObjectPool] Pool for PoolPrefab at max capacity (1)"
            );
            Assert.That(pool.Get(), Is.Null);

            LogAssert.Expect(
                LogType.Warning,
                "[ObjectPool] Pool for PoolPrefab at max capacity (1)"
            );
            Assert.That(pool.Get(Vector3.zero, Quaternion.identity), Is.Null);

            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            pool.Clear();
        }

        [Test]
        public void ReturningAStrangerIsRefusedAndReturningNothingIsIgnored()
        {
            var pool = new ObjectPool<Transform>(_prefab, parent: _parent);
            Transform stranger = NewObject("Foreign").transform;

            LogAssert.Expect(
                LogType.Warning,
                "[ObjectPool] Trying to return object not from this pool: Foreign"
            );
            pool.Return(stranger);

            Assert.That(pool.AvailableCount, Is.EqualTo(0));
            Assert.That(
                stranger.gameObject.activeSelf,
                Is.True,
                "A stranger must not be switched off by a pool that does not own it."
            );

            pool.Return(null);
            Assert.That(pool.TotalCount, Is.EqualTo(0));
        }

        [Test]
        public void ReturnAllParksEveryLoanAndTellsTheCallbacks()
        {
            var handedOut = new List<Transform>();
            var takenBack = new List<Transform>();
            var pool = new ObjectPool<Transform>(
                _prefab,
                initialSize: 2,
                maxSize: 0,
                parent: _parent,
                onGet: obj => handedOut.Add(obj),
                onReturn: obj => takenBack.Add(obj)
            );

            Transform first = pool.Get();
            Transform second = pool.Get();
            Assert.That(handedOut, Is.EqualTo(new[] { first, second }));

            pool.ReturnAll();

            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(pool.AvailableCount, Is.EqualTo(2));
            Assert.That(takenBack, Has.Count.EqualTo(2));
            Assert.That(takenBack, Has.Member(first));
            Assert.That(takenBack, Has.Member(second));

            pool.Clear();
        }

        [Test]
        public void ReturnParksOwnedObjectsUnderThePoolParentAndLeavesLooseOnesLoose()
        {
            var parented = new ObjectPool<Transform>(_prefab, parent: _parent);
            Transform owned = parented.Get();
            owned.SetParent(null, true);

            parented.Return(owned);
            Assert.That(owned.parent, Is.SameAs(_parent));

            var loose = new ObjectPool<Transform>(_prefab);
            Transform free = loose.Get();
            loose.Return(free);
            Assert.That(free.parent, Is.Null, "A parentless pool leaves its objects at the root.");

            parented.Clear();
            loose.Clear();
        }

        [Test]
        public void ObjectsDestroyedBehindThePoolsBackAreSkippedRatherThanHandedOut()
        {
            var pool = new ObjectPool<Transform>(_prefab, initialSize: 2, parent: _parent);
            for (int i = _parent.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(_parent.GetChild(i).gameObject);

            Transform replacement = pool.Get();

            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement.gameObject.activeSelf, Is.True);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.AvailableCount, Is.EqualTo(0));

            pool.Clear();
        }

        [Test]
        public void ClearDestroysBothTheLoansAndTheReserves()
        {
            var pool = new ObjectPool<Transform>(_prefab, initialSize: 2, parent: _parent);
            Transform loaned = pool.Get();
            Transform reserve = pool.Get();
            pool.Return(reserve);
            Assert.That(pool.TotalCount, Is.EqualTo(2));

            pool.Clear();

            Assert.That(loaned == null, Is.True);
            Assert.That(reserve == null, Is.True);
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(pool.AvailableCount, Is.EqualTo(0));
            Assert.That(_parent.childCount, Is.EqualTo(0));
        }

        [Test]
        public void ClearToleratesObjectsSomethingElseAlreadyDestroyed()
        {
            var pool = new ObjectPool<Transform>(_prefab, initialSize: 2, parent: _parent);
            Transform loaned = pool.Get();
            Object.DestroyImmediate(loaned.gameObject);
            Object.DestroyImmediate(_parent.GetChild(0).gameObject);

            Assert.DoesNotThrow(() => pool.Clear());
            Assert.That(pool.TotalCount, Is.EqualTo(0));
        }

        private GameObject NewObject(string objectName)
        {
            var host = new GameObject(objectName);
            _created.Add(host);
            return host;
        }
    }
}
