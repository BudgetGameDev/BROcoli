using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class DiskSmartHealthTests
    {
        private const long Now = 1000;
        private static DiskSmartReading Healthy() => new() { id = "PhysicalDrive0", nvme = true, spare = 100, spareThreshold = 10,
            sampledAt = Now, mediaErrors = "0", unsafeShutdowns = "17", errorLogEntries = "42" };

        [Test]
        public void HistoricalShutdownsAndLogEntriesAloneDoNotFailHealth() =>
            Assert.That(DiskSmartHealth.Assess(Healthy(), Now), Is.EqualTo("NOMINAL"));

        [TestCase(1)] [TestCase(2)] [TestCase(4)] [TestCase(8)] [TestCase(16)] [TestCase(32)] [TestCase(128)]
        public void EveryCriticalWarningRequiresAttention(int mask)
        {
            var r = Healthy(); r.criticalWarning = mask;
            Assert.That(DiskSmartHealth.Assess(r, Now), Is.EqualTo("ATTENTION"));
        }

        [TestCase(79, "NOMINAL")] [TestCase(80, "CAUTION")] [TestCase(99, "CAUTION")] [TestCase(100, "ATTENTION")] [TestCase(255, "ATTENTION")]
        public void EnduranceBoundaries(int used, string expected)
        {
            var r = Healthy(); r.percentageUsed = used;
            Assert.That(DiskSmartHealth.Assess(r, Now), Is.EqualTo(expected));
        }

        [Test]
        public void SpareAndMediaErrorsAreAssessedSeparately()
        {
            var r = Healthy(); r.mediaErrors = "18446744073709551616";
            Assert.That(DiskSmartHealth.Assess(r, Now), Is.EqualTo("CAUTION"));
            r.spare = 9;
            Assert.That(DiskSmartHealth.Assess(r, Now), Is.EqualTo("ATTENTION"));
        }

        [Test]
        public void UnknownDeniedAndStaleNeverBecomeNominal()
        {
            Assert.That(DiskSmartHealth.Assess(new DiskSmartReading { sampledAt = Now, status = "Access denied" }, Now), Is.EqualTo("NOT MEASURED"));
            Assert.That(DiskSmartHealth.Assess(Healthy(), Now + 31), Is.EqualTo("NOT MEASURED"));
            Assert.That(DiskSmartHealth.Assess(Healthy(), Now - 1), Is.EqualTo("NOT MEASURED"));
        }

        [TestCase(false, "NOMINAL")] [TestCase(true, "ATTENTION")]
        public void AtaSummaryPredictionIsUsedWhenAvailable(bool failed, string expected)
        {
            var r = new DiskSmartReading { sampledAt = Now, predictionKnown = true, predictedFailure = failed };
            Assert.That(DiskSmartHealth.Assess(r, Now), Is.EqualTo(expected));
        }

        private static byte[] Payload()
        {
            var data = new byte[560];
            void Put(int index, int value) => BitConverter.GetBytes(value).CopyTo(data, index);
            Put(0, 48); Put(4, 48); Put(8, 3); Put(12, 2); Put(24, 40); Put(28, 512);
            data[51] = 100; data[52] = 10;
            return data;
        }

        [Test]
        public void ProtocolOffsetsAndUnsigned128BitCountersAreDecoded()
        {
            var data = Payload(); data[48 + 160 + 8] = 1;
            var r = new DiskSmartReading(); DiskSmartHealth.DecodeNvme(data, data.Length, r);
            Assert.That(r.nvme, Is.True);
            Assert.That(r.spare, Is.EqualTo(100));
            Assert.That(r.mediaErrors, Is.EqualTo("18446744073709551616"));
            Assert.That(Convert.FromBase64String(r.rawData).Length, Is.EqualTo(512));
        }

        [Test]
        public void TruncatedEmptyAndOverflowingResponsesAreRejected()
        {
            var data = Payload();
            Assert.Throws<InvalidDataException>(() => DiskSmartHealth.DecodeNvme(data, 559, new DiskSmartReading()));
            BitConverter.GetBytes(uint.MaxValue).CopyTo(data, 24);
            Assert.Throws<InvalidDataException>(() => DiskSmartHealth.DecodeNvme(data, 560, new DiskSmartReading()));
            data = Payload(); Array.Clear(data, 48, 512);
            Assert.Throws<InvalidDataException>(() => DiskSmartHealth.DecodeNvme(data, 560, new DiskSmartReading()));
        }

        [Test]
        public void BenchmarkRetainsWarningsEvenIfLaterSnapshotLooksHealthy()
        {
            using var run = new SystemReadinessMeasurements(null, null, false);
            for (int i = 0; i < 1200; i++) run.AddFrame(1d / 60);
            var warning = Healthy(); warning.sampledAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); warning.criticalWarning = 4;
            run.CaptureSmart(new HardwareSensorService.Snapshot { ReceivedAt = Stopwatch.GetTimestamp(), diskSmart = new[] { warning } });
            var healthy = Healthy(); healthy.sampledAt = warning.sampledAt;
            run.CaptureSmart(new HardwareSensorService.Snapshot { ReceivedAt = Stopwatch.GetTimestamp(), diskSmart = new[] { healthy } });
            string report = run.BuildReport("", 40);
            Assert.That(report, Does.Contain("reliability degraded"));
            Assert.That(report, Does.Contain("Back up important data"));
        }
    }
}
