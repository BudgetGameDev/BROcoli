using System;
using System.Runtime.InteropServices;
using System.Text;

namespace BudgetGameDev.Shared
{
    /// <summary>Optional read-only driver telemetry; independent of DLSS, FG and Reflex.</summary>
    internal sealed class NvidiaResourceSensors : IDisposable
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private bool initialized;
        private IntPtr device;

        internal NvidiaResourceSensors(string graphicsName)
        {
            try
            {
                initialized = nvmlInit_v2() == 0;
                if (!initialized || nvmlDeviceGetCount_v2(out uint count) != 0)
                    return;
                int matches = 0;
                for (uint i = 0; i < count; i++)
                {
                    if (nvmlDeviceGetHandleByIndex_v2(i, out IntPtr candidate) != 0)
                        continue;
                    var name = new StringBuilder(128);
                    if (nvmlDeviceGetName(candidate, name, 128) != 0)
                        continue;
                    if (
                        string.Equals(
                            name.ToString(),
                            graphicsName,
                            StringComparison.OrdinalIgnoreCase
                        ) || (string.IsNullOrEmpty(graphicsName) && count == 1)
                    )
                    {
                        device = candidate;
                        matches++;
                    }
                }
                // Do not attach another adapter's capacity/temperature to the game's VRAM.
                if (matches != 1)
                    device = IntPtr.Zero;
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        internal void Read(PerformanceResources.Sample sample)
        {
            if (device == IntPtr.Zero)
                return;
            if (nvmlDeviceGetMemoryInfo(device, out Memory memory) == 0 && memory.Total > 0)
            {
                sample.VideoTotal = memory.Total;
                sample.VideoAvailable = memory.Free;
                sample.SystemVideoMemory = memory.Used;
            }
            if (nvmlDeviceGetTemperature(device, 0, out uint temperature) == 0 && temperature < 200)
                sample.GpuTemperature = temperature;
            try
            {
                if (nvmlDeviceGetClockInfo(device, 0, out uint core) == 0 && core > 0) sample.GpuClock = core;
                if (nvmlDeviceGetClockInfo(device, 2, out uint memoryClock) == 0 && memoryClock > 0) sample.GpuMemoryClock = memoryClock;
                if (nvmlDeviceGetMaxClockInfo(device, 0, out uint maximum) == 0 && maximum > 0) sample.GpuMaxClock = maximum;
                if (nvmlDeviceGetCurrentClocksThrottleReasons(device, out ulong reasons) == 0) sample.GpuClockReasons = reasons;
            }
            catch (EntryPointNotFoundException) { /* Optional APIs must not interrupt other resource sampling. */ }
        }

        public void Dispose()
        {
            if (initialized)
                nvmlShutdown();
            initialized = false;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Memory
        {
            public ulong Total,
                Free,
                Used;
        }

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlInit_v2();

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlShutdown();

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetCount_v2(out uint count);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int nvmlDeviceGetName(IntPtr device, StringBuilder name, uint length);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetMemoryInfo(IntPtr device, out Memory memory);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetClockInfo(IntPtr device, uint type, out uint clock);
        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetMaxClockInfo(IntPtr device, uint type, out uint clock);
        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetCurrentClocksThrottleReasons(IntPtr device, out ulong reasons);

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvmlDeviceGetTemperature(
            IntPtr device,
            uint sensor,
            out uint temperature
        );
#else
        internal NvidiaResourceSensors(string graphicsName) { }

        internal void Read(PerformanceResources.Sample sample) { }

        public void Dispose() { }
#endif
    }
}
