using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using StreamlineDLSS;

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
    private RenderTexture _dlssOutputRT;
    private int _frameCount = 0;
    
    public DLSSUpscalePass(DLSSRenderFeature.DLSSSettings settings)
    {
        _settings = settings;
        profilingSampler = new ProfilingSampler("DLSS Upscale");
        requiresIntermediateTexture = true;
    }
    
    public void Setup(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        _renderer = renderer;
    }
    
    /// <summary>
    /// RecordRenderGraph - Unity 6's Render Graph API entry point
    /// </summary>
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        _frameCount++;
        
        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData = frameData.Get<UniversalCameraData>();
        var camera = cameraData.camera;
        
        if (_settings.debugLogging && (_frameCount == 1 || _frameCount % 300 == 0))
        {
            Debug.Log($"[DLSS-RG] RecordRenderGraph frame {_frameCount}, camera: {camera.name}, " +
                      $"screen: {Screen.width}x{Screen.height}");
        }
        
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Early-out if DLSS is not supported - prevents crash on GetOptimalSettings
        if (!StreamlineDLSSPlugin.IsDLSSSupported())
        {
            if (_settings.debugLogging && _frameCount == 1)
                Debug.Log("[DLSS-RG] DLSS not supported, skipping pass");
            return;
        }
        
        int outputWidth = Screen.width;
        int outputHeight = Screen.height;
        
        // Get render resolution from DLSS optimal settings
        int renderWidth, renderHeight;
        if (StreamlineDLSSPlugin.GetOptimalSettings(
            _settings.dlssMode, 
            (uint)outputWidth, 
            (uint)outputHeight, 
            out DLSSSettings dlssSettings))
        {
            renderWidth = (int)dlssSettings.OptimalRenderWidth;
            renderHeight = (int)dlssSettings.OptimalRenderHeight;
            
            if (_settings.debugLogging && _frameCount == 1)
                Debug.Log($"[DLSS-RG] Optimal settings: render {renderWidth}x{renderHeight}");
        }
        else
        {
            renderWidth = camera.pixelWidth;
            renderHeight = camera.pixelHeight;
            
            if (_settings.debugLogging && _frameCount == 1)
                Debug.Log($"[DLSS-RG] GetOptimalSettings failed, using fallback: {renderWidth}x{renderHeight}");
        }
        
        // Ensure output RT exists
        _dlssOutputRT = DLSSOutputManager.EnsureOutputRT(_dlssOutputRT, outputWidth, outputHeight, 
            _settings.colorBuffersHDR, _settings.debugLogging);
        
        // Execute DLSS pass
        ExecuteRenderGraphPass(renderGraph, resourceData, camera);
        
        // Store for next frame
        _prevViewProjection = DLSSCameraHelper.GetCurrentViewProjection(camera);
        _firstFrame = false;
#endif
    }
    
    private void ExecuteRenderGraphPass(RenderGraph renderGraph, UniversalResourceData resourceData, Camera camera)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        using (var builder = renderGraph.AddUnsafePass<DLSSPassData>("DLSS Upscale", out var passData, profilingSampler))
        {
            passData.settings = _settings;
            passData.frameCount = _frameCount;
            passData.camera = camera;
            passData.dlssOutputRT = _dlssOutputRT;
            passData.prevViewProjection = _prevViewProjection;
            passData.firstFrame = _firstFrame;
            passData.colorTexture = resourceData.activeColorTexture;
            passData.depthTexture = resourceData.activeDepthTexture;
            
            builder.UseTexture(passData.colorTexture, AccessFlags.Read);
            builder.UseTexture(passData.depthTexture, AccessFlags.Read);
            builder.AllowPassCulling(false);
            
            builder.SetRenderFunc((DLSSPassData data, UnsafeGraphContext context) =>
            {
                ExecuteDLSSRenderGraph(data, context);
            });
        }
#endif
    }
    
    private static void ExecuteDLSSRenderGraph(DLSSPassData data, UnsafeGraphContext context)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            var cmd = context.cmd;
            
            if (data.settings.debugLogging && data.frameCount == 1)
                Debug.Log("[DLSS-RG] SetRenderFunc executing...");
            
            // Safety checks
            if (data.camera == null)
            {
                Debug.LogWarning("[DLSS-RG] Camera is null, skipping DLSS this frame");
                return;
            }
            
            if (Screen.width == 0 || Screen.height == 0)
            {
                Debug.LogWarning("[DLSS-RG] Screen dimensions are 0, skipping DLSS this frame");
                return;
            }
            
            // 1. Set viewport and camera constants
            StreamlineDLSSPlugin.SetViewport(0);
            DLSSCameraHelper.SetConstants(data.camera, data.prevViewProjection, data.firstFrame);
            
            // 2. Set DLSS options
            bool optionsSet = StreamlineDLSSPlugin.SetOptions(
                data.settings.dlssMode,
                (uint)Screen.width,
                (uint)Screen.height,
                data.settings.colorBuffersHDR
            );
            
            if (data.settings.debugLogging && data.frameCount == 1)
                Debug.Log($"[DLSS-RG] SetOptions result: {optionsSet}");
            
            if (!optionsSet)
            {
                Debug.LogWarning("[DLSS-RG] SetOptions failed, skipping DLSS evaluation this frame");
                return;
            }
            
            // 3. Tag resources
            if (!DLSSResourceTagger.TagResourcesRenderGraph(data))
            {
                if (data.settings.debugLogging && data.frameCount <= 5)
                    Debug.LogWarning("[DLSS-RG] TagResources failed, skipping DLSS evaluation this frame");
                return;
            }
            
            // 4. Issue DLSS evaluate
            if (data.settings.debugLogging && data.frameCount == 1)
                Debug.Log("[DLSS-RG] About to call IssueEvaluateEvent...");
            
            StreamlineDLSSPlugin.IssueEvaluateEvent(cmd);
            
            if (data.settings.debugLogging && data.frameCount == 1)
                Debug.Log("[DLSS-RG] IssueEvaluateEvent called");
            
            // 5. Blit DLSS output to camera target
            if (data.dlssOutputRT != null)
            {
                var nativeCmd = CommandBufferHelpers.GetNativeCommandBuffer(cmd);
                nativeCmd.Blit(data.dlssOutputRT, BuiltinRenderTextureType.CameraTarget);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[DLSS-RG] Exception in SetRenderFunc: {e.Message}\n{e.StackTrace}");
        }
#endif
    }
    
    [Obsolete("Use RecordRenderGraph instead")]
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        // Legacy path - delegates to DLSSUpscalePassLegacy
        DLSSUpscalePassLegacy.Execute(this, context, ref renderingData, _settings, 
            ref _dlssOutputRT, ref _prevViewProjection, ref _firstFrame, ref _frameCount);
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
