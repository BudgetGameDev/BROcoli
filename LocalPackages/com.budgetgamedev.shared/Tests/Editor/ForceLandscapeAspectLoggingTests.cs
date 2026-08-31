using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// The verbose trail. It is off in shipped builds, so the only way to know it
    /// still describes what actually happened is to turn it on and read it.
    /// </summary>
    public sealed class ForceLandscapeAspectLoggingTests : ForceLandscapeAspectTestBase
    {
        private static void ExpectLog(string fragment)
        {
            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(fragment)));
        }

        [Test]
        public void InitialisingIsNarratedFromStartToFinish()
        {
            ForceLandscapeAspect.DEBUG_MODE = true;

            ExpectLog("[ForceLandscapeAspect] Auto-initializing");
            ExpectLog("[ForceLandscapeAspect] Initialized successfully");

            ForceLandscapeAspect.Initialize();

            Assert.That(KeptAcrossScenes, Is.Not.Empty);
        }

        [Test]
        public void ACameraSweepReportsHowManyCamerasItTouched()
        {
            NewObject("Game").AddComponent<Camera>();
            ForceLandscapeAspect.DEBUG_MODE = true;

            ExpectLog("[ForceLandscapeAspect] Updated");

            ForceLandscapeAspect.UpdateAllCameras(1920, 1080, 10f);
        }

        [Test]
        public void ARotationIsNarratedInTheOrderItHappens()
        {
            ForceLandscapeAspect.DEBUG_MODE = true;

            ExpectLog("Entered PORTRAIT mode");
            ExpectLog("Rotate overlay created");
            ExpectLog("[ForceLandscapeAspect] Updated");

            ForceLandscapeAspect.UpdateAllCameras(600, 900, 10f);

            ExpectLog("Entered LANDSCAPE mode");
            ExpectLog("[ForceLandscapeAspect] Updated");

            ForceLandscapeAspect.UpdateAllCameras(900, 600, 20f);
        }

        [Test]
        public void ASceneLoadNamesTheSceneItIsCorrecting()
        {
            ForceLandscapeAspect.DEBUG_MODE = true;

            ExpectLog("[ForceLandscapeAspect] Scene loaded:");
            ExpectLog("[ForceLandscapeAspect] Updated");

            ForceLandscapeAspect.OnSceneLoaded(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene(),
                UnityEngine.SceneManagement.LoadSceneMode.Single
            );
        }

        [Test]
        public void FocusChangesSayWhetherTheyPausedAnything()
        {
            ForceLandscapeAspect.DEBUG_MODE = true;

            ExpectLog("Focus LOST outside gameplay - not pausing");
            ForceLandscapeAspect.OnFocusLost();

            NewPauseMenu();

            ExpectLog("Focus LOST - triggering pause");
            ForceLandscapeAspect.OnFocusLost();

            ExpectLog("Focus REGAINED");
            ForceLandscapeAspect.OnFocusRegained();
        }

        [Test]
        public void AStartWithoutFocusSaysWhyItPaused()
        {
            ForceLandscapeAspect.CheckForScreenChange();
            ForceLandscapeAspect.AspectRatioUpdater updater = NewObject("[ForceLandscapeAspect]")
                .AddComponent<ForceLandscapeAspect.AspectRatioUpdater>();
            NewPauseMenu();
            ForceLandscapeAspect.DEBUG_MODE = true;

            ExpectLog("Game started without focus - pausing");
            ExpectLog("Focus LOST - triggering pause");

            updater.Tick(false);
        }
    }
}
