using System;
using System.Collections.Generic;
using System.Text;

namespace BudgetGameDev.Shared
{
    internal sealed class ClockReadiness
    {
        private sealed class Values
        {
            internal int Count; internal double Sum, Min = double.MaxValue, Max;
            internal void Add(double? value)
            {
                if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value < 0) return;
                Count++; Sum += value.Value; Min = Math.Min(Min, value.Value); Max = Math.Max(Max, value.Value);
            }
            internal string Describe(string unit) => Count == 0 ? "Unavailable" : $"{Sum / Count:F1} {unit} average; {Min:F1}–{Max:F1} {unit} range ({Count} samples)";
        }
        private readonly Values cpu = new(), cpuBoost = new(), gpu = new(), video = new(), ram = new(), configured = new(), rate = new(),
            read = new(), write = new(), latency = new();
        private int cpuBusy, cpuLow, gpuBusy, gpuLow, thermal, reasonSamples, diskSlow, diskActive;
        private ulong reasons;
        private bool ramLower;
        private readonly HashSet<string> memoryDetails = new();
        internal string ConfigurationSummary => ramLower
            ? $"<color={PerformanceTint.Warning}><b>SUBOPTIMAL CONFIGURATION · RAM speed</b></color>\nMemory is configured below its reported capability. See MEMORY CLOCK / CONFIGURATION for rates and XMP/EXPO guidance.\n\n" : "";
        private string cpuSource = "Unavailable";
        private string cpuBoostSource = "Unavailable";

        internal static void ReadSensorClocks(HardwareSensorService.Snapshot snapshot, PerformanceResources.Sample sample, string gpuName = null)
        {
            if (snapshot == null || !snapshot.Fresh) return;
            double coreSum = 0; int cores = 0;
            double? corePeak = null;
            double? average = null, effective = null;
            sample.RamConfiguration = MemoryConfiguration.Assess(snapshot);
            sample.RamUnderConfigured = sample.RamConfiguration.Suboptimal;
            foreach (var r in snapshot.readings)
            {
                if (!Valid(r)) continue;
                if (r.category == "Cpu" && r.type == "Clock" && r.unit == "MHz")
                {
                    if (r.name == "Cores (Average Effective)") effective = r.value;
                    else if (r.name == "Cores (Average)") average = r.value;
                    else if ((r.name.StartsWith("Core #") || r.name.StartsWith("CPU Core #")) && !r.name.Contains("Effective"))
                    { coreSum += r.value; cores++; corePeak = corePeak.HasValue ? Math.Max(corePeak.Value, r.value) : r.value; }
                }
                if (r.category == "Memory")
                {
                    if (r.name == "Configured DDR clock") sample.RamConfiguredClock = Minimum(sample.RamConfiguredClock, r.value);
                    else if (r.type == "Clock" && r.unit == "MHz") sample.RamClock = Minimum(sample.RamClock, r.value);
                    if (r.name == "Configured DDR rate")
                    {
                        sample.RamConfiguredRate = Minimum(sample.RamConfiguredRate, r.value);
                    }
                    if (r.name == "Firmware DDR capability") sample.RamCapabilityRate = Minimum(sample.RamCapabilityRate, r.value);
                }
                if (!string.IsNullOrEmpty(gpuName) && string.Equals(r.hardware, gpuName, StringComparison.OrdinalIgnoreCase) && r.type == "Clock" && r.unit == "MHz")
                {
                    if (r.name == "GPU Core") sample.GpuClock ??= r.value;
                    if (r.name == "GPU Memory") sample.GpuMemoryClock ??= r.value;
                }
            }
            if (effective.HasValue) { sample.CpuClock = effective; sample.CpuClockSource = "LHM effective core average"; }
            else if (average.HasValue || cores > 0) { sample.CpuClock = average ?? coreSum / cores; sample.CpuClockSource = "LHM core clock average"; }
            if (corePeak.HasValue) { sample.CpuBoostClock = corePeak; sample.CpuBoostSource = "Core sensor"; }
        }
        private static bool Valid(HardwareSensorService.Reading r) => r != null && r.available && r.name != null && r.value > 0 && !float.IsInfinity(r.value) && !float.IsNaN(r.value);
        private static double Minimum(double? current, double value) => current.HasValue ? Math.Min(current.Value, value) : value;

        internal void Add(PerformanceResources.Sample s)
        {
            if (s == null || !s.Fresh) return;
            cpu.Add(s.CpuClock); gpu.Add(s.GpuClock); video.Add(s.GpuMemoryClock); ram.Add(s.RamClock);
            cpuBoost.Add(s.CpuBoostClock);
            if (s.CpuBoostClock.HasValue) cpuBoostSource = s.CpuBoostSource;
            configured.Add(s.RamConfiguredClock); rate.Add(s.RamConfiguredRate); ramLower |= s.RamUnderConfigured;
            if (s.RamConfiguration != null) memoryDetails.Add(s.RamConfiguration.Details);
            if (s.CpuClock.HasValue) cpuSource = s.CpuClockSource;
            if (s.SystemCpu >= 80 && s.CpuClock > 0 && s.CpuReferenceClock > 0)
            {
                cpuBusy++;
                if (s.CpuClock < s.CpuReferenceClock * .5 || (s.CpuClockLimit > 0 && s.CpuClockLimit < s.CpuReferenceClock * .8)) cpuLow++;
            }
            if (s.Gpu >= 90 && s.GpuClock > 0 && s.GpuMaxClock > 0)
            {
                gpuBusy++;
                if (s.GpuClock < s.GpuMaxClock * .5) gpuLow++;
            }
            if (s.GpuClockReasons.HasValue)
            {
                reasonSamples++; reasons |= s.GpuClockReasons.Value;
                if ((s.GpuClockReasons.Value & 0x60) != 0) thermal++;
            }
            read.Add(s.DiskRead / 1048576); write.Add(s.DiskWrite / 1048576);
            if (s.DiskTransfers >= 1 && s.DiskLatencyMs.HasValue)
            {
                diskActive++; latency.Add(s.DiskLatencyMs);
                if (s.DiskTransfers >= 10 && s.DiskLatencyMs >= 20) diskSlow++;
            }
        }

        internal string Report(double fps)
        {
            var text = new StringBuilder();
            string cpuStatus = cpu.Count == 0 ? "NOT MEASURED" : fps < 60 && cpuLow >= 3 ? "CAUTION" : cpuBusy >= 3 ? "NOMINAL" : "OBSERVED";
            Row(text, "CPU CLOCK", cpuStatus, cpu.Describe("MHz") + ". Source: " + cpuSource
                + ".\nFastest-core frequency (includes boost): " + cpuBoost.Describe("MHz") + ". Source: " + cpuBoostSource
                + ". This is observed/estimated frequency, not the advertised maximum boost rating.",
                cpuStatus == "CAUTION" ? "Low reported frequency/clock limit coincided with high CPU load and low FPS. Check power mode, cooling and background load; confirm with effective-clock telemetry."
                : "Windows values can reflect nominal/requested clocks rather than effective hardware frequency. Low-load clocks do not diagnose throttling; nominal is limited to the sampled load.");
            string gpuStatus = gpu.Count == 0 ? "NOT MEASURED" : thermal > 0 || (fps < 60 && gpuLow >= 3) ? "CAUTION" : gpuBusy >= 3 ? "NOMINAL" : "OBSERVED";
            Row(text, "GPU / VRAM CLOCKS", gpuStatus,
                "Core: " + gpu.Describe("MHz") + ". Memory: " + video.Describe("MHz") + $".\nClock-limiting reasons: {(reasonSamples == 0 ? "Unavailable" : "0x" + reasons.ToString("X"))}; thermal-limiting samples: {thermal}.",
                gpuStatus == "CAUTION" ? "Driver thermal limiting or low clocks under heavy load were observed. Check cooling and power configuration; lower rendering load and repeat. This is not proof of a defective GPU."
                : "Driver-reported clocks, not memory transfer rate. Idle/power-cap clock reasons can be normal. Reported maximum clocks are not guaranteed sustained clocks.");
            Row(text, "MEMORY CLOCK / CONFIGURATION", rate.Count == 0 && ram.Count == 0 ? "NOT MEASURED" : ramLower ? "SUBOPTIMAL CONFIGURATION" : "OBSERVED",
                "Live clock: " + ram.Describe("MHz") + ".\nConfigured DDR clock: " + configured.Describe("MHz") + "; configured rate: " + rate.Describe("MT/s") + ".\n"
                    + (memoryDetails.Count > 0 ? string.Join("\n", memoryDetails) + "\n" : "") + MemoryConfiguration.Limitations,
                ramLower ? MemoryConfiguration.Advice : "No shortfall was established against the available firmware values. If the kit is advertised for a higher rate, check its specification and supported XMP/EXPO profile in UEFI.");
            Row(text, "DISK TRANSFER SPEED / LATENCY", read.Count == 0 && write.Count == 0 ? "NOT MEASURED" : diskActive < 3 ? "NOT EXERCISED" : diskSlow >= 3 && fps < 60 ? "CAUTION" : "OBSERVED",
                "Read: " + read.Describe("MiB/s") + ".\nWrite: " + write.Describe("MiB/s") + ".\nActive-I/O latency: " + latency.Describe("ms") + ".",
                diskSlow >= 3 && fps < 60 ? "Repeated >=20 ms transfers at >=10 IOPS coincided with low FPS. Pause background transfers and compare. These system-wide counters do not isolate the game's drive or prove a failing disk; check SMART separately."
                : "All physical disks combined; background I/O is included. Zero/low MiB/s can mean no demand. This gameplay run does not measure maximum sequential disk speed or certify disk health.");
            return text.ToString();
        }

        private static void Row(StringBuilder text, string name, string status, string values, string advice)
        {
            string color = status == "CAUTION" || status == "SUBOPTIMAL CONFIGURATION" ? PerformanceTint.Warning : status == "NOMINAL" ? PerformanceTint.Good : status == "OBSERVED" ? PerformanceTint.Neutral : PerformanceTint.Unknown;
            text.Append($"<b>{name} · <color={color}>{status}</color></b>\n{values}\n{advice}\n\n");
        }
    }
}
