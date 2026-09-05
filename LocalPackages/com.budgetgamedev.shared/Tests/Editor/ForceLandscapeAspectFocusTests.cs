using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Losing focus, and the hidden component that watches for it. Only gameplay
    /// auto-pauses: a menu has no pause screen to resume from, so freezing it
    /// would strand the player, and the editor's own player gives focus away to
    /// the editor far too often to pause for it.
    /// </summary>
    public sealed class ForceLandscapeAspectFocusTests : ForceLandscapeAspectTestBase
    {
        private ForceLandscapeAspect.AspectRatioUpdater NewUpdater()
        {
            return NewObject("[ForceLandscapeAspect]")
                .AddComponent<ForceLandscapeAspect.AspectRatioUpdater>();
        }

        [Test]
        public void TheDefaultLookupSweepsTheLoadedSceneForAPauseScreen()
        {
            ForceLandscapeAspect.ResetStatics();

            Assert.That(
                ForceLandscapeAspect.FindPauseController(),
                Is.Null,
                "an editor test runs without a gameplay scene loaded"
            );
        }

        [Test]
        public void LosingFocusOutsideGameplayLeavesTheMenuRunning()
        {
            ForceLandscapeAspect.OnFocusLost();

            Assert.That(ForceLandscapeAspect._isFocusLost, Is.False);
        }

        [Test]
        public void LosingFocusInTheEditorsOwnPlayerLeavesGameplayRunning()
        {
            TestPauseController pause = NewPauseMenu();
            ForceLandscapeAspect.IsEditorPlayer = () => true;
            ForceLandscapeAspect.DEBUG_MODE = true;
            LogAssert.Expect(
                LogType.Log,
                "[ForceLandscapeAspect] Focus LOST in the editor - not pausing"
            );

            ForceLandscapeAspect.OnFocusLost();

            Assert.That(
                pause.PauseCalls,
                Is.EqualTo(0),
                "play mode loses focus to the console and the inspector constantly"
            );
            Assert.That(ForceLandscapeAspect._isFocusLost, Is.False);
        }

        [Test]
        public void TheFocusPauseIsOnByDefaultOutsideTheEditor()
        {
            ForceLandscapeAspect.ResetStatics();

            Assert.That(
                ForceLandscapeAspect.IsEditorPlayer(),
                Is.EqualTo(Application.isEditor),
                "only the editor's own player opts out of the focus pause"
            );
        }

        [Test]
        public void LosingFocusPausesGameplayExactlyOnce()
        {
            TestPauseController pause = NewPauseMenu();

            ForceLandscapeAspect.OnFocusLost();
            ForceLandscapeAspect.OnFocusLost();

            Assert.That(ForceLandscapeAspect._isFocusLost, Is.True);
            Assert.That(pause.PauseCalls, Is.EqualTo(1), "a second tab switch must not re-pause");
        }

        [Test]
        public void RegainingFocusClearsTheFlagButLeavesTheGamePaused()
        {
            TestPauseController pause = NewPauseMenu();
            ForceLandscapeAspect.OnFocusLost();

            ForceLandscapeAspect.OnFocusRegained();

            Assert.That(ForceLandscapeAspect._isFocusLost, Is.False);
            Assert.That(pause.ResumeCalls, Is.EqualTo(0), "the player dismisses the pause menu");
            Assert.That(pause.IsPaused, Is.True);
        }

        [Test]
        public void RegainingFocusThatWasNeverLostChangesNothing()
        {
            TestPauseController pause = NewPauseMenu();

            ForceLandscapeAspect.OnFocusRegained();

            Assert.That(pause.IsPaused, Is.False);
            Assert.That(ForceLandscapeAspect._isFocusLost, Is.False);
        }

        [Test]
        public void TheUpdaterPaintsTheBarsWithACameraBehindEverything()
        {
            ForceLandscapeAspect.AspectRatioUpdater updater = NewUpdater();

            updater.Start();

            Camera clearer = updater.GetComponentInChildren<Camera>();
            Assert.That(clearer, Is.Not.Null);
            Assert.That(clearer.gameObject.name, Is.EqualTo("[LetterboxClearCamera]"));
            Assert.That(clearer.depth, Is.EqualTo(-100f), "it has to render before the game");
            Assert.That(clearer.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(clearer.backgroundColor, Is.EqualTo(Color.black));
            Assert.That(clearer.cullingMask, Is.EqualTo(0), "it draws nothing but the bars");
            Assert.That(clearer.rect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
        }

        [Test]
        public void AGameThatStartsWithoutFocusPausesOnceAndOnlyOnce()
        {
            ForceLandscapeAspect.CheckForScreenChange();
            ForceLandscapeAspect.AspectRatioUpdater updater = NewUpdater();
            TestPauseController pause = NewPauseMenu();

            updater.Tick(false);

            Assert.That(pause.PauseCalls, Is.EqualTo(1));

            ForceLandscapeAspect.OnFocusRegained();
            updater.Tick(false);

            Assert.That(
                pause.PauseCalls,
                Is.EqualTo(1),
                "the startup check runs on one frame only"
            );
        }

        [Test]
        public void AGameThatStartsWithFocusIsLeftRunning()
        {
            ForceLandscapeAspect.CheckForScreenChange();
            ForceLandscapeAspect.AspectRatioUpdater updater = NewUpdater();
            TestPauseController pause = NewPauseMenu();

            updater.Tick(true);

            Assert.That(pause.PauseCalls, Is.EqualTo(0));
        }

        [Test]
        public void TheUpdaterTicksAgainstTheEditorsOwnFocusState()
        {
            ForceLandscapeAspect.CheckForScreenChange();
            ForceLandscapeAspect.AspectRatioUpdater updater = NewUpdater();

            Assert.DoesNotThrow(() => updater.Update());
        }

        [Test]
        public void EveryBackgroundingRouteDrivesTheSamePause()
        {
            ForceLandscapeAspect.AspectRatioUpdater updater = NewUpdater();
            TestPauseController pause = NewPauseMenu();

            updater.OnApplicationFocus(false);
            Assert.That(pause.PauseCalls, Is.EqualTo(1), "a tab switch pauses");
            updater.OnApplicationFocus(true);
            Assert.That(ForceLandscapeAspect._isFocusLost, Is.False);

            updater.OnApplicationPause(true);
            Assert.That(pause.PauseCalls, Is.EqualTo(2), "backgrounding the app pauses");
            updater.OnApplicationPause(false);
            Assert.That(ForceLandscapeAspect._isFocusLost, Is.False);

            updater.OnVisibilityLost();
            Assert.That(pause.PauseCalls, Is.EqualTo(3), "a hidden browser tab pauses");
            updater.OnVisibilityRegained();
            Assert.That(ForceLandscapeAspect._isFocusLost, Is.False);
        }

        [Test]
        public void DestroyingTheUpdaterDropsItsSceneSubscription()
        {
            ForceLandscapeAspect.AspectRatioUpdater updater = NewUpdater();

            Assert.DoesNotThrow(() => updater.OnDestroy());
            Assert.DoesNotThrow(
                () => updater.OnDestroy(),
                "unsubscribing an event twice has to stay harmless"
            );
        }

        [Test]
        public void InitialisingInstallsOneHiddenUpdaterThatOutlivesSceneLoads()
        {
            ForceLandscapeAspect.Initialize();
            ForceLandscapeAspect.AspectRatioUpdater[] installed = FindUpdaters();

            Assert.That(installed.Length, Is.EqualTo(1));
            Assert.That(
                installed[0].gameObject.hideFlags,
                Is.EqualTo(HideFlags.HideInHierarchy),
                "the helper is plumbing, not scene content"
            );
            Assert.That(
                KeptAcrossScenes,
                Has.Member(installed[0].gameObject),
                "the enforcer has to keep watching after the first scene load"
            );

            ForceLandscapeAspect.Initialize();

            Assert.That(
                FindUpdaters().Length,
                Is.EqualTo(1),
                "a second initialisation must not add a second updater"
            );
        }

        private static ForceLandscapeAspect.AspectRatioUpdater[] FindUpdaters()
        {
            return Object.FindObjectsByType<ForceLandscapeAspect.AspectRatioUpdater>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        }
    }
}
