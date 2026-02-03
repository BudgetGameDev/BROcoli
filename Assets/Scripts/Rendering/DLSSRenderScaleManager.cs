using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Manages Unity's render scale based on DLSS mode.
/// DLSS requires rendering at a lower internal resolution and upscaling to output resolution.
/// This component automatically adjusts the URP render scale to match the DLSS mode.
/// </summary>
public class DLSSRenderScaleManager : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The URP asset to modify. If null, will try to find it automatically.")]
    [SerializeField] private UniversalRenderPipelineAsset _urpAsset;
    
    [Tooltip("Store original render scale to restore when DLSS is disabled")]
    [SerializeField] private float _originalRenderScale = 1.0f;
    
    [Header("Debug")]
    [SerializeField] private bool _debugLogging = false;
    
    private StreamlineDLSSPlugin.DLSSMode _currentMode = StreamlineDLSSPlugin.DLSSMode.Off;
    private bool _initialized = false;
    
    /// <summary>
    /// Current DLSS render scale (internal render resolution / output resolution)
    /// </summary>
    public float CurrentRenderScale { get; private set; } = 1.0f;
    
    /// <summary>
    /// Output resolution width
    /// </summary>
    public int OutputWidth => Screen.width;
    
    /// <summary>
    /// Output resolution height
    /// </summary>
    public int OutputHeight => Screen.height;
    
    /// <summary>
    /// Internal render width (after applying render scale)
    /// </summary>
    public int RenderWidth => Mathf.RoundToInt(OutputWidth * CurrentRenderScale);
    
    /// <summary>
    /// Internal render height (after applying render scale)
    /// </summary>
    public int RenderHeight => Mathf.RoundToInt(OutputHeight * CurrentRenderScale);
    
    private void Awake()
    {
        Initialize();
    }
    
    private void Initialize()
    {
        if (_initialized) return;
        
        // Find URP asset if not assigned
        if (_urpAsset == null)
        {
            _urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        }
        
        if (_urpAsset != null)
        {
            _originalRenderScale = _urpAsset.renderScale;
            _initialized = true;
            
            if (_debugLogging)
            {
                Debug.Log($"[DLSSRenderScale] Initialized. Original render scale: {_originalRenderScale}");
            }
        }
        else
        {
            Debug.LogError("[DLSSRenderScale] Could not find URP asset!");
        }
    }
    
    /// <summary>
    /// Set DLSS mode and automatically adjust render scale
    /// </summary>
    public void SetDLSSMode(StreamlineDLSSPlugin.DLSSMode mode)
    {
        if (!_initialized) Initialize();
        if (_urpAsset == null) return;
        
        _currentMode = mode;
        
        // Calculate render scale based on DLSS mode
        float renderScale = GetRenderScaleForMode(mode);
        
        // Apply to URP
        _urpAsset.renderScale = renderScale;
        CurrentRenderScale = renderScale;
        
        if (_debugLogging)
        {
            Debug.Log($"[DLSSRenderScale] Mode: {mode}, Scale: {renderScale:F2}, " +
                      $"Render: {RenderWidth}x{RenderHeight} → Output: {OutputWidth}x{OutputHeight}");
        }
    }
    
    /// <summary>
    /// Get optimal render scale for a DLSS mode.
    /// These are approximate values - for exact values, query DLSS optimal settings.
    /// </summary>
    public static float GetRenderScaleForMode(StreamlineDLSSPlugin.DLSSMode mode)
    {
        return mode switch
        {
            StreamlineDLSSPlugin.DLSSMode.Off => 1.0f,
            StreamlineDLSSPlugin.DLSSMode.DLAA => 1.0f,           // Native resolution AA
            StreamlineDLSSPlugin.DLSSMode.UltraQuality => 0.77f,  // ~77% 
            StreamlineDLSSPlugin.DLSSMode.MaxQuality => 0.67f,    // ~67%
            StreamlineDLSSPlugin.DLSSMode.Balanced => 0.58f,      // ~58%
            StreamlineDLSSPlugin.DLSSMode.MaxPerformance => 0.50f,// ~50%
            StreamlineDLSSPlugin.DLSSMode.UltraPerformance => 0.33f, // ~33%
            _ => 1.0f
        };
    }
    
    /// <summary>
    /// Query DLSS for exact optimal render dimensions
    /// </summary>
    public bool GetOptimalRenderSize(StreamlineDLSSPlugin.DLSSMode mode, out int renderWidth, out int renderHeight)
    {
        renderWidth = OutputWidth;
        renderHeight = OutputHeight;
        
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (StreamlineDLSSPlugin.GetOptimalSettings(mode, (uint)OutputWidth, (uint)OutputHeight, 
            out StreamlineDLSSPlugin.DLSSSettings settings))
        {
            renderWidth = (int)settings.OptimalRenderWidth;
            renderHeight = (int)settings.OptimalRenderHeight;
            
            // Calculate actual render scale
            CurrentRenderScale = (float)renderWidth / OutputWidth;
            
            if (_debugLogging)
            {
                Debug.Log($"[DLSSRenderScale] DLSS optimal: {renderWidth}x{renderHeight} " +
                          $"(scale: {CurrentRenderScale:F3})");
            }
            return true;
        }
#endif
        
        // Fallback to estimated values
        float scale = GetRenderScaleForMode(mode);
        renderWidth = Mathf.RoundToInt(OutputWidth * scale);
        renderHeight = Mathf.RoundToInt(OutputHeight * scale);
        CurrentRenderScale = scale;
        return false;
    }
    
    /// <summary>
    /// Restore original render scale (when disabling DLSS)
    /// </summary>
    public void RestoreOriginalScale()
    {
        if (_urpAsset != null)
        {
            _urpAsset.renderScale = _originalRenderScale;
            CurrentRenderScale = _originalRenderScale;
            _currentMode = StreamlineDLSSPlugin.DLSSMode.Off;
            
            if (_debugLogging)
            {
                Debug.Log($"[DLSSRenderScale] Restored original scale: {_originalRenderScale}");
            }
        }
    }
    
    private void OnDestroy()
    {
        // Restore original scale when destroyed
        RestoreOriginalScale();
    }
    
    private void OnApplicationQuit()
    {
        RestoreOriginalScale();
    }
}
