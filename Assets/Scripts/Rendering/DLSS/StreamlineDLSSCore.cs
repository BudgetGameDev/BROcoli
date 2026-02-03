using System;
using UnityEngine;

namespace StreamlineDLSS
{
    /// <summary>
    /// Core DLSS functionality: support checks, mode management, and presets.
    /// </summary>
    public static class StreamlineDLSSCore
    {
        /// <summary>
        /// Check if DLSS Super Resolution is supported on this GPU
        /// </summary>
        public static bool IsDLSSSupported()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try { return StreamlineDLSSNative.SLDLSS_IsSupported(); }
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
            try { return StreamlineDLSSNative.SLDLSS_IsFrameGenSupported(); }
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
        public static bool GetOptimalSettings(DLSSMode mode, uint outputWidth, uint outputHeight, out DLSSSettings settings)
        {
            settings = default;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                return StreamlineDLSSNative.SLDLSS_GetOptimalSettings((int)mode, outputWidth, outputHeight, out settings);
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
                bool result = StreamlineDLSSNative.SLDLSS_SetMode((int)mode);
                if (result)
                    Debug.Log($"[StreamlineDLSS] DLSS mode set to: {mode}");
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
            try { return (DLSSMode)StreamlineDLSSNative.SLDLSS_GetMode(); }
            catch { return DLSSMode.Off; }
#else
            return DLSSMode.Off;
#endif
        }
        
        /// <summary>
        /// Set Frame Generation mode
        /// </summary>
        public static bool SetFrameGenMode(DLSSGMode mode, int numFramesToGenerate = 2)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                bool result = StreamlineDLSSNative.SLDLSSG_SetMode((int)mode, numFramesToGenerate);
                if (result)
                {
                    string multiplier = numFramesToGenerate switch
                    {
                        1 => "2x",
                        2 => "3x",
                        3 => "4x",
                        _ => $"{numFramesToGenerate + 1}x"
                    };
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
            try { return (DLSSGMode)StreamlineDLSSNative.SLDLSSG_GetMode(); }
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
            try { return StreamlineDLSSNative.SLDLSSG_GetNumFramesToGenerate(); }
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
            try { return StreamlineDLSSNative.SLDLSSG_GetState(out state); }
            catch { return false; }
#else
            return false;
#endif
        }
        
        /// <summary>
        /// Quick preset: Enable DLSS Performance + Frame Gen 3x
        /// </summary>
        public static bool EnablePerformanceWithFrameGen3x()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                bool result = StreamlineDLSSNative.SLStreamline_EnableDLSSPerformanceWithFrameGen3x();
                if (result)
                    Debug.Log("[StreamlineDLSS] ✓ Enabled DLSS Performance + Frame Gen 3x");
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
                bool result = StreamlineDLSSNative.SLStreamline_DisableDLSSAndFrameGen();
                if (result)
                    Debug.Log("[StreamlineDLSS] DLSS and Frame Gen disabled");
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
}
