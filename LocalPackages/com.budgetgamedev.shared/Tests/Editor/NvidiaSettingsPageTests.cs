using System.Linq;
using BudgetGameDev.Shared.Rendering;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class NvidiaSettingsPageTests
    {
        private sealed class Backend : NvidiaRendering.IBackend
        {
            public int Frames = 3,
                Reflex = 1,
                Releases;
            public bool Dlss = true;

            public NvidiaRendering.Snapshot Capture() =>
                new NvidiaRendering.Snapshot
                {
                    DlssRequested = Dlss,
                    GeneratedFrames = Frames,
                    Reflex = Reflex,
                    CanSetDlss = true,
                    CanSetFrames = true,
                    CanSetReflex = true,
                    MaximumGeneratedFrames = 3,
                    Report = string.Join(
                        "\n",
                        Enumerable.Repeat("Native diagnostic line <not markup>", 60)
                    ),
                };

            public void SetDlss(bool value) => Dlss = value;

            public void SetFrames(int value) => Frames = value;

            public void SetReflex(int value) => Reflex = value;

            public void Reset()
            {
                Dlss = true;
                Frames = 3;
                Reflex = 1;
            }

            public void ReleaseDiagnostics() => Releases++;
        }

        [Test]
        public void PageBindsSettingsAndPreservesFullScrollablePlainTextReport()
        {
            var previous = NvidiaRendering.Backend;
            var backend = new Backend();
            NvidiaRendering.Backend = backend;
            var host = new GameObject("Nvidia Page Test", typeof(RectTransform), typeof(Canvas));
            string clipboard = GUIUtility.systemCopyBuffer;
            try
            {
                bool closed = false;
                var page = NvidiaSettingsPage.Create(
                    (RectTransform)host.transform,
                    TMP_Settings.defaultFontAsset,
                    () => closed = true
                );
                page.Layout(216, 120, -120, true, true);
                page.Open();
                var scroll = page.GetComponentInChildren<ScrollRect>();
                Assert.That(
                    scroll.content.rect.height,
                    Is.GreaterThan(scroll.viewport.rect.height)
                );
                var text = scroll.GetComponentInChildren<TMP_Text>();
                Assert.That(text.richText, Is.False);
                Assert.That(text.text, Does.Contain("<not markup>"));
                page.GetComponentsInChildren<Button>()
                    .Single(button => button.name == "FrameGenControl")
                    .onClick.Invoke();
                Assert.That(backend.Frames, Is.Zero);
                page.GetComponentsInChildren<Button>()
                    .Single(button => button.name == "NvidiaCopy")
                    .onClick.Invoke();
                Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo(backend.Capture().Report));
                page.Close();
                Assert.That(closed, Is.True);
                Assert.That(page.IsOpen, Is.False);
                Assert.That(backend.Releases, Is.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(host);
                NvidiaRendering.Backend = previous;
                GUIUtility.systemCopyBuffer = clipboard;
            }
        }

        [Test]
        public void MissingBackendShowsUnavailableInsteadOfConfiguredDefaultsAsActive()
        {
            var previous = NvidiaRendering.Backend;
            try
            {
                NvidiaRendering.Backend = null;
                var state = NvidiaRendering.Capture();
                Assert.That(state.CanSetFrames, Is.False);
                Assert.That(state.Report, Does.Contain("NOT OBSERVED"));
                Assert.That(state.Report, Does.Contain("Active configuration: unavailable"));
            }
            finally
            {
                NvidiaRendering.Backend = previous;
            }
        }
    }
}
