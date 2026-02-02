using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// Automatically applies lower graphics settings on iOS/mobile browsers for better performance.
/// Attach to a GameObject in MainMenuScene or use [RuntimeInitializeOnLoadMethod] for early initialization.
/// </summary>
public class MobileGraphicsOptimizer : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int IsiOSMobile();
    
    [DllImport("__Internal")]
    private static extern int IsMobileBrowser();
#endif

    // Quality level indices (from QualitySettings.asset):
    // 0 = Very Low, 1 = Low, 2 = Medium, 3 = High, 4 = Very High, 5 = Ultra
    private const int QUALITY_VERY_LOW = 0;
    private const int QUALITY_LOW = 1;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeGraphicsSettings()
    {
        bool isiOS = CheckIsiOS();
        bool isMobile = CheckIsMobile();
        
        Debug.Log($"[MobileGraphicsOptimizer] Initializing - iOS: {isiOS}, Mobile: {isMobile}, Current Quality: {QualitySettings.GetQualityLevel()} ({QualitySettings.names[QualitySettings.GetQualityLevel()]})");
        
        if (isiOS)
        {
            // iOS Safari is particularly demanding - use Very Low
            ApplyiOSOptimizations();
        }
        else if (isMobile)
        {
            // Other mobile browsers - use Low
            ApplyMobileOptimizations();
        }
    }
    
    private static bool CheckIsiOS()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            return IsiOSMobile() == 1;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MobileGraphicsOptimizer] iOS detection failed: {e.Message}");
            return false;
        }
#elif UNITY_IOS
        return true;
#else
        return false;
#endif
    }
    
    private static bool CheckIsMobile()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            return IsMobileBrowser() == 1;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MobileGraphicsOptimizer] Mobile detection failed: {e.Message}");
            return false;
        }
#elif UNITY_IOS || UNITY_ANDROID
        return true;
#else
        return SystemInfo.deviceType == DeviceType.Handheld;
#endif
    }
    
    private static void ApplyiOSOptimizations()
    {
        Debug.Log("[MobileGraphicsOptimizer] Applying iOS optimizations (Very Low quality)");
        
        // Set to Very Low quality level
        QualitySettings.SetQualityLevel(QUALITY_VERY_LOW, true);
        
        // Additional iOS-specific optimizations
        Application.targetFrameRate = 30; // Cap at 30 FPS for battery and heat
        QualitySettings.vSyncCount = 0;   // Disable vsync, use target frame rate instead
        
        // Reduce resolution scale for better performance
        QualitySettings.resolutionScalingFixedDPIFactor = 0.75f;
        
        // Disable shadows completely on iOS
        QualitySettings.shadows = ShadowQuality.Disable;
        
        // Reduce texture quality
        QualitySettings.globalTextureMipmapLimit = 1; // Half resolution textures
        
        // Disable anti-aliasing
        QualitySettings.antiAliasing = 0;
        
        // Reduce particle budget
        QualitySettings.particleRaycastBudget = 4;
        
        Debug.Log($"[MobileGraphicsOptimizer] iOS settings applied - Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}, TargetFPS: {Application.targetFrameRate}");
    }
    
    private static void ApplyMobileOptimizations()
    {
        Debug.Log("[MobileGraphicsOptimizer] Applying mobile optimizations (Low quality)");
        
        // Set to Low quality level
        QualitySettings.SetQualityLevel(QUALITY_LOW, true);
        
        // Mobile optimizations (less aggressive than iOS)
        Application.targetFrameRate = 60; // Target 60 FPS
        QualitySettings.vSyncCount = 0;
        
        // Slight resolution reduction
        QualitySettings.resolutionScalingFixedDPIFactor = 0.85f;
        
        // Disable shadows
        QualitySettings.shadows = ShadowQuality.Disable;
        
        // Disable anti-aliasing
        QualitySettings.antiAliasing = 0;
        
        Debug.Log($"[MobileGraphicsOptimizer] Mobile settings applied - Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]}, TargetFPS: {Application.targetFrameRate}");
    }
}
