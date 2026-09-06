using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>Pipeline-independent menu contract. Streamline supplies the native implementation.</summary>
    public static class NvidiaRendering
    {
        public sealed class Snapshot
        {
            public bool DlssRequested = true;
            public int GeneratedFrames = 3,
                Reflex = 1,
                MaximumGeneratedFrames;
            public bool CanSetDlss,
                CanSetFrames,
                CanSetReflex;
            public string Summary = "UNAVAILABLE",
                Report = "";
        }

        public interface IBackend
        {
            Snapshot Capture();
            void SetDlss(bool enabled);
            void SetFrames(int frames);
            void SetReflex(int mode);
            void Reset();
            void ReleaseDiagnostics();
        }

        public static IBackend Backend { get; set; }

        public interface ILogBackend
        {
            string ReadLogFiles();
        }

        // Disk reads happen only on an explicit copy/export, never in the 4 Hz UI refresh.
        public static string CaptureForCopy() =>
            Capture().Report + (Backend is ILogBackend logs ? logs.ReadLogFiles() : "");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBackend() => Backend = null;

        public static Snapshot Capture() =>
            Backend?.Capture()
            ?? new Snapshot
            {
                Summary = "NVIDIA FEATURES UNAVAILABLE",
                Report =
                    "DLSS, FRAME GENERATION & REFLEX\n\n"
                    + "Requested defaults: DLSS Quality / Preset K; 4x Frame Generation; Reflex On.\n"
                    + "Active configuration: unavailable. No Streamline diagnostics provider is running.\n"
                    + "These features require the native Windows x64 DX12 URP or HDRP player.\n\n"
                    + $"Platform: {Application.platform}\nUnity: {Application.unityVersion}\n"
                    + $"Graphics API: {SystemInfo.graphicsDeviceType}\nGPU: {SystemInfo.graphicsDeviceName}\n"
                    + $"Driver/API: {SystemInfo.graphicsDeviceVersion}\n"
                    + $"Pipeline: {GraphicsSettings.currentRenderPipeline?.GetType().Name ?? "Built-in"}\n\n"
                    + "Actual DLSS execution, generated frames and Reflex timing: NOT OBSERVED.",
            };
    }
}
