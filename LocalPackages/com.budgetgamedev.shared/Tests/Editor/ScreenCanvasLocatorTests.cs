using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Covers the search that keeps HUD code off the world-space health-bar
    /// canvases that enemies and projectiles carry around.
    /// </summary>
    public sealed class ScreenCanvasLocatorTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            ClearScreenObjects();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            ClearScreenObjects();
        }

        [Test]
        public void FindReportsNothingWhenTheSceneHasNoCanvasAtAll()
        {
            Assert.That(ScreenCanvasLocator.Find(), Is.Null);
        }

        [Test]
        public void FindSkipsWorldSpaceAndSwitchedOffCanvases()
        {
            NewCanvas("HealthBar", RenderMode.WorldSpace);
            Canvas hidden = NewCanvas("Hidden", RenderMode.ScreenSpaceOverlay);
            hidden.gameObject.SetActive(false);

            Assert.That(
                ScreenCanvasLocator.Find(),
                Is.Null,
                "Neither a world-space nor a disabled canvas is the screen canvas."
            );
        }

        [Test]
        public void FindPrefersARootOverlayCanvasNamedCanvasOverANestedOne()
        {
            Canvas nested = NewCanvas("HudCanvas", RenderMode.ScreenSpaceCamera);
            GameObject holder = NewObject("Holder");
            nested.transform.SetParent(holder.transform, false);
            Canvas screen = NewCanvas("Canvas", RenderMode.ScreenSpaceOverlay);

            Assert.That(ScreenCanvasLocator.Find(), Is.SameAs(screen));
        }

        [Test]
        public void FindSettlesOnOneCanvasWhenSeveralAreEquallySuitable()
        {
            Canvas first = NewCanvas("Canvas", RenderMode.ScreenSpaceOverlay);
            Canvas second = NewCanvas("Canvas", RenderMode.ScreenSpaceOverlay);
            Canvas third = NewCanvas("Canvas", RenderMode.ScreenSpaceOverlay);

            Canvas found = ScreenCanvasLocator.Find();

            Assert.That(found, Is.Not.Null);
            Assert.That(
                found == first || found == second || found == third,
                Is.True,
                "The winner has to be one of the candidates."
            );
            Assert.That(
                ScreenCanvasLocator.Find(),
                Is.SameAs(found),
                "A tie must not flip from one call to the next."
            );
        }

        [Test]
        public void GetOrCreateHandsBackTheCanvasTheSceneAlreadyHas()
        {
            Canvas existing = NewCanvas("Canvas", RenderMode.ScreenSpaceOverlay);

            Assert.That(ScreenCanvasLocator.GetOrCreate(), Is.SameAs(existing));
            Assert.That(EventSystems(), Is.Empty, "Nothing was missing, so nothing was built.");
        }

        [Test]
        public void GetOrCreateBuildsAScaledOverlayCanvasAndTheEventSystemItNeeds()
        {
            Canvas canvas = ScreenCanvasLocator.GetOrCreate();
            Track(canvas.gameObject);
            foreach (EventSystem system in EventSystems())
                Track(system.gameObject);

            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null);

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f).Within(0.0001f));

            Assert.That(EventSystems().Count, Is.EqualTo(1), "UI is dead without an EventSystem.");
            Assert.That(
                ScreenCanvasLocator.Find(),
                Is.SameAs(canvas),
                "The canvas it just built has to be the one it finds next time."
            );
        }

        [Test]
        public void GetOrCreateReusesTheEventSystemTheSceneAlreadyHas()
        {
            GameObject host = NewObject("EventSystem");
            host.AddComponent<EventSystem>();

            Canvas canvas = ScreenCanvasLocator.GetOrCreate();
            Track(canvas.gameObject);

            Assert.That(EventSystems().Count, Is.EqualTo(1));
        }

        private static List<EventSystem> EventSystems()
        {
            return new List<EventSystem>(
                Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include)
            );
        }

        private static void ClearScreenObjects()
        {
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (canvas != null)
                    Object.DestroyImmediate(canvas.gameObject);
            }

            foreach (EventSystem system in EventSystems())
            {
                if (system != null)
                    Object.DestroyImmediate(system.gameObject);
            }
        }

        private GameObject NewObject(string objectName)
        {
            var host = new GameObject(objectName);
            _created.Add(host);
            return host;
        }

        private Canvas NewCanvas(string objectName, RenderMode mode)
        {
            var host = new GameObject(objectName, typeof(Canvas));
            _created.Add(host);
            Canvas canvas = host.GetComponent<Canvas>();
            canvas.renderMode = mode;
            return canvas;
        }

        private void Track(GameObject created)
        {
            _created.Add(created);
        }
    }
}
