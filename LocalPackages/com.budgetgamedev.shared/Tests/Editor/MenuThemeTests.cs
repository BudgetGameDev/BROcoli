using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Covers the palette and the pure layout maths of the shared menu look. The
    /// widget builders live in <see cref="MenuThemeWidgetTests"/>.
    /// </summary>
    public sealed class MenuThemeTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void HexReadsAWebColourAndFallsBackToWhiteOnNonsense()
        {
            Color parsed = MenuTheme.Hex("#FF8000");

            Assert.That(parsed.r, Is.EqualTo(1f).Within(0.005f));
            Assert.That(parsed.g, Is.EqualTo(128f / 255f).Within(0.005f));
            Assert.That(parsed.b, Is.EqualTo(0f).Within(0.005f));
            Assert.That(parsed.a, Is.EqualTo(1f).Within(0.005f));
            Assert.That(MenuTheme.Hex("chartreuse-ish"), Is.EqualTo(Color.white));
        }

        [Test]
        public void ThePaletteKeepsSurfacesDarkAndTheAccentReadable()
        {
            Assert.That(
                MenuTheme.Background.grayscale,
                Is.LessThan(MenuTheme.CardSurface.grayscale),
                "The page has to sit behind the cards."
            );
            Assert.That(
                MenuTheme.CardSurface.grayscale,
                Is.LessThan(MenuTheme.SurfaceVariant.grayscale)
            );
            Assert.That(MenuTheme.HeroSurface.g, Is.GreaterThan(MenuTheme.HeroSurface.r));
            Assert.That(MenuTheme.Primary.g, Is.GreaterThan(MenuTheme.Primary.r));
            Assert.That(
                MenuTheme.OnSurface.grayscale,
                Is.GreaterThan(MenuTheme.OnSurfaceMuted.grayscale)
            );
            Assert.That(
                MenuTheme.OnSurface.grayscale,
                Is.GreaterThan(MenuTheme.CardSurface.grayscale),
                "Label text has to out-contrast the card it sits on."
            );
        }

        [Test]
        public void TheAccentBrightensOnHoverAndDarkensWhenPressed()
        {
            Assert.That(
                MenuTheme.PrimaryHover.grayscale,
                Is.GreaterThan(MenuTheme.Primary.grayscale)
            );
            Assert.That(
                MenuTheme.PrimaryPressed.grayscale,
                Is.LessThan(MenuTheme.Primary.grayscale)
            );
        }

        [Test]
        public void TheSelectionAccentIsAVisibleOutlineThatGrowsTheFocusedControl()
        {
            Assert.That(
                MenuTheme.Divider.a,
                Is.LessThan(1f),
                "A divider is a hairline, not a wall."
            );
            Assert.That(MenuTheme.SelectionOutline.a, Is.GreaterThan(0.5f));
            Assert.That(MenuTheme.SelectionThickness.x, Is.GreaterThan(0f));
            Assert.That(MenuTheme.SelectionThickness.y, Is.GreaterThan(0f));
            Assert.That(MenuTheme.SelectedScale, Is.GreaterThan(1f));
            Assert.That(MenuTheme.SelectionLerpSpeed, Is.GreaterThan(0f));
        }

        [Test]
        public void StretchPinsARectToEveryEdgeOfItsParent()
        {
            RectTransform rect = NewRect();
            rect.anchorMin = new Vector2(0.2f, 0.3f);
            rect.anchorMax = new Vector2(0.4f, 0.5f);
            rect.offsetMin = new Vector2(7f, 8f);
            rect.offsetMax = new Vector2(9f, 10f);

            MenuTheme.Stretch(rect);

            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void SetTopAnchoredHangsAFixedSizeRectBelowTheTopEdge()
        {
            RectTransform rect = NewRect();

            MenuTheme.SetTopAnchored(rect, 40f, 320f, 90f);

            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(rect.pivot, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(0f, -40f)));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(320f, 90f)));
        }

        [Test]
        public void SetCenteredRectPlacesAFixedSizeRectAroundTheMiddle()
        {
            RectTransform rect = NewRect();

            MenuTheme.SetCenteredRect(rect, 260f, 70f, -120f);

            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rect.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(0f, -120f)));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(260f, 70f)));
        }

        [Test]
        public void ApplySafeAreaTurnsDevicePixelsIntoNormalisedAnchors()
        {
            RectTransform rect = NewRect();
            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);
            var pixels = new Rect(0.1f * width, 0.2f * height, 0.5f * width, 0.6f * height);

            MenuTheme.ApplySafeArea(rect, pixels);

            Assert.That(rect.anchorMin.x, Is.EqualTo(0.1f).Within(0.0005f));
            Assert.That(rect.anchorMin.y, Is.EqualTo(0.2f).Within(0.0005f));
            Assert.That(rect.anchorMax.x, Is.EqualTo(0.6f).Within(0.0005f));
            Assert.That(rect.anchorMax.y, Is.EqualTo(0.8f).Within(0.0005f));
            Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ApproximatelyAbsorbsSubPixelDriftButNotARealMove()
        {
            var reference = new Rect(10f, 20f, 300f, 400f);

            Assert.That(
                MenuTheme.Approximately(reference, new Rect(10.4f, 20.4f, 300.4f, 400.4f)),
                Is.True,
                "Half a pixel of rounding is not a layout change."
            );
            Assert.That(
                MenuTheme.Approximately(reference, new Rect(11f, 20f, 300f, 400f)),
                Is.False
            );
            Assert.That(
                MenuTheme.Approximately(reference, new Rect(10f, 21f, 300f, 400f)),
                Is.False
            );
            Assert.That(
                MenuTheme.Approximately(reference, new Rect(10f, 20f, 301f, 400f)),
                Is.False
            );
            Assert.That(
                MenuTheme.Approximately(reference, new Rect(10f, 20f, 300f, 401f)),
                Is.False
            );
        }

        [Test]
        public void FindDescendantReachesNestedChildrenAndReportsAMiss()
        {
            RectTransform root = MenuTheme.CreateRect("Root", null);
            _created.Add(root.gameObject);
            RectTransform branch = MenuTheme.CreateRect("Branch", root);
            RectTransform leaf = MenuTheme.CreateRect("Leaf", branch);

            Assert.That(MenuTheme.FindDescendant(root, "Branch"), Is.SameAs(branch));
            Assert.That(MenuTheme.FindDescendant(root, "Leaf"), Is.SameAs(leaf));
            Assert.That(MenuTheme.FindDescendant(root, "Nowhere"), Is.Null);
        }

        private RectTransform NewRect()
        {
            var host = new GameObject("Rect", typeof(RectTransform));
            _created.Add(host);
            return host.GetComponent<RectTransform>();
        }
    }
}
