using System;
using System.Runtime.InteropServices;

namespace BudgetGameDev.Shared.Rendering
{
    internal static partial class StreamlineNative
    {
        internal const int SuperResolutionEvent = CaptureEvent + 3;

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct SuperResolutionData
        {
            public FrameData frame;
            public IntPtr input,
                output;
            public float preExposure;
            public uint hdr,
                motionWidth,
                motionHeight;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct SuperResolutionStatus
        {
            public uint available,
                supportResult,
                optionsResult,
                evaluationResult;
            public uint width,
                height,
                outputWidth,
                outputHeight;
            public ulong attempts,
                evaluations,
                evaluationTick,
                snapshotTick;
        }

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint BgdSL_GetSuperResolutionStatus(
            out SuperResolutionStatus status,
            uint size
        );

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint BgdSL_GetOptimalResolution(
            uint width,
            uint height,
            out uint x,
            out uint y
        );

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr BgdSL_CopySuperResolution(
            in SuperResolutionData data,
            uint size
        );

        internal static bool TryGetSuperResolutionStatus(out SuperResolutionStatus status)
        {
            status = default;
            if (!TryGetStatus(out var bridge) || bridge.initialized == 0)
                return false;
            try
            {
                return BgdSL_GetSuperResolutionStatus(
                        out status,
                        (uint)Marshal.SizeOf<SuperResolutionStatus>()
                    ) == 1;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }
    }
}
