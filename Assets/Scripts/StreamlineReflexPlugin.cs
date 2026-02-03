using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Native plugin wrapper for NVIDIA Streamline Reflex SDK.
/// 
/// This class provides C# bindings to the GfxPluginStreamline native DLL.
/// The plugin integrates NVIDIA's Streamline SDK for Reflex Low Latency Mode.
/// 
/// The plugin uses Unity's native plugin interface (UnityPluginLoad) to automatically
/// capture the D3D12/D3D11 device, so no manual device passing is required.
/// 
/// Setup:
/// 1. Run build-reflex-plugin.ps1 to build the native plugin
/// 2. Download Streamline SDK release from GitHub
/// 3. Copy required DLLs to Assets/Plugins/x86_64/
/// 
/// Required DLLs:
/// - GfxPluginStreamline.dll (built by this project)
/// - sl.interposer.dll (from Streamline SDK)
/// - sl.reflex.dll (from Streamline SDK)
/// - sl.dlss.dll, sl.dlss_g.dll (for DLSS/Frame Gen)
/// - nvngx_dlss.dll, nvngx_dlssg.dll (NVIDIA neural network models)
/// </summary>
public static class StreamlineReflexPlugin
{
    // GfxPlugin* prefix ensures Unity loads this plugin early in the graphics pipeline
    private const string DLL_NAME = "GfxPluginStreamline";
    
    /// <summary>
    /// Reflex latency mode settings
    /// </summary>
    public enum ReflexMode
    {
        Off = 0,
        LowLatency = 1,           // Low Latency Mode
        LowLatencyWithBoost = 2   // Low Latency + Boost (increases GPU clocks when CPU-bound)
    }
    
    /// <summary>
    /// PCL Markers for latency measurement
    /// </summary>
    public enum PCLMarker
    {
        SimulationStart = 0,
        SimulationEnd = 1,
        RenderSubmitStart = 2,
        RenderSubmitEnd = 3,
        PresentStart = 4,
        PresentEnd = 5,
        TriggerFlash = 7,
        PCLatencyPing = 8
    }
    
    /// <summary>
    /// Latency statistics from Reflex
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LatencyStats
    {
        public float SimulationMs;
        public float RenderSubmitMs;
        public float PresentMs;
        public float DriverMs;
        public float OsRenderQueueMs;
        public float GpuRenderMs;
        public float TotalLatencyMs;
    }
    
    // ==================== Native Function Imports ====================
    
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void LogCallbackDelegate(string message);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SLReflex_SetLogCallback(IntPtr callback);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLReflex_IsAvailable();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLReflex_Initialize(IntPtr d3dDevice);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SLReflex_Shutdown();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLReflex_IsSupported();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLReflex_IsPCLSupported();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLReflex_SetMode(int mode);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int SLReflex_GetMode();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLReflex_GetState(out bool lowLatencyAvailable, out bool flashIndicatorDriverControlled);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SLReflex_BeginFrame();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SLReflex_Sleep();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SLReflex_SetMarker(int marker);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SLReflex_MarkSimulationStart();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SLReflex_MarkSimulationEnd();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SLReflex_MarkRenderSubmitStart();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SLReflex_MarkRenderSubmitEnd();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SLReflex_MarkPresentStart();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SLReflex_MarkPresentEnd();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SLReflex_TriggerFlash();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLReflex_GetLatencyStats(out LatencyStats stats);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SLReflex_GetRenderEventFunc();
    
    // New diagnostic functions
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLReflex_IsInitialized();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int SLReflex_GetRendererType();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLReflex_HasD3D12Device();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLReflex_HasD3D11Device();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern int SLReflex_GetLastErrorCode();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SLReflex_GetLastErrorMessage();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SLReflex_TryInitialize();
    
    private static LogCallbackDelegate _logCallback;
    private static GCHandle _logCallbackHandle;
#endif

    private static bool _initialized = false;
    private static bool _available = false;
    private static bool _checkedAvailability = false;
    
    // ==================== Public API ====================
    
    /// <summary>
    /// Check if the Streamline Reflex plugin DLLs are available
    /// </summary>
    public static bool IsAvailable()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (!_checkedAvailability)
        {
            _checkedAvailability = true;
            try
            {
                _available = SLReflex_IsAvailable();
            }
            catch (DllNotFoundException)
            {
                _available = false;
                Debug.Log("[StreamlineReflex] Plugin DLL not found - Reflex not available");
            }
            catch (Exception e)
            {
                _available = false;
                Debug.LogWarning($"[StreamlineReflex] Error checking availability: {e.Message}");
            }
        }
        return _available;
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Initialize Streamline Reflex. Call once at startup.
    /// </summary>
    public static bool Initialize()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (_initialized) return true;
        if (!IsAvailable()) return false;
        
        try
        {
            // Set up logging callback
            _logCallback = (message) => Debug.Log($"[StreamlineReflex Native] {message}");
            _logCallbackHandle = GCHandle.Alloc(_logCallback);
            SLReflex_SetLogCallback(Marshal.GetFunctionPointerForDelegate(_logCallback));
            
            // Initialize with null device (Streamline will get it from DirectX)
            _initialized = SLReflex_Initialize(IntPtr.Zero);
            
            if (_initialized)
            {
                Debug.Log("[StreamlineReflex] ✓ Initialized successfully");
                Debug.Log($"[StreamlineReflex]   Reflex supported: {IsReflexSupported()}");
                Debug.Log($"[StreamlineReflex]   PCL Stats supported: {IsPCLSupported()}");
            }
            else
            {
                Debug.LogWarning("[StreamlineReflex] Initialization failed");
            }
            
            return _initialized;
        }
        catch (Exception e)
        {
            Debug.LogError($"[StreamlineReflex] Initialize failed: {e.Message}");
            return false;
        }
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Shutdown Streamline Reflex. Call when application exits.
    /// </summary>
    public static void Shutdown()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Check native initialization state (native auto-initializes)
        if (!IsStreamlineInitialized() && !_initialized) return;
        try
        {
            SLReflex_Shutdown();
            
            if (_logCallbackHandle.IsAllocated)
            {
                _logCallbackHandle.Free();
            }
            
            _initialized = false;
            Debug.Log("[StreamlineReflex] Shut down");
        }
        catch { }
#endif
    }
    
    /// <summary>
    /// Check if Reflex Low Latency is supported on this hardware
    /// </summary>
    public static bool IsReflexSupported()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Check native initialization state, not C# flag (native auto-initializes)
        if (!IsStreamlineInitialized()) return false;
        try { return SLReflex_IsSupported(); }
        catch { return false; }
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Check if PCL Stats (latency measurement) is supported
    /// </summary>
    public static bool IsPCLSupported()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Check native initialization state, not C# flag (native auto-initializes)
        if (!IsStreamlineInitialized()) return false;
        try { return SLReflex_IsPCLSupported(); }
        catch { return false; }
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Check if Streamline is fully initialized with a D3D device
    /// </summary>
    public static bool IsStreamlineInitialized()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try { return SLReflex_IsInitialized(); }
        catch { return false; }
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Get the renderer type Unity is using (for diagnostics)
    /// 2 = D3D11, 18 = D3D12
    /// </summary>
    public static int GetRendererType()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try { return SLReflex_GetRendererType(); }
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
        try { return SLReflex_HasD3D12Device(); }
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
        try { return SLReflex_HasD3D11Device(); }
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
        try { return SLReflex_GetLastErrorCode(); }
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
            IntPtr ptr = SLReflex_GetLastErrorMessage();
            return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) : "Unknown error";
        }
        catch { return "Error getting message"; }
#else
        return "Not available on this platform";
#endif
    }
    
    /// <summary>
    /// Try to manually initialize Streamline (for debugging)
    /// </summary>
    public static bool TryInitialize()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try 
        { 
            bool result = SLReflex_TryInitialize();
            if (result)
            {
                _initialized = true;
            }
            return result;
        }
        catch (Exception e)
        { 
            Debug.LogError($"[StreamlineReflex] TryInitialize failed: {e.Message}");
            return false; 
        }
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Set the Reflex latency mode
    /// </summary>
    public static bool SetMode(ReflexMode mode)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Check native initialization state, not C# flag (native auto-initializes)
        if (!IsStreamlineInitialized()) return false;
        try
        {
            bool result = SLReflex_SetMode((int)mode);
            if (result)
            {
                _initialized = true; // Sync C# flag for compatibility
                Debug.Log($"[StreamlineReflex] Mode set to: {mode}");
            }
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"[StreamlineReflex] SetMode failed: {e.Message}");
            return false;
        }
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Get current Reflex mode
    /// </summary>
    public static ReflexMode GetMode()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Check native initialization state, not C# flag (native auto-initializes)
        if (!IsStreamlineInitialized()) return ReflexMode.Off;
        try { return (ReflexMode)SLReflex_GetMode(); }
        catch { return ReflexMode.Off; }
#else
        return ReflexMode.Off;
#endif
    }
    
    /// <summary>
    /// Get Reflex state information
    /// </summary>
    public static bool GetState(out bool lowLatencyAvailable, out bool flashIndicatorDriverControlled)
    {
        lowLatencyAvailable = false;
        flashIndicatorDriverControlled = false;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Check native initialization state, not C# flag (native auto-initializes)
        if (!IsStreamlineInitialized()) return false;
        try { return SLReflex_GetState(out lowLatencyAvailable, out flashIndicatorDriverControlled); }
        catch { return false; }
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Call at the beginning of each frame before processing input
    /// </summary>
    public static void BeginFrame()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Check native initialization state, not C# flag (native auto-initializes)
        if (!IsStreamlineInitialized()) return;
        try { SLReflex_BeginFrame(); } catch { }
#endif
    }
    
    /// <summary>
    /// Call Reflex Sleep to synchronize CPU-GPU timing for lowest latency.
    /// Call this at the start of your frame, before processing input.
    /// </summary>
    public static void Sleep()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (!IsStreamlineInitialized()) return;
        try { SLReflex_Sleep(); } catch { }
#endif
    }
    
    /// <summary>
    /// Set a PCL timing marker
    /// </summary>
    public static void SetMarker(PCLMarker marker)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (!IsStreamlineInitialized()) return;
        try { SLReflex_SetMarker((int)marker); } catch { }
#endif
    }
    
    /// <summary>
    /// Mark the start of simulation/game logic
    /// </summary>
    public static void MarkSimulationStart()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (!IsStreamlineInitialized()) return;
        try { SLReflex_MarkSimulationStart(); } catch { }
#endif
    }
    
    /// <summary>
    /// Mark the end of simulation/game logic
    /// </summary>
    public static void MarkSimulationEnd()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (!IsStreamlineInitialized()) return;
        try { SLReflex_MarkSimulationEnd(); } catch { }
#endif
    }
    
    /// <summary>
    /// Mark the start of render submission
    /// </summary>
    public static void MarkRenderSubmitStart()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (!IsStreamlineInitialized()) return;
        try { SLReflex_MarkRenderSubmitStart(); } catch { }
#endif
    }
    
    /// <summary>
    /// Mark the end of render submission
    /// </summary>
    public static void MarkRenderSubmitEnd()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (!IsStreamlineInitialized()) return;
        try { SLReflex_MarkRenderSubmitEnd(); } catch { }
#endif
    }
    
    /// <summary>
    /// Trigger the Reflex Flash Indicator (for testing latency)
    /// </summary>
    public static void TriggerFlash()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (!IsStreamlineInitialized()) return;
        try { SLReflex_TriggerFlash(); } catch { }
#endif
    }
    
    /// <summary>
    /// Get latency statistics (requires PCL markers to be set)
    /// </summary>
    public static bool GetLatencyStats(out LatencyStats stats)
    {
        stats = default;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (!IsStreamlineInitialized()) return false;
        try { return SLReflex_GetLatencyStats(out stats); }
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
        if (!IsStreamlineInitialized()) return IntPtr.Zero;
        try { return SLReflex_GetRenderEventFunc(); }
        catch { return IntPtr.Zero; }
#else
        return IntPtr.Zero;
#endif
    }
}
