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
            float peakNits,
            SystemHdrState systemState = SystemHdrState.Unknown
        )
        {
            if (!nativeHdrPlayer)
                return "NATIVE HDR • WINDOWS / MACOS BUILDS ONLY";
            string system = windows ? "WINDOWS" : "SYSTEM";
            if (!enabled)
            {
                if (!active)
                    return systemState == SystemHdrState.Disabled
                        ? $"FOLLOWS {system} • HDR IS OFF IN {system} DISPLAY SETTINGS"
                        : "NATIVE HDR OUTPUT DISABLED";
                return canSwitch
                    ? "SWITCHING TO SDR…"
                    : "HDR ACTIVE • PLATFORM DOES NOT SUPPORT LIVE SWITCHING";
            }
            if (!available && !active)
                return systemState == SystemHdrState.Enabled
                    ? $"FOLLOWS {system} • HDR IS ON BUT NO HDR OUTPUT WAS DETECTED"
                    : $"ENABLE HDR IN {system} DISPLAY SETTINGS";
            if (!active)
                return "SWITCHING TO NATIVE HDR…";

            string output =
                windows && tenBit ? "10-BIT HDR10 ACTIVE"
                : macOS && tenBit ? "10-BIT METAL HDR ACTIVE"
                : macOS ? "NATIVE METAL HDR ACTIVE"
                : $"HDR ACTIVE • {format}";
            string source = systemDefaults ? "SYSTEM DISPLAY PROFILE" : "IN-GAME CALIBRATION";
            string prefix = systemState == SystemHdrState.Enabled ? $"FOLLOWS {system} • " : "";
            return $"{prefix}{output} • {Mathf.RoundToInt(peakNits)} NITS • {source}";
        }
    }
}
