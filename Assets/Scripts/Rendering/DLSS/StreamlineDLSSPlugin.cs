using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace StreamlineDLSS
{
    /// <summary>
    /// Main facade for NVIDIA DLSS and Frame Generation via Streamline SDK.
    /// 
    /// This class provides a unified API that delegates to specialized modules:
    /// - StreamlineDLSSCore: Support checks and mode management
    /// - StreamlineDLSSBuffers: Resource tagging and configuration
    /// - StreamlineDLSSEvaluate: Render callback and evaluation
    /// 
    /// Required DLLs in Assets/Plugins/x86_64/:
    /// - GfxPluginStreamline.dll (built by build-reflex-plugin.ps1)
    /// - sl.interposer.dll, sl.dlss.dll, sl.dlss_g.dll (Streamline SDK)
    /// - nvngx_dlss.dll, nvngx_dlssg.dll (NVIDIA NGX model files)
    /// </summary>
    public static class StreamlineDLSSPlugin
    {
        // Re-export types for backward compatibility
        public static bool IsDLSSSupported() => StreamlineDLSSCore.IsDLSSSupported();
        public static bool IsFrameGenSupported() => StreamlineDLSSCore.IsFrameGenSupported();
        
        public static bool GetOptimalSettings(DLSSMode mode, uint outputWidth, uint outputHeight, out DLSSSettings settings)
            => StreamlineDLSSCore.GetOptimalSettings(mode, outputWidth, outputHeight, out settings);
        
        public static bool SetDLSSMode(DLSSMode mode) => StreamlineDLSSCore.SetDLSSMode(mode);
        public static DLSSMode GetDLSSMode() => StreamlineDLSSCore.GetDLSSMode();
        
        public static bool SetFrameGenMode(DLSSGMode mode, int numFramesToGenerate = 2)
            => StreamlineDLSSCore.SetFrameGenMode(mode, numFramesToGenerate);
        public static DLSSGMode GetFrameGenMode() => StreamlineDLSSCore.GetFrameGenMode();
        public static int GetNumFramesToGenerate() => StreamlineDLSSCore.GetNumFramesToGenerate();
        public static bool GetFrameGenState(out DLSSGState state) => StreamlineDLSSCore.GetFrameGenState(out state);
        
        public static bool EnablePerformanceWithFrameGen3x() => StreamlineDLSSCore.EnablePerformanceWithFrameGen3x();
        public static bool DisableAll() => StreamlineDLSSCore.DisableAll();
        public static void LogSupport() => StreamlineDLSSCore.LogSupport();
        
        // Buffer operations
        public static void SetViewport(uint viewportId) => StreamlineDLSSBuffers.SetViewport(viewportId);
        
        public static bool SetConstants(
            Matrix4x4 cameraViewToClip,
            Matrix4x4 clipToCameraView,
            Matrix4x4 clipToPrevClip,
            Matrix4x4 prevClipToClip,
            Vector2 jitterOffset,
            Vector2 mvecScale,
            float cameraNear,
            float cameraFar,
            float cameraFOV,
            float cameraAspectRatio,
            bool depthInverted,
            bool cameraMotionIncluded,
            bool reset)
            => StreamlineDLSSBuffers.SetConstants(
                cameraViewToClip, clipToCameraView, clipToPrevClip, prevClipToClip,
                jitterOffset, mvecScale, cameraNear, cameraFar, cameraFOV, cameraAspectRatio,
                depthInverted, cameraMotionIncluded, reset);
        
        public static bool TagResource(IntPtr d3d12Resource, BufferType bufferType, uint width, uint height, uint format, uint state)
            => StreamlineDLSSBuffers.TagResource(d3d12Resource, bufferType, width, height, format, state);
        
        public static bool SetOptions(DLSSMode mode, uint outputWidth, uint outputHeight, bool colorBuffersHDR = true)
            => StreamlineDLSSBuffers.SetOptions(mode, outputWidth, outputHeight, colorBuffersHDR);
        
        public static bool Evaluate(IntPtr commandBuffer = default)
            => StreamlineDLSSBuffers.Evaluate(commandBuffer);
        
        public static bool SetFrameGenOptions(DLSSGMode mode, uint numFramesToGenerate, uint colorWidth, uint colorHeight, uint mvecDepthWidth, uint mvecDepthHeight)
            => StreamlineDLSSBuffers.SetFrameGenOptions(mode, numFramesToGenerate, colorWidth, colorHeight, mvecDepthWidth, mvecDepthHeight);
        
        public static bool TagHUDLessColor(IntPtr d3d12Resource, uint width, uint height, uint format, uint state)
            => StreamlineDLSSBuffers.TagHUDLessColor(d3d12Resource, width, height, format, state);
        
        public static bool TagUIColorAndAlpha(IntPtr d3d12Resource, uint width, uint height, uint format, uint state)
            => StreamlineDLSSBuffers.TagUIColorAndAlpha(d3d12Resource, width, height, format, state);
        
        // Evaluation
        public static IntPtr GetRenderCallback() => StreamlineDLSSEvaluate.GetRenderCallback();
        public static int GetEvaluateEventID() => StreamlineDLSSEvaluate.GetEvaluateEventID();
        
        public static void IssueEvaluateEvent(CommandBuffer cmd) => StreamlineDLSSEvaluate.IssueEvaluateEvent(cmd);
        public static void IssueEvaluateEvent(UnsafeCommandBuffer cmd) => StreamlineDLSSEvaluate.IssueEvaluateEvent(cmd);
    }
}

// Backward compatibility aliases at global scope
public static class StreamlineDLSSPluginCompat
{
    // Type aliases for code using the old API
    public static bool IsDLSSSupported() => StreamlineDLSS.StreamlineDLSSPlugin.IsDLSSSupported();
    public static bool IsFrameGenSupported() => StreamlineDLSS.StreamlineDLSSPlugin.IsFrameGenSupported();
}
