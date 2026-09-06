using BudgetGameDev.Shared.Rendering;
using NUnit.Framework;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class FrameGenerationStatisticsTests
    {
        private static StreamlineNative.Status Active(uint generated = 3) => new()
        { initialized = 1, frameGenerationAvailable = 1, swapchainHooked = 1, generatedFrames = generated };
        private static StreamlineNative.Diagnostics Sample(ulong tick, ulong real, ulong total, ulong queries) => new()
        { snapshotTick = tick, presentTick = tick, presentedFrames = real, slPresentedFrames = total, slStateSamples = queries };

        [TestCase(1u, 120ul)]
        [TestCase(2u, 180ul)]
        [TestCase(3u, 240ul)]
        [TestCase(3u, 120ul)] // 4x selected, but only 2x observed: never multiply by the setting.
        [TestCase(3u, 60ul)]
        public void RatesUseObservedCounters(uint configured, ulong total)
        {
            var stats = new FrameGenerationStatistics();
            stats.Add(3, true, true, Active(configured), Sample(1000, 10, 40, 10));
            Assert.That(stats.TotalFps, Is.Null);
            stats.Add(3, true, true, Active(configured), Sample(2000, 70, 40 + total, 70));
            Assert.That(stats.RenderedFps, Is.EqualTo(60));
            Assert.That(stats.TotalFps, Is.EqualTo(total));
            Assert.That(stats.FormatRates(), Does.Contain("TOTAL").And.Contain("including generated"));
            Assert.That(stats.FormatRates(), Does.Not.Contain("GENERATED ≈"));
        }

        [TestCase("off")]
        [TestCase("suspended")]
        [TestCase("unsupported")]
        [TestCase("unavailable")]
        [TestCase("telemetry")]
        [TestCase("stale")]
        [TestCase("error")]
        public void UnavailableOrInactiveCountersDoNotInventGeneratedFrames(string reason)
        {
            var stats = new FrameGenerationStatistics();
            var status = Active();
            var sample = Sample(3000, 100, 400, 100);
            if (reason == "suspended") status.generatedFrames = 0;
            if (reason == "unsupported") status.frameGenerationAvailable = 0;
            if (reason == "stale") sample.presentTick = 1000;
            if (reason == "error") sample.fgStateResult = 1;
            stats.Add(reason == "off" ? 0 : 3, reason != "unavailable", reason != "telemetry", status, sample);
            Assert.That(stats.TotalFps, Is.Null);
            Assert.That(stats.RenderedFps, Is.Null);
        }

        [Test]
        public void MissingStateQueriesDoNotProduceFalseRates()
        {
            var stats = new FrameGenerationStatistics();
            stats.Add(3, true, true, Active(), Sample(1000, 10, 40, 10));
            stats.Add(3, true, true, Active(), Sample(2000, 70, 140, 35));
            Assert.That(stats.TotalFps, Is.Null);
            Assert.That(stats.State, Does.Contain("incomplete"));
        }

        [Test]
        public void CounterResetFocusGapAndModeChangeDiscardOldWindow()
        {
            var stats = new FrameGenerationStatistics();
            stats.Add(3, true, true, Active(), Sample(1000, 100, 400, 100));
            stats.Add(3, true, true, Active(), Sample(2000, 160, 640, 160));
            Assert.That(stats.TotalFps, Is.EqualTo(240));
            stats.Add(3, true, true, Active(), Sample(3000, 1, 4, 1));
            Assert.That(stats.TotalFps, Is.Null);
            stats.Add(3, true, true, Active(), Sample(5000, 121, 484, 121));
            Assert.That(stats.TotalFps, Is.Null);
            stats.Add(1, true, true, Active(1), Sample(6000, 181, 604, 181));
            Assert.That(stats.TotalFps, Is.Null);
            stats.Clear();
            Assert.That(stats.RenderedFps, Is.Null);
        }
    }
}
