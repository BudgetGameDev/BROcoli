using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Native plugin wrapper for NVIDIA DLSS and Frame Generation via Streamline SDK.
/// 
/// DLSS (Deep Learning Super Sampling) uses AI to upscale lower resolution renders
/// to higher output resolutions with better quality than traditional upscaling.
/// 
/// Frame Generation creates interpolated frames between rendered frames
/// for smoother gameplay without additional rendering cost.
/// 
/// Setup:
/// 1. Run build-reflex-plugin.ps1 to build the native plugin
/// 2. Required DLLs are copied automatically to Assets/Plugins/x86_64/
/// 
/// Required DLLs:
/// - StreamlineReflexPlugin.dll (built by this project)
/// - sl.interposer.dll, sl.dlss.dll, sl.dlss_g.dll (from Streamline SDK)
/// - nvngx_dlss.dll, nvngx_dlssg.dll (NVIDIA NGX model files)
/// 
/// Note: DLSS requires RTX GPUs. Frame Generation requires RTX 40-series or newer.
/// </summary>
public static class StreamlineDLSSPlugin
{
    private const string DLL_NAME = "StreamlineReflexPlugin";
    
    /// <summary>
    /// DLSS Quality/Performance modes
    /// </summary>
    public enum DLSSMode
    {
        Off = 0,
        MaxPerformance = 1,    // ~50% render scale - best performance
        Balanced = 2,          // ~58% render scale - balanced
        MaxQuality = 3,        // ~67% render scale - best quality
        UltraPerformance = 4,  // ~33% render scale - extreme performance (may reduce quality)
        UltraQuality = 5,      // ~77% render scale - highest quality
        DLAA = 6               // Native resolution AA - no upscaling, just anti-aliasing
    }
    
    /// <summary>
    /// Frame Generation modes
    /// </summary>
    public enum DLSSGMode
    {
        Off = 0,
        On = 1,
        Auto = 2   // Automatically enables based on GPU capability
    }
    
    /// <summary>
    /// DLSS optimal render settings for a given output resolution and mode
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DLSSSettings
    {
        public uint OptimalRenderWidth;
        public uint OptimalRenderHeight;
        public uint MinRenderWidth;
        public uint MinRenderHeight;
        public uint MaxRenderWidth;
        public uint MaxRenderHeight;
        public float OptimalSharpness;
    }
    
    /// <summary>
    /// Frame Generation state information
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DLSSGState
    {
        public ulong EstimatedVRAMUsage;
        public uint Status;
        public uint MinWidthOrHeight;
        public uint NumFramesActuallyPresented;
        public uint NumFramesToGenerateMax;
    }
    
    // ==================== Native Function Imports ====================
    
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLDLSS_IsSupported();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLDLSS_IsFrameGenSupported();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLDLSS_GetOptimalSettings(
        int mode,
        uint targetWidth,
        uint targetHeight,
        out DLSSSettings outSettings);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLDLSS_SetMode(int mode);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int SLDLSS_GetMode();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLDLSSG_SetMode(int mode, int numFramesToGenerate);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int SLDLSSG_GetMode();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int SLDLSSG_GetNumFramesToGenerate();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLDLSSG_GetState(out DLSSGState outState);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLStreamline_EnableDLSSPerformanceWithFrameGen3x();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLStreamline_DisableDLSSAndFrameGen();
#endif

    // ==================== Public API ====================
    
    /// <summary>
    /// Check if DLSS Super Resolution is supported on this GPU
    /// </summary>
    public static bool IsDLSSSupported()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try { return SLDLSS_IsSupported(); }
        catch (Exception e)
        {
            Debug.LogWarning($"[StreamlineDLSS] Error checking DLSS support: {e.Message}");
            return false;
        }
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Check if DLSS Frame Generation is supported (requires RTX 40-series or newer)
    /// </summary>
    public static bool IsFrameGenSupported()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try { return SLDLSS_IsFrameGenSupported(); }
        catch (Exception e)
        {
            Debug.LogWarning($"[StreamlineDLSS] Error checking Frame Gen support: {e.Message}");
            return false;
        }
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Get optimal render settings for a given DLSS mode and output resolution
    /// </summary>
    /// <param name="mode">DLSS quality mode</param>
    /// <param name="outputWidth">Target output width (e.g., 1920)</param>
    /// <param name="outputHeight">Target output height (e.g., 1080)</param>
    /// <param name="settings">Output settings with optimal render dimensions</param>
    /// <returns>True if successful</returns>
    public static bool GetOptimalSettings(DLSSMode mode, uint outputWidth, uint outputHeight, out DLSSSettings settings)
    {
        settings = default;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            return SLDLSS_GetOptimalSettings((int)mode, outputWidth, outputHeight, out settings);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[StreamlineDLSS] Error getting optimal settings: {e.Message}");
            return false;
        }
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Set DLSS Super Resolution mode
    /// </summary>
    public static bool SetDLSSMode(DLSSMode mode)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            bool result = SLDLSS_SetMode((int)mode);
            if (result)
            {
                Debug.Log($"[StreamlineDLSS] DLSS mode set to: {mode}");
            }
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"[StreamlineDLSS] SetDLSSMode failed: {e.Message}");
            return false;
        }
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Get current DLSS mode
    /// </summary>
    public static DLSSMode GetDLSSMode()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try { return (DLSSMode)SLDLSS_GetMode(); }
        catch { return DLSSMode.Off; }
#else
        return DLSSMode.Off;
#endif
    }
    
    /// <summary>
    /// Set Frame Generation mode
    /// </summary>
    /// <param name="mode">Frame Generation mode</param>
    /// <param name="numFramesToGenerate">
    /// Number of frames to generate between rendered frames:
    /// 1 = 2x frame rate (1 generated frame)
    /// 2 = 3x frame rate (2 generated frames)
    /// 3 = 4x frame rate (3 generated frames) - requires RTX 50 series
    /// </param>
    public static bool SetFrameGenMode(DLSSGMode mode, int numFramesToGenerate = 2)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            bool result = SLDLSSG_SetMode((int)mode, numFramesToGenerate);
            if (result)
            {
                string multiplier = numFramesToGenerate == 1 ? "2x" : 
                                   numFramesToGenerate == 2 ? "3x" : 
                                   numFramesToGenerate == 3 ? "4x" : $"{numFramesToGenerate+1}x";
                Debug.Log($"[StreamlineDLSS] Frame Generation: {mode} ({multiplier})");
            }
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"[StreamlineDLSS] SetFrameGenMode failed: {e.Message}");
            return false;
        }
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Get current Frame Generation mode
    /// </summary>
    public static DLSSGMode GetFrameGenMode()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try { return (DLSSGMode)SLDLSSG_GetMode(); }
        catch { return DLSSGMode.Off; }
#else
        return DLSSGMode.Off;
#endif
    }
    
    /// <summary>
    /// Get the current number of frames being generated per rendered frame
    /// </summary>
    public static int GetNumFramesToGenerate()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try { return SLDLSSG_GetNumFramesToGenerate(); }
        catch { return 0; }
#else
        return 0;
#endif
    }
    
    /// <summary>
    /// Get Frame Generation state information
    /// </summary>
    public static bool GetFrameGenState(out DLSSGState state)
    {
        state = default;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try { return SLDLSSG_GetState(out state); }
        catch { return false; }
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Quick preset: Enable DLSS Performance + Frame Gen 3x
    /// This is the recommended setting for maximum performance boost.
    /// DLSS Performance renders at ~50% resolution, Frame Gen 3x triples apparent framerate.
    /// </summary>
    public static bool EnablePerformanceWithFrameGen3x()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            bool result = SLStreamline_EnableDLSSPerformanceWithFrameGen3x();
            if (result)
            {
                Debug.Log("[StreamlineDLSS] ✓ Enabled DLSS Performance + Frame Gen 3x");
            }
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"[StreamlineDLSS] EnablePerformanceWithFrameGen3x failed: {e.Message}");
            return false;
        }
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Disable DLSS and Frame Generation
    /// </summary>
    public static bool DisableAll()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            bool result = SLStreamline_DisableDLSSAndFrameGen();
            if (result)
            {
                Debug.Log("[StreamlineDLSS] DLSS and Frame Gen disabled");
            }
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"[StreamlineDLSS] DisableAll failed: {e.Message}");
            return false;
        }
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Log current DLSS feature support to the console
    /// </summary>
    public static void LogSupport()
    {
        Debug.Log("[StreamlineDLSS] Feature Support Check:");
        Debug.Log($"  DLSS Super Resolution: {(IsDLSSSupported() ? "✓ Supported" : "✗ Not Supported")}");
        Debug.Log($"  Frame Generation: {(IsFrameGenSupported() ? "✓ Supported (RTX 40+)" : "✗ Not Supported")}");
    }
}
