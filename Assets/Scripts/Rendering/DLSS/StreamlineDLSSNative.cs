using System;
using System.Runtime.InteropServices;

namespace StreamlineDLSS
{
    /// <summary>
    /// Native P/Invoke declarations for Streamline DLSS plugin.
    /// Internal use only - use StreamlineDLSSPlugin for public API.
    /// </summary>
    internal static class StreamlineDLSSNative
    {
        internal const string DLL_NAME = "GfxPluginStreamline";

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // ==================== Core Functions ====================
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SLDLSS_BeginFrame();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong SLDLSS_GetFrameId();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLDLSS_IsSupported();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLDLSS_IsFrameGenSupported();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLDLSS_GetOptimalSettings(
            int mode,
            uint targetWidth,
            uint targetHeight,
            out DLSSSettings outSettings);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLDLSS_SetMode(int mode);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SLDLSS_GetMode();
        
        // ==================== Frame Generation Functions ====================
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLDLSSG_SetMode(int mode, int numFramesToGenerate);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SLDLSSG_GetMode();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SLDLSSG_GetNumFramesToGenerate();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLDLSSG_GetState(out DLSSGState outState);
        
        // ==================== Preset Functions ====================
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLStreamline_EnableDLSSPerformanceWithFrameGen3x();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLStreamline_DisableDLSSAndFrameGen();
        
        // ==================== Buffer Tagging Functions ====================
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SLDLSS_SetViewport(uint viewportId);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLDLSS_SetConstants(
            float[] cameraViewToClip,
            float[] clipToCameraView,
            float[] clipToPrevClip,
            float[] prevClipToClip,
            float jitterOffsetX, float jitterOffsetY,
            float mvecScaleX, float mvecScaleY,
            float cameraNear, float cameraFar,
            float cameraFOV, float cameraAspectRatio,
            bool depthInverted,
            bool cameraMotionIncluded,
            bool reset
        );
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLDLSS_TagResourceD3D12(
            IntPtr d3d12Resource,
            uint bufferType,
            uint width,
            uint height,
            uint nativeFormat,
            uint state
        );
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLDLSS_SetOptions(
            int mode,
            uint outputWidth,
            uint outputHeight,
            bool colorBuffersHDR
        );
        
        // ==================== Evaluation Functions ====================
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLDLSS_Evaluate(IntPtr commandBuffer);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SLDLSS_PrepareEvaluate();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr SLDLSS_GetRenderCallback();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SLDLSS_GetEvaluateEventID();
        
        // ==================== Frame Gen Options ====================
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLDLSSG_SetOptions(
            int mode,
            uint numFramesToGenerate,
            uint colorWidth,
            uint colorHeight,
            uint mvecDepthWidth,
            uint mvecDepthHeight
        );
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLDLSSG_TagHUDLessColor(
            IntPtr d3d12Resource,
            uint width,
            uint height,
            uint nativeFormat,
            uint state
        );
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLDLSSG_TagUIColorAndAlpha(
            IntPtr d3d12Resource,
            uint width,
            uint height,
            uint nativeFormat,
            uint state
        );
#endif
    }
}
