using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public class SprayRecoilTests
    {
        [Test]
        public void FiringAnimationBuildsSmoothlyIntoTheKick()
        {
            SprayRecoil.Pose start = SprayRecoil.Evaluate(0f);
            SprayRecoil.Pose entering = SprayRecoil.Evaluate(SprayRecoil.PeakNormalizedTime * 0.5f);
            SprayRecoil.Pose peak = SprayRecoil.Evaluate(SprayRecoil.PeakNormalizedTime);

            Assert.That(start.LocalOffset, Is.EqualTo(Vector3.zero));
            Assert.That(entering.LocalOffset.x, Is.LessThan(0f));
            Assert.That(
                Mathf.Abs(entering.LocalOffset.x),
                Is.LessThan(Mathf.Abs(peak.LocalOffset.x))
            );
            Assert.That(peak.LocalEulerAngles.z, Is.GreaterThan(0f));
        }

        [Test]
        public void RecoveryAddsASmallBobBeforeSettlingAtRest()
        {
            float midwayThroughRecovery = (SprayRecoil.PeakNormalizedTime + 1f) * 0.5f;
            SprayRecoil.Pose midway = SprayRecoil.Evaluate(midwayThroughRecovery);
            SprayRecoil.Pose settled = SprayRecoil.Evaluate(1f);

            Assert.That(midway.LocalOffset.y, Is.GreaterThan(0f));
            Assert.That(settled.LocalOffset, Is.EqualTo(Vector3.zero));
            Assert.That(settled.LocalEulerAngles, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void TriggeredRecoilFinishesWithinItsConfiguredDuration()
        {
            var recoil = new SprayRecoil();

            recoil.Trigger();
            Assert.That(recoil.IsActive, Is.True);

            recoil.Update(SpraySettings.HandSprayRecoilDuration);
            Assert.That(recoil.IsActive, Is.False);
        }

        [Test]
        public void RecoilTemporarilyMakesTheWalkPoseYieldToTheKick()
        {
            float startingWeight = SprayRecoil.ResolveWalkPoseWeight(
                SprayRecoil.Evaluate(0f).Influence
            );
            float firingWeight = SprayRecoil.ResolveWalkPoseWeight(
                SprayRecoil.Evaluate(SprayRecoil.PeakNormalizedTime).Influence
            );
            float midwayThroughRecovery = (SprayRecoil.PeakNormalizedTime + 1f) * 0.5f;
            float recoveringWeight = SprayRecoil.ResolveWalkPoseWeight(
                SprayRecoil.Evaluate(midwayThroughRecovery).Influence
            );
            float settledWeight = SprayRecoil.ResolveWalkPoseWeight(
                SprayRecoil.Evaluate(1f).Influence
            );

            Assert.That(startingWeight, Is.EqualTo(1f));
            Assert.That(firingWeight, Is.EqualTo(SpraySettings.HandWalkWeightDuringRecoil));
            Assert.That(recoveringWeight, Is.GreaterThan(firingWeight).And.LessThan(1f));
            Assert.That(settledWeight, Is.EqualTo(1f));
        }

        [Test]
        public void BackpedalingIsMeasuredRelativeToTheAimDirection()
        {
            float forward = SprayRecoil.ResolveBackpedalAmount(Vector2.right, Vector2.right, 1f);
            float sideways = SprayRecoil.ResolveBackpedalAmount(Vector2.up, Vector2.right, 1f);
            float backward = SprayRecoil.ResolveBackpedalAmount(Vector2.left, Vector2.right, 1f);

            Assert.That(forward, Is.EqualTo(0f));
            Assert.That(sideways, Is.EqualTo(0f));
            Assert.That(backward, Is.EqualTo(1f));
        }

        [Test]
        public void MovingAndBackpedalingStrengthenTheRecoilPose()
        {
            float stationary = SprayRecoil.ResolveMovementMultiplier(0f, 0f);
            float moving = SprayRecoil.ResolveMovementMultiplier(1f, 0f);
            float backpedaling = SprayRecoil.ResolveMovementMultiplier(1f, 1f);

            Assert.That(stationary, Is.EqualTo(1f));
            Assert.That(moving, Is.GreaterThan(stationary));
            Assert.That(backpedaling, Is.GreaterThan(moving));
        }
    }
}
