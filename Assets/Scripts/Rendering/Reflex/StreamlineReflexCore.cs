using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace StreamlineReflex
{
    /// <summary>
    /// Core Reflex functionality: initialization, support checks, and mode management.
    /// </summary>
    public static class StreamlineReflexCore
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static StreamlineReflexNative.LogCallbackDelegate _logCallback;
        private static GCHandle _logCallbackHandle;
#endif
        private static bool _initialized = false;
        private static bool _available = false;
        private static bool _checkedAvailability = false;
        
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
                    _available = StreamlineReflexNative.SLReflex_IsAvailable();
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
                StreamlineReflexNative.SLReflex_SetLogCallback(
                    Marshal.GetFunctionPointerForDelegate(_logCallback));
                
                // Initialize with null device (Streamline will get it from DirectX)
                _initialized = StreamlineReflexNative.SLReflex_Initialize(IntPtr.Zero);
                
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
            if (!IsStreamlineInitialized() && !_initialized) return;
            try
            {
                StreamlineReflexNative.SLReflex_Shutdown();
                
                if (_logCallbackHandle.IsAllocated)
                    _logCallbackHandle.Free();
                
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
            if (!IsStreamlineInitialized()) return false;
            try { return StreamlineReflexNative.SLReflex_IsSupported(); }
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
            if (!IsStreamlineInitialized()) return false;
            try { return StreamlineReflexNative.SLReflex_IsPCLSupported(); }
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
            try { return StreamlineReflexNative.SLReflex_IsInitialized(); }
            catch { return false; }
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
            if (!IsStreamlineInitialized()) return false;
            try
            {
                bool result = StreamlineReflexNative.SLReflex_SetMode((int)mode);
                if (result)
                {
                    _initialized = true;
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
            if (!IsStreamlineInitialized()) return ReflexMode.Off;
            try { return (ReflexMode)StreamlineReflexNative.SLReflex_GetMode(); }
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
            if (!IsStreamlineInitialized()) return false;
            try { return StreamlineReflexNative.SLReflex_GetState(out lowLatencyAvailable, out flashIndicatorDriverControlled); }
            catch { return false; }
#else
            return false;
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
                bool result = StreamlineReflexNative.SLReflex_TryInitialize();
                if (result)
                    _initialized = true;
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
    }
}
