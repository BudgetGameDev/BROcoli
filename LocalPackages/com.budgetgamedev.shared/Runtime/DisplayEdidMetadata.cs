using System;
using System.Text;
using UnityEngine;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// Raw luminance capabilities advertised by a monitor's CTA-861 HDR static metadata block.
    /// This is deliberately separate from the OS HDR profile exposed by HDROutputSettings.
    /// </summary>
    public readonly partial struct DisplayEdidMetadata
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
                for (int cursor = 4; cursor < dataEnd; )
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
                        float maximum = hasMaximum ? DecodeLuminance(edid[luminance]) : 0f;
                        bool hasFullFrame = length >= 5 && edid[luminance + 1] != 0;
                        float fullFrame = hasFullFrame ? DecodeLuminance(edid[luminance + 1]) : 0f;
                        bool hasMinimum = length >= 6 && edid[luminance + 2] != 0 && hasMaximum;
                        float minimum = hasMinimum
                            ? maximum * Mathf.Pow(edid[luminance + 2] / 255f, 2f) / 100f
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

                string name = Encoding
                    .ASCII.GetString(edid, offset + 5, 13)
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
            new(displayName, false, false, 0f, false, 0f, false, 0f, status);
    }
}
