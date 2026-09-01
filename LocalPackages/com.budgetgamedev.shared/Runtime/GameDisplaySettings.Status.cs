using UnityEngine;

namespace BudgetGameDev.Shared
{
    public static partial class GameDisplaySettings
    {
        internal static string ResolveHdrStatus(
            bool nativeHdrPlayer,
            bool enabled,
            bool active,
            bool canSwitch,
            bool available,
            bool windows,
            bool macOS,
            bool tenBit,
            string format,
            bool systemDefaults,
            float peakNits
        )
        {
            if (!nativeHdrPlayer)
                return "NATIVE HDR • WINDOWS / MACOS BUILDS ONLY";
            if (!enabled)
            {
                if (!active)
                    return "NATIVE HDR OUTPUT DISABLED";
                return canSwitch
                    ? "SWITCHING TO SDR…"
                    : "HDR ACTIVE • PLATFORM DOES NOT SUPPORT LIVE SWITCHING";
            }
            if (!available && !active)
                return "ENABLE HDR IN SYSTEM DISPLAY SETTINGS";
            if (!active)
                return "SWITCHING TO NATIVE HDR…";

            string output =
                windows && tenBit ? "10-BIT HDR10 ACTIVE"
                : macOS && tenBit ? "10-BIT METAL HDR ACTIVE"
                : macOS ? "NATIVE METAL HDR ACTIVE"
                : $"HDR ACTIVE • {format}";
            string source = systemDefaults ? "SYSTEM DISPLAY PROFILE" : "IN-GAME CALIBRATION";
            return $"{output} • {Mathf.RoundToInt(peakNits)} NITS • {source}";
        }
    }
}
