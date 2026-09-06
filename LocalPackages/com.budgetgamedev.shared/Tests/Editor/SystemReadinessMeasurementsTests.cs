using NUnit.Framework;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class SystemReadinessMeasurementsTests
    {
        [Test]
        public void ReportUsesWholeRunAndDoesNotCallMissingSensorsNominal()
        {
            using var run = new SystemReadinessMeasurements(null, null, false);
            for (int i = 0; i < 300; i++) run.AddFrame(1d / 30);
            for (int i = 0; i < 1200; i++) run.AddFrame(1d / 120);
            string report = run.BuildReport("Current settings", 40);
            Assert.That(report, Does.Contain("75.0 FPS"));
            Assert.That(report, Does.Contain("30.0 FPS 1% low"));
            Assert.That(report, Does.Contain("NOT MEASURED"));
            Assert.That(report, Does.Not.Contain("No supported, fresh sensor readings.\nHigh GPU load"));
        }

        [Test]
        public void PartialRunDoesNotPublishComponentAssessments()
        {
            using var run = new SystemReadinessMeasurements(null, null, false);
            for (int i = 0; i < 60; i++) run.AddFrame(1d / 60);
            Assert.That(run.BuildReport("", 3), Does.StartWith("INCOMPLETE TEST"));
        }

        [TestCase(null, "NOT MEASURED")]
        [TestCase(79.9, "NOMINAL")]
        [TestCase(80, "CAUTION")]
        [TestCase(95, "ATTENTION")]
        public void CapacityAssessmentHonorsBoundaries(double? value, string expected) =>
            Assert.That(SystemReadinessMeasurements.HighStatus(value, 80, 95), Is.EqualTo(expected));
    }
}
