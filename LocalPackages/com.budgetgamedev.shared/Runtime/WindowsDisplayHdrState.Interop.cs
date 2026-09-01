using System;
using System.Runtime.InteropServices;

namespace BudgetGameDev.Shared
{
    internal static partial class WindowsDisplayHdrState
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential)]
        private struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigPathSourceInfo
        {
            public Luid AdapterId;
            public uint Id;
            public uint ModeInfoIndex;
            public uint StatusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigPathTargetInfo
        {
            public Luid AdapterId;
            public uint Id;
            public uint ModeInfoIndex;
            public uint OutputTechnology;
            public uint Rotation;
            public uint Scaling;
            public uint RefreshRateNumerator;
            public uint RefreshRateDenominator;
            public uint ScanLineOrdering;
            public int TargetAvailable;
            public uint StatusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigPathInfo
        {
            public DisplayConfigPathSourceInfo SourceInfo;
            public DisplayConfigPathTargetInfo TargetInfo;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigModeInfo
        {
            public uint InfoType;
            public uint Id;
            public Luid AdapterId;
            public ulong Union0;
            public ulong Union1;
            public ulong Union2;
            public ulong Union3;
            public ulong Union4;
            public ulong Union5;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigDeviceInfoHeader
        {
            public uint Type;
            public uint Size;
            public Luid AdapterId;
            public uint Id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DisplayConfigSourceDeviceName
        {
            public DisplayConfigDeviceInfoHeader Header;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string ViewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigGetAdvancedColorInfo
        {
            public DisplayConfigDeviceInfoHeader Header;
            public uint Value;
            public uint ColorEncoding;
            public uint BitsPerColorChannel;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DisplayConfigGetAdvancedColorInfo2
        {
            public DisplayConfigDeviceInfoHeader Header;
            public uint Value;
            public uint ColorEncoding;
            public uint BitsPerColorChannel;
            public uint ActiveColorMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MonitorInfoEx
        {
            public int Size;
            public Rect Monitor;
            public Rect Work;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string Device;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfoEx info);

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(
            uint flags,
            out uint pathCount,
            out uint modeCount
        );

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(
            uint flags,
            ref uint pathCount,
            [Out] DisplayConfigPathInfo[] paths,
            ref uint modeCount,
            [Out] DisplayConfigModeInfo[] modes,
            IntPtr currentTopologyId
        );

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(
            ref DisplayConfigSourceDeviceName request
        );

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(
            ref DisplayConfigGetAdvancedColorInfo request
        );

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(
            ref DisplayConfigGetAdvancedColorInfo2 request
        );
#endif
    }
}
