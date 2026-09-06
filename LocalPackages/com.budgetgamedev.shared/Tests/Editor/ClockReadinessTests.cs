using System.Diagnostics;
using NUnit.Framework;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class ClockReadinessTests
    {
        private static PerformanceResources.Sample Sample() => new() { Timestamp = Stopwatch.GetTimestamp(),
            CpuClock = 800, CpuReferenceClock = 4000, SystemCpu = 5, GpuClock = 300, GpuMaxClock = 2500, Gpu = 5,
            DiskRead = 0, DiskWrite = 0, DiskTransfers = 0 };

        [Test]
        public void IdleClocksAndZeroTransfersDoNotBecomeFaults()
        {
            var run = new ClockReadiness(); for (int i = 0; i < 5; i++) run.Add(Sample());
            string report = run.Report(20);
            Assert.That(report, Does.Not.Contain(">CAUTION<"));
            Assert.That(report, Does.Contain("NOT EXERCISED"));
        }

        [Test]
        public void RepeatedLowClocksUnderLoadAndPoorFpsProduceCaution()
        {
            var run = new ClockReadiness();
            for (int i = 0; i < 4; i++) { var s = Sample(); s.SystemCpu = s.Gpu = 95; run.Add(s); }
            Assert.That(run.Report(25), Does.Contain("Low reported frequency/clock limit"));
            Assert.That(run.Report(120), Does.Not.Contain(">CAUTION<"));
        }

        [Test]
        public void ThermalReasonsAreDistinctFromIdleAndNormalPowerCaps()
        {
            var normal = new ClockReadiness(); var thermal = new ClockReadiness();
            var s = Sample(); s.GpuClockReasons = 5; normal.Add(s);
            s.GpuClockReasons = 0x20; thermal.Add(s);
            Assert.That(normal.Report(60), Does.Not.Contain(">CAUTION<"));
            Assert.That(thermal.Report(60), Does.Contain(">CAUTION<"));
        }

        [Test]
        public void DiskThroughputAloneDoesNotDiagnoseDiskHealth()
        {
            var run = new ClockReadiness();
            for (int i = 0; i < 4; i++) { var s = Sample(); s.DiskRead = 100; s.DiskTransfers = 20; s.DiskLatencyMs = 30; run.Add(s); }
            Assert.That(run.Report(25), Does.Contain("Repeated >=20 ms transfers"));
            Assert.That(run.Report(120), Does.Not.Contain(">CAUTION<"));
        }

        [Test]
        public void StaleSamplesAreExcluded()
        {
            var s = Sample(); s.Timestamp -= 10 * Stopwatch.Frequency;
            var run = new ClockReadiness(); run.Add(s);
            Assert.That(run.Report(30), Does.Contain("CPU CLOCK · <color=#9BA7AE>NOT MEASURED"));
        }

        [Test]
        public void ConfiguredMemoryIsNeverPresentedAsLiveAndZeroCpuDoesNotOverrideFallback()
        {
            var s = Sample();
            var snapshot = new HardwareSensorService.Snapshot { ReceivedAt = Stopwatch.GetTimestamp(), readings = new[] {
                new HardwareSensorService.Reading { category="Cpu", name="Cores (Average)", type="Clock", unit="MHz", available=true, value=0 },
                new HardwareSensorService.Reading { category="Memory", hardware="DIMM A", name="Configured DDR clock", type="Clock", unit="MHz", available=true, value=2400 },
                new HardwareSensorService.Reading { category="Memory", hardware="DIMM A", name="Configured DDR rate", type="DataRate", unit="MT/s", available=true, value=4800 },
                new HardwareSensorService.Reading { category="Memory", hardware="DIMM A", name="Firmware DDR capability", type="DataRate", unit="MT/s", available=true, value=6000 },
            } };
            ClockReadiness.ReadSensorClocks(snapshot, s);
            Assert.That(s.CpuClock, Is.EqualTo(800));
            Assert.That(s.RamClock, Is.Null);
            Assert.That(s.RamConfiguredClock, Is.EqualTo(2400));
            Assert.That(s.RamUnderConfigured, Is.True);
            var run = new ClockReadiness(); run.Add(s);
            Assert.That(run.Report(60), Does.Contain("SUBOPTIMAL CONFIGURATION").And.Contain("4800 MT/s configured").And.Contain("6000 MT/s firmware capability"));
            Assert.That(run.Report(60), Does.Contain("20% below reported rate").And.Contain("XMP/EXPO"));
            Assert.That(run.ConfigurationSummary, Does.Contain("SUBOPTIMAL CONFIGURATION"));
        }
    }
}
