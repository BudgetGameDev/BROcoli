using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BudgetGameDev.Shared.Rendering
{
    internal static partial class StreamlineNative
    {
        internal const string Library = "GfxPluginBudgetGameDevStreamline";
        internal const int CaptureEvent = 0x425200;
        internal const int SubmitStartEvent = CaptureEvent + 1;
        internal const int SubmitEndEvent = CaptureEvent + 2;

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct Status
        {
            public uint abi,
                initialized,
                reflexAvailable,
                frameGenerationAvailable;
            public uint maxGeneratedFrames,
                generatedFrames,
                lastError,
                frameGenerationStatus;
            public uint swapchainHooked;
            public uint requirementsResult,
                featureSupportResult,
                integrationWarnings;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct FrameData
        {
            public IntPtr token,
                depth,
                motion,
                ui,
                hudless;
            public Matrix4x4 viewToClip,
                clipToView,
                clipToPrevious,
                previousToClip;
            public Vector3 position,
                up,
                right,
                forward;
            public Vector2 jitter,
                motionScale;
            public float nearPlane,
                farPlane,
                fieldOfView,
                aspect;
            public uint width,
                height,
                outputWidth,
                outputHeight,
                reset,
                invertedDepth;
        }

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint BgdSL_GetStatus(out Status status, uint size);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint BgdSL_FrameDataSize();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr BgdSL_BeginFrame();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void BgdSL_EndSimulation(IntPtr token);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void BgdSL_Configure(
            uint generatedFrames,
            uint reflexMode,
            uint focused
        );

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr BgdSL_CopyFrame(in FrameData frame, uint size);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr BgdSL_GetRenderEvent();

        internal static string UnavailableReason { get; private set; } = "Not queried";

        internal static bool TryGetStatus(out Status status)
        {
            status = default;
            if (Application.platform != RuntimePlatform.WindowsPlayer)
            {
                UnavailableReason =
                    $"Native bridge requires Windows Player; current platform: {Application.platform}.";
                return false;
            }
            try
            {
                bool valid =
                    BgdSL_GetStatus(out status, (uint)Marshal.SizeOf<Status>()) == 2
                    && status.abi == 2
                    && BgdSL_FrameDataSize() == Marshal.SizeOf<FrameData>();
                UnavailableReason = valid
                    ? ""
                    : "Native bridge ABI mismatch; rebuild the shared plugin.";
                return valid;
            }
            catch (DllNotFoundException exception)
            {
                UnavailableReason = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
            catch (EntryPointNotFoundException exception)
            {
                UnavailableReason = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
            catch (BadImageFormatException exception)
            {
                UnavailableReason = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }
    }
}
