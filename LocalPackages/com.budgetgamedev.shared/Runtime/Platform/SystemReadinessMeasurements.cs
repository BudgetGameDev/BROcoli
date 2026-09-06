using System;
using System.Collections.Generic;
using System.Text;

namespace BudgetGameDev.Shared
{
    /// <summary>Whole-run frame statistics and independent, once-per-sample resource evidence.</summary>
    public sealed class SystemReadinessMeasurements : IDisposable
    {
        private readonly PerformanceResources resources;
        private readonly List<double> frames = new(8192);
        private readonly Metric cpu = new(), gameCpu = new(), gpu = new(), ram = new(),
            vram = new(), disk = new(), space = new(), freeSpace = new(),
            gpuTemp = new(), diskTemp = new(), cpuTemp = new(), ramTemp = new(), boardTemp = new();
        private long lastSample;
        private double seconds;
        private readonly Dictionary<string, DiskSmartReading> smart = new();
        private readonly ClockReadiness clocks = new();
        public int FrameCount => frames.Count;
        public int ResourceSamples => cpu.Count;

        private sealed class Metric
        {
            internal int Count;
            internal double Sum, Peak = double.MinValue, Minimum = double.MaxValue;
            internal double? Mean => Count > 0 ? Sum / Count : null;
            internal double? Maximum => Count > 0 ? Peak : null;
            internal double? Min => Count > 0 ? Minimum : null;
            internal void Add(double? value)
            {
                if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
                    return;
                Count++;
                Sum += value.Value;
                Peak = Math.Max(Peak, value.Value);
                Minimum = Math.Min(Minimum, value.Value);
            }
        }

        public SystemReadinessMeasurements(string gpuName, string gameDirectory)
            : this(gpuName, gameDirectory, true) { }

        internal SystemReadinessMeasurements(string gpuName, string gameDirectory, bool collectHardware) =>
            resources = collectHardware ? new PerformanceResources(gpuName, gameDirectory) : null;

        public void AddFrame(double duration)
        {
            if (duration <= 0 || double.IsNaN(duration) || double.IsInfinity(duration))
                return;
            frames.Add(duration * 1000);
            seconds += duration;
            CaptureSmart(HardwareSensorService.Latest);
            var s = resources?.Latest;
            if (s == null) return;
            if (!s.Fresh || s.Timestamp == lastSample)
                return;
            lastSample = s.Timestamp;
            clocks.Add(s);
            cpu.Add(s.SystemCpu);
            gameCpu.Add(s.GameCpu);
            gpu.Add(s.Gpu);
            ram.Add(Percent(s.RamUsed, s.RamTotal));
            vram.Add(Percent(s.SystemVideoMemory, s.VideoTotal));
            disk.Add(s.DiskBusy);
            space.Add(Percent(s.DiskSpaceUsed, s.DiskSpaceTotal));
            freeSpace.Add(s.DiskSpaceFree / (1024d * 1024 * 1024));
            gpuTemp.Add(s.GpuTemperature);
            diskTemp.Add(s.DiskTemperature);
            cpuTemp.Add(s.CpuTemperature);
            ramTemp.Add(s.RamTemperature);
            boardTemp.Add(s.BoardTemperature);
        }

        private static double? Percent(double? used, double? total) =>
            total > 0 ? used / total * 100 : null;

        internal void CaptureSmart(HardwareSensorService.Snapshot snapshot)
        {
            if (snapshot == null || !snapshot.Fresh) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var reading in snapshot.diskSmart ?? Array.Empty<DiskSmartReading>())
            {
                if (!DiskSmartHealth.Fresh(reading, now) || string.IsNullOrEmpty(reading.id)) continue;
                if (!smart.TryGetValue(reading.id, out var previous)
                    || DiskSmartHealth.Severity(DiskSmartHealth.Assess(reading, now)) >= DiskSmartHealth.Severity(DiskSmartHealth.Assess(previous, previous.sampledAt)))
                    smart[reading.id] = reading;
            }
        }

        internal static string HighStatus(double? value, double caution, double attention) =>
            !value.HasValue ? "NOT MEASURED"
            : value >= attention ? "ATTENTION" : value >= caution ? "CAUTION" : "NOMINAL";

        private static string StatusColor(string status) => status switch
        {
            "NOMINAL" => PerformanceTint.Good,
            "CAUTION" => PerformanceTint.Warning,
            "ATTENTION" => PerformanceTint.Bad,
            _ => PerformanceTint.Unknown,
        };

        private static void Row(StringBuilder text, string name, string status, string evidence, string advice)
        {
            text.Append("<b>").Append(name).Append(" · <color=").Append(StatusColor(status))
                .Append('>').Append(status).Append("</color></b>\n")
                .Append(evidence).Append('\n');
            if (!string.IsNullOrEmpty(advice))
                text.Append(advice).Append('\n');
            text.Append('\n');
        }

        private static string Values(Metric metric, string suffix = "%") => metric.Count == 0
            ? "No supported, fresh sensor readings."
            : $"Average {metric.Mean:F1}{suffix}; peak {metric.Maximum:F1}{suffix} ({metric.Count} samples).";

        public string BuildReport(string settings, double distance)
        {
            if (frames.Count < 30 || seconds < 19)
                return "INCOMPLETE TEST\nNot enough gameplay was measured. No component assessment was made. Run again with the game focused.";
            double[] sorted = frames.ToArray();
            Array.Sort(sorted);
            double fps = frames.Count / seconds;
            double p99 = sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * .99) - 1)];
            int slowCount = Math.Max(1, (int)Math.Ceiling(sorted.Length * .01));
            double slowSum = 0;
            for (int i = sorted.Length - slowCount; i < sorted.Length; i++)
                slowSum += sorted[i];
            double low = 1000 * slowCount / slowSum;
            var text = new StringBuilder();
            text.Append("<b>SYSTEM READINESS RESULTS</b>\n").Append(settings)
                .Append($"\n{seconds:F1} s measured · {frames.Count} rendered frames · {distance:F1} m travelled\n")
                .Append("5 s warm-up excluded. Frame generation is excluded from FPS. Target: 60 rendered FPS.\n\n");
            text.Append(clocks.ConfigurationSummary);
            string renderStatus = fps < 30 || low < 20 ? "ATTENTION" : fps < 60 || low < 45 ? "CAUTION" : "NOMINAL";
            Row(text, "FRAME PACING", renderStatus,
                $"{fps:F1} FPS · {1000 / fps:F1} ms mean · {low:F1} FPS 1% low · {p99:F1} ms P99.",
                renderStatus == "NOMINAL" ? "The measured route met the 60 FPS baseline."
                    : "Lower the quality preset, ray tracing, shadows or resolution; try a faster DLSS mode. Check any configured FPS cap, then repeat.");
            Row(text, "CPU", HighStatus(cpu.Mean, 80, 95),
                Values(cpu) + (gameCpu.Count > 0 ? $" Game average {gameCpu.Mean:F1}%." : " Game CPU unavailable."),
                cpu.Mean >= 80 || cpu.Mean - gameCpu.Mean >= 25
                    ? "Close CPU-heavy background applications and retry. Aggregate utilization cannot rule out a single-thread bottleneck."
                    : "Aggregate utilization cannot rule out a single-thread bottleneck.");
            string gpuStatus = gpu.Count == 0 ? "NOT MEASURED" : fps < 60 && gpu.Mean >= 90 ? "CAUTION" : "NOMINAL";
            Row(text, "GPU LOAD", gpuStatus, Values(gpu),
                gpuStatus == "CAUTION" ? "High GPU load coincides with low FPS. Lower ray tracing, render resolution or GPU-heavy effects."
                : fps < 60 ? "Low FPS was observed; this utilization counter alone does not identify the limiting component. Try lower rendering settings and compare."
                : "High GPU utilization by itself is normal during rendering.");
            Row(text, "SYSTEM RAM", HighStatus(ram.Maximum, 80, 95), Values(ram),
                ram.Maximum >= 80 ? "Close memory-heavy applications. At sustained pressure, lower texture/world detail or consider more system RAM." : "");
            Row(text, "VRAM", HighStatus(vram.Maximum, 80, 95), Values(vram),
                vram.Maximum >= 80 ? "Lower texture quality, ray tracing or render resolution; close other GPU applications. Usage includes the system and driver." : "Usage includes the system and driver.");
            Row(text, "DISK ACTIVITY", HighStatus(disk.Mean, 80, 95), Values(disk),
                disk.Mean >= 80 ? "Pause background downloads, file copies or indexing and rerun. Consider an SSD for persistent loading stalls." : "This is observed activity, not a disk speed or health test.");
            Row(text, "GAME DISK SPACE", HighStatus(space.Maximum, 80, 95),
                Values(space) + (freeSpace.Count > 0 ? $" Minimum free {freeSpace.Min:F1} GiB." : ""),
                space.Maximum >= 80 ? "Free space by removing unneeded files or uninstalling unused games, or move the game to a roomier drive. No files are removed by this test." : "");
            text.Append(DiskSmartHealth.Format(new List<DiskSmartReading>(smart.Values).ToArray(), DateTimeOffset.UtcNow.ToUnixTimeSeconds(), observedDuringTest: true));
            Row(text, "GPU TEMPERATURE", HighStatus(gpuTemp.Maximum, 80, 90), Values(gpuTemp, "°C"),
                gpuTemp.Maximum >= 80 ? "Check airflow and cooling, clean dust with the machine powered down, and lower rendering load. Confirm your GPU's specified limits." : "");
            Row(text, "DISK TEMPERATURE", HighStatus(diskTemp.Maximum, 60, 70), Values(diskTemp, "°C"),
                diskTemp.Maximum >= 60 ? "Check drive airflow/heatsink and reduce sustained transfers. Confirm the drive's specified temperature limits." : "");
            Row(text, "CPU TEMPERATURE", HighStatus(cpuTemp.Maximum, 80, 95), Values(cpuTemp, "°C"),
                cpuTemp.Maximum >= 80 ? "Check CPU cooling and background load. Confirm the CPU's specified limits." : "");
            Row(text, "RAM TEMPERATURE", HighStatus(ramTemp.Maximum, 70, 85), Values(ramTemp, "°C"),
                ramTemp.Maximum >= 70 ? "Check airflow around the memory modules and their specified limits." : "");
            Row(text, "MOTHERBOARD TEMPERATURE", HighStatus(boardTemp.Maximum, 70, 85), Values(boardTemp, "°C"),
                boardTemp.Maximum >= 70 ? "Check motherboard/VRM airflow and the named sensors in the Sensors page." : "");
            text.Append(clocks.Report(fps));
            text.Append("Open SENSORS for every discovered reading and access/driver status. Missing readings are not evidence of nominal temperatures.\n\n");
            text.Append("Nominal means no warning was found in the available data. SMART is a read-only status snapshot, not a drive self-test, stability test or guarantee of hardware health. Recommendations indicate possible remedies, not a proven fault. No benchmark progress was saved.");
            return text.ToString();
        }

        public void Dispose() => resources?.Dispose();
    }
}
