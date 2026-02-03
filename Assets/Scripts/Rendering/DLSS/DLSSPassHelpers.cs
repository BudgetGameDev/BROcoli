using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using StreamlineDLSS;

/// <summary>
/// Pass data container for DLSS Render Graph system
/// </summary>
public class DLSSPassData
{
    public UnityEngine.Rendering.RenderGraphModule.TextureHandle colorTexture;
    public UnityEngine.Rendering.RenderGraphModule.TextureHandle depthTexture;
    public DLSSRenderFeature.DLSSSettings settings;
    public int frameCount;
    public Camera camera;
    public RenderTexture dlssOutputRT;
    public Matrix4x4 prevViewProjection;
    public bool firstFrame;
}

/// <summary>
/// Helper for managing DLSS output render texture
/// </summary>
public static class DLSSOutputManager
{
    /// <summary>
    /// Ensure DLSS output render texture exists at correct resolution
    /// </summary>
    public static RenderTexture EnsureOutputRT(RenderTexture existingRT, int width, int height, bool colorBuffersHDR, bool debugLogging)
    {
        try
        {
            if (existingRT != null && existingRT.width == width && existingRT.height == height)
                return existingRT;
            
            // Release old RT
            if (existingRT != null)
            {
                existingRT.Release();
                UnityEngine.Object.Destroy(existingRT);
            }
            
            if (width <= 0 || height <= 0)
            {
                Debug.LogError($"[DLSS] EnsureOutputRT: Invalid dimensions {width}x{height}");
                return null;
            }
            
            // Create new RT at full output resolution
            var desc = new RenderTextureDescriptor(width, height)
            {
                colorFormat = colorBuffersHDR ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32,
                depthBufferBits = 0,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true, // Required for DLSS to write to
                sRGB = !colorBuffersHDR
            };
            
            var rt = new RenderTexture(desc);
            rt.name = "DLSS Output";
            
            if (!rt.Create())
            {
                Debug.LogError($"[DLSS] Failed to create output RT: {width}x{height}");
                return null;
            }
            
            if (debugLogging)
            {
                Debug.Log($"[DLSS] Created output RT: {width}x{height}, format={desc.colorFormat}");
            }
            
            return rt;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DLSS] EnsureOutputRT exception: {e.Message}\n{e.StackTrace}");
            return null;
        }
    }
}

/// <summary>
/// Helper for DLSS camera constants calculation
/// </summary>
public static class DLSSCameraHelper
{
    /// <summary>
    /// Set DLSS camera constants from camera data
    /// </summary>
    public static void SetConstants(Camera camera, Matrix4x4 prevViewProjection, bool firstFrame)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
        Matrix4x4 viewProj = projMatrix * viewMatrix;
        Matrix4x4 invViewProj = viewProj.inverse;
        
        Matrix4x4 clipToPrevClip = firstFrame ? Matrix4x4.identity : prevViewProjection * invViewProj;
        Matrix4x4 prevClipToClip = clipToPrevClip.inverse;
        
        Vector2 jitterOffset = Vector2.zero;
        Vector2 mvecScale = new Vector2(camera.pixelWidth, camera.pixelHeight);
        
        StreamlineDLSSPlugin.SetConstants(
            projMatrix,
            projMatrix.inverse,
            clipToPrevClip,
            prevClipToClip,
            jitterOffset,
            mvecScale,
            camera.nearClipPlane,
            camera.farClipPlane,
            camera.fieldOfView * Mathf.Deg2Rad,
            camera.aspect,
            depthInverted: SystemInfo.usesReversedZBuffer,
            cameraMotionIncluded: true,
            reset: firstFrame
        );
#endif
    }
    
    /// <summary>
    /// Calculate and store view projection matrix for next frame
    /// </summary>
    public static Matrix4x4 GetCurrentViewProjection(Camera camera)
    {
        Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
        return projMatrix * viewMatrix;
    }
}
