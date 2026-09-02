using System;
using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Shared.Tests
{
    public class GameCursorTests
    {
        private Func<bool> registered;

        [TearDown]
        public void TearDown()
        {
            if (registered == null)
                return;

            GameCursor.RemoveVisibilityHold(registered);
            registered = null;
        }

        [Test]
        public void ThePointerIsHeldWhileAnyRegisteredScreenSaysSo()
        {
            bool open = false;
            registered = () => open;

            Assert.That(GameCursor.IsHeldVisible(), Is.False);

            GameCursor.AddVisibilityHold(registered);
            Assert.That(GameCursor.IsHeldVisible(), Is.False);

            open = true;
            Assert.That(GameCursor.IsHeldVisible(), Is.True);

            GameCursor.RemoveVisibilityHold(registered);
            registered = null;
            Assert.That(
                GameCursor.IsHeldVisible(),
                Is.False,
                "a screen that unregisters must not keep holding the pointer"
            );
        }

        [Test]
        public void RegisteringTheSameHoldTwiceRegistersItOnce()
        {
            bool open = true;
            registered = () => open;

            GameCursor.AddVisibilityHold(registered);
            GameCursor.AddVisibilityHold(registered);
            GameCursor.RemoveVisibilityHold(registered);

            Assert.That(
                GameCursor.IsHeldVisible(),
                Is.False,
                "one removal has to undo one registration, or a scene loaded twice would "
                    + "strand the pointer on screen"
            );
            registered = null;
        }

        [Test]
        public void AHoldThatThrowsIsReportedAndDoesNotDecideForTheOthers()
        {
            registered = () => throw new InvalidOperationException("broken screen");
            GameCursor.AddVisibilityHold(registered);

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: broken screen");
            Assert.That(GameCursor.IsHeldVisible(), Is.False);
        }

        [Test]
        public void TintKeepsTheShapeAndShadingAndOnlyMovesTheColour()
        {
            Texture2D source = new(2, 1, TextureFormat.RGBA32, false);
            Texture2D tinted = null;
            try
            {
                source.SetPixels(new[] { new Color(1f, 1f, 1f, 1f), new Color(0f, 0f, 0f, 0.4f) });
                source.Apply();

                tinted = GameCursor.Tint(source, new Color(0.5f, 0.25f, 1f));
                Color[] pixels = tinted.GetPixels();

                Assert.That(pixels[0].r, Is.EqualTo(0.5f).Within(0.01f));
                Assert.That(pixels[0].g, Is.EqualTo(0.25f).Within(0.01f));
                Assert.That(pixels[0].b, Is.EqualTo(1f).Within(0.01f));
                Assert.That(pixels[0].a, Is.EqualTo(1f).Within(0.01f), "shape is alpha");

                Assert.That(
                    pixels[1].r,
                    Is.EqualTo(0f).Within(0.01f),
                    "the dark outline stays dark, which is what keeps the pointer legible "
                        + "against a pale menu"
                );
                Assert.That(pixels[1].a, Is.EqualTo(0.4f).Within(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                if (tinted != null)
                    UnityEngine.Object.DestroyImmediate(tinted);
            }
        }

        [Test]
        public void TheHotspotIsTheTipOfADiagonalArrowRatherThanACornerOfItsBounds()
        {
            // A three-pixel diagonal from (1,1) down-right. Its bounding box starts at (1,1),
            // which here is also the tip; the row below is what would drag a bounding-box
            // answer away from it if the shape were any wider.
            Texture2D arrow = BuildPointer(
                8,
                8,
                new[] { (1, 1), (2, 2), (3, 3), (2, 3), (0, 4) }
            );
            try
            {
                Assert.That(GameCursor.MeasureHotspot(arrow), Is.EqualTo(new Vector2(1f, 1f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(arrow);
            }
        }

        [Test]
        public void TheHotspotIsMeasuredFromTheTopLeftAsUnityCounts()
        {
            // One solid pixel near the bottom-left. Texture rows run bottom-up, so a hotspot
            // that forgot to flip would report y=1 instead of the 6 Unity wants.
            Texture2D pointer = BuildPointer(8, 8, new[] { (2, 6) });
            try
            {
                Assert.That(GameCursor.MeasureHotspot(pointer), Is.EqualTo(new Vector2(2f, 6f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pointer);
            }
        }

        [Test]
        public void FaintAntialiasingAroundTheTipIsNotMistakenForIt()
        {
            Texture2D pointer = BuildPointer(8, 8, new[] { (3, 3) });
            try
            {
                // A hint of alpha one pixel closer to the corner: the edge of the drawing,
                // not the point of it.
                pointer.SetPixel(0, 8 - 1 - 0, new Color(1f, 1f, 1f, 0.2f));
                pointer.Apply();

                Assert.That(
                    GameCursor.MeasureHotspot(pointer),
                    Is.EqualTo(new Vector2(3f, 3f)),
                    "a click has to land on the drawn tip, not on the fade around it"
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pointer);
            }
        }

        [Test]
        public void AnImageWithNothingDrawnOnItPointsAtItsOwnCorner()
        {
            Texture2D empty = BuildPointer(4, 4, Array.Empty<(int, int)>());
            try
            {
                Assert.That(GameCursor.MeasureHotspot(empty), Is.EqualTo(Vector2.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(empty);
            }
            Assert.That(GameCursor.MeasureHotspot(null), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void PointerArtWithoutAnImageIsTreatedAsHavingNone()
        {
            Assert.That(default(GameCursor.PointerArt).IsEmpty, Is.True);
            Assert.That(
                new GameCursor.PointerArt(string.Empty, Color.white).IsEmpty,
                Is.True
            );
            Assert.That(
                new GameCursor.PointerArt("Some/Pointer", Color.white).IsEmpty,
                Is.False
            );
        }

        /// <summary>
        /// A transparent texture with the given points drawn solid, addressed from the top
        /// left the way a hotspot is.
        /// </summary>
        private static Texture2D BuildPointer(int width, int height, (int x, int y)[] solid)
        {
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;
            texture.SetPixels(pixels);

            foreach ((int x, int y) in solid)
                texture.SetPixel(x, height - 1 - y, Color.white);

            texture.Apply();
            return texture;
        }
    }
}
