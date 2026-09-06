using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BudgetGameDev.Shared
{
    /// <summary>StorageDeviceTemperatureProperty; query access only, never opens a disk for writing.</summary>
    internal sealed class DiskTemperatureSensors : IDisposable
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private readonly List<SafeFileHandle> drives = new();
        private readonly byte[] request = { 52, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private readonly byte[] response = new byte[1024];
        private double? lastTemperature;
        private int untilRefresh;

        internal DiskTemperatureSensors()
        {
            for (int index = 0; index < 32; index++)
            {
                var handle = CreateFileW(
                    $@"\\.\PhysicalDrive{index}",
                    0,
                    3,
                    IntPtr.Zero,
                    3,
                    0,
                    IntPtr.Zero
                );
                if (!handle.IsInvalid && Read(handle).HasValue)
                    drives.Add(handle);
                else
                    handle.Dispose();
            }
        }

        internal double? Read()
        {
            if (untilRefresh-- > 0)
                return lastTemperature;
            untilRefresh = 4;
            lastTemperature = null;
            foreach (var drive in drives)
            {
                double? temperature = Read(drive);
                if (temperature.HasValue)
                    lastTemperature = !lastTemperature.HasValue
                        ? temperature
                        : Math.Max(lastTemperature.Value, temperature.Value);
            }
            return lastTemperature;
        }

        private double? Read(SafeFileHandle drive)
        {
            if (
                !DeviceIoControl(
                    drive,
                    0x2D1400,
                    request,
                    (uint)request.Length,
                    response,
                    (uint)response.Length,
                    out uint length,
                    IntPtr.Zero
                )
                || length < 40
            )
                return null;
            int count = BitConverter.ToUInt16(response, 12);
            double? hottest = null;
            for (int i = 0; i < count && 24 + (i + 1) * 16 <= length; i++)
            {
                int offset = 24 + i * 16;
                short temperature = BitConverter.ToInt16(response, offset + 2);
                if (temperature == short.MinValue || temperature < -50 || temperature > 200)
                    continue;
                if (BitConverter.ToUInt16(response, offset) == 0)
                    return temperature; // Composite temperature.
                hottest = !hottest.HasValue ? temperature : Math.Max(hottest.Value, temperature);
            }
            return hottest;
        }

        public void Dispose()
        {
            foreach (var drive in drives)
                drive.Dispose();
            drives.Clear();
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern SafeFileHandle CreateFileW(
            string path,
            uint access,
            uint share,
            IntPtr security,
            uint creation,
            uint flags,
            IntPtr template
        );

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(
            SafeFileHandle device,
            uint code,
            byte[] input,
            uint inputSize,
            byte[] output,
            uint outputSize,
            out uint returned,
            IntPtr overlapped
        );
#else
        internal double? Read() => null;

        public void Dispose() { }
#endif
    }
}
