using System.Diagnostics;
using NUnit.Framework;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class MemoryConfigurationTests
    {
        private static HardwareSensorService.Reading Reading(string slot, string name, float value, string id = null) => new()
        { category = "Memory", hardware = slot, name = name, value = value, available = true, type = "DataRate", unit = "MT/s", id = id };
        private static HardwareSensorService.Snapshot Snapshot(params HardwareSensorService.Reading[] readings) => new()
        { ReceivedAt = Stopwatch.GetTimestamp(), readings = readings };

        [TestCase(4800, 6000, true)]
        [TestCase(6000, 6000, false)]
        [TestCase(5980, 6000, false)]
        [TestCase(6200, 6000, false)]
        [TestCase(4800, 0, false)]
        [TestCase(0, 6000, false)]
        public void ComparesConfiguredTransferRateWithSameModuleCapability(float configured, float capability, bool expected)
        {
            var result = MemoryConfiguration.Assess(Snapshot(Reading("DDR5 DIMM A", "Configured DDR rate", configured),
                Reading("DDR5 DIMM A", "Firmware DDR capability", capability)));
            Assert.That(result.Suboptimal, Is.EqualTo(expected));
            if (expected)
            {
                Assert.That(result.Details, Does.Contain("20% below reported rate"));
                Assert.That(result.OverlayLine, Does.Contain("#FFD166").And.Contain("4800 / 6000 MT/s"));
            }
            else Assert.That(result.OverlayLine, Is.Empty);
        }

        [Test]
        public void MixedKitsAndRepeatedSlotNamesAreNotCrossCompared()
        {
            var result = MemoryConfiguration.Assess(Snapshot(
                Reading("Unknown slot", "Configured DDR rate", 4800, "/firmware/memory/0/configured"),
                Reading("Unknown slot", "Firmware DDR capability", 4800, "/firmware/memory/0/capability"),
                Reading("Unknown slot", "Configured DDR rate", 6000, "/firmware/memory/1/configured"),
                Reading("Unknown slot", "Firmware DDR capability", 6000, "/firmware/memory/1/capability")));
            Assert.That(result.Suboptimal, Is.False);
        }

        [Test]
        public void MissingCapabilityDoesNotBorrowAnotherDimmsRate()
        {
            var result = MemoryConfiguration.Assess(Snapshot(Reading("A", "Configured DDR rate", 4800),
                Reading("B", "Firmware DDR capability", 6000)));
            Assert.That(result.Suboptimal, Is.False);
            Assert.That(result.Details, Does.Contain("capability unavailable"));
        }

        [Test]
        public void StaleAndInvalidValuesCannotFlagConfiguration()
        {
            var snapshot = Snapshot(Reading("A", "Configured DDR rate", 4800), Reading("A", "Firmware DDR capability", float.PositiveInfinity));
            Assert.That(MemoryConfiguration.Assess(snapshot).Suboptimal, Is.False);
            snapshot.readings[1].value = 6000;
            snapshot.ReceivedAt -= 11 * Stopwatch.Frequency;
            Assert.That(MemoryConfiguration.Assess(snapshot).OverlayLine, Is.Empty);
        }

        [Test]
        public void KitNamesAreEscapedAndMatchingFirmwareDoesNotClaimXmpIsEnabled()
        {
            var snapshot = Snapshot(Reading("<b>DIMM", "Configured DDR rate", 4800), Reading("<b>DIMM", "Firmware DDR capability", 4800));
            var result = MemoryConfiguration.Assess(snapshot);
            Assert.That(result.Details, Does.Not.Contain("<b>DIMM"));
            Assert.That(MemoryConfiguration.Limitations, Does.Contain("not a verified advertised XMP/EXPO rating"));
            var sample = new PerformanceResources.Sample { Timestamp = Stopwatch.GetTimestamp() };
            ClockReadiness.ReadSensorClocks(snapshot, sample);
            var run = new ClockReadiness(); run.Add(sample);
            Assert.That(run.Report(120), Does.Contain("matching firmware values do not prove"));
            Assert.That(run.ConfigurationSummary, Is.Empty);
        }
    }
}
