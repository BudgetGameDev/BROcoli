using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace StreamlineDLSS
{
    /// <summary>
    /// DLSS evaluation and render callback management.
    /// Handles issuing plugin events for GPU-based DLSS execution.
    /// </summary>
    public static class StreamlineDLSSEvaluate
    {
        // Cache the render callback to avoid repeated P/Invoke
        private static IntPtr _cachedRenderCallback = IntPtr.Zero;
        private static int _cachedEventID = 0;
        
        /// <summary>
        /// Get the DLSS render callback function pointer.
        /// </summary>
        public static IntPtr GetRenderCallback()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                return StreamlineDLSSNative.SLDLSS_GetRenderCallback();
            }
            catch (Exception e)
            {
                Debug.LogError($"[StreamlineDLSS] GetRenderCallback failed: {e.Message}");
                return IntPtr.Zero;
            }
#else
            return IntPtr.Zero;
#endif
        }
        
        /// <summary>
        /// Get the event ID for DLSS evaluation plugin event.
        /// </summary>
        public static int GetEvaluateEventID()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                return StreamlineDLSSNative.SLDLSS_GetEvaluateEventID();
            }
            catch
            {
                return 0;
            }
#else
            return 0;
#endif
        }
        
        /// <summary>
        /// Prepare DLSS for evaluation and issue a plugin event to run it on the render thread.
        /// This is the correct way to invoke DLSS as it provides access to the D3D12 command list.
        /// </summary>
        public static void IssueEvaluateEvent(CommandBuffer cmd)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                Debug.Log("[StreamlineDLSS] IssueEvaluateEvent called");
                
                if (!EnsureCallback())
                    return;
                
                // Increment frame ID before evaluation
                StreamlineDLSSNative.SLDLSS_BeginFrame();
                
                Debug.Log("[StreamlineDLSS] Calling PrepareEvaluate...");
                StreamlineDLSSNative.SLDLSS_PrepareEvaluate();
                
                Debug.Log($"[StreamlineDLSS] Issuing plugin event 0x{_cachedEventID:X}...");
                cmd.IssuePluginEvent(_cachedRenderCallback, _cachedEventID);
                Debug.Log("[StreamlineDLSS] Plugin event issued successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[StreamlineDLSS] IssueEvaluateEvent failed: {e.Message}\n{e.StackTrace}");
            }
#else
            Debug.LogWarning("[StreamlineDLSS] IssueEvaluateEvent: Not available on this platform");
#endif
        }
        
        /// <summary>
        /// Overload for UnsafeCommandBuffer (used in Render Graph passes).
        /// </summary>
        public static void IssueEvaluateEvent(UnsafeCommandBuffer cmd)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                Debug.Log("[StreamlineDLSS] IssueEvaluateEvent (UnsafeCommandBuffer) called");
                
                if (!EnsureCallback())
                    return;
                
                // Increment frame ID before evaluation
                StreamlineDLSSNative.SLDLSS_BeginFrame();
                
                Debug.Log("[StreamlineDLSS] Calling PrepareEvaluate...");
                StreamlineDLSSNative.SLDLSS_PrepareEvaluate();
                
                Debug.Log($"[StreamlineDLSS] Issuing plugin event 0x{_cachedEventID:X} on UnsafeCommandBuffer...");
                cmd.IssuePluginEvent(_cachedRenderCallback, _cachedEventID);
                Debug.Log("[StreamlineDLSS] Plugin event issued successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[StreamlineDLSS] IssueEvaluateEvent (UnsafeCommandBuffer) failed: {e.Message}\n{e.StackTrace}");
            }
#else
            Debug.LogWarning("[StreamlineDLSS] IssueEvaluateEvent: Not available on this platform");
#endif
        }
        
        /// <summary>
        /// Ensure render callback is cached and valid
        /// </summary>
        private static bool EnsureCallback()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_cachedRenderCallback == IntPtr.Zero)
            {
                Debug.Log("[StreamlineDLSS] Getting render callback from native plugin...");
                _cachedRenderCallback = StreamlineDLSSNative.SLDLSS_GetRenderCallback();
                _cachedEventID = StreamlineDLSSNative.SLDLSS_GetEvaluateEventID();
                Debug.Log($"[StreamlineDLSS] Got callback: 0x{_cachedRenderCallback.ToInt64():X}, eventID: 0x{_cachedEventID:X}");
            }
            
            if (_cachedRenderCallback == IntPtr.Zero)
            {
                Debug.LogError("[StreamlineDLSS] Failed to get render callback - pointer is null!");
                return false;
            }
            
            return true;
#else
            return false;
#endif
        }
        
        /// <summary>
        /// Clear cached callback (call if plugin is reloaded)
        /// </summary>
        public static void ClearCache()
        {
            _cachedRenderCallback = IntPtr.Zero;
            _cachedEventID = 0;
        }
    }
}
