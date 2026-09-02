using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Covers what a pool does about loans something else destroyed. A caller that
    /// destroys a borrowed object instead of returning it used to leave the slot on
    /// loan forever, which held the pool at its cap for the rest of the session.
    /// </summary>
    public sealed partial class ObjectPoolTests
    {
        [Test]
        public void LoansDestroyedElsewhereGiveTheirCapacityBack()
        {
            var pool = new ObjectPool<Transform>(_prefab, maxSize: 2, parent: _parent);
            Transform first = pool.Get();
            Transform second = pool.Get();
            Assert.That(pool.ActiveCount, Is.EqualTo(2));

            // A caller that destroys a loan instead of returning it must not hold
            // the pool at its cap for the rest of the session.
            Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(second.gameObject);

            Transform replacement = pool.Get();

            Assert.That(replacement, Is.Not.Null);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            pool.Clear();
        }

        [Test]
        public void ReturningALoanDestroyedElsewhereFreesItsSlotQuietly()
        {
            var pool = new ObjectPool<Transform>(_prefab, maxSize: 1, parent: _parent);
            Transform loan = pool.Get();
            Object.DestroyImmediate(loan.gameObject);

            pool.Return(loan);

            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(
                pool.AvailableCount,
                Is.EqualTo(0),
                "A destroyed loan is gone, not a reserve to hand out again."
            );
            pool.Clear();
        }
    }
}
