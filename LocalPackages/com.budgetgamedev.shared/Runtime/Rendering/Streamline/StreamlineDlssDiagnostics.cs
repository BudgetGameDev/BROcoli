namespace BudgetGameDev.Shared.Rendering
{
    internal sealed class StreamlineDlssDiagnostics
    {
        internal string Read(out bool supported)
        {
            bool native = StreamlineNative.TryGetSuperResolutionStatus(out var status);
            supported = native && status.available != 0;
            if (!native)
                return "Streamline DLSS telemetry unavailable; SR execution NOT OBSERVED.";
            return Describe(status);
        }

        internal static string Describe(StreamlineNative.SuperResolutionStatus status)
        {
            bool supported = status.available != 0;
            bool observed =
                supported
                && status.evaluations > 0
                && status.evaluationResult == 0
                && status.optionsResult == 0
                && StreamlineDiagnosticsReport.Fresh(status.snapshotTick, status.evaluationTick);
            return $"Backend: Streamline 2.12.0 / Quality / Preset K\nSupported: {supported}; support result: {status.supportResult}\n"
                + $"Options result: {status.optionsResult}; evaluation result: {status.evaluationResult}\n"
                + $"Dispatches: {status.evaluations}/{status.attempts}; {status.width}x{status.height} -> {status.outputWidth}x{status.outputHeight}\n"
                + (
                    observed
                        ? "Successful SR dispatch OBSERVED (GPU completion / image quality not measured)."
                        : "Recent SR execution NOT OBSERVED."
                );
        }

        internal void Release() { }
    }
}
