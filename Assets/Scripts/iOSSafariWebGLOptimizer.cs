using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Runtime.InteropServices;

// Alias to resolve ambiguity
using UnityShadowQuality = UnityEngine.ShadowQuality;
using UnityShadowResolution = UnityEngine.ShadowResolution;

/// <summary>
/// Applies aggressive performance optimizations for iOS Safari WebGL builds only.
/// Does NOT affect native iOS builds or other platforms.
/// 
/// Settings applied:
/// - Lowest quality level
/// - Native resolution (no scaling)
/// - 120 FPS target frame rate
/// - VSync disabled
/// - MSAA disabled
/// </summary>
[DefaultExecutionOrder(-1000)] // Run very early
public class iOSSafariWebGLOptimizer : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int IsiOSMobile();
    
    [DllImport("__Internal")]
    private static extern int IsSafariBrowser();
#endif

    private static bool _optimizationsApplied = false;

    private void Awake()
    {
        if (_optimizationsApplied)
        {
            Destroy(gameObject);
            return;
        }
        
        ApplyOptimizationsIfNeeded();
    }

    private void ApplyOptimizationsIfNeeded()
    {
        bool isiOSSafariWebGL = false;

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            bool isiOS = IsiOSMobile() == 1;
            bool isSafari = IsSafariBrowser() == 1;
            isiOSSafariWebGL = isiOS || isSafari; // Safari on any device or iOS
            
            Debug.Log($"[iOSSafariOptimizer] Detection - iOS: {isiOS}, Safari: {isSafari}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[iOSSafariOptimizer] JS detection failed: {e.Message}");
            return;
        }
#endif

        if (!isiOSSafariWebGL)
        {
            Debug.Log("[iOSSafariOptimizer] Not iOS Safari WebGL - skipping optimizations");
            return;
        }

        Debug.Log("[iOSSafariOptimizer] iOS Safari WebGL detected - applying optimizations");
        _optimizationsApplied = true;

        ApplyQualitySettings();
        ApplyFrameRateSettings();
        ApplyURPSettings();
        
        Debug.Log("[iOSSafariOptimizer] All optimizations applied");
    }

    private void ApplyQualitySettings()
    {
        // Set to lowest quality level (index 0)
        int lowestQuality = 0;
        if (QualitySettings.GetQualityLevel() != lowestQuality)
        {
            QualitySettings.SetQualityLevel(lowestQuality, true);
            Debug.Log($"[iOSSafariOptimizer] Quality level set to {lowestQuality} (lowest)");
        }

        // Disable VSync
        QualitySettings.vSyncCount = 0;
        Debug.Log("[iOSSafariOptimizer] VSync disabled");

        // Disable MSAA via quality settings
        QualitySettings.antiAliasing = 0;
        Debug.Log("[iOSSafariOptimizer] MSAA disabled (QualitySettings)");

        // Set shadow settings to minimum
        QualitySettings.shadows = UnityShadowQuality.Disable;
        QualitySettings.shadowResolution = UnityShadowResolution.Low;
        QualitySettings.shadowDistance = 0f;
        Debug.Log("[iOSSafariOptimizer] Shadows disabled");

        // Reduce other quality settings
        QualitySettings.softParticles = false;
        QualitySettings.softVegetation = false;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.billboardsFaceCameraPosition = false;
        QualitySettings.lodBias = 0.5f;
        QualitySettings.maximumLODLevel = 2;
        QualitySettings.particleRaycastBudget = 16;
        Debug.Log("[iOSSafariOptimizer] Additional quality reductions applied");
    }

    private void ApplyFrameRateSettings()
    {
        // Target 120 FPS for ProMotion displays
        Application.targetFrameRate = 120;
        Debug.Log("[iOSSafariOptimizer] Target frame rate set to 120 FPS");

        // Ensure OnDemandRendering is at full rate
        OnDemandRendering.renderFrameInterval = 1;
        Debug.Log("[iOSSafariOptimizer] OnDemandRendering set to every frame");
    }

    private void ApplyURPSettings()
    {
        // Try to modify URP asset settings at runtime
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null)
        {
            Debug.LogWarning("[iOSSafariOptimizer] URP asset not found");
            return;
        }

        // Disable MSAA in URP
        urpAsset.msaaSampleCount = 1;
        Debug.Log("[iOSSafariOptimizer] URP MSAA disabled");

        // Set render scale to 1.0 (native resolution)
        urpAsset.renderScale = 1.0f;
        Debug.Log("[iOSSafariOptimizer] URP render scale set to 1.0 (native)");

        // Disable HDR
        // Note: HDR property might not be directly settable at runtime
        // urpAsset.supportsHDR = false;

        // Reduce shadow settings in URP
        urpAsset.shadowDistance = 0f;
        Debug.Log("[iOSSafariOptimizer] URP shadow distance set to 0");

        // Set max additional lights to 0 for performance
        urpAsset.maxAdditionalLightsCount = 0;
        Debug.Log("[iOSSafariOptimizer] URP max additional lights set to 0");
    }
}
