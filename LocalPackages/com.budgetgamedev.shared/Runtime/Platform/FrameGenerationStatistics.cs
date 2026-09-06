using System;
using BudgetGameDev.Shared.Rendering;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// Rates over a rolling two-second window of cached presentation counters.
    /// SDK total includes real/generated Presents, not monitor scan-out.
    /// Never query slDLSSGGetState here: that resets its count and is not thread-safe.
    /// </summary>
    internal sealed class FrameGenerationStatistics
    {
        private readonly StreamlineNative.Diagnostics[] samples = new StreamlineNative.Diagnostics[16];
        private int count;
        private uint accepted;
        internal double? RenderedFps { get; private set; }
        internal double? TotalFps { get; private set; }
        internal string State { get; private set; } = "FG · measuring";

        internal void Clear()
        {
            count = 0;
            RenderedFps = TotalFps = null;
        }

        internal void Add(int requested, bool native, bool telemetry,
            StreamlineNative.Status status, StreamlineNative.Diagnostics current)
        {
            string inactive = requested == 0 ? "FG OFF"
                : !native || status.initialized == 0 ? "FG · unavailable"
                : status.frameGenerationAvailable == 0 ? "FG · unsupported"
                : !telemetry ? "FG · counters unavailable"
                : status.swapchainHooked == 0 ? "FG · awaiting presentation"
                : status.generatedFrames == 0 ? "FG · suspended"
                : current.fgStateResult != 0 || status.frameGenerationStatus != 0 ? "FG · error"
                : !StreamlineDiagnosticsReport.Fresh(current.snapshotTick, current.presentTick) ? "FG · no recent frames"
                : null;
            if (inactive != null)
            {
                Clear();
                State = inactive;
                return;
            }
            if (accepted != status.generatedFrames) Clear();
            accepted = status.generatedFrames;
            if (count > 0)
            {
                var last = samples[count - 1];
                if (current.snapshotTick <= last.snapshotTick
                    || current.snapshotTick - last.snapshotTick > 1500
                    || current.presentedFrames < last.presentedFrames
                    || current.slPresentedFrames < last.slPresentedFrames
                    || current.slStateSamples < last.slStateSamples)
                    Clear();
            }
            while (count > 0 && (current.snapshotTick - samples[0].snapshotTick > 2000 || count == samples.Length))
            {
                Array.Copy(samples, 1, samples, 0, --count);
            }
            samples[count++] = current;
            State = $"FG {accepted + 1}× · measuring";
            RenderedFps = TotalFps = null;
            var first = samples[0];
            double seconds = (current.snapshotTick - first.snapshotTick) / 1000d;
            if (seconds < .75) return;
            ulong rendered = current.presentedFrames - first.presentedFrames;
            ulong total = current.slPresentedFrames - first.slPresentedFrames;
            ulong queries = current.slStateSamples - first.slStateSamples;
            // Missing SDK queries or stalled counters cannot establish a rate. One
            // query of skew is allowed because the snapshot may land during Present.
            if (rendered == 0 || queries == 0 || queries + 1 < rendered || total + 1 < rendered)
            {
                State = $"FG {accepted + 1}× · counters incomplete";
                return;
            }
            RenderedFps = rendered / seconds;
            TotalFps = total / seconds;
            State = $"FG {accepted + 1}× · SDK presentations";
        }

        internal string FormatRates() =>
            $"TOTAL {Rate(TotalFps)} FPS (including generated)\n{State}\n";

        private static string Rate(double? value) => PerformanceTint.Format(value, "F0", PerformanceTint.Neutral);
    }
}
