using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class CpuBoostFrequencyTests
    {
        [Test]
        public void BoostAboveNominalIsPreservedAndDifferentCoreTypesAreMatched()
        {
            var frequencies = new Dictionary<string, double> { ["0,0"] = 4700, ["0,1"] = 3000, ["_Total"] = 9000 };
            var performance = new Dictionary<string, double> { ["0,0"] = 120, ["0,1"] = 150, ["_Total"] = 200 };
            Assert.That(CpuBoostFrequency.EstimatePeak(frequencies, performance), Is.EqualTo(5640));
        }

        [Test]
        public void UnmatchedZeroAndInvalidSamplesCannotCreateABoostClock()
        {
            var frequencies = new Dictionary<string, double> { ["0,0"] = 4700, ["0,1"] = 3000, ["0,2"] = 3000 };
            var performance = new Dictionary<string, double> { ["0,1"] = double.NaN, ["0,2"] = 0 };
            Assert.That(CpuBoostFrequency.EstimatePeak(frequencies, performance), Is.Null);
        }

        [Test]
        public void AvailableCoreSensorsTakePriorityOverWindowsEstimate()
        {
            var s = new PerformanceResources.Sample { CpuBoostClock = 5000, CpuBoostSource = "Windows estimate" };
            var snapshot = new HardwareSensorService.Snapshot { ReceivedAt = Stopwatch.GetTimestamp(), readings = new[] {
                new HardwareSensorService.Reading { category = "Cpu", name = "Core #1", type = "Clock", unit = "MHz", available = true, value = 5500 },
                new HardwareSensorService.Reading { category = "Cpu", name = "Core #2", type = "Clock", unit = "MHz", available = true, value = 5300 } } };
            ClockReadiness.ReadSensorClocks(snapshot, s);
            Assert.That(s.CpuBoostClock, Is.EqualTo(5500));
            Assert.That(s.CpuBoostSource, Is.EqualTo("Core sensor"));
            Assert.That(s.CpuClock, Is.EqualTo(5400));
        }
    }
}
