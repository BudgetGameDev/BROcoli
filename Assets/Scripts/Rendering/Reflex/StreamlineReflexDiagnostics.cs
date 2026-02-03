using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace StreamlineReflex
{
    /// <summary>
    /// Reflex diagnostics: device info, error codes, and renderer detection.
    /// </summary>
    public static class StreamlineReflexDiagnostics
    {
        /// <summary>
        /// Get the renderer type Unity is using (for diagnostics)
        /// 2 = D3D11, 18 = D3D12
        /// </summary>
        public static int GetRendererType()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try { return StreamlineReflexNative.SLReflex_GetRendererType(); }
            catch { return 0; }
#else
            return 0;
#endif
        }
        
        /// <summary>
        /// Check if a D3D12 device was captured
        /// </summary>
        public static bool HasD3D12Device()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try { return StreamlineReflexNative.SLReflex_HasD3D12Device(); }
            catch { return false; }
#else
            return false;
#endif
        }
        
        /// <summary>
        /// Check if a D3D11 device was captured
        /// </summary>
        public static bool HasD3D11Device()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try { return StreamlineReflexNative.SLReflex_HasD3D11Device(); }
            catch { return false; }
#else
            return false;
#endif
        }
        
        /// <summary>
        /// Get the last Streamline error code (0 = success, negative = error)
        /// </summary>
        public static int GetLastErrorCode()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try { return StreamlineReflexNative.SLReflex_GetLastErrorCode(); }
            catch { return -9999; }
#else
            return 0;
#endif
        }
        
        /// <summary>
        /// Get the last Streamline error message
        /// </summary>
        public static string GetLastErrorMessage()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try 
            { 
                IntPtr ptr = StreamlineReflexNative.SLReflex_GetLastErrorMessage();
                return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) : "Unknown error";
            }
            catch { return "Error getting message"; }
#else
            return "Not available on this platform";
#endif
        }
        
        /// <summary>
        /// Log full diagnostic info to console
        /// </summary>
        public static void LogDiagnostics()
        {
            Debug.Log("[StreamlineReflex] Diagnostics:");
            Debug.Log($"  Renderer Type: {GetRendererType()} (2=D3D11, 18=D3D12)");
            Debug.Log($"  Has D3D11 Device: {HasD3D11Device()}");
            Debug.Log($"  Has D3D12 Device: {HasD3D12Device()}");
            Debug.Log($"  Last Error Code: {GetLastErrorCode()}");
            Debug.Log($"  Last Error Message: {GetLastErrorMessage()}");
        }
    }
}
