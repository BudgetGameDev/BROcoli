using BudgetGameDev.Shared.Rendering;

namespace BudgetGameDev.Shared
{
    internal static class PerformanceLatency
    {
        // Simulation start to GPU render end, as in the native NVIDIA diagnostics.
        // No invented FG penalty or conversion from total FPS. Does not measure
        // peripheral delay, display scan-out or later FG presentation buffering.
        internal static double? PcMilliseconds(bool telemetry, StreamlineNative.Diagnostics d) =>
            telemetry && d.reflexStateResult == 0 && d.latencyValid != 0 && d.pcLatencyUs > 0
                && StreamlineDiagnosticsReport.Fresh(d.snapshotTick, d.reportTick)
                ? d.pcLatencyUs / 1000d : null;

        internal static string Format(bool telemetry, StreamlineNative.Diagnostics d)
        {
            double? latency = PcMilliseconds(telemetry, d);
            return "PC LATENCY " + PerformanceTint.Format(latency, "F1", PerformanceTint.High(latency, 25, 50))
                + " ms · Reflex\nInput/display delay not measured\n";
        }

        internal static string Pipeline(string typeName) => typeName == null ? "BUILT-IN"
            : typeName == "UniversalRenderPipelineAsset" ? "URP"
            : typeName == "HDRenderPipelineAsset" ? "HDRP" : "CUSTOM SRP";
    }
}
