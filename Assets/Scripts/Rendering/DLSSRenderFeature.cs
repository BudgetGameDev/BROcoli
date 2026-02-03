using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP ScriptableRenderFeature that integrates NVIDIA DLSS with Unity's rendering pipeline.
/// 
/// This feature:
/// 1. Sets render scale to render at lower internal resolution
/// 2. Tags depth, motion vectors, and color buffers with native GPU pointers
/// 3. Evaluates DLSS to upscale to output resolution
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
        [Tooltip("Log DLSS operations to console")]
        public bool debugLogging = false;
    }
    
    public DLSSSettings settings = new DLSSSettings();
    
    private DLSSUpscalePass _upscalePass;
    private bool _dlssInitialized = false;
#pragma warning disable CS0649 // Field never assigned (only used in Windows builds)
    private static DLSSRenderScaleManager _renderScaleManager;
#pragma warning restore CS0649
    
    public override void Create()
    {
        _upscalePass = new DLSSUpscalePass(settings)
        {
            // Run after all rendering but before final blit
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
        
        _dlssInitialized = false;
        
        if (settings.debugLogging)
        {
            Debug.Log($"[DLSS] DLSSRenderFeature.Create() - pass created, event={RenderPassEvent.AfterRenderingPostProcessing}");
        }
    }
    
    private static int _addPassCount = 0;
    
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        _addPassCount++;
        
        // Only run in game view, not scene view or preview
        if (renderingData.cameraData.cameraType != CameraType.Game)
        {
            if (settings.debugLogging && _addPassCount <= 5)
                Debug.Log($"[DLSS] AddRenderPasses: skipping non-game camera type {renderingData.cameraData.cameraType}");
            return;
        }
            
        // Skip if DLSS is off
        if (settings.dlssMode == StreamlineDLSSPlugin.DLSSMode.Off)
        {
            if (settings.debugLogging && _addPassCount <= 5)
                Debug.Log("[DLSS] AddRenderPasses: DLSS mode is Off, skipping");
            return;
        }
        
        // Log that we're adding the pass
        if (settings.debugLogging && (_addPassCount == 1 || _addPassCount % 300 == 0))
        {
            Debug.Log($"[DLSS] AddRenderPasses #{_addPassCount}: enqueueing DLSS pass for camera {renderingData.cameraData.camera.name}");
        }
        
        // Initialize DLSS on first frame
        if (!_dlssInitialized)
        {
            if (settings.debugLogging)
                Debug.Log("[DLSS] AddRenderPasses: initializing DLSS...");
            InitializeDLSS(ref renderingData);
            _dlssInitialized = true;
        }
        
        // Pass the camera data to the upscale pass
        _upscalePass.Setup(renderer, ref renderingData);
        renderer.EnqueuePass(_upscalePass);
    }
    
    private void InitializeDLSS(ref RenderingData renderingData)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Find or create render scale manager
        if (_renderScaleManager == null)
        {
            var go = new GameObject("[DLSS Render Scale Manager]");
            go.hideFlags = HideFlags.HideAndDontSave;
            _renderScaleManager = go.AddComponent<DLSSRenderScaleManager>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
        
        // Set render scale for DLSS mode
        _renderScaleManager.SetDLSSMode(settings.dlssMode);
        
        // Enable Reflex Low Latency Mode
        if (StreamlineReflexPlugin.IsReflexSupported())
        {
            StreamlineReflexPlugin.SetMode(StreamlineReflexPlugin.ReflexMode.LowLatencyWithBoost);
            if (settings.debugLogging)
                Debug.Log("[DLSS] Reflex Low Latency enabled");
        }
        
        // Set DLSS options in native plugin - MUST use SetOptions with output dimensions
        if (StreamlineDLSSPlugin.IsDLSSSupported())
        {
            // Get actual output resolution (the final display size)
            uint outputWidth = (uint)Screen.width;
            uint outputHeight = (uint)Screen.height;
            
            bool success = StreamlineDLSSPlugin.SetOptions(
                settings.dlssMode,
                outputWidth,
                outputHeight,
                colorBuffersHDR: true
            );
            
            if (settings.debugLogging)
                Debug.Log($"[DLSS] SetOptions({settings.dlssMode}, {outputWidth}x{outputHeight}): {(success ? "OK" : "FAILED")}");
        }
        else if (settings.debugLogging)
        {
            Debug.LogWarning("[DLSS] DLSS not supported on this GPU");
        }
        
        // Enable Frame Generation if requested
        if (settings.enableFrameGen && StreamlineDLSSPlugin.IsFrameGenSupported())
        {
            StreamlineDLSSPlugin.SetFrameGenMode(StreamlineDLSSPlugin.DLSSGMode.On, settings.framesToGenerate);
            if (settings.debugLogging)
                Debug.Log($"[DLSS] Frame Generation enabled ({settings.framesToGenerate + 1}x)");
        }
#endif
    }
    
    protected override void Dispose(bool disposing)
    {
        _upscalePass?.Dispose();
        
        // Restore render scale
        if (_renderScaleManager != null)
        {
            _renderScaleManager.RestoreOriginalScale();
        }
    }
}
