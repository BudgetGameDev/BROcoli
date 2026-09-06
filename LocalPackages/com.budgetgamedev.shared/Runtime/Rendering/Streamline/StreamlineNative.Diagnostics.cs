using System;
using System.Runtime.InteropServices;
using System.Text;

namespace BudgetGameDev.Shared.Rendering
{
    internal static partial class StreamlineNative
    {
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct Diagnostics
        {
            public ulong simulationId,
                submissionId,
                presentId,
                simulatedFrames,
                presentedFrames;
            public ulong sleepCalls,
                sleepSuccesses,
                markers,
                completeTags,
                slPresentedFrames,
                slStateSamples;
            public ulong reflexReportFrame,
                reflexReportUpdates,
                presentTick,
                reportTick,
                generatedTick,
                snapshotTick;
            public uint activeReflex,
                tagMask,
                renderWidth,
                renderHeight,
                outputWidth,
                outputHeight;
            public uint actualPresentedLast,
                fgStateResult,
                reflexStateResult,
                latencyValid,
                pclWindowBound,
                presentResult;
            public uint pcLatencyUs,
                simulationLatencyUs,
                renderLatencyUs,
                gpuLatencyUs,
                markerResult,
                sleepResult;
        }

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint BgdSL_GetDiagnostics(out Diagnostics diagnostics, uint size);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint BgdSL_GetRecentLog([Out] byte[] buffer, uint capacity);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint BgdSL_GetLogDirectory([Out] byte[] buffer, uint capacity);

        internal static string GetLogDirectory()
        {
            if (!TryGetStatus(out _))
                return "";
            try
            {
                var buffer = new byte[131073];
                uint length = BgdSL_GetLogDirectory(buffer, (uint)buffer.Length);
                return Encoding.UTF8.GetString(buffer, 0, (int)Math.Min(length, buffer.Length - 1));
            }
            catch (EntryPointNotFoundException)
            {
                return "";
            }
        }

        private static readonly byte[] logBuffer = new byte[131073];

        // Overlay polling needs only the cached counters, not native log allocation/copying.
        internal static bool TryGetDiagnostics(out Diagnostics diagnostics)
        {
            diagnostics = default;
            if (!TryGetStatus(out _)) return false;
            try
            {
                return BgdSL_GetDiagnostics(out diagnostics, (uint)Marshal.SizeOf<Diagnostics>()) == 1;
            }
            catch (EntryPointNotFoundException) { return false; }
        }

        internal static bool TryGetDiagnostics(out Diagnostics diagnostics, out string log)
        {
            diagnostics = default;
            log = "Native diagnostic exports unavailable; rebuild the shared bridge.";
            if (!TryGetStatus(out _))
            {
                log = UnavailableReason;
                return false;
            }
            try
            {
                if (BgdSL_GetDiagnostics(out diagnostics, (uint)Marshal.SizeOf<Diagnostics>()) != 1)
                    return false;
                uint length = BgdSL_GetRecentLog(logBuffer, (uint)logBuffer.Length);
                log = Encoding.UTF8.GetString(
                    logBuffer,
                    0,
                    (int)Math.Min(length, logBuffer.Length - 1)
                );
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }
    }
}
