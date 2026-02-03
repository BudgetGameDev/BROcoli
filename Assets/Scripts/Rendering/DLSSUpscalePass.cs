using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

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
    
    // Frame counter for debug logging
    private int _frameCount = 0;
    
    public DLSSUpscalePass(DLSSRenderFeature.DLSSSettings settings)
    {
        _settings = settings;
        profilingSampler = new ProfilingSampler("DLSS Upscale");
        
        // Tell URP we need access to certain resources
        requiresIntermediateTexture = true;
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
    
    // ==================== Render Graph API (Unity 6+) ====================
    
    /// <summary>
    /// Pass data for the Render Graph system
    /// </summary>
    private class DLSSPassData
    {
        public TextureHandle colorTexture;
        public TextureHandle depthTexture;
        public DLSSRenderFeature.DLSSSettings settings;
        public int frameCount;
        public Camera camera;
        public RenderTexture dlssOutputRT;
        public Matrix4x4 prevViewProjection;
        public bool firstFrame;
    }
    
    /// <summary>
    /// RecordRenderGraph - Unity 6's Render Graph API entry point
    /// This is called instead of Execute() when using Render Graph
    /// </summary>
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        _frameCount++;
        
        // Get frame data
        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData = frameData.Get<UniversalCameraData>();
        var camera = cameraData.camera;
        
        // Always log first frame and periodically
        if (_settings.debugLogging && (_frameCount == 1 || _frameCount % 300 == 0))
        {
            Debug.Log($"[DLSS-RG] RecordRenderGraph frame {_frameCount}, camera: {camera.name}, " +
                      $"screen: {Screen.width}x{Screen.height}");
        }
        
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Get output resolution (what DLSS upscales TO)
        int outputWidth = Screen.width;
        int outputHeight = Screen.height;
        
        // Get render resolution from DLSS optimal settings
        int renderWidth, renderHeight;
        if (StreamlineDLSSPlugin.GetOptimalSettings(
            _settings.dlssMode, 
            (uint)outputWidth, 
            (uint)outputHeight, 
            out StreamlineDLSSPlugin.DLSSSettings dlssSettings))
        {
            renderWidth = (int)dlssSettings.OptimalRenderWidth;
            renderHeight = (int)dlssSettings.OptimalRenderHeight;
            
            if (_settings.debugLogging && _frameCount == 1)
            {
                Debug.Log($"[DLSS-RG] Optimal settings: render {renderWidth}x{renderHeight}");
            }
        }
        else
        {
            renderWidth = camera.pixelWidth;
            renderHeight = camera.pixelHeight;
            
            if (_settings.debugLogging && _frameCount == 1)
            {
                Debug.Log($"[DLSS-RG] GetOptimalSettings failed, using fallback: {renderWidth}x{renderHeight}");
            }
        }
        
        // Ensure we have an output RT
        EnsureOutputRT(outputWidth, outputHeight);
        
        // Use an unsafe render graph pass to execute DLSS
        using (var builder = renderGraph.AddUnsafePass<DLSSPassData>("DLSS Upscale", out var passData, profilingSampler))
        {
            // Setup pass data
            passData.settings = _settings;
            passData.frameCount = _frameCount;
            passData.camera = camera;
            passData.dlssOutputRT = _dlssOutputRT;
            passData.prevViewProjection = _prevViewProjection;
            passData.firstFrame = _firstFrame;
            
            // Get the textures we need access to
            passData.colorTexture = resourceData.activeColorTexture;
            passData.depthTexture = resourceData.activeDepthTexture;
            
            // Declare resource usage
            builder.UseTexture(passData.colorTexture, AccessFlags.Read);
            builder.UseTexture(passData.depthTexture, AccessFlags.Read);
            
            // Allow pass to access command buffer
            builder.AllowPassCulling(false);
            
            // The render function
            builder.SetRenderFunc((DLSSPassData data, UnsafeGraphContext context) =>
            {
                var cmd = context.cmd;
                
                if (data.settings.debugLogging && data.frameCount == 1)
                {
                    Debug.Log("[DLSS-RG] SetRenderFunc executing...");
                }
                
                // 1. Set viewport
                StreamlineDLSSPlugin.SetViewport(0);
                
                // 2. Set camera constants
                SetConstantsStatic(data.camera, data.prevViewProjection, data.firstFrame);
                
                // 3. Set DLSS options
                bool optionsSet = StreamlineDLSSPlugin.SetOptions(
                    data.settings.dlssMode,
                    (uint)Screen.width,
                    (uint)Screen.height,
                    data.settings.colorBuffersHDR
                );
                
                if (data.settings.debugLogging && data.frameCount == 1)
                {
                    Debug.Log($"[DLSS-RG] SetOptions result: {optionsSet}");
                }
                
                // 4. Tag resources - get native texture pointers from global shader textures
                // Note: In render graph, we tag resources using global texture references
                TagResourcesRenderGraph(data);
                
                // 5. Issue DLSS evaluate using UnsafeCommandBuffer directly
                if (data.settings.debugLogging && data.frameCount == 1)
                {
                    Debug.Log("[DLSS-RG] About to call IssueEvaluateEvent...");
                }
                
                // Issue plugin event directly on UnsafeCommandBuffer (has same IssuePluginEvent API)
                StreamlineDLSSPlugin.IssueEvaluateEvent(cmd);
                
                if (data.settings.debugLogging && data.frameCount == 1)
                {
                    Debug.Log("[DLSS-RG] IssueEvaluateEvent called");
                }
                
                // 6. Blit DLSS output to camera target
                // Note: UnsafeCommandBuffer doesn't have Blit, so we need to get native CommandBuffer for this
                if (data.dlssOutputRT != null)
                {
                    var nativeCmd = CommandBufferHelpers.GetNativeCommandBuffer(cmd);
                    nativeCmd.Blit(data.dlssOutputRT, BuiltinRenderTextureType.CameraTarget);
                }
            });
        }
        
        // Store for next frame
        Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
        _prevViewProjection = projMatrix * viewMatrix;
        _firstFrame = false;
#endif
    }
    
    /// <summary>
    /// Static version of SetConstants for use in render graph lambda
    /// </summary>
    private static void SetConstantsStatic(Camera camera, Matrix4x4 prevViewProjection, bool firstFrame)
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
    /// Tag resources for render graph path
    /// </summary>
    private static void TagResourcesRenderGraph(DLSSPassData data)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        int outputWidth = Screen.width;
        int outputHeight = Screen.height;
        int renderWidth = data.camera.pixelWidth;
        int renderHeight = data.camera.pixelHeight;
        
        // Tag depth
        var depthTexture = Shader.GetGlobalTexture("_CameraDepthTexture");
        if (depthTexture != null)
        {
            IntPtr depthPtr = depthTexture.GetNativeTexturePtr();
            if (depthPtr != IntPtr.Zero)
            {
                StreamlineDLSSPlugin.TagResource(depthPtr, StreamlineDLSSPlugin.BufferType.Depth,
                    (uint)renderWidth, (uint)renderHeight, 40, D3D12_RESOURCE_STATE_DEPTH_READ);
            }
        }
        
        // Tag motion vectors
        var motionTexture = Shader.GetGlobalTexture("_MotionVectorTexture");
        if (motionTexture != null)
        {
            IntPtr mvecPtr = motionTexture.GetNativeTexturePtr();
            if (mvecPtr != IntPtr.Zero)
            {
                StreamlineDLSSPlugin.TagResource(mvecPtr, StreamlineDLSSPlugin.BufferType.MotionVectors,
                    (uint)renderWidth, (uint)renderHeight, 34, D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE);
            }
        }
        
        // Tag input color
        var colorTexture = Shader.GetGlobalTexture("_CameraColorTexture");
        if (colorTexture != null)
        {
            IntPtr colorPtr = colorTexture.GetNativeTexturePtr();
            if (colorPtr != IntPtr.Zero)
            {
                uint colorFormat = data.settings.colorBuffersHDR ? (uint)10 : (uint)28;
                StreamlineDLSSPlugin.TagResource(colorPtr, StreamlineDLSSPlugin.BufferType.ScalingInputColor,
                    (uint)renderWidth, (uint)renderHeight, colorFormat, D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE);
            }
        }
        
        // Tag output
        if (data.dlssOutputRT != null)
        {
            IntPtr outputPtr = data.dlssOutputRT.GetNativeTexturePtr();
            if (outputPtr != IntPtr.Zero)
            {
                uint outputFormat = data.settings.colorBuffersHDR ? (uint)10 : (uint)28;
                StreamlineDLSSPlugin.TagResource(outputPtr, StreamlineDLSSPlugin.BufferType.ScalingOutputColor,
                    (uint)outputWidth, (uint)outputHeight, outputFormat, D3D12_RESOURCE_STATE_UNORDERED_ACCESS);
            }
        }
#endif
    }
    
    // ==================== Legacy Execute API (for compatibility mode) ====================

    [Obsolete("Use RecordRenderGraph instead")]
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        _frameCount++;
        var camera = renderingData.cameraData.camera;
        
        // Always log first frame and every 300 frames regardless of platform
        if (_settings.debugLogging && (_frameCount == 1 || _frameCount % 300 == 0))
        {
            Debug.Log($"[DLSS] Execute frame {_frameCount}, camera: {camera.name}, " +
                      $"screen: {Screen.width}x{Screen.height}, camPx: {camera.pixelWidth}x{camera.pixelHeight}");
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        var cmd = CommandBufferPool.Get("DLSS Upscale");
        
        using (new ProfilingScope(cmd, profilingSampler))
        {
            // Get output resolution (what DLSS upscales TO)
            int outputWidth = Screen.width;
            int outputHeight = Screen.height;
            
            // Get render resolution (what Unity rendered AT)
            // camera.pixelWidth is already the scaled resolution when renderScale < 1
            // We need to query DLSS for optimal render dimensions based on output
            int renderWidth, renderHeight;
            if (StreamlineDLSSPlugin.GetOptimalSettings(
                _settings.dlssMode, 
                (uint)outputWidth, 
                (uint)outputHeight, 
                out StreamlineDLSSPlugin.DLSSSettings dlssSettings))
            {
                renderWidth = (int)dlssSettings.OptimalRenderWidth;
                renderHeight = (int)dlssSettings.OptimalRenderHeight;
                
                if (_settings.debugLogging && _frameCount == 1)
                {
                    Debug.Log($"[DLSS] Optimal settings: render {renderWidth}x{renderHeight}, " +
                              $"sharpness={dlssSettings.OptimalSharpness}");
                }
            }
            else
            {
                // Fallback: use camera's actual pixel dimensions
                renderWidth = camera.pixelWidth;
                renderHeight = camera.pixelHeight;
                
                if (_settings.debugLogging && _frameCount == 1)
                {
                    Debug.Log($"[DLSS] GetOptimalSettings failed, using fallback: {renderWidth}x{renderHeight}");
                }
            }
            
            // Ensure we have an output RT at the correct resolution
            EnsureOutputRT(outputWidth, outputHeight);
            
            // 1. Set viewport
            StreamlineDLSSPlugin.SetViewport(0);
            
            // 2. Set camera constants
            SetConstants(camera);
            
            // 3. Set DLSS options with correct output dimensions
            bool optionsSet = StreamlineDLSSPlugin.SetOptions(
                _settings.dlssMode,
                (uint)outputWidth,
                (uint)outputHeight,
                _settings.colorBuffersHDR
            );
            
            if (_settings.debugLogging && _frameCount == 1)
            {
                Debug.Log($"[DLSS] SetOptions: mode={_settings.dlssMode}, output={outputWidth}x{outputHeight}, result={optionsSet}");
            }
            
            // 4. Tag all required resources
            bool resourcesTagged = TagResources(renderingData.cameraData, renderWidth, renderHeight, outputWidth, outputHeight);
            
            if (_settings.debugLogging && _frameCount == 1)
            {
                Debug.Log($"[DLSS] TagResources result: {resourcesTagged}");
            }
            
            // 5. Execute command buffer to ensure resources are ready before DLSS
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            // 6. Issue DLSS evaluate via plugin event
            if (_settings.debugLogging && _frameCount == 1)
            {
                Debug.Log("[DLSS] About to call IssueEvaluateEvent...");
            }
            
            StreamlineDLSSPlugin.IssueEvaluateEvent(cmd);
            
            if (_settings.debugLogging && _frameCount == 1)
            {
                Debug.Log($"[DLSS] IssueEvaluateEvent called " +
                          $"(Render: {renderWidth}x{renderHeight} → Output: {outputWidth}x{outputHeight})");
            }
            
            // 7. Execute the plugin event
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            
            // 8. Blit DLSS output to camera target
            if (_dlssOutputRT != null)
            {
                cmd.Blit(_dlssOutputRT, BuiltinRenderTextureType.CameraTarget);
                
                if (_settings.debugLogging && _frameCount == 1)
                {
                    Debug.Log($"[DLSS] Blitting output RT to camera target");
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
