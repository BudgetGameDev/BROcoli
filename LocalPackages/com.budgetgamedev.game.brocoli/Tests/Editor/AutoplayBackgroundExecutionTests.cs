using System.Reflection;
using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class AutoplayBackgroundExecutionTests
    {
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void EndingAutoplayRestoresBothBackgroundPolicies(bool background, bool suppression)
        {
            bool originalBackground = Application.runInBackground;
            bool originalSuppression = ForceLandscapeAspect.SuppressFocusLossPause;
            float originalScale = Time.timeScale;
            float originalCaptureStep = Time.captureDeltaTime;
            float originalVolume = AudioListener.volume;
            var owner = new GameObject("autoplay-focus-test");
            try
            {
                Application.runInBackground = background;
                ForceLandscapeAspect.SuppressFocusLossPause = suppression;
                Time.timeScale = 0f;
                var controller = owner.AddComponent<AutoplayController>();
                Assert.That(Application.runInBackground, Is.EqualTo(background));
                Assert.That(ForceLandscapeAspect.SuppressFocusLossPause, Is.EqualTo(suppression));
                controller.BeginBackgroundExecution();
                controller.BeginBackgroundExecution();
                Assert.That(Application.runInBackground, Is.True);
                Assert.That(ForceLandscapeAspect.SuppressFocusLossPause, Is.True);
                Assert.That(Time.timeScale, Is.Zero, "manual and level-up pauses must stay paused");
                controller.enabled = false;
                // EditMode need not dispatch MonoBehaviour callbacks, so replay the
                // same lifecycle message explicitly; restoration is idempotent.
                typeof(AutoplayController)
                    .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                Assert.That(Application.runInBackground, Is.EqualTo(background));
                Assert.That(ForceLandscapeAspect.SuppressFocusLossPause, Is.EqualTo(suppression));
                Object.DestroyImmediate(owner);
                Assert.That(Application.runInBackground, Is.EqualTo(background));
                Assert.That(ForceLandscapeAspect.SuppressFocusLossPause, Is.EqualTo(suppression));
                Assert.That(Time.timeScale, Is.Zero);
            }
            finally
            {
                if (owner != null)
                    Object.DestroyImmediate(owner);
                Application.runInBackground = originalBackground;
                ForceLandscapeAspect.SuppressFocusLossPause = originalSuppression;
                Time.timeScale = originalScale;
                Time.captureDeltaTime = originalCaptureStep;
                AudioListener.volume = originalVolume;
            }
        }
    }
}
