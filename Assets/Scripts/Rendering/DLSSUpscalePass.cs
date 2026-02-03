using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Render pass that performs DLSS upscaling.
/// Tags resources and evaluates DLSS in a single pass.
/// </summary>
public class DLSSUpscalePass : ScriptableRenderPass, IDisposable
{
    private DLSSRenderFeature.DLSSSettings _settings;
    private Matrix4x4 _prevViewProjection;
#pragma warning disable CS0414 // Field assigned but not used (only used in Windows builds)
    private bool _firstFrame = true;
#pragma warning restore CS0414
    private ScriptableRenderer _renderer;
    private RenderingData _renderingData;
    
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
        _renderingData = renderingData;
    }
    
    [Obsolete]
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        var camera = renderingData.cameraData.camera;
        var cmd = CommandBufferPool.Get("DLSS Upscale");
        
        using (new ProfilingScope(cmd, profilingSampler))
        {
            // 1. Set viewport
            StreamlineDLSSPlugin.SetViewport(0);
            
            // 2. Set camera constants
            SetConstants(camera);
            
            // 3. Get render dimensions
            int renderWidth = camera.pixelWidth;
            int renderHeight = camera.pixelHeight;
            int outputWidth = Screen.width;
            int outputHeight = Screen.height;
            
            // 4. Set DLSS options
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
            
            // 5. Tag resources (depth, motion vectors, input color, output color)
            TagResources(renderingData.cameraData, renderWidth, renderHeight);
            
            // 6. Evaluate DLSS
            bool evalResult = StreamlineDLSSPlugin.Evaluate(IntPtr.Zero);
            
            if (_settings.debugLogging)
            {
                if (_firstFrame || !evalResult)
                {
                    Debug.Log($"[DLSS] Evaluate: {(evalResult ? "OK" : "FAILED")} " +
                              $"(Render: {renderWidth}x{renderHeight} → Output: {outputWidth}x{outputHeight})");
                }
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
        // For now using zero jitter as we're not using TAA
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
    
    private void TagResources(CameraData cameraData, int renderWidth, int renderHeight)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Get native texture pointers for DLSS buffer tagging
        
        // Try to get depth texture
        var depthTexture = Shader.GetGlobalTexture("_CameraDepthTexture");
        if (depthTexture != null)
        {
            IntPtr depthPtr = depthTexture.GetNativeTexturePtr();
            if (depthPtr != IntPtr.Zero)
            {
                // DXGI_FORMAT_D32_FLOAT = 40
                uint depthFormat = 40;
                StreamlineDLSSPlugin.TagResource(
                    depthPtr,
                    StreamlineDLSSPlugin.BufferType.Depth,
                    (uint)renderWidth,
                    (uint)renderHeight,
                    depthFormat,
                    D3D12_RESOURCE_STATE_DEPTH_READ
                );
                
                if (_settings.debugLogging && _firstFrame)
                {
                    Debug.Log($"[DLSS] Tagged depth buffer: {renderWidth}x{renderHeight}");
                }
            }
        }
        
        // Try to get motion vectors texture
        var motionTexture = Shader.GetGlobalTexture("_MotionVectorTexture");
        if (motionTexture != null)
        {
            IntPtr mvecPtr = motionTexture.GetNativeTexturePtr();
            if (mvecPtr != IntPtr.Zero)
            {
                // DXGI_FORMAT_R16G16_FLOAT = 34
                uint mvecFormat = 34;
                StreamlineDLSSPlugin.TagResource(
                    mvecPtr,
                    StreamlineDLSSPlugin.BufferType.MotionVectors,
                    (uint)renderWidth,
                    (uint)renderHeight,
                    mvecFormat,
                    D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE
                );
                
                if (_settings.debugLogging && _firstFrame)
                {
                    Debug.Log($"[DLSS] Tagged motion vectors: {renderWidth}x{renderHeight}");
                }
            }
        }
        else if (_settings.debugLogging && _firstFrame)
        {
            Debug.LogWarning("[DLSS] Motion vectors texture not available - enable in URP settings!");
        }
        
        // Try to get the current camera color target
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
                {
                    Debug.Log($"[DLSS] Tagged input color: {renderWidth}x{renderHeight}");
                }
            }
        }
#endif
    }
    
    public void Dispose()
    {
    }
}
