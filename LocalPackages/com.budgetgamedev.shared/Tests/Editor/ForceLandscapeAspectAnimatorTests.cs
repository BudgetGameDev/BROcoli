using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// The turning phone icon. Its progress is kept in statics so that iOS Safari,
    /// which can disable and re-enable the overlay several times a second, does
    /// not restart the animation from scratch each time.
    /// </summary>
    public sealed class ForceLandscapeAspectAnimatorTests : ForceLandscapeAspectTestBase
    {
        // The icon turns 90 degrees per half second, so half a second is one sweep.
        private const float FullSweep = 0.5f;

        private ForceLandscapeAspect.RotateAnimator NewAnimator()
        {
            return NewObject("PhoneIcon").AddComponent<ForceLandscapeAspect.RotateAnimator>();
        }

        private static float Angle => ForceLandscapeAspect.RotateAnimator._persistedAngle;

        private static float Target => ForceLandscapeAspect.RotateAnimator._persistedTargetAngle;

        private static float PauseTimer => ForceLandscapeAspect.RotateAnimator._persistedPauseTimer;

        private static void AssertFacing(Transform transform, float degrees)
        {
            Assert.That(
                Quaternion.Angle(transform.localRotation, Quaternion.Euler(0f, 0f, degrees)),
                Is.LessThan(0.01f),
                $"the icon should be facing {degrees} degrees"
            );
        }

        [Test]
        public void TheIconTurnsToLandscapeAndThenRestsBeforeTurningBack()
        {
            ForceLandscapeAspect.RotateAnimator animator = NewAnimator();

            animator.Step(FullSweep);

            Assert.That(Angle, Is.EqualTo(-90f).Within(0.001f));
            AssertFacing(animator.transform, -90f);
            Assert.That(Target, Is.EqualTo(0f), "reaching landscape aims the icon back");
            Assert.That(PauseTimer, Is.EqualTo(1f), "the landscape pose is held the longest");

            animator.Step(FullSweep);

            Assert.That(Angle, Is.EqualTo(-90f).Within(0.001f), "the rest is spent, not skipped");
            Assert.That(PauseTimer, Is.EqualTo(0.5f).Within(0.001f));

            animator.Step(FullSweep);
            animator.Step(FullSweep);

            Assert.That(Angle, Is.EqualTo(0f).Within(0.001f));
            AssertFacing(animator.transform, 0f);
            Assert.That(Target, Is.EqualTo(-90f), "the loop turns around");
            Assert.That(PauseTimer, Is.EqualTo(0.5f), "the portrait pose is held briefly");
        }

        [Test]
        public void ReEnablingTheIconPicksUpTheSweepWhereItStopped()
        {
            ForceLandscapeAspect.RotateAnimator animator = NewAnimator();
            animator.Step(FullSweep / 2f);
            animator.transform.localRotation = Quaternion.identity;

            animator.OnEnable();

            AssertFacing(animator.transform, -45f);
        }

        [Test]
        public void AnIconThatNeverAnimatedKeepsItsAuthoredRotation()
        {
            ForceLandscapeAspect.RotateAnimator animator = NewAnimator();
            animator.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);

            animator.OnEnable();

            AssertFacing(animator.transform, 30f);
        }

        [Test]
        public void TheAnimatorStepsWithUnscaledTimeSoItKeepsMovingWhilePaused()
        {
            ForceLandscapeAspect.RotateAnimator animator = NewAnimator();

            animator.Update();

            Assert.That(Angle, Is.InRange(-90f, 0f), "an unscaled step stays inside the sweep");
        }
    }
}
