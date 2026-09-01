using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// Reads whether Windows has HDR switched on for the display showing the game window.
    /// Unity's HDROutputSettings only reports what the swapchain negotiated, so this asks the
    /// DisplayConfig API directly and lets the game follow the operating-system setting.
    /// </summary>
    internal static partial class WindowsDisplayHdrState
    {
        public static SystemHdrState Query()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (
                Application.platform != RuntimePlatform.WindowsPlayer
                && Application.platform != RuntimePlatform.WindowsEditor
            )
                return SystemHdrState.Unknown;
            try
            {
                return QueryWindows();
            }
            catch (Exception)
            {
                // Missing entry points on unsupported Windows builds leave the game on its
                // own preference rather than crashing the settings menu.
                return SystemHdrState.Unknown;
            }
#else
            return SystemHdrState.Unknown;
#endif
        }

        internal static SystemHdrState ResolveAdvancedColorMode(uint activeColorMode) =>
            activeColorMode == AdvancedColorModeHdr
                ? SystemHdrState.Enabled
                : SystemHdrState.Disabled;

        internal static SystemHdrState ResolveLegacyAdvancedColor(uint flags) =>
            (flags & LegacyAdvancedColorEnabled) != 0
                ? SystemHdrState.Enabled
                : SystemHdrState.Disabled;

        private const uint AdvancedColorModeHdr = 2;
        private const uint LegacyAdvancedColorEnabled = 0x2;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const uint QueryOnlyActivePaths = 0x2;
        private const uint MonitorDefaultToPrimary = 0x1;
        private const uint DeviceInfoGetSourceName = 1;
        private const uint DeviceInfoGetAdvancedColorInfo = 9;
        private const uint DeviceInfoGetAdvancedColorInfo2 = 15;
        private const int ErrorSuccess = 0;
        private const int ErrorInsufficientBuffer = 122;
        private const int QueryAttempts = 3;

        private static IntPtr lastGameWindow;

        private static SystemHdrState QueryWindows()
        {
            if (!TryGetGameMonitorDeviceName(out string deviceName))
                return SystemHdrState.Unknown;

            for (int attempt = 0; attempt < QueryAttempts; attempt++)
            {
                if (
                    GetDisplayConfigBufferSizes(
                        QueryOnlyActivePaths,
                        out uint pathCount,
                        out uint modeCount
                    ) != ErrorSuccess
                )
                    return SystemHdrState.Unknown;

                var paths = new DisplayConfigPathInfo[pathCount];
                var modes = new DisplayConfigModeInfo[modeCount];
                int result = QueryDisplayConfig(
                    QueryOnlyActivePaths,
                    ref pathCount,
                    paths,
                    ref modeCount,
                    modes,
                    IntPtr.Zero
                );
                if (result == ErrorInsufficientBuffer)
                    continue;
                if (result != ErrorSuccess)
                    return SystemHdrState.Unknown;

                for (int index = 0; index < pathCount; index++)
                {
                    if (!MatchesSource(paths[index].SourceInfo, deviceName))
                        continue;
                    return QueryTargetHdrState(paths[index].TargetInfo);
                }
                return SystemHdrState.Unknown;
            }
            return SystemHdrState.Unknown;
        }

        private static bool TryGetGameMonitorDeviceName(out string deviceName)
        {
            deviceName = string.Empty;
            IntPtr window = GetActiveWindow();
            if (window != IntPtr.Zero)
                lastGameWindow = window;
            IntPtr monitor = MonitorFromWindow(lastGameWindow, MonitorDefaultToPrimary);
            if (monitor == IntPtr.Zero)
                return false;

            MonitorInfoEx info = new() { Size = Marshal.SizeOf<MonitorInfoEx>() };
            if (!GetMonitorInfoW(monitor, ref info) || string.IsNullOrEmpty(info.Device))
                return false;
            deviceName = info.Device;
            return true;
        }

        private static bool MatchesSource(DisplayConfigPathSourceInfo source, string deviceName)
        {
            DisplayConfigSourceDeviceName request = new()
            {
                Header = new DisplayConfigDeviceInfoHeader
                {
                    Type = DeviceInfoGetSourceName,
                    Size = (uint)Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                    AdapterId = source.AdapterId,
                    Id = source.Id,
                },
            };
            return DisplayConfigGetDeviceInfo(ref request) == ErrorSuccess
                && string.Equals(
                    request.ViewGdiDeviceName,
                    deviceName,
                    StringComparison.OrdinalIgnoreCase
                );
        }

        private static SystemHdrState QueryTargetHdrState(DisplayConfigPathTargetInfo target)
        {
            // Windows 11 24H2 distinguishes HDR from SDR-with-automatic-colour-management.
            DisplayConfigGetAdvancedColorInfo2 modern = new()
            {
                Header = new DisplayConfigDeviceInfoHeader
                {
                    Type = DeviceInfoGetAdvancedColorInfo2,
                    Size = (uint)Marshal.SizeOf<DisplayConfigGetAdvancedColorInfo2>(),
                    AdapterId = target.AdapterId,
                    Id = target.Id,
                },
            };
            if (DisplayConfigGetDeviceInfo(ref modern) == ErrorSuccess)
                return ResolveAdvancedColorMode(modern.ActiveColorMode);

            DisplayConfigGetAdvancedColorInfo legacy = new()
            {
                Header = new DisplayConfigDeviceInfoHeader
                {
                    Type = DeviceInfoGetAdvancedColorInfo,
                    Size = (uint)Marshal.SizeOf<DisplayConfigGetAdvancedColorInfo>(),
                    AdapterId = target.AdapterId,
                    Id = target.Id,
                },
            };
            return DisplayConfigGetDeviceInfo(ref legacy) == ErrorSuccess
                ? ResolveLegacyAdvancedColor(legacy.Value)
                : SystemHdrState.Unknown;
        }
#endif
    }
}
