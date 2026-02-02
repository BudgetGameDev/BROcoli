using UnityEngine;

/// <summary>
/// Component to manage NVIDIA DLSS and Frame Generation settings at runtime.
/// Attach to a persistent GameObject in your scene.
/// 
/// Recommended usage:
/// - Add to your main camera or a persistent manager object
/// - Enable "Auto Enable On Start" for immediate activation
/// - Or use the EnableDLSS() method from your game settings menu
/// 
/// Performance impact:
/// - DLSS Performance mode: ~50% internal render resolution → ~2x GPU performance
/// - Frame Gen 3x: Generates 2 frames per rendered frame → ~3x apparent framerate
/// - Combined: Massive performance boost for RTX GPUs
/// 
/// Requirements:
/// - DLSS: Any RTX GPU (20, 30, 40 series)
/// - Frame Gen: RTX 40-series or newer
/// </summary>
public class DLSSManager : MonoBehaviour
{
    [Header("DLSS Settings")]
    [Tooltip("DLSS Super Resolution quality mode")]
    [SerializeField] private StreamlineDLSSPlugin.DLSSMode _dlssMode = StreamlineDLSSPlugin.DLSSMode.MaxQuality;
    
    [Header("Frame Generation Settings")]
    [Tooltip("Enable Frame Generation (RTX 40+ only)")]
    [SerializeField] private bool _enableFrameGen = true;
    
    [Tooltip("Frame multiplier: 1=2x, 2=3x, 3=4x")]
    [Range(1, 3)]
    [SerializeField] private int _frameGenMultiplier = 1;
    
    [Header("Startup")]
    [Tooltip("Automatically enable DLSS settings on Start()")]
    [SerializeField] private bool _autoEnableOnStart = true;
    
    [Header("Debug")]
    [SerializeField] private bool _logSupportOnStart = true;
    
    // Runtime state
    private bool _dlssEnabled = false;
    private bool _frameGenEnabled = false;
    
    /// <summary>
    /// Is DLSS currently enabled?
    /// </summary>
    public bool IsDLSSEnabled => _dlssEnabled;
    
    /// <summary>
    /// Is Frame Generation currently enabled?
    /// </summary>
    public bool IsFrameGenEnabled => _frameGenEnabled;
    
    /// <summary>
    /// Current DLSS mode
    /// </summary>
    public StreamlineDLSSPlugin.DLSSMode DLSSMode => _dlssMode;
    
    /// <summary>
    /// Current Frame Generation multiplier (1-3)
    /// </summary>
    public int FrameGenMultiplier => _frameGenMultiplier;
    
    private void Start()
    {
        if (_logSupportOnStart)
        {
            StreamlineDLSSPlugin.LogSupport();
        }
        
        if (_autoEnableOnStart)
        {
            EnableDLSS();
        }
    }
    
    private void OnDestroy()
    {
        if (_dlssEnabled || _frameGenEnabled)
        {
            DisableDLSS();
        }
    }
    
    /// <summary>
    /// Enable DLSS with current settings
    /// </summary>
    public void EnableDLSS()
    {
        // Enable DLSS Super Resolution
        if (StreamlineDLSSPlugin.IsDLSSSupported())
        {
            _dlssEnabled = StreamlineDLSSPlugin.SetDLSSMode(_dlssMode);
            
            if (_dlssEnabled && _dlssMode != StreamlineDLSSPlugin.DLSSMode.Off)
            {
                // Get and log optimal settings for current resolution
                uint width = (uint)Screen.width;
                uint height = (uint)Screen.height;
                
                if (StreamlineDLSSPlugin.GetOptimalSettings(_dlssMode, width, height, out var settings))
                {
                    Debug.Log($"[DLSSManager] DLSS {_dlssMode}: Render at {settings.OptimalRenderWidth}x{settings.OptimalRenderHeight} → Output {width}x{height}");
                }
            }
        }
        else
        {
            Debug.LogWarning("[DLSSManager] DLSS not supported on this GPU");
        }
        
        // Enable Frame Generation if requested and supported
        if (_enableFrameGen && StreamlineDLSSPlugin.IsFrameGenSupported())
        {
            _frameGenEnabled = StreamlineDLSSPlugin.SetFrameGenMode(
                StreamlineDLSSPlugin.DLSSGMode.On, 
                _frameGenMultiplier);
        }
        else if (_enableFrameGen)
        {
            Debug.LogWarning("[DLSSManager] Frame Generation requires RTX 40-series or newer");
        }
    }
    
    /// <summary>
    /// Disable DLSS and Frame Generation
    /// </summary>
    public void DisableDLSS()
    {
        StreamlineDLSSPlugin.DisableAll();
        _dlssEnabled = false;
        _frameGenEnabled = false;
    }
    
    /// <summary>
    /// Set DLSS mode at runtime
    /// </summary>
    public void SetDLSSMode(StreamlineDLSSPlugin.DLSSMode mode)
    {
        _dlssMode = mode;
        if (_dlssEnabled || _autoEnableOnStart)
        {
            _dlssEnabled = StreamlineDLSSPlugin.SetDLSSMode(mode);
        }
    }
    
    /// <summary>
    /// Set Frame Generation multiplier at runtime (1=2x, 2=3x, 3=4x)
    /// </summary>
    public void SetFrameGenMultiplier(int multiplier)
    {
        _frameGenMultiplier = Mathf.Clamp(multiplier, 1, 3);
        if (_frameGenEnabled)
        {
            StreamlineDLSSPlugin.SetFrameGenMode(
                StreamlineDLSSPlugin.DLSSGMode.On, 
                _frameGenMultiplier);
        }
    }
    
    /// <summary>
    /// Toggle Frame Generation on/off
    /// </summary>
    public void ToggleFrameGen(bool enabled)
    {
        _enableFrameGen = enabled;
        if (enabled && StreamlineDLSSPlugin.IsFrameGenSupported())
        {
            _frameGenEnabled = StreamlineDLSSPlugin.SetFrameGenMode(
                StreamlineDLSSPlugin.DLSSGMode.On, 
                _frameGenMultiplier);
        }
        else
        {
            StreamlineDLSSPlugin.SetFrameGenMode(StreamlineDLSSPlugin.DLSSGMode.Off, 0);
            _frameGenEnabled = false;
        }
    }
    
    /// <summary>
    /// Quick preset: Maximum performance (DLSS Performance + Frame Gen 3x)
    /// </summary>
    [ContextMenu("Enable Max Performance")]
    public void EnableMaxPerformance()
    {
        _dlssMode = StreamlineDLSSPlugin.DLSSMode.MaxPerformance;
        _enableFrameGen = true;
        _frameGenMultiplier = 2;
        EnableDLSS();
    }
    
    /// <summary>
    /// Quick preset: Balanced (DLSS Balanced + Frame Gen 2x)
    /// </summary>
    [ContextMenu("Enable Balanced")]
    public void EnableBalanced()
    {
        _dlssMode = StreamlineDLSSPlugin.DLSSMode.Balanced;
        _enableFrameGen = true;
        _frameGenMultiplier = 1;
        EnableDLSS();
    }
    
    /// <summary>
    /// Quick preset: Quality (DLSS Quality, no Frame Gen)
    /// </summary>
    [ContextMenu("Enable Quality")]
    public void EnableQuality()
    {
        _dlssMode = StreamlineDLSSPlugin.DLSSMode.MaxQuality;
        _enableFrameGen = false;
        _frameGenMultiplier = 0;
        EnableDLSS();
    }
}
