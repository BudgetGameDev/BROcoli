using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// The letterbox maths and the camera sweep. None of these tests may turn the
    /// screen portrait: that path pauses the game and builds the rotate overlay,
    /// which is covered separately.
    /// </summary>
    public sealed class ForceLandscapeAspectViewportTests : ForceLandscapeAspectTestBase
    {
        private const float SixteenByNine = 16f / 9f;
        private const float TwentyOneByNine = 21f / 9f;

        [Test]
        public void ClearCameraOnlyRendersWhenLetterboxingIsNeeded()
        {
            var camera = NewObject("[LetterboxClearCamera]").AddComponent<Camera>();
            ForceLandscapeAspect.UpdateAllCameras(2560, 1440, 10f);
            Assert.That(
                camera.enabled,
                Is.False,
                "A redundant output camera prevents Streamline's single-view path."
            );
            ForceLandscapeAspect.UpdateAllCameras(1600, 1200, 11f);
            Assert.That(
                camera.enabled,
                Is.True,
                "The exposed letterbox area still needs clearing."
            );
            ForceLandscapeAspect.UpdateAllCameras(2560, 1440, 12f);
            Assert.That(camera.enabled, Is.False);
        }

        [Test]
        public void AScreenNarrowerThanSixteenByNineIsLetterboxed()
        {
            Rect rect = ForceLandscapeAspect.CalculateViewportRect(1f);

            Assert.That(rect.x, Is.EqualTo(0f), "letterboxing never trims width");
            Assert.That(rect.width, Is.EqualTo(1f));
            Assert.That(rect.height, Is.EqualTo(1f / SixteenByNine).Within(0.0001f));
            Assert.That(
                rect.y,
                Is.EqualTo((1f - rect.height) / 2f).Within(0.0001f),
                "the bars have to be equal top and bottom"
            );
        }

        [Test]
        public void AScreenAtExactlySixteenByNineKeepsTheWholeViewport()
        {
            Assert.That(
                ForceLandscapeAspect.CalculateViewportRect(SixteenByNine),
                Is.EqualTo(new Rect(0f, 0f, 1f, 1f))
            );
        }

        [Test]
        public void AnUltraWideScreenIsOnlyPillarboxedOnceTheLimitIsTurnedOn()
        {
            Assert.That(
                ForceLandscapeAspect.CalculateViewportRect(3f),
                Is.EqualTo(new Rect(0f, 0f, 1f, 1f)),
                "ultra-wide is allowed by default"
            );

            ForceLandscapeAspect.ENFORCE_MAX_ASPECT = true;
            Rect rect = ForceLandscapeAspect.CalculateViewportRect(3f);

            Assert.That(rect.y, Is.EqualTo(0f), "pillarboxing never trims height");
            Assert.That(rect.height, Is.EqualTo(1f));
            Assert.That(rect.width, Is.EqualTo(TwentyOneByNine / 3f).Within(0.0001f));
            Assert.That(rect.x, Is.EqualTo((1f - rect.width) / 2f).Within(0.0001f));
        }

        [Test]
        public void EveryCameraExceptTheLetterboxClearerGetsTheViewport()
        {
            Camera game = NewObject("Game").AddComponent<Camera>();
            Camera clearer = NewObject("[LetterboxClearCamera]").AddComponent<Camera>();
            clearer.rect = new Rect(0f, 0f, 1f, 1f);

            ForceLandscapeAspect.UpdateAllCameras(800, 800, 10f);

            Assert.That(game.rect.height, Is.LessThan(1f), "a square screen has to be letterboxed");
            Assert.That(
                clearer.rect,
                Is.EqualTo(new Rect(0f, 0f, 1f, 1f)),
                "the camera that paints the bars must keep the whole screen"
            );
        }

        [Test]
        public void InactiveCamerasAreCorrectedBeforeTheyAreEverShown()
        {
            Camera hidden = NewObject("Hidden").AddComponent<Camera>();
            hidden.rect = new Rect(0.25f, 0.25f, 0.5f, 0.5f);
            hidden.gameObject.SetActive(false);

            ForceLandscapeAspect.UpdateAllCameras(800, 800, 10f);

            Assert.That(hidden.rect.width, Is.EqualTo(1f));
        }

        [Test]
        public void ANewlyLoadedSceneHasItsCamerasCorrected()
        {
            Camera camera = NewObject("Game").AddComponent<Camera>();
            camera.rect = new Rect(0.25f, 0.25f, 0.5f, 0.5f);

            ForceLandscapeAspect.OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);

            Assert.That(camera.rect.x, Is.EqualTo(0f));
            Assert.That(camera.rect.width, Is.EqualTo(1f));
        }

        [Test]
        public void ChecksCloserTogetherThanTheRateLimitAreDropped()
        {
            Camera camera = NewObject("Game").AddComponent<Camera>();
            ForceLandscapeAspect.CheckForScreenChange(1920, 1080, 10f);
            camera.rect = new Rect(0.25f, 0.25f, 0.5f, 0.5f);

            ForceLandscapeAspect.CheckForScreenChange(1000, 1000, 10.05f);

            Assert.That(camera.rect.width, Is.EqualTo(0.5f), "the check was rate limited away");

            ForceLandscapeAspect.CheckForScreenChange(1000, 1000, 10.2f);

            Assert.That(camera.rect.width, Is.EqualTo(1f), "a real resize reaches the cameras");
        }

        [Test]
        public void AScreenThatDidNotResizeLeavesTheCamerasAlone()
        {
            Camera camera = NewObject("Game").AddComponent<Camera>();
            ForceLandscapeAspect.CheckForScreenChange(1920, 1080, 10f);
            camera.rect = new Rect(0.25f, 0.25f, 0.5f, 0.5f);

            ForceLandscapeAspect.CheckForScreenChange(1920, 1080, 20f);

            Assert.That(camera.rect.width, Is.EqualTo(0.5f));
        }

        [Test]
        public void ThePublicEntryPointsReadTheLiveScreen()
        {
            Camera camera = NewObject("Game").AddComponent<Camera>();
            camera.rect = new Rect(0.25f, 0.25f, 0.5f, 0.5f);

            ForceLandscapeAspect.UpdateAllCameras();

            Assert.That(camera.rect.x, Is.EqualTo(0f), "the viewport is always centred");
            Assert.That(camera.rect.width, Is.EqualTo(1f), "the viewport always spans the width");

            camera.rect = new Rect(0.25f, 0.25f, 0.5f, 0.5f);
            ForceLandscapeAspect.CheckForScreenChange();

            Assert.That(
                camera.rect.width,
                Is.EqualTo(0.5f),
                "the screen has not changed since the update, so nothing is touched"
            );
        }
    }
}
