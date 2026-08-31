using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Covers the overlay a preloader raises while a game warms up: it has to
    /// cover everything, report progress, and go away again.
    /// </summary>
    public sealed class LoadingScreenUITests
    {
        private static readonly Color Backdrop = new Color(0.02f, 0.06f, 0.04f, 1f);
        private static readonly Color BarBackground = new Color(0.2f, 0.2f, 0.2f, 1f);
        private static readonly Color BarFill = new Color(0.25f, 0.65f, 0.3f, 1f);

        private LoadingScreenUI _screen;

        [TearDown]
        public void TearDown()
        {
            _screen?.Destroy();
            _screen = null;
        }

        [Test]
        public void TheOverlayCoversTheWholeScreenAboveEverythingElse()
        {
            LoadingScreenUI screen = Create("Loading");

            Canvas canvas = screen._canvas;
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(
                canvas.sortingOrder,
                Is.GreaterThan(1000),
                "The loading screen has to outrank every gameplay canvas."
            );
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null);

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void TheBackgroundIsAnOpaqueSheetStretchedOverTheCanvas()
        {
            LoadingScreenUI screen = Create("Loading");

            Transform background = screen._canvas.transform.Find("Background");
            Assert.That(background, Is.Not.Null);

            Image sheet = background.GetComponent<Image>();
            Assert.That(sheet.color, Is.EqualTo(Backdrop));

            var rect = (RectTransform)background;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void TheProgressBarFillLivesInsideItsTrackAndStartsEmpty()
        {
            LoadingScreenUI screen = Create("Loading");

            Transform track = screen._canvas.transform.Find("ProgressBarBg");
            Assert.That(track, Is.Not.Null);
            Assert.That(track.GetComponent<Image>().color, Is.EqualTo(BarBackground));

            RectTransform fill = screen._progressBarFill.rectTransform;
            Assert.That(fill.parent, Is.SameAs(track));
            Assert.That(screen._progressBarFill.color, Is.EqualTo(BarFill));
            Assert.That(
                fill.anchorMax.x,
                Is.EqualTo(0f),
                "A freshly built loading screen shows no progress."
            );
        }

        [Test]
        public void ProgressDrivesTheFillWidthAndIsClampedToTheTrack()
        {
            LoadingScreenUI screen = Create("Loading");
            RectTransform fill = screen._progressBarFill.rectTransform;

            screen.SetProgress(0.25f);
            Assert.That(fill.anchorMax, Is.EqualTo(new Vector2(0.25f, 1f)));

            screen.SetProgress(2f);
            Assert.That(fill.anchorMax.x, Is.EqualTo(1f));

            screen.SetProgress(-3f);
            Assert.That(fill.anchorMax.x, Is.EqualTo(0f));
        }

        [Test]
        public void TheLabelStartsWithTheOfferedTextAndCanBeReplaced()
        {
            LoadingScreenUI screen = Create("Warming up");

            Assert.That(screen._loadingLabel.text, Is.EqualTo("Warming up"));
            Assert.That(
                screen._loadingLabel.alignment,
                Is.EqualTo(TMPro.TextAlignmentOptions.Center)
            );

            screen.SetText("Almost there");
            Assert.That(screen._loadingLabel.text, Is.EqualTo("Almost there"));
        }

        [Test]
        public void DestroyRemovesTheOverlayAndLaterUpdatesAreIgnored()
        {
            LoadingScreenUI screen = Create("Loading");
            Canvas canvas = screen._canvas;

            screen.Destroy();

            Assert.That(canvas == null, Is.True, "The overlay's canvas is gone from the scene.");
            Assert.DoesNotThrow(() =>
            {
                screen.SetProgress(0.5f);
                screen.SetText("ignored");
                screen.Destroy();
            });
        }

        private LoadingScreenUI Create(string initialText)
        {
            _screen = new LoadingScreenUI(null, Backdrop, BarBackground, BarFill, initialText);
            return _screen;
        }
    }
}
