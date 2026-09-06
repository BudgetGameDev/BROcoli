using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace BudgetGameDev.Shared
{
    /// <summary>Low-frequency OS sampling. No Unity APIs or counter queries on the render thread.</summary>
    internal sealed class PerformanceResources : IDisposable
    {
        internal sealed class Sample
        {
            internal double? SystemCpu,
                GameCpu,
                Gpu,
                RamUsed,
                RamTotal,
                GameRam,
                VideoMemory,
                VideoTotal,
                VideoAvailable,
                SystemVideoMemory,
                GpuTemperature,
                DiskTemperature,
                CpuTemperature,
                RamTemperature,
                BoardTemperature,
                DiskBusy,
                DiskRead,
                DiskWrite,
                DiskSpaceUsed,
                DiskSpaceFree,
                DiskSpaceTotal;
            internal double? CpuClock, CpuReferenceClock, CpuClockLimit, GpuClock, GpuMemoryClock,
                GpuMaxClock, RamClock, RamConfiguredClock, RamConfiguredRate, RamCapabilityRate,
                DiskLatencyMs, DiskTransfers;
            internal string CpuClockSource = "Unavailable";
            internal double? CpuBoostClock;
            internal string CpuBoostSource = "Unavailable";
            internal ulong? GpuClockReasons;
            internal bool RamUnderConfigured;
            internal MemoryConfiguration RamConfiguration;
            internal long Timestamp;
            internal bool Fresh =>
                Timestamp != 0
                && (Stopwatch.GetTimestamp() - Timestamp) / (double)Stopwatch.Frequency < 4;
        }

        private readonly ManualResetEvent stop = new(false);
        private volatile Sample latest = new();
        private int disposed;
        private readonly string graphicsName;
        private readonly string gameDirectory;
        internal Sample Latest => latest;

        internal PerformanceResources(string graphicsName = null, string gameDirectory = null)
        {
            this.graphicsName = graphicsName;
            this.gameDirectory = gameDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
            new Thread(Collect)
            {
                IsBackground = true,
                Name = "Performance resource sampler",
            }.Start();
        }

        private void Collect()
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                using var windows = new WindowsPerformanceCounters(process.Id);
                using var nvidia = new NvidiaResourceSensors(graphicsName);
                using var disks = new DiskTemperatureSensors();
                double previousCpu = process.TotalProcessorTime.TotalSeconds;
                long previousTime = Stopwatch.GetTimestamp();
                while (!stop.WaitOne(1000))
                {
                    var sample = new Sample();
                    try
                    {
                        process.Refresh();
                        long now = Stopwatch.GetTimestamp();
                        double cpu = process.TotalProcessorTime.TotalSeconds;
                        sample.GameCpu = CpuPercent(
                            cpu - previousCpu,
                            (now - previousTime) / (double)Stopwatch.Frequency,
                            Environment.ProcessorCount
                        );
                        long resident = process.WorkingSet64;
                        sample.GameRam = resident > 0 ? resident : null;
                        previousCpu = cpu;
                        previousTime = now;
                        windows.Read(sample);
                        nvidia.Read(sample);
                        ClockReadiness.ReadSensorClocks(HardwareSensorService.Latest, sample, graphicsName);
                        sample.DiskTemperature = disks.Read();
                        sample.CpuTemperature = HardwareSensorService.PeakTemperature("Cpu");
                        sample.RamTemperature = HardwareSensorService.PeakTemperature("Memory");
                        sample.BoardTemperature = HardwareSensorService.PeakTemperature("Motherboard");
                        ReadDiskSpace(sample);
                        sample.Timestamp = now;
                    }
                    catch (Exception)
                    { /* OS counters may disappear during shutdown or device changes. */
                    }
                    latest = sample;
                }
            }
            catch (Exception)
            {
                latest = new Sample();
            }
            finally
            {
                stop.Dispose();
            }
        }

        internal static double? CpuPercent(double cpuSeconds, double elapsed, int processors) =>
            elapsed <= 0 || processors <= 0 || cpuSeconds < 0
                ? null
                : Math.Min(100, 100 * cpuSeconds / (elapsed * processors));

        private void ReadDiskSpace(Sample sample)
        {
            try
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                // Query the actual game directory so mounted volumes and UNC paths resolve correctly.
                if (!GetDiskFreeSpaceExW(gameDirectory, out _, out ulong total, out ulong free))
                    return;
#else
                var drive = new DriveInfo(Path.GetPathRoot(gameDirectory));
                if (!drive.IsReady)
                    return;
                ulong total = (ulong)drive.TotalSize;
                ulong free = (ulong)drive.TotalFreeSpace;
#endif
                if (total == 0 || free > total)
                    return;
                sample.DiskSpaceTotal = total;
                sample.DiskSpaceFree = free;
                sample.DiskSpaceUsed = total - free;
            }
            catch (Exception)
            { /* A disconnected or inaccessible volume must not interrupt other telemetry. */ }
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetDiskFreeSpaceExW(
            string directory, out ulong available, out ulong total, out ulong free);
#endif

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;
            try
            {
                stop.Set();
            }
            catch (ObjectDisposedException) { }
        }
    }
}
