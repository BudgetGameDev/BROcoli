using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// Helper class for Progressive Web App (PWA) functionality.
/// Provides methods to check install status, show install prompts, and manage fullscreen.
/// </summary>
public static class PWAHelper
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int IsPWAInstalled();
    
    [DllImport("__Internal")]
    private static extern void ShowPWAInstallPrompt();
    
    [DllImport("__Internal")]
    private static extern void RequestFullscreen();
    
    [DllImport("__Internal")]
    private static extern void ExitFullscreen();
    
    [DllImport("__Internal")]
    private static extern int IsFullscreen();
    
    [DllImport("__Internal")]
    private static extern string GetPWADisplayMode();
#endif

    /// <summary>
    /// Check if the game is running as an installed PWA (standalone mode).
    /// </summary>
    public static bool IsInstalledAsPWA
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return IsPWAInstalled() == 1;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// Check if the browser is currently in fullscreen mode.
    /// </summary>
    public static bool IsInFullscreen
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return IsFullscreen() == 1;
#else
            return Screen.fullScreen;
#endif
        }
    }

    /// <summary>
    /// Get the current PWA display mode.
    /// Returns: "browser", "standalone", "standalone-ios", "fullscreen", or "minimal-ui"
    /// </summary>
    public static string DisplayMode
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return GetPWADisplayMode();
#else
            return "editor";
#endif
        }
    }

    /// <summary>
    /// Show the PWA install wizard/prompt.
    /// On iOS, shows instructions for "Add to Home Screen".
    /// On Android/Desktop with supported browsers, triggers native install prompt.
    /// </summary>
    public static void ShowInstallPrompt()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("[PWAHelper] Showing install prompt");
        ShowPWAInstallPrompt();
#else
        Debug.Log("[PWAHelper] Install prompt not available in editor");
#endif
    }

    /// <summary>
    /// Request fullscreen mode. Useful for non-PWA browser sessions.
    /// Note: Must be called from a user interaction (click/touch) to work.
    /// </summary>
    public static void EnterFullscreen()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("[PWAHelper] Requesting fullscreen");
        RequestFullscreen();
#else
        Screen.fullScreen = true;
#endif
    }

    /// <summary>
    /// Exit fullscreen mode.
    /// </summary>
    public static void LeaveFullscreen()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("[PWAHelper] Exiting fullscreen");
        ExitFullscreen();
#else
        Screen.fullScreen = false;
#endif
    }

    /// <summary>
    /// Toggle fullscreen mode.
    /// </summary>
    public static void ToggleFullscreen()
    {
        if (IsInFullscreen)
        {
            LeaveFullscreen();
        }
        else
        {
            EnterFullscreen();
        }
    }

    /// <summary>
    /// Log current PWA status (useful for debugging).
    /// </summary>
    public static void LogStatus()
    {
        Debug.Log($"[PWAHelper] Status - Installed: {IsInstalledAsPWA}, Fullscreen: {IsInFullscreen}, Mode: {DisplayMode}");
    }
}
