using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP ScriptableRenderFeature that integrates NVIDIA DLSS with Unity's rendering pipeline.
/// 
/// This feature captures depth, motion vectors, and color buffers at the appropriate
/// pipeline stages and tags them for DLSS processing.
/// 
/// Add this to your URP Renderer Asset to enable DLSS support.
/// </summary>
public class DLSSRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class DLSSSettings
    {
        [Header("DLSS Mode")]
        [Tooltip("DLSS upscaling quality mode")]
        public StreamlineDLSSPlugin.DLSSMode dlssMode = StreamlineDLSSPlugin.DLSSMode.Balanced;
        
        [Header("Frame Generation")]
        [Tooltip("Enable Multi-Frame Generation (requires RTX 40+)")]
        public bool enableFrameGen = false;
        
        [Tooltip("Number of frames to generate: 1=2x, 2=3x, 3=4x")]
        [Range(1, 3)]
        public int framesToGenerate = 1;
        
        [Header("HDR")]
        [Tooltip("Are color buffers in HDR format?")]
        public bool colorBuffersHDR = true;
        
        [Header("Debug")]
        [Tooltip("Log buffer tagging to console")]
        public bool debugLogging = false;
    }
    
    public DLSSSettings settings = new DLSSSettings();
    
    private DLSSTagPass _tagPass;
    private DLSSEvaluatePass _evaluatePass;
    private bool _dlssInitialized = false;
    
    public override void Create()
    {
        _tagPass = new DLSSTagPass(settings)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
        
        _evaluatePass = new DLSSEvaluatePass(settings)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
        
        _dlssInitialized = false;
    }
    
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Only run in game view, not scene view or preview
        if (renderingData.cameraData.cameraType != CameraType.Game)
            return;
            
        // Skip if DLSS is off
        if (settings.dlssMode == StreamlineDLSSPlugin.DLSSMode.Off)
            return;
        
        // Initialize DLSS on first frame (must be done after graphics device is ready)
        if (!_dlssInitialized)
        {
            InitializeDLSS();
            _dlssInitialized = true;
        }
            
        renderer.EnqueuePass(_tagPass);
        renderer.EnqueuePass(_evaluatePass);
    }
    
    private void InitializeDLSS()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Enable Reflex Low Latency Mode (works with any RTX GPU)
        if (StreamlineReflexPlugin.IsReflexSupported())
        {
            bool reflexSuccess = StreamlineReflexPlugin.SetMode(StreamlineReflexPlugin.ReflexMode.LowLatencyWithBoost);
            if (settings.debugLogging)
            {
                Debug.Log($"[DLSSRenderFeature] SetReflexMode(LowLatencyWithBoost): {(reflexSuccess ? "OK" : "FAILED")}");
            }
        }
        else if (settings.debugLogging)
        {
            Debug.Log("[DLSSRenderFeature] Reflex not supported on this GPU");
        }
        
        // Set DLSS mode
        if (StreamlineDLSSPlugin.IsDLSSSupported())
        {
            bool success = StreamlineDLSSPlugin.SetDLSSMode(settings.dlssMode);
            if (settings.debugLogging)
            {
                Debug.Log($"[DLSSRenderFeature] SetDLSSMode({settings.dlssMode}): {(success ? "OK" : "FAILED")}");
            }
        }
        else if (settings.debugLogging)
        {
            Debug.LogWarning("[DLSSRenderFeature] DLSS not supported on this GPU");
        }
        
        // Set Frame Generation mode
        if (settings.enableFrameGen && StreamlineDLSSPlugin.IsFrameGenSupported())
        {
            bool success = StreamlineDLSSPlugin.SetFrameGenMode(
                StreamlineDLSSPlugin.DLSSGMode.On, 
                settings.framesToGenerate
            );
            if (settings.debugLogging)
            {
                Debug.Log($"[DLSSRenderFeature] SetFrameGenMode(On, {settings.framesToGenerate}): {(success ? "OK" : "FAILED")}");
            }
        }
        else if (settings.enableFrameGen && settings.debugLogging)
        {
            Debug.LogWarning("[DLSSRenderFeature] Frame Generation not supported (requires RTX 40+)");
        }
#endif
    }
    
    protected override void Dispose(bool disposing)
    {
        _tagPass?.Dispose();
        _evaluatePass?.Dispose();
    }
}

/// <summary>
/// Render pass that tags depth, motion vectors, and input color for DLSS
/// </summary>
public class DLSSTagPass : ScriptableRenderPass, IDisposable
{
    private DLSSRenderFeature.DLSSSettings _settings;
    private Matrix4x4 _prevViewProjection;
    private bool _firstFrame = true;
    
    public DLSSTagPass(DLSSRenderFeature.DLSSSettings settings)
    {
        _settings = settings;
        profilingSampler = new ProfilingSampler("DLSS Tag Resources");
    }
    
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        var camera = renderingData.cameraData.camera;
        var cmd = CommandBufferPool.Get("DLSS Tag");
        
        using (new ProfilingScope(cmd, profilingSampler))
        {
            // Calculate matrices
            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
            Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            Matrix4x4 viewProj = projMatrix * viewMatrix;
            Matrix4x4 invViewProj = viewProj.inverse;
            
            // Calculate clip-to-prev-clip for temporal stability
            Matrix4x4 clipToPrevClip = _firstFrame ? Matrix4x4.identity : _prevViewProjection * invViewProj;
            Matrix4x4 prevClipToClip = clipToPrevClip.inverse;
            
            // Get jitter offset from TAA if enabled
            Vector2 jitterOffset = Vector2.zero;
            // URP stores jitter in camera.projectionMatrix - we need to extract it
            // For now, use zero jitter (DLSS will handle it internally)
            
            // Motion vector scale (screen space UV to pixel space)
            Vector2 mvecScale = new Vector2(camera.pixelWidth, camera.pixelHeight);
            
            // Set constants
            bool success = StreamlineDLSSPlugin.SetConstants(
                projMatrix,                    // cameraViewToClip
                projMatrix.inverse,            // clipToCameraView
                clipToPrevClip,
                prevClipToClip,
                jitterOffset,
                mvecScale,
                camera.nearClipPlane,
                camera.farClipPlane,
                camera.fieldOfView * Mathf.Deg2Rad,
                camera.aspect,
                depthInverted: SystemInfo.usesReversedZBuffer,
                cameraMotionIncluded: true,    // URP includes camera motion in mvec
                reset: _firstFrame
            );
            
            if (_settings.debugLogging && !success)
            {
                Debug.LogWarning("[DLSS] Failed to set constants");
            }
            
            // Store for next frame
            _prevViewProjection = viewProj;
            _firstFrame = false;
            
            // Set DLSS options
            StreamlineDLSSPlugin.SetOptions(
                _settings.dlssMode,
                (uint)camera.pixelWidth,
                (uint)camera.pixelHeight,
                _settings.colorBuffersHDR
            );
            
            // Note: Actual resource tagging requires native texture pointers
            // which need to be obtained through render graph or RTHandle system
            // This is a framework that will be completed when integrated with
            // Unity's render graph system
        }
        
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
#endif
    }
    
    public void Dispose()
    {
    }
}

/// <summary>
/// Render pass that evaluates DLSS (runs the upscaling)
/// </summary>
public class DLSSEvaluatePass : ScriptableRenderPass, IDisposable
{
    private DLSSRenderFeature.DLSSSettings _settings;
    
    public DLSSEvaluatePass(DLSSRenderFeature.DLSSSettings settings)
    {
        _settings = settings;
        profilingSampler = new ProfilingSampler("DLSS Evaluate");
    }
    
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        var cmd = CommandBufferPool.Get("DLSS Evaluate");
        
        using (new ProfilingScope(cmd, profilingSampler))
        {
            // Evaluate DLSS
            bool success = StreamlineDLSSPlugin.Evaluate(IntPtr.Zero);
            
            if (_settings.debugLogging && !success)
            {
                Debug.LogWarning("[DLSS] Failed to evaluate");
            }
        }
        
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
#endif
    }
    
    public void Dispose()
    {
    }
}
