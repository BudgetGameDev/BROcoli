using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace BudgetGameDev.Shared
{
    internal sealed class WindowsPerformanceCounters : IDisposable
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private IntPtr query;
        private Counter gpu,
            videoMemory,
            diskIdle,
            diskRead,
            diskWrite, diskLatency, diskTransfers;
        private Counter cpuFrequency, cpuPerformance;
        private ulong lastIdle,
            lastKernel,
            lastUser;
        private bool haveCpu;

        internal WindowsPerformanceCounters(int processId)
        {
            haveCpu = GetSystemTimes(out lastIdle, out lastKernel, out lastUser);
            if (PdhOpenQueryW(null, UIntPtr.Zero, out query) != 0)
                return;
            gpu = new Counter(query, $@"\GPU Engine(pid_{processId}_*)\Utilization Percentage");
            videoMemory = new Counter(
                query,
                $@"\GPU Process Memory(pid_{processId}_*)\Dedicated Usage"
            );
            diskIdle = new Counter(query, @"\PhysicalDisk(*)\% Idle Time");
            diskRead = new Counter(query, @"\PhysicalDisk(_Total)\Disk Read Bytes/sec");
            diskWrite = new Counter(query, @"\PhysicalDisk(_Total)\Disk Write Bytes/sec");
            diskLatency = new Counter(query, @"\PhysicalDisk(_Total)\Avg. Disk sec/Transfer");
            diskTransfers = new Counter(query, @"\PhysicalDisk(_Total)\Disk Transfers/sec");
            cpuFrequency = new Counter(query, @"\Processor Information(*)\Processor Frequency");
            cpuPerformance = new Counter(query, @"\Processor Information(*)\% Processor Performance", true);
            PdhCollectQueryData(query); // Rate counters need two samples.
        }

        internal void Read(PerformanceResources.Sample sample)
        {
            ReadCpuClock(sample);
            var resident = new ProcessMemory { Size = (uint)Marshal.SizeOf<ProcessMemory>() };
            if (GetProcessMemoryInfo(new IntPtr(-1), ref resident, resident.Size))
                sample.GameRam = resident.WorkingSet.ToUInt64();
            if (GetSystemTimes(out ulong idle, out ulong kernel, out ulong user))
            {
                if (haveCpu && kernel >= lastKernel && user >= lastUser && idle >= lastIdle)
                {
                    ulong total = kernel - lastKernel + user - lastUser;
                    if (total > 0)
                        sample.SystemCpu = Clamp(100 * (1d - (idle - lastIdle) / (double)total));
                }
                lastIdle = idle;
                lastKernel = kernel;
                lastUser = user;
                haveCpu = true;
            }
            var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
            if (GlobalMemoryStatusEx(ref memory))
            {
                sample.RamTotal = memory.TotalPhysical;
                sample.RamUsed = memory.TotalPhysical - memory.AvailablePhysical;
            }
            if (query == IntPtr.Zero || PdhCollectQueryData(query) != 0)
                return;
            // Task Manager's per-process convention: the busiest engine, not a sum across engines.
            sample.Gpu = gpu.Read(false, false);
            if (sample.Gpu.HasValue)
                sample.Gpu = Clamp(sample.Gpu.Value);
            sample.VideoMemory = videoMemory.Read(true, false);
            sample.DiskBusy = diskIdle.Read(false, true);
            sample.DiskRead = diskRead.Read(true, false);
            sample.DiskWrite = diskWrite.Read(true, false);
            sample.DiskTransfers = diskTransfers.Read(true, false);
            if (sample.DiskTransfers > 0) sample.DiskLatencyMs = diskLatency.Read(false, false) * 1000;
            var frequencies = new Dictionary<string, double>();
            var performance = new Dictionary<string, double>();
            cpuFrequency.Read(false, false, frequencies);
            cpuPerformance.Read(false, false, performance);
            sample.CpuBoostClock = CpuBoostFrequency.EstimatePeak(frequencies, performance);
            if (sample.CpuBoostClock.HasValue) sample.CpuBoostSource = "Windows estimate";
        }

        private static void ReadCpuClock(PerformanceResources.Sample sample)
        {
            var values = new ProcessorPower[Math.Max(1, Environment.ProcessorCount)];
            if (CallNtPowerInformation(11, IntPtr.Zero, 0, values, (uint)(values.Length * Marshal.SizeOf<ProcessorPower>())) != 0) return;
            double sum = 0, reference = 0, limit = 0; int count = 0;
            foreach (var value in values)
            {
                if (value.CurrentMhz == 0 || value.CurrentMhz > 20000 || value.MaxMhz == 0) continue;
                sum += value.CurrentMhz; reference += value.MaxMhz; limit += value.MhzLimit; count++;
            }
            if (count == 0) return;
            sample.CpuClock = sum / count;
            sample.CpuReferenceClock = reference / count;
            sample.CpuClockLimit = limit / count;
            sample.CpuClockSource = "Windows reported (not effective clock)";
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessorPower { public uint Number, MaxMhz, CurrentMhz, MhzLimit, MaxIdle, CurrentIdle; }
        [DllImport("powrprof.dll")]
        private static extern uint CallNtPowerInformation(int level, IntPtr input, uint inputSize, [Out] ProcessorPower[] output, uint outputSize);

        private static double Clamp(double value) => Math.Max(0, Math.Min(100, value));

        // PDH returns localized instance names; English counter paths are resolved by this API.
        private sealed class Counter : IDisposable
        {
            private IntPtr handle,
                buffer;
            private uint capacity;
            private readonly uint format;

            internal Counter(IntPtr query, string path, bool allowOver100 = false)
            {
                format = 0x200u | (allowOver100 ? 0x8000u : 0); // PDH_FMT_DOUBLE | PDH_FMT_NOCAP100
                if (PdhAddEnglishCounterW(query, path, UIntPtr.Zero, out handle) != 0)
                    handle = IntPtr.Zero;
            }

            internal double? Read(bool sum, bool idle, Dictionary<string, double> instances = null)
            {
                if (handle == IntPtr.Zero)
                    return null;
                uint size = capacity,
                    count = 0;
                uint result = PdhGetFormattedCounterArrayW(
                    handle,
                    format,
                    ref size,
                    ref count,
                    buffer
                );
                if (result == 0x800007D2) // PDH_MORE_DATA: wildcard instances can change.
                {
                    if (buffer != IntPtr.Zero)
                        Marshal.FreeHGlobal(buffer);
                    buffer = IntPtr.Zero;
                    capacity = 0;
                    if (size == 0 || size > 16 * 1024 * 1024)
                        return null;
                    buffer = Marshal.AllocHGlobal((int)size);
                    capacity = size;
                    result = PdhGetFormattedCounterArrayW(
                        handle,
                        format,
                        ref size,
                        ref count,
                        buffer
                    );
                }
                if (result != 0)
                    return null;
                double? value = null;
                int stride = Marshal.SizeOf<CounterItem>();
                for (int i = 0; i < count; i++)
                {
                    var item = Marshal.PtrToStructure<CounterItem>(IntPtr.Add(buffer, i * stride));
                    if (
                        item.Value.Status > 1
                        || double.IsNaN(item.Value.Value)
                        || double.IsInfinity(item.Value.Value)
                    )
                        continue;
                    if (idle && Marshal.PtrToStringUni(item.Name) == "_Total")
                        continue;
                    double current = idle
                        ? Clamp(100 - item.Value.Value)
                        : Math.Max(0, item.Value.Value);
                    if (instances != null)
                    {
                        string name = Marshal.PtrToStringUni(item.Name);
                        if (name != null) instances[name] = current;
                    }
                    value =
                        !value.HasValue ? current
                        : sum ? value.Value + current
                        : Math.Max(value.Value, current);
                }
                return value;
            }

            public void Dispose()
            {
                if (buffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct CounterValue
        {
            [FieldOffset(0)]
            public uint Status;

            [FieldOffset(8)]
            public double Value;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CounterItem
        {
            public IntPtr Name;
            public CounterValue Value;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public uint Length,
                MemoryLoad;
            public ulong TotalPhysical,
                AvailablePhysical,
                TotalPageFile,
                AvailablePageFile,
                TotalVirtual,
                AvailableVirtual,
                AvailableExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessMemory
        {
            public uint Size,
                PageFaultCount;
            public UIntPtr PeakWorkingSet,
                WorkingSet,
                PeakPagedPool,
                PagedPool,
                PeakNonPagedPool,
                NonPagedPool,
                PageFile,
                PeakPageFile,
                PrivateUsage;
        }

        [DllImport("psapi.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessMemoryInfo(
            IntPtr process,
            ref ProcessMemory memory,
            uint size
        );

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint PdhOpenQueryW(string source, UIntPtr userData, out IntPtr query);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint PdhAddEnglishCounterW(
            IntPtr query,
            string path,
            UIntPtr userData,
            out IntPtr counter
        );

        [DllImport("pdh.dll", ExactSpelling = true)]
        private static extern uint PdhCollectQueryData(IntPtr query);

        [DllImport("pdh.dll", ExactSpelling = true)]
        private static extern uint PdhCloseQuery(IntPtr query);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint PdhGetFormattedCounterArrayW(
            IntPtr counter,
            uint format,
            ref uint size,
            ref uint count,
            IntPtr items
        );

        public void Dispose()
        {
            gpu?.Dispose();
            videoMemory?.Dispose();
            diskIdle?.Dispose();
            diskRead?.Dispose();
            diskWrite?.Dispose();
            diskLatency?.Dispose();
            diskTransfers?.Dispose();
            cpuFrequency?.Dispose();
            cpuPerformance?.Dispose();
            if (query != IntPtr.Zero)
                PdhCloseQuery(query);
            query = IntPtr.Zero;
        }
#else
        internal WindowsPerformanceCounters(int processId) { }

        internal void Read(PerformanceResources.Sample sample) { }

        public void Dispose() { }
#endif
    }
}
