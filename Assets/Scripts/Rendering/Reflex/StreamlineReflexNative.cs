using System;
using System.Runtime.InteropServices;

namespace StreamlineReflex
{
    /// <summary>
    /// Native P/Invoke declarations for Streamline Reflex plugin.
    /// Internal use only - use StreamlineReflexPlugin for public API.
    /// </summary>
    internal static class StreamlineReflexNative
    {
        internal const string DLL_NAME = "GfxPluginStreamline";

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void LogCallbackDelegate(string message);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SLReflex_SetLogCallback(IntPtr callback);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLReflex_IsAvailable();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLReflex_Initialize(IntPtr d3dDevice);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SLReflex_Shutdown();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLReflex_IsSupported();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLReflex_IsPCLSupported();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLReflex_SetMode(int mode);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SLReflex_GetMode();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLReflex_GetState(out bool lowLatencyAvailable, out bool flashIndicatorDriverControlled);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SLReflex_BeginFrame();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SLReflex_Sleep();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SLReflex_SetMarker(int marker);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SLReflex_MarkSimulationStart();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SLReflex_MarkSimulationEnd();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SLReflex_MarkRenderSubmitStart();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SLReflex_MarkRenderSubmitEnd();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SLReflex_MarkPresentStart();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SLReflex_MarkPresentEnd();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SLReflex_TriggerFlash();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLReflex_GetLatencyStats(out LatencyStats stats);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr SLReflex_GetRenderEventFunc();
        
        // Diagnostic functions
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLReflex_IsInitialized();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SLReflex_GetRendererType();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLReflex_HasD3D12Device();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLReflex_HasD3D11Device();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SLReflex_GetLastErrorCode();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr SLReflex_GetLastErrorMessage();
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SLReflex_TryInitialize();
#endif
    }
}
