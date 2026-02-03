// GfxPluginStreamline - Unity Native Plugin for NVIDIA Streamline SDK
// Provides Reflex Low Latency, DLSS Super Resolution, and Frame Generation
//
// This plugin uses Unity's native plugin interface (UnityPluginLoad) to properly
// capture the D3D12/D3D11 device from Unity's graphics system. This is REQUIRED
// for Streamline SDK to work correctly.
//
// Named GfxPlugin* so Unity loads it early in the graphics pipeline.

#include <windows.h>
#include <shlobj.h>  // For SHGetFolderPathA
#include <d3d12.h>
#include <dxgi1_4.h>
#include <cstdint>
#include <cstring>
#include <mutex>
#include <string>
#include <cstdarg>
#include <cstdio>
#include <ctime>

// Unity Native Plugin Interface
#include "Unity/IUnityInterface.h"
#include "Unity/IUnityGraphics.h"
#include "Unity/IUnityGraphicsD3D11.h"
#include "Unity/IUnityGraphicsD3D12.h"

// Streamline SDK headers
#include "sl.h"
#include "sl_reflex.h"
#include "sl_pcl.h"
#include "sl_dlss.h"
#include "sl_dlss_g.h"

// ============================================================================
// Unity Plugin Interface Globals
// ============================================================================

static IUnityInterfaces* s_UnityInterfaces = nullptr;
static IUnityGraphics* s_Graphics = nullptr;
static UnityGfxRenderer s_RendererType = kUnityGfxRendererNull;

// D3D12 interfaces (preferred for Windows 11)
static IUnityGraphicsD3D12v7* s_D3D12v7 = nullptr;
static IUnityGraphicsD3D12v6* s_D3D12v6 = nullptr;
static ID3D12Device* s_D3D12Device = nullptr;
static ID3D12CommandQueue* s_D3D12CommandQueue = nullptr;

// D3D11 interface (fallback)
static IUnityGraphicsD3D11* s_D3D11 = nullptr;
static ID3D11Device* s_D3D11Device = nullptr;

// ============================================================================
// Streamline Plugin State
// (Non-static for cross-module access from DLSS plugin)
// ============================================================================

bool g_initialized = false;
bool g_reflexSupported = false;
bool g_pclSupported = false;
bool g_dlssSupported = false;
bool g_dlssgSupported = false;
sl::ReflexMode g_currentMode = sl::ReflexMode::eOff;
sl::DLSSMode g_dlssMode = sl::DLSSMode::eOff;
sl::DLSSGMode g_dlssgMode = sl::DLSSGMode::eOff;
uint32_t g_numFramesToGenerate = 1;
uint64_t g_frameId = 0;
std::mutex g_mutex;

// Logging callback storage
typedef void (*LogCallback)(const char* message);
static LogCallback g_logCallback = nullptr;

// Error tracking for diagnostics
static int g_lastErrorCode = 0;  // 0 = no error, negative = init error
static const char* g_lastErrorMessage = "Not initialized yet";

// File logging
static FILE* g_logFile = nullptr;
static char g_logFilePath[MAX_PATH] = {0};

// ============================================================================
// Export Macro
// ============================================================================

#define EXPORT extern "C" __declspec(dllexport)

// ============================================================================
// File Logging
// ============================================================================

static void InitLogFile()
{
    if (g_logFile) return;  // Already initialized
    
    // Get the directory of the executable
    char exePath[MAX_PATH];
    GetModuleFileNameA(NULL, exePath, MAX_PATH);
    
    // Find the last backslash and replace filename with log filename
    char* lastSlash = strrchr(exePath, '\\');
    if (lastSlash)
    {
        *(lastSlash + 1) = '\0';
        snprintf(g_logFilePath, MAX_PATH, "%sGfxPluginStreamline.log", exePath);
    }
    else
    {
        snprintf(g_logFilePath, MAX_PATH, "GfxPluginStreamline.log");
    }
    
    // Open log file (overwrite previous)
    g_logFile = fopen(g_logFilePath, "w");
    if (g_logFile)
    {
        // Write header
        time_t now = time(NULL);
        struct tm* t = localtime(&now);
        fprintf(g_logFile, "=== GfxPluginStreamline Log ===\n");
        fprintf(g_logFile, "Started: %04d-%02d-%02d %02d:%02d:%02d\n",
                t->tm_year + 1900, t->tm_mon + 1, t->tm_mday,
                t->tm_hour, t->tm_min, t->tm_sec);
        fprintf(g_logFile, "Log file: %s\n", g_logFilePath);
        fprintf(g_logFile, "================================\n\n");
        fflush(g_logFile);
    }
}

static void CloseLogFile()
{
    if (g_logFile)
    {
        fprintf(g_logFile, "\n=== Log Closed ===\n");
        fclose(g_logFile);
        g_logFile = nullptr;
    }
}

// ============================================================================
// Logging
// ============================================================================

static void LogMessage(const char* format, ...)
{
    char buffer[1024];
    va_list args;
    va_start(args, format);
    vsnprintf(buffer, sizeof(buffer), format, args);
    va_end(args);
    
    // Get timestamp
    time_t now = time(NULL);
    struct tm* t = localtime(&now);
    char timestamp[32];
    snprintf(timestamp, sizeof(timestamp), "[%02d:%02d:%02d] ",
             t->tm_hour, t->tm_min, t->tm_sec);
    
    // Write to log file
    if (g_logFile)
    {
        fprintf(g_logFile, "%s%s\n", timestamp, buffer);
        fflush(g_logFile);  // Flush immediately so we don't lose logs on crash
    }
    
    // C# callback
    if (g_logCallback)
    {
        g_logCallback(buffer);
    }
    
    // Debug output
    OutputDebugStringA("[GfxPluginStreamline] ");
    OutputDebugStringA(buffer);
    OutputDebugStringA("\n");
}

static void SLLogCallback(sl::LogType type, const char* msg)
{
    const char* typeStr = "";
    switch (type)
    {
        case sl::LogType::eInfo: typeStr = "INFO"; break;
        case sl::LogType::eWarn: typeStr = "WARN"; break;
        case sl::LogType::eError: typeStr = "ERROR"; break;
        default: typeStr = "DEBUG"; break;
    }
    LogMessage("[SL_%s] %s", typeStr, msg);
}

EXPORT void SLReflex_SetLogCallback(LogCallback callback)
{
    g_logCallback = callback;
}

// ============================================================================
// Streamline Initialization (called from device event)
// ============================================================================

static bool InitializeStreamline()
{
    if (g_initialized)
    {
        LogMessage("Streamline already initialized");
        return true;
    }
    
    LogMessage("--- InitializeStreamline() called ---");
    
    // Need either D3D12 or D3D11 device
    void* d3dDevice = nullptr;
    if (s_D3D12Device)
    {
        d3dDevice = s_D3D12Device;
        LogMessage("Using D3D12 device for Streamline: 0x%p", s_D3D12Device);
        LogMessage("D3D12 CommandQueue: 0x%p", s_D3D12CommandQueue);
    }
    else if (s_D3D11Device)
    {
        d3dDevice = s_D3D11Device;
        LogMessage("Using D3D11 device for Streamline: 0x%p", s_D3D11Device);
    }
    else
    {
        g_lastErrorCode = -999;
        g_lastErrorMessage = "No D3D device available";
        LogMessage("ERROR: No D3D device available!");
        return false;
    }
    
    LogMessage("Initializing Streamline SDK...");
    
    // Check if sl.interposer.dll is loadable
    HMODULE hInterposer = LoadLibraryA("sl.interposer.dll");
    if (hInterposer)
    {
        LogMessage("sl.interposer.dll found and loadable");
        FreeLibrary(hInterposer);
    }
    else
    {
        DWORD err = GetLastError();
        LogMessage("WARNING: sl.interposer.dll load failed, error: %d", err);
    }
    
    // Set up Streamline preferences
    sl::Preferences prefs{};
    prefs.showConsole = true;  // Show Streamline console for debugging
    prefs.logLevel = sl::LogLevel::eVerbose;  // Verbose logging for debugging
    prefs.logMessageCallback = SLLogCallback;
    
    // Use recommended default flags:
    // - eDisableCLStateTracking: Don't track compute shader state (not needed for our use)
    // - eAllowOTA: Allow over-the-air updates to plugins (may get newer RTX 5070 support)
    // - eLoadDownloadedPlugins: Load any OTA-downloaded plugins
    prefs.flags = sl::PreferenceFlags::eDisableCLStateTracking | 
                  sl::PreferenceFlags::eAllowOTA |
                  sl::PreferenceFlags::eLoadDownloadedPlugins;
    LogMessage("Preferences: flags=eDisableCLStateTracking|eAllowOTA|eLoadDownloadedPlugins");
    
    // Specify we're using D3D12 (important for feature requirements)
    prefs.renderAPI = sl::RenderAPI::eD3D12;
    LogMessage("RenderAPI: D3D12");
    
    // Application ID - required for NGX-based features (DLSS, Frame Gen)
    // Use 0x0E658700 - this is the generic NVIDIA dev app ID registered for DLSS
    // (The log shows app_E658700 has dlss=310.5.2, while E658703 only has dlisp)
    prefs.applicationId = 0x0E658700;
    LogMessage("Application ID: 0x%08X (%u)", prefs.applicationId, prefs.applicationId);
    
    prefs.engine = sl::EngineType::eUnity;
    prefs.engineVersion = "6000.0";
    LogMessage("Engine: Unity 6000.0");
    
    // Setup plugin search paths
    // Priority order: 1) App directory (for NGX DLLs), 2) Unity Plugins folder, 3) OTA path
    static wchar_t appDirPath[MAX_PATH] = {0};
    static wchar_t unityPluginsPath[MAX_PATH] = {0};
    static wchar_t ngxPath[MAX_PATH] = {0};
    static const wchar_t* pluginPaths[4] = {nullptr, nullptr, nullptr, nullptr};
    int numPaths = 0;
    
    // Get application directory (where exe is) - NGX DLLs should be here or nearby
    char exePath[MAX_PATH];
    if (GetModuleFileNameA(NULL, exePath, MAX_PATH))
    {
        char* lastSlash = strrchr(exePath, '\\');
        if (lastSlash)
        {
            *lastSlash = '\0';  // Truncate to directory path
            MultiByteToWideChar(CP_UTF8, 0, exePath, -1, appDirPath, MAX_PATH);
            LogMessage("Application directory: %s", exePath);
            pluginPaths[numPaths++] = appDirPath;
            
            // Also add Unity's Plugins/x86_64 subdirectory (where Unity copies native plugins)
            // Format: {ExeName}_Data/Plugins/x86_64/
            char unityPlugins[MAX_PATH];
            
            // First, try to find the _Data folder dynamically
            // The exe name without extension becomes the Data folder prefix
            char* exeStart = strrchr(exePath, '\\');
            if (exeStart) exeStart++;
            else exeStart = exePath;
            
            // Reconstruct as: exePath/{ExeName}_Data/Plugins/x86_64
            // Since we truncated exePath, we need to get exe name from original module path
            char fullExePath[MAX_PATH];
            GetModuleFileNameA(NULL, fullExePath, MAX_PATH);
            char* exeName = strrchr(fullExePath, '\\');
            if (exeName) exeName++;
            else exeName = fullExePath;
            
            // Remove .exe extension
            char* ext = strrchr(exeName, '.');
            if (ext) *ext = '\0';
            
            snprintf(unityPlugins, MAX_PATH, "%s\\%s_Data\\Plugins\\x86_64", exePath, exeName);
            MultiByteToWideChar(CP_UTF8, 0, unityPlugins, -1, unityPluginsPath, MAX_PATH);
            LogMessage("Unity Plugins directory: %s", unityPlugins);
            pluginPaths[numPaths++] = unityPluginsPath;
        }
    }
    
    // Add OTA plugin path (ProgramData/NVIDIA/NGX/models)
    char programData[MAX_PATH];
    if (SUCCEEDED(SHGetFolderPathA(NULL, CSIDL_COMMON_APPDATA, NULL, 0, programData)))
    {
        // Convert to wide string
        MultiByteToWideChar(CP_UTF8, 0, programData, -1, ngxPath, MAX_PATH);
        wcscat_s(ngxPath, MAX_PATH, L"\\NVIDIA\\NGX\\models");
        
        LogMessage("OTA plugin path: %S", ngxPath);
        pluginPaths[numPaths++] = ngxPath;
    }
    
    prefs.pathsToPlugins = pluginPaths;
    prefs.numPathsToPlugins = numPaths;
    LogMessage("Total plugin search paths: %d", numPaths);
    
    // Log OTA paths for debugging
    char localAppData[MAX_PATH];
    if (SUCCEEDED(SHGetFolderPathA(NULL, CSIDL_LOCAL_APPDATA, NULL, 0, localAppData)))
    {
        LogMessage("LocalAppData: %s", localAppData);
    }
    
    // Request all features
    sl::Feature features[] = { 
        sl::kFeatureReflex, 
        sl::kFeaturePCL,
        sl::kFeatureDLSS,
        sl::kFeatureDLSS_G
    };
    prefs.featuresToLoad = features;
    prefs.numFeaturesToLoad = 4;
    LogMessage("Requesting features: Reflex, PCL, DLSS, DLSS_G");
    
    // Initialize Streamline
    LogMessage("Calling slInit()...");
    sl::Result result = slInit(prefs);
    LogMessage("slInit returned: %d", (int)result);
    
    if (result != sl::Result::eOk)
    {
        g_lastErrorCode = -(int)result;
        
        // Decode error for better logging
        const char* errorName = "Unknown";
        switch (result)
        {
            case sl::Result::eErrorNotInitialized: errorName = "eErrorNotInitialized"; break;
            case sl::Result::eErrorMissingOrInvalidAPI: errorName = "eErrorMissingOrInvalidAPI"; break;
            case sl::Result::eErrorDriverOutOfDate: errorName = "eErrorDriverOutOfDate"; break;
            case sl::Result::eErrorOSOutOfDate: errorName = "eErrorOSOutOfDate"; break;
            case sl::Result::eErrorOSDisabledHWS: errorName = "eErrorOSDisabledHWS"; break;
            case sl::Result::eErrorAdapterNotSupported: errorName = "eErrorAdapterNotSupported"; break;
            default: break;
        }
        
        g_lastErrorMessage = errorName;
        LogMessage("slInit FAILED: %s (%d)", errorName, (int)result);
        return false;
    }
    
    LogMessage("slInit succeeded!");
    LogMessage("Calling slSetD3DDevice(0x%p)...", d3dDevice);
    
    // Set the D3D device - CRITICAL for Streamline to work
    result = slSetD3DDevice(d3dDevice);
    LogMessage("slSetD3DDevice returned: %d", (int)result);
    
    if (result != sl::Result::eOk)
    {
        g_lastErrorCode = -100 - (int)result;  // Offset to distinguish from slInit errors
        g_lastErrorMessage = "slSetD3DDevice failed";
        LogMessage("slSetD3DDevice FAILED: %d", (int)result);
        slShutdown();
        return false;
    }
    
    LogMessage("D3D device set successfully");
    
    // Check feature support
    sl::AdapterInfo adapterInfo{};
    
    result = slIsFeatureSupported(sl::kFeatureReflex, adapterInfo);
    g_reflexSupported = (result == sl::Result::eOk);
    LogMessage("Reflex supported: %s", g_reflexSupported ? "YES" : "NO");
    
    result = slIsFeatureSupported(sl::kFeaturePCL, adapterInfo);
    g_pclSupported = (result == sl::Result::eOk);
    LogMessage("PCL supported: %s", g_pclSupported ? "YES" : "NO");
    
    result = slIsFeatureSupported(sl::kFeatureDLSS, adapterInfo);
    g_dlssSupported = (result == sl::Result::eOk);
    LogMessage("DLSS supported: %s", g_dlssSupported ? "YES" : "NO");
    
    result = slIsFeatureSupported(sl::kFeatureDLSS_G, adapterInfo);
    g_dlssgSupported = (result == sl::Result::eOk);
    LogMessage("Frame Generation supported: %s", g_dlssgSupported ? "YES" : "NO");
    
    g_initialized = true;
    g_frameId = 0;
    g_lastErrorCode = 0;
    g_lastErrorMessage = "Initialized successfully";
    
    LogMessage("=== Streamline SDK initialized successfully! ===");
    return true;
}

static void ShutdownStreamline()
{
    if (!g_initialized) return;
    
    LogMessage("Shutting down Streamline...");
    
    slShutdown();
    
    g_initialized = false;
    g_reflexSupported = false;
    g_pclSupported = false;
    g_dlssSupported = false;
    g_dlssgSupported = false;
    g_currentMode = sl::ReflexMode::eOff;
    g_dlssMode = sl::DLSSMode::eOff;
    g_dlssgMode = sl::DLSSGMode::eOff;
    
    LogMessage("Streamline shut down");
}

// ============================================================================
// Unity Graphics Device Event Callback
// ============================================================================

static void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
{
    switch (eventType)
    {
        case kUnityGfxDeviceEventInitialize:
        {
            s_RendererType = s_Graphics->GetRenderer();
            LogMessage("Graphics device initializing, renderer type: %d", (int)s_RendererType);
            
            if (s_RendererType == kUnityGfxRendererD3D12)
            {
                LogMessage("D3D12 renderer detected - preferred for NVIDIA features");
                
                // Try v7 first (latest), then fall back
                s_D3D12v7 = s_UnityInterfaces->Get<IUnityGraphicsD3D12v7>();
                if (s_D3D12v7)
                {
                    s_D3D12Device = s_D3D12v7->GetDevice();
                    s_D3D12CommandQueue = s_D3D12v7->GetCommandQueue();
                    LogMessage("Got D3D12 device via IUnityGraphicsD3D12v7");
                }
                else
                {
                    s_D3D12v6 = s_UnityInterfaces->Get<IUnityGraphicsD3D12v6>();
                    if (s_D3D12v6)
                    {
                        s_D3D12Device = s_D3D12v6->GetDevice();
                        s_D3D12CommandQueue = s_D3D12v6->GetCommandQueue();
                        LogMessage("Got D3D12 device via IUnityGraphicsD3D12v6");
                    }
                }
                
                if (s_D3D12Device)
                {
                    LogMessage("D3D12 Device: 0x%p", s_D3D12Device);
                    LogMessage("D3D12 CommandQueue: 0x%p", s_D3D12CommandQueue);
                }
                else
                {
                    LogMessage("ERROR: Failed to get D3D12 device from Unity!");
                }
            }
            else if (s_RendererType == kUnityGfxRendererD3D11)
            {
                LogMessage("D3D11 renderer detected");
                
                s_D3D11 = s_UnityInterfaces->Get<IUnityGraphicsD3D11>();
                if (s_D3D11)
                {
                    s_D3D11Device = s_D3D11->GetDevice();
                    LogMessage("Got D3D11 device: 0x%p", s_D3D11Device);
                }
                else
                {
                    LogMessage("ERROR: Failed to get D3D11 interface from Unity!");
                }
            }
            else
            {
                LogMessage("Unsupported renderer type: %d (NVIDIA features require D3D11/D3D12)", (int)s_RendererType);
            }
            
            // Initialize Streamline now that we have the device
            if (s_D3D12Device || s_D3D11Device)
            {
                InitializeStreamline();
            }
            break;
        }
        
        case kUnityGfxDeviceEventShutdown:
        {
            LogMessage("Graphics device shutting down");
            ShutdownStreamline();
            
            s_D3D12Device = nullptr;
            s_D3D12CommandQueue = nullptr;
            s_D3D11Device = nullptr;
            s_D3D12v7 = nullptr;
            s_D3D12v6 = nullptr;
            s_D3D11 = nullptr;
            s_RendererType = kUnityGfxRendererNull;
            break;
        }
        
        default:
            break;
    }
}

// ============================================================================
// Unity Plugin Entry Points
// ============================================================================

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API 
UnityPluginLoad(IUnityInterfaces* unityInterfaces)
{
    // Initialize log file first so we can log everything
    InitLogFile();
    
    LogMessage("=== GfxPluginStreamline Loading ===");
    LogMessage("Plugin version: 1.0.0");
    LogMessage("Streamline SDK version: 2.10.3");
    
    // Log system info
    LogMessage("--- System Info ---");
    SYSTEM_INFO sysInfo;
    GetSystemInfo(&sysInfo);
    LogMessage("Processor architecture: %d", sysInfo.wProcessorArchitecture);
    LogMessage("Number of processors: %d", sysInfo.dwNumberOfProcessors);
    
    OSVERSIONINFOEXA osvi = {0};
    osvi.dwOSVersionInfoSize = sizeof(osvi);
    #pragma warning(disable: 4996)
    GetVersionExA((LPOSVERSIONINFOA)&osvi);
    #pragma warning(default: 4996)
    LogMessage("Windows version: %d.%d.%d", osvi.dwMajorVersion, osvi.dwMinorVersion, osvi.dwBuildNumber);
    LogMessage("-------------------");
    
    s_UnityInterfaces = unityInterfaces;
    s_Graphics = unityInterfaces->Get<IUnityGraphics>();
    
    if (s_Graphics)
    {
        LogMessage("Got IUnityGraphics interface");
        s_Graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);
        
        // In case the device is already initialized when we load
        OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);
    }
    else
    {
        LogMessage("ERROR: Failed to get IUnityGraphics interface!");
    }
    
    LogMessage("=== GfxPluginStreamline Loaded ===");
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload()
{
    LogMessage("=== GfxPluginStreamline Unloading ===");
    
    if (s_Graphics)
    {
        s_Graphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);
    }
    
    ShutdownStreamline();
    
    s_UnityInterfaces = nullptr;
    s_Graphics = nullptr;
    
    LogMessage("=== GfxPluginStreamline Unloaded ===");
    
    // Close log file last
    CloseLogFile();
}

// ============================================================================
// Public API - Availability Checks
// ============================================================================

EXPORT bool SLReflex_IsAvailable()
{
    // Check if Streamline DLLs can be loaded
    HMODULE hModule = LoadLibraryA("sl.interposer.dll");
    if (hModule)
    {
        FreeLibrary(hModule);
        return true;
    }
    return false;
}

EXPORT bool SLReflex_IsInitialized()
{
    return g_initialized;
}

EXPORT int SLReflex_GetLastErrorCode()
{
    return g_lastErrorCode;
}

EXPORT const char* SLReflex_GetLastErrorMessage()
{
    return g_lastErrorMessage;
}

EXPORT bool SLReflex_HasD3D12Device()
{
    return s_D3D12Device != nullptr;
}

EXPORT bool SLReflex_HasD3D11Device()
{
    return s_D3D11Device != nullptr;
}

EXPORT int SLReflex_GetRendererType()
{
    return (int)s_RendererType;
}

// Try to manually trigger initialization (for debugging)
EXPORT bool SLReflex_TryInitialize()
{
    if (g_initialized)
    {
        return true;
    }
    
    if (s_D3D12Device || s_D3D11Device)
    {
        return InitializeStreamline();
    }
    
    g_lastErrorCode = -999;
    g_lastErrorMessage = "No D3D device available";
    return false;
}

EXPORT bool SLReflex_IsSupported()
{
    return g_reflexSupported;
}

EXPORT bool SLReflex_IsPCLSupported()
{
    return g_pclSupported;
}

EXPORT bool SLDLSS_IsSupported()
{
    return g_dlssSupported;
}

EXPORT bool SLDLSS_IsFrameGenSupported()
{
    return g_dlssgSupported;
}

// ============================================================================
// DLSS Super Resolution - Buffer Tagging & Evaluation
// ============================================================================

// Stored constants for DLSS frame evaluation
static sl::Constants g_dlssConstants{};
static bool g_dlssConstantsValid = false;
static sl::ViewportHandle g_dlssViewport = {0};

// Set DLSS viewport
EXPORT void SLDLSS_SetViewport(uint32_t viewportId)
{
    g_dlssViewport = sl::ViewportHandle(viewportId);
    LogMessage("DLSS viewport set to: %u", viewportId);
}

// Set common constants for DLSS (camera matrices, jitter, etc.)
// This must be called each frame before tagging resources
EXPORT bool SLDLSS_SetConstants(
    // Camera matrices (row-major, no jitter)
    const float* cameraViewToClip,      // 16 floats (4x4 matrix)
    const float* clipToCameraView,      // 16 floats (4x4 matrix)
    const float* clipToPrevClip,        // 16 floats (4x4 matrix)
    const float* prevClipToClip,        // 16 floats (4x4 matrix)
    // Jitter and motion vector params
    float jitterOffsetX, float jitterOffsetY,
    float mvecScaleX, float mvecScaleY,
    // Camera params
    float cameraNear, float cameraFar,
    float cameraFOV, float cameraAspectRatio,
    // Flags
    bool depthInverted,
    bool cameraMotionIncluded,
    bool reset
)
{
    if (!g_initialized)
    {
        LogMessage("SLDLSS_SetConstants failed: not initialized");
        return false;
    }
    
    // Copy matrices
    memcpy(&g_dlssConstants.cameraViewToClip, cameraViewToClip, sizeof(float) * 16);
    memcpy(&g_dlssConstants.clipToCameraView, clipToCameraView, sizeof(float) * 16);
    memcpy(&g_dlssConstants.clipToPrevClip, clipToPrevClip, sizeof(float) * 16);
    memcpy(&g_dlssConstants.prevClipToClip, prevClipToClip, sizeof(float) * 16);
    
    // Set jitter offset (in pixel space)
    g_dlssConstants.jitterOffset = {jitterOffsetX, jitterOffsetY};
    
    // Motion vector scale (to normalize to [-1,1] range)
    g_dlssConstants.mvecScale = {mvecScaleX, mvecScaleY};
    
    // Camera parameters
    g_dlssConstants.cameraNear = cameraNear;
    g_dlssConstants.cameraFar = cameraFar;
    g_dlssConstants.cameraFOV = cameraFOV;
    g_dlssConstants.cameraAspectRatio = cameraAspectRatio;
    
    // Flags
    g_dlssConstants.depthInverted = depthInverted ? sl::Boolean::eTrue : sl::Boolean::eFalse;
    g_dlssConstants.cameraMotionIncluded = cameraMotionIncluded ? sl::Boolean::eTrue : sl::Boolean::eFalse;
    g_dlssConstants.motionVectors3D = sl::Boolean::eFalse;  // Unity uses 2D motion vectors
    g_dlssConstants.reset = reset ? sl::Boolean::eTrue : sl::Boolean::eFalse;
    g_dlssConstants.orthographicProjection = sl::Boolean::eFalse;
    g_dlssConstants.motionVectorsDilated = sl::Boolean::eFalse;
    g_dlssConstants.motionVectorsJittered = sl::Boolean::eFalse;
    
    g_dlssConstantsValid = true;
    
    // Submit constants to Streamline
    sl::FrameToken* frameToken = nullptr;
    uint32_t frameIndex = (uint32_t)(g_frameId & 0xFFFFFFFF);
    sl::Result result = slGetNewFrameToken(frameToken, &frameIndex);
    
    if (result == sl::Result::eOk && frameToken)
    {
        result = slSetConstants(g_dlssConstants, *frameToken, g_dlssViewport);
        if (result != sl::Result::eOk)
        {
            LogMessage("slSetConstants failed: %d", (int)result);
            return false;
        }
    }
    
    return true;
}

// Tag a D3D12 resource for DLSS
// bufferType: 0=Depth, 1=MotionVectors, 3=ScalingInputColor, 4=ScalingOutputColor
EXPORT bool SLDLSS_TagResourceD3D12(
    void* d3d12Resource,        // ID3D12Resource*
    uint32_t bufferType,        // sl::BufferType
    uint32_t width,
    uint32_t height,
    uint32_t nativeFormat,      // DXGI_FORMAT
    uint32_t state              // D3D12_RESOURCE_STATE
)
{
    if (!g_initialized || !d3d12Resource)
    {
        LogMessage("SLDLSS_TagResourceD3D12 failed: not initialized or null resource");
        return false;
    }
    
    sl::Resource slResource{};
    slResource.type = sl::ResourceType::eTex2d;
    slResource.native = d3d12Resource;
    slResource.width = width;
    slResource.height = height;
    slResource.nativeFormat = nativeFormat;
    slResource.state = state;
    
    sl::ResourceTag tag{};
    tag.type = static_cast<sl::BufferType>(bufferType);
    tag.resource = &slResource;
    tag.extent = sl::Extent{0, 0, width, height};
    tag.lifecycle = sl::ResourceLifecycle::eOnlyValidNow;
    
    sl::FrameToken* frameToken = nullptr;
    uint32_t frameIndex = (uint32_t)(g_frameId & 0xFFFFFFFF);
    sl::Result result = slGetNewFrameToken(frameToken, &frameIndex);
    
    if (result != sl::Result::eOk || !frameToken)
    {
        LogMessage("Failed to get frame token for tagging");
        return false;
    }
    
    // Tag the resource
    result = slSetTagForFrame(*frameToken, g_dlssViewport, &tag, 1, nullptr);
    
    if (result != sl::Result::eOk)
    {
        LogMessage("slSetTagForFrame failed for buffer type %u: %d", bufferType, (int)result);
        return false;
    }
    
    return true;
}

// Set DLSS options (mode, output resolution)
EXPORT bool SLDLSS_SetOptions(
    int mode,           // 0=Off, 1=MaxPerf, 2=Balanced, 3=MaxQuality, 4=UltraPerf, 5=UltraQuality, 6=DLAA
    uint32_t outputWidth,
    uint32_t outputHeight,
    bool colorBuffersHDR
)
{
    if (!g_initialized)
    {
        LogMessage("SLDLSS_SetOptions failed: not initialized");
        return false;
    }
    
    sl::DLSSOptions options{};
    options.mode = static_cast<sl::DLSSMode>(mode);
    options.outputWidth = outputWidth;
    options.outputHeight = outputHeight;
    options.colorBuffersHDR = colorBuffersHDR ? sl::Boolean::eTrue : sl::Boolean::eFalse;
    options.preExposure = 1.0f;
    options.exposureScale = 1.0f;
    
    sl::Result result = slDLSSSetOptions(g_dlssViewport, options);
    
    if (result == sl::Result::eOk)
    {
        g_dlssMode = options.mode;
        LogMessage("DLSS mode set to: %d, output: %ux%u", mode, outputWidth, outputHeight);
        return true;
    }
    
    LogMessage("slDLSSSetOptions failed: %d", (int)result);
    return false;
}

// Get optimal DLSS settings for a given mode and output resolution
EXPORT bool SLDLSS_GetOptimalSettings(
    int mode,
    uint32_t outputWidth,
    uint32_t outputHeight,
    uint32_t* optimalRenderWidth,
    uint32_t* optimalRenderHeight,
    uint32_t* minRenderWidth,
    uint32_t* minRenderHeight,
    uint32_t* maxRenderWidth,
    uint32_t* maxRenderHeight
)
{
    if (!g_initialized) return false;
    
    sl::DLSSOptions options{};
    options.mode = static_cast<sl::DLSSMode>(mode);
    options.outputWidth = outputWidth;
    options.outputHeight = outputHeight;
    
    sl::DLSSOptimalSettings settings{};
    sl::Result result = slDLSSGetOptimalSettings(options, settings);
    
    if (result == sl::Result::eOk)
    {
        if (optimalRenderWidth) *optimalRenderWidth = settings.optimalRenderWidth;
        if (optimalRenderHeight) *optimalRenderHeight = settings.optimalRenderHeight;
        if (minRenderWidth) *minRenderWidth = settings.renderWidthMin;
        if (minRenderHeight) *minRenderHeight = settings.renderHeightMin;
        if (maxRenderWidth) *maxRenderWidth = settings.renderWidthMax;
        if (maxRenderHeight) *maxRenderHeight = settings.renderHeightMax;
        
        LogMessage("DLSS optimal settings for mode %d @ %ux%u: render=%ux%u", 
                   mode, outputWidth, outputHeight, 
                   settings.optimalRenderWidth, settings.optimalRenderHeight);
        return true;
    }
    
    return false;
}

// Evaluate DLSS (run the upscaling)
// Call this after tagging all resources and setting constants
EXPORT bool SLDLSS_Evaluate(void* commandBuffer)
{
    if (!g_initialized || !g_dlssSupported)
    {
        LogMessage("SLDLSS_Evaluate failed: not initialized or DLSS not supported");
        return false;
    }
    
    sl::FrameToken* frameToken = nullptr;
    uint32_t frameIndex = (uint32_t)(g_frameId & 0xFFFFFFFF);
    sl::Result result = slGetNewFrameToken(frameToken, &frameIndex);
    
    if (result != sl::Result::eOk || !frameToken)
    {
        LogMessage("Failed to get frame token for DLSS evaluation");
        return false;
    }
    
    // Evaluate DLSS
    result = slEvaluateFeature(sl::kFeatureDLSS, *frameToken, nullptr, 0, 
                               static_cast<sl::CommandBuffer*>(commandBuffer));
    
    if (result != sl::Result::eOk)
    {
        LogMessage("slEvaluateFeature(DLSS) failed: %d", (int)result);
        return false;
    }
    
    return true;
}

// ============================================================================
// DLSS Frame Generation (DLSS-G / MFG) APIs
// ============================================================================

// Set Frame Generation options
EXPORT bool SLDLSSG_SetOptions(
    int mode,               // 0=Off, 1=On, 2=Auto
    uint32_t numFramesToGenerate,  // 1=2x, 2=3x, 3=4x
    uint32_t colorWidth,
    uint32_t colorHeight,
    uint32_t mvecDepthWidth,
    uint32_t mvecDepthHeight
)
{
    if (!g_initialized)
    {
        LogMessage("SLDLSSG_SetOptions failed: not initialized");
        return false;
    }
    
    sl::DLSSGOptions options{};
    options.mode = static_cast<sl::DLSSGMode>(mode);
    options.numFramesToGenerate = numFramesToGenerate;
    options.colorWidth = colorWidth;
    options.colorHeight = colorHeight;
    options.mvecDepthWidth = mvecDepthWidth;
    options.mvecDepthHeight = mvecDepthHeight;
    
    // Enable dynamic resolution support
    options.flags = sl::DLSSGFlags::eDynamicResolutionEnabled;
    
    sl::Result result = slDLSSGSetOptions(g_dlssViewport, options);
    
    if (result == sl::Result::eOk)
    {
        g_dlssgMode = options.mode;
        g_numFramesToGenerate = numFramesToGenerate;
        LogMessage("DLSS-G mode set to: %d, frames to generate: %u", mode, numFramesToGenerate);
        return true;
    }
    
    LogMessage("slDLSSGSetOptions failed: %d", (int)result);
    return false;
}

// Get Frame Generation state
EXPORT bool SLDLSSG_GetState(
    uint64_t* estimatedVRAMUsage,
    uint32_t* status,
    uint32_t* minWidthOrHeight,
    uint32_t* numFramesPresented,
    uint32_t* numFramesToGenerateMax
)
{
    if (!g_initialized) return false;
    
    sl::DLSSGState state{};
    sl::Result result = slDLSSGGetState(g_dlssViewport, state, nullptr);
    
    if (result == sl::Result::eOk)
    {
        if (estimatedVRAMUsage) *estimatedVRAMUsage = state.estimatedVRAMUsageInBytes;
        if (status) *status = static_cast<uint32_t>(state.status);
        if (minWidthOrHeight) *minWidthOrHeight = state.minWidthOrHeight;
        if (numFramesPresented) *numFramesPresented = state.numFramesActuallyPresented;
        if (numFramesToGenerateMax) *numFramesToGenerateMax = state.numFramesToGenerateMax;
        
        LogMessage("DLSS-G state: status=%u, maxFrames=%u", 
                   static_cast<uint32_t>(state.status), state.numFramesToGenerateMax);
        return true;
    }
    
    return false;
}

// Tag HUDLessColor buffer for Frame Generation
// This must be the final color without UI elements
EXPORT bool SLDLSSG_TagHUDLessColor(
    void* d3d12Resource,
    uint32_t width,
    uint32_t height,
    uint32_t nativeFormat,
    uint32_t state
)
{
    return SLDLSS_TagResourceD3D12(d3d12Resource, sl::kBufferTypeHUDLessColor, 
                                    width, height, nativeFormat, state);
}

// Tag UIColorAndAlpha buffer for Frame Generation
// This is the UI-only buffer with alpha
EXPORT bool SLDLSSG_TagUIColorAndAlpha(
    void* d3d12Resource,
    uint32_t width,
    uint32_t height,
    uint32_t nativeFormat,
    uint32_t state
)
{
    return SLDLSS_TagResourceD3D12(d3d12Resource, sl::kBufferTypeUIColorAndAlpha, 
                                    width, height, nativeFormat, state);
}

// Legacy initialize function - now initialization happens automatically in UnityPluginLoad
EXPORT bool SLReflex_Initialize(void* d3dDevice)
{
    // Device is ignored - we get it from Unity's plugin interface
    (void)d3dDevice;
    
    if (g_initialized)
    {
        return true;
    }
    
    // Try to initialize if we have a device
    if (s_D3D12Device || s_D3D11Device)
    {
        return InitializeStreamline();
    }
    
    LogMessage("SLReflex_Initialize called but no device available yet");
    return false;
}

EXPORT void SLReflex_Shutdown()
{
    std::lock_guard<std::mutex> lock(g_mutex);
    ShutdownStreamline();
}

// ============================================================================
// Reflex Control
// ============================================================================

EXPORT bool SLReflex_SetMode(int mode)
{
    if (!g_initialized || !g_reflexSupported) return false;
    
    sl::ReflexMode newMode;
    switch (mode)
    {
        case 0: newMode = sl::ReflexMode::eOff; break;
        case 1: newMode = sl::ReflexMode::eLowLatency; break;
        case 2: newMode = sl::ReflexMode::eLowLatencyWithBoost; break;
        default: return false;
    }
    
    sl::ReflexOptions options{};
    options.mode = newMode;
    options.frameLimitUs = 0;
    
    sl::Result result = slReflexSetOptions(options);
    if (result == sl::Result::eOk)
    {
        g_currentMode = newMode;
        LogMessage("Reflex mode set to: %d", mode);
        return true;
    }
    
    LogMessage("Failed to set Reflex mode: %d", (int)result);
    return false;
}

EXPORT int SLReflex_GetMode()
{
    return (int)g_currentMode;
}

EXPORT bool SLReflex_GetState(bool* lowLatencyAvailable, bool* flashIndicatorDriverControlled)
{
    if (!g_initialized) return false;
    
    sl::ReflexState state{};
    sl::Result result = slReflexGetState(state);
    
    if (result == sl::Result::eOk)
    {
        if (lowLatencyAvailable) *lowLatencyAvailable = state.lowLatencyAvailable;
        if (flashIndicatorDriverControlled) *flashIndicatorDriverControlled = state.flashIndicatorDriverControlled;
        return true;
    }
    
    return false;
}

// ============================================================================
// Frame Management
// ============================================================================

EXPORT void SLReflex_BeginFrame()
{
    if (!g_initialized) return;
    g_frameId++;
}

EXPORT void SLReflex_Sleep()
{
    if (!g_initialized) return;
    
    sl::FrameToken* frameToken = nullptr;
    uint32_t frameIndex = (uint32_t)(g_frameId & 0xFFFFFFFF);
    sl::Result result = slGetNewFrameToken(frameToken, &frameIndex);
    
    if (result == sl::Result::eOk && frameToken)
    {
        slReflexSleep(*frameToken);
    }
}

// ============================================================================
// PCL Markers
// ============================================================================

EXPORT void SLReflex_SetMarker(int marker)
{
    if (!g_initialized) return;
    if (marker < 0 || marker >= (int)sl::PCLMarker::eMaximum) return;
    
    sl::FrameToken* frameToken = nullptr;
    uint32_t frameIndex = (uint32_t)(g_frameId & 0xFFFFFFFF);
    sl::Result result = slGetNewFrameToken(frameToken, &frameIndex);
    
    if (result == sl::Result::eOk && frameToken)
    {
        slPCLSetMarker(static_cast<sl::PCLMarker>(marker), *frameToken);
    }
}

EXPORT void SLReflex_MarkSimulationStart() { SLReflex_SetMarker((int)sl::PCLMarker::eSimulationStart); }
EXPORT void SLReflex_MarkSimulationEnd() { SLReflex_SetMarker((int)sl::PCLMarker::eSimulationEnd); }
EXPORT void SLReflex_MarkRenderSubmitStart() { SLReflex_SetMarker((int)sl::PCLMarker::eRenderSubmitStart); }
EXPORT void SLReflex_MarkRenderSubmitEnd() { SLReflex_SetMarker((int)sl::PCLMarker::eRenderSubmitEnd); }
EXPORT void SLReflex_MarkPresentStart() { SLReflex_SetMarker((int)sl::PCLMarker::ePresentStart); }
EXPORT void SLReflex_MarkPresentEnd() { SLReflex_SetMarker((int)sl::PCLMarker::ePresentEnd); }
EXPORT void SLReflex_TriggerFlash() { SLReflex_SetMarker((int)sl::PCLMarker::eTriggerFlash); }

// ============================================================================
// Latency Stats
// ============================================================================

#pragma pack(push, 1)
struct ReflexLatencyStats
{
    float simulationMs;
    float renderSubmitMs;
    float presentMs;
    float driverMs;
    float osRenderQueueMs;
    float gpuRenderMs;
    float totalLatencyMs;
};
#pragma pack(pop)

EXPORT bool SLReflex_GetLatencyStats(ReflexLatencyStats* stats)
{
    if (!g_initialized || !stats) return false;
    
    sl::ReflexState state{};
    sl::Result result = slReflexGetState(state);
    
    if (result != sl::Result::eOk || !state.latencyReportAvailable)
    {
        return false;
    }
    
    memset(stats, 0, sizeof(ReflexLatencyStats));
    int validFrames = 0;
    
    for (int i = 0; i < sl::kReflexFrameReportCount; i++)
    {
        const auto& report = state.frameReport[i];
        if (report.frameID == 0) continue;
        
        if (report.simEndTime > report.simStartTime)
            stats->simulationMs += (report.simEndTime - report.simStartTime) / 1000.0f;
        if (report.renderSubmitEndTime > report.renderSubmitStartTime)
            stats->renderSubmitMs += (report.renderSubmitEndTime - report.renderSubmitStartTime) / 1000.0f;
        if (report.presentEndTime > report.presentStartTime)
            stats->presentMs += (report.presentEndTime - report.presentStartTime) / 1000.0f;
        if (report.driverEndTime > report.driverStartTime)
            stats->driverMs += (report.driverEndTime - report.driverStartTime) / 1000.0f;
        if (report.osRenderQueueEndTime > report.osRenderQueueStartTime)
            stats->osRenderQueueMs += (report.osRenderQueueEndTime - report.osRenderQueueStartTime) / 1000.0f;
        if (report.gpuRenderEndTime > report.gpuRenderStartTime)
            stats->gpuRenderMs += (report.gpuRenderEndTime - report.gpuRenderStartTime) / 1000.0f;
        
        validFrames++;
    }
    
    if (validFrames > 0)
    {
        float divisor = (float)validFrames;
        stats->simulationMs /= divisor;
        stats->renderSubmitMs /= divisor;
        stats->presentMs /= divisor;
        stats->driverMs /= divisor;
        stats->osRenderQueueMs /= divisor;
        stats->gpuRenderMs /= divisor;
        stats->totalLatencyMs = stats->simulationMs + stats->renderSubmitMs + 
                                stats->presentMs + stats->driverMs + 
                                stats->osRenderQueueMs + stats->gpuRenderMs;
        return true;
    }
    
    return false;
}

// ============================================================================
// Render Event (for GL.IssuePluginEvent)
// ============================================================================

typedef void (*UnityRenderingEvent)(int eventID);

static void UNITY_INTERFACE_API OnRenderEvent(int eventID)
{
    switch (eventID)
    {
        case 0: // Frame begin
            SLReflex_BeginFrame();
            SLReflex_Sleep();
            SLReflex_MarkSimulationStart();
            break;
        case 1: SLReflex_MarkSimulationEnd(); break;
        case 2: SLReflex_MarkRenderSubmitStart(); break;
        case 3: SLReflex_MarkRenderSubmitEnd(); break;
        case 4: SLReflex_MarkPresentStart(); break;
        case 5: SLReflex_MarkPresentEnd(); break;
    }
}

EXPORT UnityRenderingEvent SLReflex_GetRenderEventFunc()
{
    return OnRenderEvent;
}

// ============================================================================
// DLL Entry Point
// ============================================================================

BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved)
{
    switch (fdwReason)
    {
        case DLL_PROCESS_ATTACH:
            DisableThreadLibraryCalls(hinstDLL);
            break;
        case DLL_PROCESS_DETACH:
            break;
    }
    return TRUE;
}
