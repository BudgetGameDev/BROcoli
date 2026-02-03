using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Render pass that performs DLSS upscaling.
/// 
/// DLSS flow:
/// 1. Unity renders at lower resolution (renderScale applied by DLSSRenderScaleManager)
/// 2. This pass tags the lower-res input and full-res output buffers
/// 3. DLSS evaluates and writes upscaled result to output buffer
/// 4. Output buffer is blitted to screen
/// </summary>
public class DLSSUpscalePass : ScriptableRenderPass, IDisposable
{
    private DLSSRenderFeature.DLSSSettings _settings;
    private Matrix4x4 _prevViewProjection;
    private bool _firstFrame = true;
    private ScriptableRenderer _renderer;
    
    // Output render texture at full resolution for DLSS to write to
    private RenderTexture _dlssOutputRT;
    private int _lastOutputWidth;
    private int _lastOutputHeight;
    
    // D3D12 resource states
    private const uint D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE = 0x80;
    private const uint D3D12_RESOURCE_STATE_DEPTH_READ = 0x20;
    private const uint D3D12_RESOURCE_STATE_RENDER_TARGET = 0x4;
    private const uint D3D12_RESOURCE_STATE_UNORDERED_ACCESS = 0x8;
    
    public DLSSUpscalePass(DLSSRenderFeature.DLSSSettings settings)
    {
        _settings = settings;
        profilingSampler = new ProfilingSampler("DLSS Upscale");
    }
    
    public void Setup(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        _renderer = renderer;
    }
    
    /// <summary>
    /// Ensure DLSS output render texture exists at correct resolution
    /// </summary>
    private void EnsureOutputRT(int width, int height)
    {
        if (_dlssOutputRT != null && _dlssOutputRT.width == width && _dlssOutputRT.height == height)
            return;
        
        // Release old RT
        if (_dlssOutputRT != null)
        {
            _dlssOutputRT.Release();
            UnityEngine.Object.Destroy(_dlssOutputRT);
        }
        
        // Create new RT at full output resolution
        var desc = new RenderTextureDescriptor(width, height)
        {
            colorFormat = _settings.colorBuffersHDR ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32,
            depthBufferBits = 0,
            msaaSamples = 1,
            useMipMap = false,
            autoGenerateMips = false,
            enableRandomWrite = true, // Required for DLSS to write to
            sRGB = !_settings.colorBuffersHDR
        };
        
        _dlssOutputRT = new RenderTexture(desc);
        _dlssOutputRT.name = "DLSS Output";
        _dlssOutputRT.Create();
        
        _lastOutputWidth = width;
        _lastOutputHeight = height;
        
        if (_settings.debugLogging)
        {
            Debug.Log($"[DLSS] Created output RT: {width}x{height}, format={desc.colorFormat}");
        }
    }
    
    [Obsolete]
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        var camera = renderingData.cameraData.camera;
        var cmd = CommandBufferPool.Get("DLSS Upscale");
        
        using (new ProfilingScope(cmd, profilingSampler))
        {
            // Get render and output dimensions
            // renderWidth/Height = what Unity rendered at (after renderScale)
            // outputWidth/Height = final display resolution
            int renderWidth = camera.pixelWidth;
            int renderHeight = camera.pixelHeight;
            int outputWidth = Screen.width;
            int outputHeight = Screen.height;
            
            // Ensure we have an output RT at the correct resolution
            EnsureOutputRT(outputWidth, outputHeight);
            
            // 1. Set viewport
            StreamlineDLSSPlugin.SetViewport(0);
            
            // 2. Set camera constants
            SetConstants(camera);
            
            // 3. Set DLSS options
            bool optionsSet = StreamlineDLSSPlugin.SetOptions(
                _settings.dlssMode,
                (uint)outputWidth,
                (uint)outputHeight,
                _settings.colorBuffersHDR
            );
            
            if (_settings.debugLogging && !optionsSet)
            {
                Debug.LogWarning("[DLSS] SetOptions failed");
            }
            
            // 4. Tag all required resources
            bool resourcesTagged = TagResources(renderingData.cameraData, renderWidth, renderHeight, outputWidth, outputHeight);
            
            if (!resourcesTagged)
            {
                if (_settings.debugLogging)
                    Debug.LogWarning("[DLSS] Failed to tag all required resources");
            }
            
            // 5. Execute command buffer to ensure resources are in correct state
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            // 6. Evaluate DLSS (this runs the upscaling)
            bool evalResult = StreamlineDLSSPlugin.Evaluate(IntPtr.Zero);
            
            if (_settings.debugLogging)
            {
                if (_firstFrame || !evalResult)
                {
                    Debug.Log($"[DLSS] Evaluate: {(evalResult ? "OK" : "FAILED")} " +
                              $"(Render: {renderWidth}x{renderHeight} → Output: {outputWidth}x{outputHeight})");
                }
            }
            
            // 7. Blit DLSS output to screen
            if (evalResult && _dlssOutputRT != null)
            {
                cmd.Blit(_dlssOutputRT, BuiltinRenderTextureType.CameraTarget);
            }
            
            _firstFrame = false;
        }
        
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
#endif
    }
    
    private void SetConstants(Camera camera)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Calculate camera matrices
        Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
        Matrix4x4 viewProj = projMatrix * viewMatrix;
        Matrix4x4 invViewProj = viewProj.inverse;
        
        // Calculate clip-to-prev-clip for temporal stability
        Matrix4x4 clipToPrevClip = _firstFrame ? Matrix4x4.identity : _prevViewProjection * invViewProj;
        Matrix4x4 prevClipToClip = clipToPrevClip.inverse;
        
        // Jitter offset - DLSS needs this for temporal stability
        // Without TAA, we use zero jitter but DLSS can still work
        Vector2 jitterOffset = Vector2.zero;
        
        // Motion vector scale - converts from screen UV to pixel space
        Vector2 mvecScale = new Vector2(camera.pixelWidth, camera.pixelHeight);
        
        bool success = StreamlineDLSSPlugin.SetConstants(
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
            reset: _firstFrame
        );
        
        if (_settings.debugLogging && !success)
        {
            Debug.LogWarning("[DLSS] SetConstants failed");
        }
        
        // Store for next frame
        _prevViewProjection = viewProj;
#endif
    }
    
    private bool TagResources(CameraData cameraData, int renderWidth, int renderHeight, int outputWidth, int outputHeight)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        bool allTagged = true;
        
        // === DEPTH BUFFER ===
        var depthTexture = Shader.GetGlobalTexture("_CameraDepthTexture");
        if (depthTexture != null)
        {
            IntPtr depthPtr = depthTexture.GetNativeTexturePtr();
            if (depthPtr != IntPtr.Zero)
            {
                // DXGI_FORMAT_D32_FLOAT = 40
                StreamlineDLSSPlugin.TagResource(
                    depthPtr,
                    StreamlineDLSSPlugin.BufferType.Depth,
                    (uint)renderWidth,
                    (uint)renderHeight,
                    40,
                    D3D12_RESOURCE_STATE_DEPTH_READ
                );
                
                if (_settings.debugLogging && _firstFrame)
                    Debug.Log($"[DLSS] Tagged depth: {renderWidth}x{renderHeight}");
            }
            else allTagged = false;
        }
        else 
        {
            allTagged = false;
            if (_settings.debugLogging && _firstFrame)
                Debug.LogWarning("[DLSS] Depth texture not available!");
        }
        
        // === MOTION VECTORS ===
        var motionTexture = Shader.GetGlobalTexture("_MotionVectorTexture");
        if (motionTexture != null)
        {
            IntPtr mvecPtr = motionTexture.GetNativeTexturePtr();
            if (mvecPtr != IntPtr.Zero)
            {
                // DXGI_FORMAT_R16G16_FLOAT = 34
                StreamlineDLSSPlugin.TagResource(
                    mvecPtr,
                    StreamlineDLSSPlugin.BufferType.MotionVectors,
                    (uint)renderWidth,
                    (uint)renderHeight,
                    34,
                    D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE
                );
                
                if (_settings.debugLogging && _firstFrame)
                    Debug.Log($"[DLSS] Tagged motion vectors: {renderWidth}x{renderHeight}");
            }
            else allTagged = false;
        }
        else
        {
            // Motion vectors are important but DLSS can work without them (quality suffers)
            if (_settings.debugLogging && _firstFrame)
                Debug.LogWarning("[DLSS] Motion vectors not available - enable in URP settings for best quality!");
        }
        
        // === INPUT COLOR (lower resolution rendered image) ===
        var colorTexture = Shader.GetGlobalTexture("_CameraColorTexture");
        if (colorTexture != null)
        {
            IntPtr colorPtr = colorTexture.GetNativeTexturePtr();
            if (colorPtr != IntPtr.Zero)
            {
                // DXGI_FORMAT_R16G16B16A16_FLOAT = 10 (HDR), DXGI_FORMAT_R8G8B8A8_UNORM = 28 (SDR)
                uint colorFormat = _settings.colorBuffersHDR ? (uint)10 : (uint)28;
                StreamlineDLSSPlugin.TagResource(
                    colorPtr,
                    StreamlineDLSSPlugin.BufferType.ScalingInputColor,
                    (uint)renderWidth,
                    (uint)renderHeight,
                    colorFormat,
                    D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE
                );
                
                if (_settings.debugLogging && _firstFrame)
                    Debug.Log($"[DLSS] Tagged input color: {renderWidth}x{renderHeight}");
            }
            else allTagged = false;
        }
        else
        {
            allTagged = false;
            if (_settings.debugLogging && _firstFrame)
                Debug.LogWarning("[DLSS] Camera color texture not available!");
        }
        
        // === OUTPUT COLOR (full resolution - DLSS writes upscaled result here) ===
        if (_dlssOutputRT != null)
        {
            IntPtr outputPtr = _dlssOutputRT.GetNativeTexturePtr();
            if (outputPtr != IntPtr.Zero)
            {
                // DXGI_FORMAT_R16G16B16A16_FLOAT = 10 (HDR), DXGI_FORMAT_R8G8B8A8_UNORM = 28 (SDR)
                uint outputFormat = _settings.colorBuffersHDR ? (uint)10 : (uint)28;
                StreamlineDLSSPlugin.TagResource(
                    outputPtr,
                    StreamlineDLSSPlugin.BufferType.ScalingOutputColor,
                    (uint)outputWidth,
                    (uint)outputHeight,
                    outputFormat,
                    D3D12_RESOURCE_STATE_UNORDERED_ACCESS
                );
                
                if (_settings.debugLogging && _firstFrame)
                    Debug.Log($"[DLSS] Tagged output color: {outputWidth}x{outputHeight}");
            }
            else
            {
                allTagged = false;
                if (_settings.debugLogging)
                    Debug.LogError("[DLSS] Failed to get native pointer for output RT!");
            }
        }
        else
        {
            allTagged = false;
            if (_settings.debugLogging)
                Debug.LogError("[DLSS] Output RT is null!");
        }
        
        return allTagged;
#else
        return false;
#endif
    }
    
    public void Dispose()
    {
        if (_dlssOutputRT != null)
        {
            _dlssOutputRT.Release();
            UnityEngine.Object.Destroy(_dlssOutputRT);
            _dlssOutputRT = null;
        }
    }
}
