using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public class SprayWalkBobTests
    {
        [Test]
        public void IdlePoseAddsNoWalkMotion()
        {
            SprayWalkBob.Pose pose = SprayWalkBob.Evaluate(1.2f, 0f);

            Assert.That(pose.LocalOffset, Is.EqualTo(Vector3.zero));
            Assert.That(pose.LocalEulerAngles, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void WalkCycleBobsAboveAndBelowItsRestingHeight()
        {
            SprayWalkBob.Pose low = SprayWalkBob.Evaluate(0f, 1f);
            SprayWalkBob.Pose high = SprayWalkBob.Evaluate(Mathf.PI * 0.5f, 1f);

            Assert.That(low.LocalOffset.y, Is.LessThan(0f));
            Assert.That(high.LocalOffset.y, Is.GreaterThan(0f));
            Assert.That(high.LocalOffset.z, Is.GreaterThan(0f));
            Assert.That(high.LocalEulerAngles.z, Is.LessThan(0f));
        }

        [Test]
        public void MovementBlendEntersAndSettlesWithoutSnapping()
        {
            var bob = new SprayWalkBob();

            bob.Update(1f, 0.02f);
            float entering = bob.MovementBlend;
            bob.Update(0f, 0.02f);
            float settling = bob.MovementBlend;

            Assert.That(entering, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(settling, Is.GreaterThanOrEqualTo(0f).And.LessThan(entering));
        }
    }
}
