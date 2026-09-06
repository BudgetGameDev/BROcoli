using BudgetGameDev.Shared.Rendering;
using NUnit.Framework;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class PerformanceLatencyTests
    {
        private static StreamlineNative.Diagnostics Sample() => new()
        { snapshotTick = 2000, reportTick = 1900, latencyValid = 1, pcLatencyUs = 78000,
            actualPresentedLast = 4, activeReflex = 2, reflexStateResult = 0 };

        [Test]
        public void GeneratedFramesDoNotDivideMeasuredLatencyOrAddAnInventedPenalty()
        {
            var d = Sample();
            Assert.That(PerformanceLatency.PcMilliseconds(true, d), Is.EqualTo(78));
            d.actualPresentedLast = 1;
            Assert.That(PerformanceLatency.PcMilliseconds(true, d), Is.EqualTo(78));
            Assert.That(PerformanceLatency.Format(true, d), Does.Contain("#FF7373").And.Contain("Input/display delay not measured"));
        }

        [TestCase("missing")]
        [TestCase("stale")]
        [TestCase("failed")]
        [TestCase("invalid")]
        [TestCase("zero")]
        public void MissingOrStaleLatencyIsNotShownAsZero(string reason)
        {
            var d = Sample();
            if (reason == "stale") d.reportTick = 1;
            if (reason == "failed") d.reflexStateResult = 1;
            if (reason == "invalid") d.latencyValid = 0;
            if (reason == "zero") d.pcLatencyUs = 0;
            Assert.That(PerformanceLatency.PcMilliseconds(reason != "missing", d), Is.Null);
        }

        [TestCase("UniversalRenderPipelineAsset", "URP")]
        [TestCase("HDRenderPipelineAsset", "HDRP")]
        [TestCase(null, "BUILT-IN")]
        [TestCase("CustomPipelineAsset", "CUSTOM SRP")]
        public void LabelsTheActivePipeline(string typeName, string expected) =>
            Assert.That(PerformanceLatency.Pipeline(typeName), Is.EqualTo(expected));
    }
}
