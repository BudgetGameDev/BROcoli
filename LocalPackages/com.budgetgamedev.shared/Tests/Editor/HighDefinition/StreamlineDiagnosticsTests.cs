using System.Runtime.InteropServices;
using BudgetGameDev.Shared.Rendering.HighDefinition;
using NUnit.Framework;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class StreamlineDiagnosticsTests
    {
        [Test]
        public void DiagnosticsPacketMatchesNativeWindowsLayout()
        {
            Assert.That(Marshal.SizeOf<StreamlineNative.Diagnostics>(), Is.EqualTo(208));
            Assert.That(
                Marshal.OffsetOf<StreamlineNative.Diagnostics>("activeReflex").ToInt32(),
                Is.EqualTo(136)
            );
        }

        [Test]
        public void AcceptedOptionsAloneNeverClaimObservedFrameGeneration()
        {
            var status = new StreamlineNative.Status
            {
                initialized = 1,
                frameGenerationAvailable = 1,
                swapchainHooked = 1,
                generatedFrames = 3,
            };
            var data = new StreamlineNative.Diagnostics
            {
                snapshotTick = 10000,
                presentTick = 9900,
                tagMask = 7,
            };
            Assert.That(
                StreamlineDiagnosticsReport.FrameGenerationState(status, data),
                Does.Contain("NOT OBSERVED")
            );
            data.generatedTick = 9990;
            Assert.That(
                StreamlineDiagnosticsReport.FrameGenerationState(status, data),
                Is.EqualTo("EXTRA PRESENTS OBSERVED BY STREAMLINE")
            );
            data.generatedTick = 7000;
            Assert.That(
                StreamlineDiagnosticsReport.FrameGenerationState(status, data),
                Does.Contain("NOT OBSERVED")
            );
            data.presentTick = 7000;
            Assert.That(
                StreamlineDiagnosticsReport.FrameGenerationState(status, data),
                Is.EqualTo("NO RECENT SUCCESSFUL PRESENT")
            );
        }

        [Test]
        public void MissingProxyAndInvalidInputsOverrideHistoricalSuccess()
        {
            var status = new StreamlineNative.Status
            {
                initialized = 1,
                frameGenerationAvailable = 1,
                generatedFrames = 3,
            };
            var data = new StreamlineNative.Diagnostics
            {
                snapshotTick = 10000,
                presentTick = 9990,
                generatedTick = 9990,
                tagMask = 7,
            };
            Assert.That(
                StreamlineDiagnosticsReport.FrameGenerationState(status, data),
                Is.EqualTo("SWAPCHAIN NOT ATTACHED")
            );
            status.swapchainHooked = 1;
            data.tagMask = 3;
            Assert.That(
                StreamlineDiagnosticsReport.FrameGenerationState(status, data),
                Is.EqualTo("INCOMPLETE HDRP INPUTS")
            );
            status.frameGenerationStatus = 2;
            Assert.That(
                StreamlineDiagnosticsReport.FrameGenerationState(status, data),
                Is.EqualTo("FRAME GENERATION ERROR")
            );
        }

        [Test]
        public void UnsupportedEditorCanRenderFullDiagnosticsWithoutQueryingHdrMetadata()
        {
            var backend = new StreamlineSettingsBackend();
            var state = backend.Capture();
            Assert.That(state.CanSetFrames, Is.False);
            Assert.That(state.Report, Does.Contain("Windows player only"));
            Assert.That(state.Report, Does.Contain("No accepted configuration"));
        }

        [Test]
        public void UnreportedOrFutureTimestampsAreNeverFresh()
        {
            Assert.That(StreamlineDiagnosticsReport.Fresh(10000, 0), Is.False);
            Assert.That(StreamlineDiagnosticsReport.Fresh(10000, 11000), Is.False);
            Assert.That(StreamlineDiagnosticsReport.Result(4), Does.Contain("HAGS disabled"));
            Assert.That(StreamlineDiagnosticsReport.Result(999), Does.Contain("Unknown"));
            Assert.That(
                StreamlineDiagnosticsReport.FgStatus(6),
                Does.Contain("Reflex").And.Contain("HDR")
            );
        }
    }
}
