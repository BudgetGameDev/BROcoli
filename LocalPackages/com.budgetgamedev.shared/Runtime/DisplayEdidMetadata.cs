using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// Raw luminance capabilities advertised by a monitor's CTA-861 HDR static metadata block.
    /// This is deliberately separate from the OS HDR profile exposed by HDROutputSettings.
    /// </summary>
    public readonly struct DisplayEdidMetadata
    {
        private const int EdidBlockSize = 128;

        private DisplayEdidMetadata(
            string displayName,
            bool hasHdrStaticMetadata,
            bool hasMinimumLuminance,
            float minimumLuminanceNits,
            bool hasMaximumLuminance,
            float maximumLuminanceNits,
            bool hasMaximumFullFrameLuminance,
            float maximumFullFrameLuminanceNits,
            string status
        )
        {
            DisplayName = displayName;
            HasHdrStaticMetadata = hasHdrStaticMetadata;
            HasMinimumLuminance = hasMinimumLuminance;
            MinimumLuminanceNits = minimumLuminanceNits;
            HasMaximumLuminance = hasMaximumLuminance;
            MaximumLuminanceNits = maximumLuminanceNits;
            HasMaximumFullFrameLuminance = hasMaximumFullFrameLuminance;
            MaximumFullFrameLuminanceNits = maximumFullFrameLuminanceNits;
            Status = status;
        }

        public string DisplayName { get; }
        public bool HasHdrStaticMetadata { get; }
        public bool HasMinimumLuminance { get; }
        public float MinimumLuminanceNits { get; }
        public bool HasMaximumLuminance { get; }
        public float MaximumLuminanceNits { get; }
        public bool HasMaximumFullFrameLuminance { get; }
        public float MaximumFullFrameLuminanceNits { get; }
        public string Status { get; }

        public static DisplayEdidMetadata Detect(float preferredPeakNits = 0f)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (
                Application.platform != RuntimePlatform.WindowsPlayer
                && Application.platform != RuntimePlatform.WindowsEditor
            )
                return Unavailable("Raw EDID luminance is available in Windows builds.");
            return DetectWindows(preferredPeakNits);
#else
            return Unavailable("Raw EDID luminance is available in Windows builds.");
#endif
        }

        internal static bool TryParse(byte[] edid, out DisplayEdidMetadata metadata)
        {
            metadata = Unavailable("No CTA-861 HDR static metadata was found.");
            if (!HasValidBaseBlock(edid))
                return false;

            string name = ReadDisplayName(edid);
            int extensionCount = Mathf.Min(edid[126], (edid.Length / EdidBlockSize) - 1);
            for (int extension = 0; extension < extensionCount; extension++)
            {
                int offset = (extension + 1) * EdidBlockSize;
                if (edid[offset] != 0x02)
                    continue;

                int dataEnd = edid[offset + 2];
                dataEnd = dataEnd == 0 ? 4 : Mathf.Clamp(dataEnd, 4, 127);
                for (int cursor = 4; cursor < dataEnd;)
                {
                    byte header = edid[offset + cursor];
                    int length = header & 0x1f;
                    int next = cursor + length + 1;
                    if (next > dataEnd || offset + next > edid.Length)
                        break;

                    int tag = header >> 5;
                    if (tag == 7 && length >= 3 && edid[offset + cursor + 1] == 0x06)
                    {
                        int luminance = offset + cursor + 4;
                        bool hasMaximum = length >= 4 && edid[luminance] != 0;
                        float maximum = hasMaximum
                            ? DecodeLuminance(edid[luminance])
                            : 0f;
                        bool hasFullFrame = length >= 5 && edid[luminance + 1] != 0;
                        float fullFrame = hasFullFrame
                            ? DecodeLuminance(edid[luminance + 1])
                            : 0f;
                        bool hasMinimum = length >= 6 && edid[luminance + 2] != 0 && hasMaximum;
                        float minimum = hasMinimum
                            ? maximum
                                * Mathf.Pow(edid[luminance + 2] / 255f, 2f)
                                / 100f
                            : 0f;
                        metadata = new DisplayEdidMetadata(
                            name,
                            true,
                            hasMinimum,
                            minimum,
                            hasMaximum,
                            maximum,
                            hasFullFrame,
                            fullFrame,
                            hasMaximum
                                ? "Read from the monitor's CTA-861 HDR metadata."
                                : "HDR metadata is present, but luminance values are not reported."
                        );
                        return true;
                    }

                    cursor = next;
                }
            }

            metadata = Unavailable("No CTA-861 HDR static metadata was found.", name);
            return false;
        }

        private static bool HasValidBaseBlock(byte[] edid)
        {
            if (edid == null || edid.Length < EdidBlockSize)
                return false;
            byte[] header = { 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x00 };
            for (int index = 0; index < header.Length; index++)
                if (edid[index] != header[index])
                    return false;
            return true;
        }

        private static float DecodeLuminance(byte codeValue) =>
            50f * Mathf.Pow(2f, codeValue / 32f);

        private static string ReadDisplayName(byte[] edid)
        {
            for (int offset = 54; offset + 17 < EdidBlockSize; offset += 18)
            {
                if (
                    edid[offset] != 0
                    || edid[offset + 1] != 0
                    || edid[offset + 2] != 0
                    || edid[offset + 3] != 0xfc
                )
                    continue;

                string name = Encoding.ASCII.GetString(edid, offset + 5, 13)
                    .Trim('\0', '\n', '\r', ' ');
                if (!string.IsNullOrEmpty(name))
                    return name;
            }

            int manufacturerCode = (edid[8] << 8) | edid[9];
            char first = (char)('A' + ((manufacturerCode >> 10) & 0x1f) - 1);
            char second = (char)('A' + ((manufacturerCode >> 5) & 0x1f) - 1);
            char third = (char)('A' + (manufacturerCode & 0x1f) - 1);
            int productCode = edid[10] | (edid[11] << 8);
            return $"{first}{second}{third}-{productCode:X4}";
        }

        private static DisplayEdidMetadata Unavailable(string status, string displayName = "") =>
            new(
                displayName,
                false,
                false,
                0f,
                false,
                0f,
                false,
                0f,
                status
            );

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
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

                    float distance = preferredPeakNits > 0f && candidate.HasMaximumLuminance
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
            if (string.IsNullOrEmpty(deviceKey) || !deviceKey.StartsWith(machinePrefix, StringComparison.OrdinalIgnoreCase))
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
                if (RegQueryValueEx(key, "EDID", IntPtr.Zero, out _, null, ref size) != 0 || size < 128)
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
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;

            public static DisplayDevice Create() => new()
            {
                Cb = Marshal.SizeOf<DisplayDevice>(),
            };
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
#endif
    }
}
