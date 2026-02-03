using System;
using UnityEngine;

namespace StreamlineDLSS
{
    /// <summary>
    /// Buffer tagging and resource management for DLSS.
    /// </summary>
    public static class StreamlineDLSSBuffers
    {
        /// <summary>
        /// Set the viewport ID for DLSS operations
        /// </summary>
        public static void SetViewport(uint viewportId)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try { StreamlineDLSSNative.SLDLSS_SetViewport(viewportId); }
            catch (Exception e) { Debug.LogError($"[StreamlineDLSS] SetViewport failed: {e.Message}"); }
#endif
        }
        
        /// <summary>
        /// Set DLSS constants including camera matrices and jitter
        /// </summary>
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
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                return StreamlineDLSSNative.SLDLSS_SetConstants(
                    MatrixHelper.ToArray(cameraViewToClip),
                    MatrixHelper.ToArray(clipToCameraView),
                    MatrixHelper.ToArray(clipToPrevClip),
                    MatrixHelper.ToArray(prevClipToClip),
                    jitterOffset.x, jitterOffset.y,
                    mvecScale.x, mvecScale.y,
                    cameraNear, cameraFar,
                    cameraFOV, cameraAspectRatio,
                    depthInverted,
                    cameraMotionIncluded,
                    reset
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"[StreamlineDLSS] SetConstants failed: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }
        
        /// <summary>
        /// Tag a D3D12 resource for DLSS
        /// </summary>
        public static bool TagResource(IntPtr d3d12Resource, BufferType bufferType, uint width, uint height, uint format, uint state)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                return StreamlineDLSSNative.SLDLSS_TagResourceD3D12(d3d12Resource, (uint)bufferType, width, height, format, state);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StreamlineDLSS] TagResource failed: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }
        
        /// <summary>
        /// Configure DLSS options
        /// </summary>
        public static bool SetOptions(DLSSMode mode, uint outputWidth, uint outputHeight, bool colorBuffersHDR = true)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                return StreamlineDLSSNative.SLDLSS_SetOptions((int)mode, outputWidth, outputHeight, colorBuffersHDR);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StreamlineDLSS] SetOptions failed: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }
        
        /// <summary>
        /// Evaluate DLSS (run upscaling) - call after tagging all resources
        /// </summary>
        public static bool Evaluate(IntPtr commandBuffer = default)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                return StreamlineDLSSNative.SLDLSS_Evaluate(commandBuffer);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StreamlineDLSS] Evaluate failed: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }
        
        /// <summary>
        /// Configure Frame Generation options
        /// </summary>
        public static bool SetFrameGenOptions(DLSSGMode mode, uint numFramesToGenerate, uint colorWidth, uint colorHeight, uint mvecDepthWidth, uint mvecDepthHeight)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                return StreamlineDLSSNative.SLDLSSG_SetOptions((int)mode, numFramesToGenerate, colorWidth, colorHeight, mvecDepthWidth, mvecDepthHeight);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StreamlineDLSS] SetFrameGenOptions failed: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }
        
        /// <summary>
        /// Tag HUD-less color buffer for Frame Generation
        /// </summary>
        public static bool TagHUDLessColor(IntPtr d3d12Resource, uint width, uint height, uint format, uint state)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                return StreamlineDLSSNative.SLDLSSG_TagHUDLessColor(d3d12Resource, width, height, format, state);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StreamlineDLSS] TagHUDLessColor failed: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }
        
        /// <summary>
        /// Tag UI color+alpha buffer for Frame Generation
        /// </summary>
        public static bool TagUIColorAndAlpha(IntPtr d3d12Resource, uint width, uint height, uint format, uint state)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                return StreamlineDLSSNative.SLDLSSG_TagUIColorAndAlpha(d3d12Resource, width, height, format, state);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StreamlineDLSS] TagUIColorAndAlpha failed: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }
    }
    
    /// <summary>
    /// Helper for matrix conversion
    /// </summary>
    internal static class MatrixHelper
    {
        /// <summary>
        /// Convert Unity Matrix4x4 to float array (row-major for Streamline)
        /// </summary>
        public static float[] ToArray(Matrix4x4 m)
        {
            // Unity uses column-major, Streamline uses row-major - transpose when passing
            return new float[]
            {
                m.m00, m.m10, m.m20, m.m30,
                m.m01, m.m11, m.m21, m.m31,
                m.m02, m.m12, m.m22, m.m32,
                m.m03, m.m13, m.m23, m.m33
            };
        }
    }
}
