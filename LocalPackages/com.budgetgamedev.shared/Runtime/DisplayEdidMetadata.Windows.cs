#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BudgetGameDev.Shared
{
    public readonly partial struct DisplayEdidMetadata
    {
        private const int DisplayDeviceActive = 0x00000001;
        private const int DisplayDeviceAttachedToDesktop = 0x00000001;
        private const int KeyRead = 0x20019;
        private static readonly IntPtr HkeyLocalMachine = new(unchecked((int)0x80000002));

        private static DisplayEdidMetadata DetectWindows(float preferredPeakNits)
        {
            DisplayEdidMetadata best = Unavailable("Windows did not expose readable EDID data.");
            float bestDistance = float.PositiveInfinity;
            for (uint adapterIndex = 0; ; adapterIndex++)
            {
                DisplayDevice adapter = DisplayDevice.Create();
                if (!EnumDisplayDevices(null, adapterIndex, ref adapter, 0))
                    break;
                if ((adapter.StateFlags & DisplayDeviceAttachedToDesktop) == 0)
                    continue;

                for (uint monitorIndex = 0; ; monitorIndex++)
                {
                    DisplayDevice monitor = DisplayDevice.Create();
                    if (!EnumDisplayDevices(adapter.DeviceName, monitorIndex, ref monitor, 0))
                        break;
                    if ((monitor.StateFlags & DisplayDeviceActive) == 0)
                        continue;
                    if (!TryReadRegistryEdid(monitor.DeviceKey, out byte[] edid))
                        continue;

                    TryParse(edid, out DisplayEdidMetadata candidate);
                    if (!candidate.HasHdrStaticMetadata)
                    {
                        if (!best.HasHdrStaticMetadata)
                            best = candidate;
                        continue;
                    }

                    float distance =
                        preferredPeakNits > 0f && candidate.HasMaximumLuminance
                            ? Mathf.Abs(candidate.MaximumLuminanceNits - preferredPeakNits)
                            : 0f;
                    if (distance < bestDistance)
                    {
                        best = candidate;
                        bestDistance = distance;
                    }
                }
            }
            return best;
        }

        private static bool TryReadRegistryEdid(string deviceKey, out byte[] edid)
        {
            edid = null;
            const string machinePrefix = @"\Registry\Machine\";
            if (
                string.IsNullOrEmpty(deviceKey)
                || !deviceKey.StartsWith(machinePrefix, StringComparison.OrdinalIgnoreCase)
            )
                return false;

            string path = deviceKey.Substring(machinePrefix.Length);
            return TryReadRegistryEdidAtPath(path, out edid)
                || TryReadRegistryEdidAtPath(path + @"\Device Parameters", out edid);
        }

        private static bool TryReadRegistryEdidAtPath(string path, out byte[] edid)
        {
            edid = null;
            if (RegOpenKeyEx(HkeyLocalMachine, path, 0, KeyRead, out IntPtr key) != 0)
                return false;
            try
            {
                uint size = 0;
                if (
                    RegQueryValueEx(key, "EDID", IntPtr.Zero, out _, null, ref size) != 0
                    || size < 128
                )
                    return false;
                byte[] buffer = new byte[size];
                if (RegQueryValueEx(key, "EDID", IntPtr.Zero, out _, buffer, ref size) != 0)
                    return false;
                if (size != buffer.Length)
                    Array.Resize(ref buffer, (int)size);
                edid = buffer;
                return true;
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DisplayDevice
        {
            public int Cb;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceId;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;

            public static DisplayDevice Create() => new() { Cb = Marshal.SizeOf<DisplayDevice>() };
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayDevices(
            string device,
            uint deviceIndex,
            ref DisplayDevice displayDevice,
            uint flags
        );

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegOpenKeyEx(
            IntPtr key,
            string subKey,
            uint options,
            int desiredAccess,
            out IntPtr result
        );

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegQueryValueEx(
            IntPtr key,
            string valueName,
            IntPtr reserved,
            out uint type,
            byte[] data,
            ref uint dataSize
        );

        [DllImport("advapi32.dll")]
        private static extern int RegCloseKey(IntPtr key);
    }
}
#endif
