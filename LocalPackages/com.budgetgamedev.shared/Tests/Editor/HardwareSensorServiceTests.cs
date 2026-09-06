using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class HardwareSensorServiceTests
    {
        [Test]
        public void ZeroTemperatureIsNotReportedAsHealthy()
        {
            var s = HardwareSensorService.ParseSnapshot("{\"state\":\"Ready\",\"readings\":[{\"category\":\"Cpu\",\"type\":\"Temperature\",\"available\":true,\"value\":0}]}");
            Assert.That(s.readings[0].available, Is.False);
            Assert.That(HardwareSensorService.PeakTemperature(s, "Cpu"), Is.Null);
            Assert.That(HardwareSensorService.FormatReport(s), Does.Contain("Invalid reading"));
        }

        [Test]
        public void StaleTemperatureIsExcludedFromAssessment()
        {
            var s = HardwareSensorService.ParseSnapshot("{\"readings\":[{\"category\":\"Cpu\",\"type\":\"Temperature\",\"available\":true,\"value\":65}]}");
            Assert.That(HardwareSensorService.PeakTemperature(s, "Cpu"), Is.EqualTo(65));
            s.ReceivedAt -= 11 * Stopwatch.Frequency;
            Assert.That(HardwareSensorService.PeakTemperature(s, "Cpu"), Is.Null);
            Assert.That(HardwareSensorService.FormatReport(s), Does.Contain("(stale)"));
        }

        [TestCase(false, false, "Standard user", "Not installed")]
        [TestCase(true, false, "Administrator", "Not installed")]
        [TestCase(true, true, "Administrator", "Installed")]
        public void AccessAndDriverAvailabilityAreIndependent(bool elevated, bool driver, string access, string driverLabel)
        {
            var s = new HardwareSensorService.Snapshot { elevated = elevated, pawnIoInstalled = driver, ReceivedAt = Stopwatch.GetTimestamp() };
            string report = HardwareSensorService.FormatReport(s);
            Assert.That(report, Does.Contain("Process access: " + access));
            Assert.That(report, Does.Contain("PawnIO driver: " + driverLabel));
        }

        [Test]
        public void UntrustedDeviceNamesCannotInjectRichText()
        {
            var s = HardwareSensorService.ParseSnapshot("{\"readings\":[{\"hardware\":\"<size=500>device\",\"available\":false,\"status\":\"Access denied\"}]}");
            Assert.That(HardwareSensorService.FormatReport(s), Does.Not.Contain("<size=500>"));
            Assert.That(HardwareSensorService.FormatReport(s), Does.Contain("Access denied"));
        }

        [Test]
        public void ConflictPauseExplainsMissingSensorsInsteadOfReportingHealthyTemperature()
        {
            var s = HardwareSensorService.ParseSnapshot("{\"state\":\"Hardware probing paused\",\"readings\":[],\"notices\":[{\"source\":\"Sensor access coordination\",\"status\":\"Probing paused\",\"detail\":\"Other monitoring/tuning software detected: MSIAfterburner.\"}]}");
            string report = HardwareSensorService.FormatReport(s);
            Assert.That(report, Does.Contain("Hardware probing paused"));
            Assert.That(report, Does.Contain("MSIAfterburner"));
            Assert.That(report, Does.Contain("Elevation does not bypass"));
            Assert.That(HardwareSensorService.PeakTemperature(s, "Cpu"), Is.Null);
        }

        [Test]
        public void OversizedProtocolIsRejected() =>
            Assert.Throws<InvalidDataException>(() => HardwareSensorService.ParseSnapshot(new string(' ', 1024 * 1024 + 1)));
    }
}
