using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Covers the sweep that keeps GraphicRaycaster from tripping over graphics
    /// Unity has already destroyed. The registry it reaches into is global, so
    /// every canvas this fixture registers is purged again on the way out.
    /// </summary>
    public sealed class GraphicRegistryCleanerTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly List<Canvas> _registered = new List<Canvas>();

        [SetUp]
        public void SetUp()
        {
            GraphicRegistryCleaner.instance = null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Canvas canvas in _registered)
                PurgeRegistry(canvas);
            _registered.Clear();

            foreach (GameObject created in _created)
            {
                if (created != null)
                    UnityEngine.Object.DestroyImmediate(created);
            }

            _created.Clear();
            GraphicRegistryCleaner.instance = null;
        }

        [Test]
        public void TheFirstCleanerClaimsTheSingletonAndADuplicateRemovesItself()
        {
            GraphicRegistryCleaner owner = NewCleaner();
            owner.Awake();
            Assert.That(GraphicRegistryCleaner.instance, Is.SameAs(owner));

            GraphicRegistryCleaner duplicate = NewCleaner();
            duplicate.Awake();

            Assert.That(GraphicRegistryCleaner.instance, Is.SameAs(owner));
            Assert.That(duplicate == null, Is.True, "The second cleaner tears itself down.");

            owner.Awake();
            Assert.That(
                GraphicRegistryCleaner.instance,
                Is.SameAs(owner),
                "Re-awaking the owner must not disown it."
            );
        }

        [Test]
        public void OnlyTheOwningCleanerReleasesTheSingletonWhenItGoesAway()
        {
            GraphicRegistryCleaner owner = NewCleaner();
            owner.Awake();
            GraphicRegistryCleaner bystander = NewCleaner();

            bystander.OnDestroy();
            Assert.That(GraphicRegistryCleaner.instance, Is.SameAs(owner));

            owner.OnDestroy();
            Assert.That(GraphicRegistryCleaner.instance, Is.Null);
        }

        [Test]
        public void UpdateOnlySweepsOnceTheIntervalHasElapsed()
        {
            GraphicRegistryCleaner cleaner = NewCleaner();
            cleaner.cleanupInterval = 0.5f;

            cleaner.lastCleanupTime = float.PositiveInfinity;
            cleaner.Update();
            Assert.That(
                cleaner.lastCleanupTime,
                Is.EqualTo(float.PositiveInfinity),
                "A sweep that just ran must not run again on the next frame."
            );

            cleaner.lastCleanupTime = float.NegativeInfinity;
            cleaner.Update();
            Assert.That(
                float.IsInfinity(cleaner.lastCleanupTime),
                Is.False,
                "An overdue sweep runs and stamps the clock."
            );
        }

        [Test]
        public void TheSweepLeavesEveryRaycasterInTheStateItFoundIt()
        {
            Canvas live = NewCanvas("Live", withRaycaster: true);
            Canvas muted = NewCanvas("Muted", withRaycaster: true);
            muted.GetComponent<GraphicRaycaster>().enabled = false;
            Canvas bare = NewCanvas("Bare", withRaycaster: false);

            GraphicRegistryCleaner.CleanupDestroyedGraphics();

            Assert.That(live.GetComponent<GraphicRaycaster>().enabled, Is.True);
            Assert.That(
                muted.GetComponent<GraphicRaycaster>().enabled,
                Is.False,
                "The sweep must not switch on a raycaster the game turned off."
            );
            Assert.That(bare.GetComponent<GraphicRaycaster>(), Is.Null);
        }

        [Test]
        public void ACanvasHoldingADestroyedGraphicHasItsLiveGraphicsReRegistered()
        {
            Canvas host = NewCanvas("Host", withRaycaster: true);
            Image live = NewImage("Live", host.transform);
            Image muted = NewImage("Muted", host.transform);
            muted.enabled = false;

            // A graphic that belongs to another canvas but was also registered
            // against this one leaves exactly the stale entry this sweep exists
            // to survive: destroying it only unregisters it from its own canvas.
            Canvas elsewhere = NewCanvas("Elsewhere", withRaycaster: false);
            Image ghost = NewImage("Ghost", elsewhere.transform);
            GraphicRegistry.RegisterGraphicForCanvas(host, ghost);
            UnityEngine.Object.DestroyImmediate(ghost.gameObject);

            GraphicRegistryCleaner.CleanupGraphicRegistry(GraphicRegistryCleaner.RegistryTypeName);

            Assert.That(live.enabled, Is.True);
            Assert.That(
                muted.enabled,
                Is.False,
                "Re-registration must not switch a hidden graphic back on."
            );
            // IndexedSet refuses to be enumerated, so ask it directly.
            IList<Graphic> registered = GraphicRegistry.GetGraphicsForCanvas(host);
            Assert.That(
                registered.Contains(live),
                Is.True,
                "The live graphic must come back registered against its canvas."
            );
        }

        [Test]
        public void ARegistryEntryLeftBehindByADestroyedCanvasIsSkipped()
        {
            Canvas doomed = NewCanvas("Doomed", withRaycaster: false);
            Canvas keeper = NewCanvas("Keeper", withRaycaster: false);
            Image orphan = NewImage("Orphan", keeper.transform);
            GraphicRegistry.RegisterGraphicForCanvas(doomed, orphan);
            UnityEngine.Object.DestroyImmediate(doomed.gameObject);

            Assert.DoesNotThrow(() =>
                GraphicRegistryCleaner.CleanupGraphicRegistry(
                    GraphicRegistryCleaner.RegistryTypeName
                )
            );
            Assert.That(orphan.enabled, Is.True);
        }

        [Test]
        public void ARegistryUnityHasRenamedIsGivenUpOnQuietly()
        {
            // The WebGL smoke probes fail the build on an application warning, so a
            // registry Unity has moved must degrade in silence rather than shout.
            // LogAssert.NoUnexpectedReceived plus TestMustExpectAllLogs is what
            // holds that: any log at all here would fail this test.
            Assert.DoesNotThrow(() =>
                GraphicRegistryCleaner.CleanupGraphicRegistry("UnityEngine.UI.NoSuchRegistry")
            );
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ARegistryEntryWithNoIndexedSetIsSkipped()
        {
            Canvas canvas = NewCanvas("Missing Indexed Set", withRaycaster: false);
            MissingSetRegistry.Graphics.Clear();
            MissingSetRegistry.Graphics.Add(canvas, null);
            Assert.DoesNotThrow(() =>
                GraphicRegistryCleaner.CleanupGraphicRegistryType(typeof(MissingSetRegistry))
            );
            MissingSetRegistry.Graphics.Clear();
        }

        private sealed class MissingSetRegistry
        {
            private static readonly IDictionary m_Graphics = new Hashtable();
            public static object instance { get; } = new MissingSetRegistry();
            internal static IDictionary Graphics => m_Graphics;
        }

        private GraphicRegistryCleaner NewCleaner()
        {
            var host = new GameObject("Cleaner");
            _created.Add(host);
            return host.AddComponent<GraphicRegistryCleaner>();
        }

        private Canvas NewCanvas(string objectName, bool withRaycaster)
        {
            var host = new GameObject(objectName, typeof(Canvas));
            _created.Add(host);
            Canvas canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (withRaycaster)
                host.AddComponent<GraphicRaycaster>();

            _registered.Add(canvas);
            return canvas;
        }

        private Image NewImage(string objectName, Transform parent)
        {
            var host = new GameObject(objectName, typeof(RectTransform));
            host.transform.SetParent(parent, false);
            _created.Add(host);
            return host.AddComponent<Image>();
        }

        /// <summary>
        /// Drops a canvas from Unity's global graphic registry. Without this a
        /// deliberately stale entry would outlive the test that made it.
        /// </summary>
        private static void PurgeRegistry(Canvas canvas)
        {
            Type registryType = typeof(Graphic).Assembly.GetType(
                GraphicRegistryCleaner.RegistryTypeName
            );
            object registry = registryType
                .GetProperty("instance", BindingFlags.Static | BindingFlags.Public)
                .GetValue(null);

            foreach (string fieldName in new[] { "m_Graphics", "m_RaycastableGraphics" })
            {
                FieldInfo field = registryType.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                ((IDictionary)field.GetValue(registry)).Remove(canvas);
            }
        }
    }
}
