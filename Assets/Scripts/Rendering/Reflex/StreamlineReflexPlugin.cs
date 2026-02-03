using System;
using UnityEngine;

namespace StreamlineReflex
{
    /// <summary>
    /// Main facade for NVIDIA Streamline Reflex SDK.
    /// 
    /// This class provides a unified API that delegates to specialized modules:
    /// - StreamlineReflexCore: Initialization and mode management
    /// - StreamlineReflexMarkers: Frame timing markers and latency stats
    /// - StreamlineReflexDiagnostics: Error codes and device info
    /// 
    /// Required DLLs in Assets/Plugins/x86_64/:
    /// - GfxPluginStreamline.dll (built by build-reflex-plugin.ps1)
    /// - sl.interposer.dll (from Streamline SDK)
    /// - sl.reflex.dll (from Streamline SDK)
    /// </summary>
    public static class StreamlineReflexPlugin
    {
        // Core functionality
        public static bool IsAvailable() => StreamlineReflexCore.IsAvailable();
        public static bool Initialize() => StreamlineReflexCore.Initialize();
        public static void Shutdown() => StreamlineReflexCore.Shutdown();
        public static bool IsReflexSupported() => StreamlineReflexCore.IsReflexSupported();
        public static bool IsPCLSupported() => StreamlineReflexCore.IsPCLSupported();
        public static bool IsStreamlineInitialized() => StreamlineReflexCore.IsStreamlineInitialized();
        public static bool SetMode(ReflexMode mode) => StreamlineReflexCore.SetMode(mode);
        public static ReflexMode GetMode() => StreamlineReflexCore.GetMode();
        public static bool GetState(out bool lowLatencyAvailable, out bool flashIndicatorDriverControlled)
            => StreamlineReflexCore.GetState(out lowLatencyAvailable, out flashIndicatorDriverControlled);
        public static bool TryInitialize() => StreamlineReflexCore.TryInitialize();
        
        // Markers and latency
        public static void BeginFrame() => StreamlineReflexMarkers.BeginFrame();
        public static void Sleep() => StreamlineReflexMarkers.Sleep();
        public static void SetMarker(PCLMarker marker) => StreamlineReflexMarkers.SetMarker(marker);
        public static void MarkSimulationStart() => StreamlineReflexMarkers.MarkSimulationStart();
        public static void MarkSimulationEnd() => StreamlineReflexMarkers.MarkSimulationEnd();
        public static void MarkRenderSubmitStart() => StreamlineReflexMarkers.MarkRenderSubmitStart();
        public static void MarkRenderSubmitEnd() => StreamlineReflexMarkers.MarkRenderSubmitEnd();
        public static void TriggerFlash() => StreamlineReflexMarkers.TriggerFlash();
        public static bool GetLatencyStats(out LatencyStats stats) => StreamlineReflexMarkers.GetLatencyStats(out stats);
        public static IntPtr GetRenderEventFunc() => StreamlineReflexMarkers.GetRenderEventFunc();
        
        // Diagnostics
        public static int GetRendererType() => StreamlineReflexDiagnostics.GetRendererType();
        public static bool HasD3D12Device() => StreamlineReflexDiagnostics.HasD3D12Device();
        public static bool HasD3D11Device() => StreamlineReflexDiagnostics.HasD3D11Device();
        public static int GetLastErrorCode() => StreamlineReflexDiagnostics.GetLastErrorCode();
        public static string GetLastErrorMessage() => StreamlineReflexDiagnostics.GetLastErrorMessage();
    }
}
