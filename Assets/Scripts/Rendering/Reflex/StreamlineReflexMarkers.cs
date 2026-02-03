using System;
using UnityEngine;

namespace StreamlineReflex
{
    /// <summary>
    /// Reflex frame markers and latency measurement.
    /// Use these methods to mark timing points for PCL latency stats.
    /// </summary>
    public static class StreamlineReflexMarkers
    {
        /// <summary>
        /// Call at the beginning of each frame before processing input
        /// </summary>
        public static void BeginFrame()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!StreamlineReflexCore.IsStreamlineInitialized()) return;
            try { StreamlineReflexNative.SLReflex_BeginFrame(); } catch { }
#endif
        }
        
        /// <summary>
        /// Call Reflex Sleep to synchronize CPU-GPU timing for lowest latency.
        /// Call this at the start of your frame, before processing input.
        /// </summary>
        public static void Sleep()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!StreamlineReflexCore.IsStreamlineInitialized()) return;
            try { StreamlineReflexNative.SLReflex_Sleep(); } catch { }
#endif
        }
        
        /// <summary>
        /// Set a PCL timing marker
        /// </summary>
        public static void SetMarker(PCLMarker marker)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!StreamlineReflexCore.IsStreamlineInitialized()) return;
            try { StreamlineReflexNative.SLReflex_SetMarker((int)marker); } catch { }
#endif
        }
        
        /// <summary>
        /// Mark the start of simulation/game logic
        /// </summary>
        public static void MarkSimulationStart()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!StreamlineReflexCore.IsStreamlineInitialized()) return;
            try { StreamlineReflexNative.SLReflex_MarkSimulationStart(); } catch { }
#endif
        }
        
        /// <summary>
        /// Mark the end of simulation/game logic
        /// </summary>
        public static void MarkSimulationEnd()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!StreamlineReflexCore.IsStreamlineInitialized()) return;
            try { StreamlineReflexNative.SLReflex_MarkSimulationEnd(); } catch { }
#endif
        }
        
        /// <summary>
        /// Mark the start of render submission
        /// </summary>
        public static void MarkRenderSubmitStart()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!StreamlineReflexCore.IsStreamlineInitialized()) return;
            try { StreamlineReflexNative.SLReflex_MarkRenderSubmitStart(); } catch { }
#endif
        }
        
        /// <summary>
        /// Mark the end of render submission
        /// </summary>
        public static void MarkRenderSubmitEnd()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!StreamlineReflexCore.IsStreamlineInitialized()) return;
            try { StreamlineReflexNative.SLReflex_MarkRenderSubmitEnd(); } catch { }
#endif
        }
        
        /// <summary>
        /// Trigger the Reflex Flash Indicator (for testing latency)
        /// </summary>
        public static void TriggerFlash()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!StreamlineReflexCore.IsStreamlineInitialized()) return;
            try { StreamlineReflexNative.SLReflex_TriggerFlash(); } catch { }
#endif
        }
        
        /// <summary>
        /// Get latency statistics (requires PCL markers to be set)
        /// </summary>
        public static bool GetLatencyStats(out LatencyStats stats)
        {
            stats = default;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!StreamlineReflexCore.IsStreamlineInitialized()) return false;
            try { return StreamlineReflexNative.SLReflex_GetLatencyStats(out stats); }
            catch { return false; }
#else
            return false;
#endif
        }
        
        /// <summary>
        /// Get the render event function pointer for use with GL.IssuePluginEvent
        /// </summary>
        public static IntPtr GetRenderEventFunc()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!StreamlineReflexCore.IsStreamlineInitialized()) return IntPtr.Zero;
            try { return StreamlineReflexNative.SLReflex_GetRenderEventFunc(); }
            catch { return IntPtr.Zero; }
#else
            return IntPtr.Zero;
#endif
        }
    }
}
