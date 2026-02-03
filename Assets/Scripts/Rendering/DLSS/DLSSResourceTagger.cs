using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using StreamlineDLSS;

/// <summary>
/// Resource tagging for DLSS buffers.
/// Tags depth, motion vectors, color input, and output buffers.
/// </summary>
public static class DLSSResourceTagger
{
    // D3D12 resource states
    private const uint D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE = 0x80;
    private const uint D3D12_RESOURCE_STATE_DEPTH_READ = 0x20;
    private const uint D3D12_RESOURCE_STATE_UNORDERED_ACCESS = 0x8;
    
    /// <summary>
    /// Tag all required resources for DLSS in Render Graph path
    /// </summary>
    public static bool TagResourcesRenderGraph(DLSSPassData data)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            if (data.camera == null)
            {
                Debug.LogWarning("[DLSS-RG] TagResources: camera is null");
                return false;
            }
            
            int outputWidth = Screen.width;
            int outputHeight = Screen.height;
            int renderWidth = data.camera.pixelWidth;
            int renderHeight = data.camera.pixelHeight;
            
            if (renderWidth == 0 || renderHeight == 0 || outputWidth == 0 || outputHeight == 0)
            {
                Debug.LogWarning($"[DLSS-RG] TagResources: invalid dimensions render={renderWidth}x{renderHeight} output={outputWidth}x{outputHeight}");
                return false;
            }
            
            bool hasRequiredResources = false;
            
            // Tag depth (required)
            hasRequiredResources |= TagDepth(renderWidth, renderHeight);
            
            // Tag motion vectors (optional but recommended)
            TagMotionVectors(renderWidth, renderHeight);
            
            // Tag input color (required)
            hasRequiredResources |= TagInputColor(renderWidth, renderHeight, data.settings.colorBuffersHDR);
            
            // Tag output (required)
            if (!TagOutput(data.dlssOutputRT, outputWidth, outputHeight, data.settings.colorBuffersHDR))
            {
                return false;
            }
            
            return hasRequiredResources;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DLSS-RG] TagResources exception: {e.Message}");
            return false;
        }
#else
        return false;
#endif
    }
    
    /// <summary>
    /// Tag all required resources for DLSS in legacy path
    /// </summary>
    public static bool TagResourcesLegacy(
        CameraData cameraData,
        RenderTexture dlssOutputRT,
        int renderWidth,
        int renderHeight,
        int outputWidth,
        int outputHeight,
        bool colorBuffersHDR,
        bool debugLogging,
        bool firstFrame)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        bool allTagged = true;
        
        // Tag depth
        if (!TagDepth(renderWidth, renderHeight))
        {
            allTagged = false;
            if (debugLogging && firstFrame)
                Debug.LogWarning("[DLSS] Depth texture not available!");
        }
        else if (debugLogging && firstFrame)
        {
            Debug.Log($"[DLSS] Tagged depth: {renderWidth}x{renderHeight}");
        }
        
        // Tag motion vectors
        if (!TagMotionVectors(renderWidth, renderHeight))
        {
            if (debugLogging && firstFrame)
                Debug.LogWarning("[DLSS] Motion vectors not available - enable in URP settings for best quality!");
        }
        else if (debugLogging && firstFrame)
        {
            Debug.Log($"[DLSS] Tagged motion vectors: {renderWidth}x{renderHeight}");
        }
        
        // Tag input color
        if (!TagInputColor(renderWidth, renderHeight, colorBuffersHDR))
        {
            allTagged = false;
            if (debugLogging && firstFrame)
                Debug.LogWarning("[DLSS] Camera color texture not available!");
        }
        else if (debugLogging && firstFrame)
        {
            Debug.Log($"[DLSS] Tagged input color: {renderWidth}x{renderHeight}");
        }
        
        // Tag output
        if (!TagOutput(dlssOutputRT, outputWidth, outputHeight, colorBuffersHDR))
        {
            allTagged = false;
            if (debugLogging)
                Debug.LogError("[DLSS] Failed to tag output RT!");
        }
        else if (debugLogging && firstFrame)
        {
            Debug.Log($"[DLSS] Tagged output color: {outputWidth}x{outputHeight}");
        }
        
        return allTagged;
#else
        return false;
#endif
    }
    
    private static bool TagDepth(int width, int height)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        var depthTexture = Shader.GetGlobalTexture("_CameraDepthTexture");
        if (depthTexture != null)
        {
            IntPtr depthPtr = depthTexture.GetNativeTexturePtr();
            if (depthPtr != IntPtr.Zero)
            {
                StreamlineDLSSPlugin.TagResource(depthPtr, BufferType.Depth,
                    (uint)width, (uint)height, DXGIFormats.D32_FLOAT, D3D12_RESOURCE_STATE_DEPTH_READ);
                return true;
            }
        }
#endif
        return false;
    }
    
    private static bool TagMotionVectors(int width, int height)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        var motionTexture = Shader.GetGlobalTexture("_MotionVectorTexture");
        if (motionTexture != null)
        {
            IntPtr mvecPtr = motionTexture.GetNativeTexturePtr();
            if (mvecPtr != IntPtr.Zero)
            {
                StreamlineDLSSPlugin.TagResource(mvecPtr, BufferType.MotionVectors,
                    (uint)width, (uint)height, DXGIFormats.R16G16_FLOAT, D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE);
                return true;
            }
        }
#endif
        return false;
    }
    
    private static bool TagInputColor(int width, int height, bool hdr)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        var colorTexture = Shader.GetGlobalTexture("_CameraColorTexture");
        if (colorTexture != null)
        {
            IntPtr colorPtr = colorTexture.GetNativeTexturePtr();
            if (colorPtr != IntPtr.Zero)
            {
                uint format = hdr ? DXGIFormats.R16G16B16A16_FLOAT : DXGIFormats.R8G8B8A8_UNORM;
                StreamlineDLSSPlugin.TagResource(colorPtr, BufferType.ScalingInputColor,
                    (uint)width, (uint)height, format, D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE);
                return true;
            }
        }
#endif
        return false;
    }
    
    private static bool TagOutput(RenderTexture outputRT, int width, int height, bool hdr)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (outputRT != null)
        {
            IntPtr outputPtr = outputRT.GetNativeTexturePtr();
            if (outputPtr != IntPtr.Zero)
            {
                uint format = hdr ? DXGIFormats.R16G16B16A16_FLOAT : DXGIFormats.R8G8B8A8_UNORM;
                StreamlineDLSSPlugin.TagResource(outputPtr, BufferType.ScalingOutputColor,
                    (uint)width, (uint)height, format, D3D12_RESOURCE_STATE_UNORDERED_ACCESS);
                return true;
            }
            Debug.LogWarning("[DLSS] TagOutput: native ptr is null");
        }
        else
        {
            Debug.LogWarning("[DLSS] TagOutput: outputRT is null");
        }
#endif
        return false;
    }
}
