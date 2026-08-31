using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Turning the device portrait: the game freezes and the rotate overlay
    /// appears. The fixture stands in for the scene-load survival call, which the
    /// editor refuses outright, so these tests can build the real widget.
    /// </summary>
    public sealed class ForceLandscapeAspectOverlayTests : ForceLandscapeAspectTestBase
    {
        [Test]
        public void TurningPortraitFreezesTheGameAndAsksForALandscapeScreen()
        {
            Time.timeScale = 1f;

            ForceLandscapeAspect.UpdateAllCameras(600, 900, 10f);

            Assert.That(ForceLandscapeAspect._isPortrait, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f), "portrait has to stop the game");
            Assert.That(ForceLandscapeAspect._rotateOverlay, Is.Not.Null);
            Assert.That(ForceLandscapeAspect._rotateOverlay.activeSelf, Is.True);
        }

        [Test]
        public void TurningBackToLandscapeRestoresTheSpeedThePlayerHad()
        {
            Time.timeScale = 0.25f;

            ForceLandscapeAspect.UpdateAllCameras(600, 900, 10f);
            ForceLandscapeAspect.UpdateAllCameras(900, 600, 20f);

            Assert.That(ForceLandscapeAspect._isPortrait, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.25f), "the saved speed comes back, not 1");
            Assert.That(ForceLandscapeAspect._rotateOverlay.activeSelf, Is.False);
        }

        [Test]
        public void AGamePausedByFocusLossStaysPausedWhenTheScreenIsRotatedBack()
        {
            NewPauseMenu();
            Time.timeScale = 1f;

            ForceLandscapeAspect.UpdateAllCameras(600, 900, 10f);
            ForceLandscapeAspect.OnFocusLost();
            ForceLandscapeAspect.UpdateAllCameras(900, 600, 20f);

            Assert.That(
                Time.timeScale,
                Is.EqualTo(0f),
                "rotating back must not undo a pause the player has not dismissed"
            );
        }

        [Test]
        public void OrientationFlipsFasterThanTheDebounceAreIgnored()
        {
            ForceLandscapeAspect.UpdateAllCameras(600, 900, 10f);

            ForceLandscapeAspect.UpdateAllCameras(900, 600, 10.2f);

            Assert.That(
                ForceLandscapeAspect._isPortrait,
                Is.True,
                "a jittering viewport must not flip the overlay on and off"
            );

            ForceLandscapeAspect.UpdateAllCameras(900, 600, 10.6f);

            Assert.That(ForceLandscapeAspect._isPortrait, Is.False, "a settled flip is honoured");
        }

        [Test]
        public void TheOverlayIsBuiltOnceAndThenOnlyToggled()
        {
            ForceLandscapeAspect.ShowRotateOverlay(true);
            GameObject overlay = ForceLandscapeAspect._rotateOverlay;

            Assert.That(overlay, Is.Not.Null);
            Assert.That(overlay.activeSelf, Is.True);
            Assert.That(
                KeptAcrossScenes,
                Has.Member(overlay),
                "the prompt has to stay up while the game reloads behind it"
            );

            ForceLandscapeAspect.ShowRotateOverlay(false);
            Assert.That(overlay.activeSelf, Is.False);

            ForceLandscapeAspect.ShowRotateOverlay(true);
            Assert.That(
                ForceLandscapeAspect._rotateOverlay,
                Is.SameAs(overlay),
                "the overlay is reused, not rebuilt on every rotation"
            );
        }

        [Test]
        public void HidingAnOverlayThatWasNeverBuiltDoesNotBuildOne()
        {
            ForceLandscapeAspect.ShowRotateOverlay(false);

            Assert.That(ForceLandscapeAspect._rotateOverlay, Is.Null);
        }

        [Test]
        public void TheOverlayCoversTheScreenWithAMessageAndAnAnimatedPhone()
        {
            ForceLandscapeAspect.ShowRotateOverlay(true);
            GameObject overlay = ForceLandscapeAspect._rotateOverlay;

            Canvas canvas = overlay.GetComponent<Canvas>();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(canvas.sortingOrder, Is.EqualTo(9999), "nothing may draw over the prompt");
            Assert.That(overlay.GetComponent<GraphicRaycaster>(), Is.Not.Null);

            CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(
                scaler.referenceResolution,
                Is.EqualTo(new Vector2(1080f, 1920f)),
                "the prompt is authored against a portrait screen"
            );

            TMP_Text message = overlay.GetComponentInChildren<TMP_Text>(true);
            Assert.That(message.text, Does.Contain("landscape"));

            Assert.That(
                overlay.GetComponentInChildren<ForceLandscapeAspect.RotateAnimator>(true),
                Is.Not.Null,
                "the phone icon has to turn so the prompt reads as an instruction"
            );

            // Backdrop, phone body, phone screen, six arc segments and the arrow head.
            Assert.That(overlay.GetComponentsInChildren<Image>(true).Length, Is.EqualTo(10));
        }
    }
}
