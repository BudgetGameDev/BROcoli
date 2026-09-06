using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared.Rendering
{
    internal sealed class StreamlineSettingsBackend : NvidiaRendering.IBackend
    {
        private readonly StreamlineDlssDiagnostics dlss = new StreamlineDlssDiagnostics();
        private bool HasPlayer =>
            Application.platform == RuntimePlatform.WindowsPlayer
            && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12
            && (RenderPipelineProbe.IsHighDefinition || RenderPipelineProbe.IsUniversal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register() => NvidiaRendering.Backend = new StreamlineSettingsBackend();

        public NvidiaRendering.Snapshot Capture()
        {
            var snapshot = new NvidiaRendering.Snapshot
            {
                DlssRequested = StreamlineSettings.DlssEnabled,
                GeneratedFrames = StreamlineSettings.GeneratedFrames,
                Reflex = (int)StreamlineSettings.Reflex,
            };
            bool native = StreamlineNative.TryGetStatus(out var status);
            bool telemetry = StreamlineNative.TryGetDiagnostics(
                out var diagnostics,
                out string log
            );
            string sr = dlss.Read(out bool srSupported);
            snapshot.CanSetDlss = HasPlayer && srSupported;
            snapshot.CanSetFrames =
                HasPlayer
                && StreamlineRuntime.Pipeline?.CanCapture == true
                && status.frameGenerationAvailable != 0
                && status.swapchainHooked != 0;
            snapshot.CanSetReflex = HasPlayer && status.reflexAvailable != 0;
            snapshot.MaximumGeneratedFrames = (int)status.maxGeneratedFrames;
            snapshot.Summary =
                !HasPlayer ? "WINDOWS DX12 URP OR HDRP PLAYER REQUIRED"
                : !native ? "NATIVE BRIDGE UNAVAILABLE"
                : !telemetry ? "DIAGNOSTIC EXPORTS UNAVAILABLE"
                : StreamlineDiagnosticsReport.FrameGenerationState(status, diagnostics);
            snapshot.Report = StreamlineDiagnosticsReport.Build(
                snapshot,
                status,
                diagnostics,
                native,
                telemetry,
                sr,
                log
            );
            return snapshot;
        }

        public void SetDlss(bool enabled) => StreamlineSettings.DlssEnabled = enabled;

        public void SetFrames(int frames) => StreamlineSettings.GeneratedFrames = frames;

        public void SetReflex(int mode)
        {
            if (mode == 0)
                StreamlineSettings.GeneratedFrames = 0;
            StreamlineSettings.Reflex = (StreamlineSettings.ReflexMode)mode;
        }

        public void Reset() => StreamlineSettings.ResetDefaults();

        public void ReleaseDiagnostics() => dlss.Release();
    }
}
