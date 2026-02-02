// StreamlineReflexPlugin.cpp
// Unity Native Plugin wrapping NVIDIA Streamline SDK for Reflex Low Latency
//
// This plugin provides a simplified interface for Unity to use Streamline's Reflex feature.
// It manually loads the Streamline interposer and calls Reflex functions.
//
// Build with CMake:
//   cd Assets/Plugins/NvidiaReflex
//   cmake -B build -G "Visual Studio 17 2022" -A x64
//   cmake --build build --config Release
//
// Required DLLs (copy to Assets/Plugins/x86_64/):
//   - StreamlineReflexPlugin.dll (built by this project)
//   - sl.interposer.dll (from Streamline SDK bin/x64/)
//   - sl.reflex.dll (from Streamline SDK bin/x64/)
//   - sl.pcl.dll (from Streamline SDK bin/x64/)

#include <windows.h>
#include <cstdint>
#include <cstring>
#include <mutex>
#include <string>
#include <sstream>

// Streamline SDK headers
#include "sl.h"
#include "sl_reflex.h"
#include "sl_pcl.h"
#include "sl_dlss.h"
#include "sl_dlss_g.h"

// ============================================================================
// Plugin State
// ============================================================================

static bool g_initialized = false;
static bool g_reflexSupported = false;
static bool g_pclSupported = false;
static sl::ReflexMode g_currentMode = sl::ReflexMode::eOff;
static uint64_t g_frameId = 0;
static std::mutex g_mutex;

// Logging callback storage
typedef void (*LogCallback)(const char* message);
static LogCallback g_logCallback = nullptr;

// ============================================================================
// Internal Helpers
// ============================================================================

static void LogMessage(const char* format, ...)
{
    char buffer[1024];
    va_list args;
    va_start(args, format);
    vsnprintf(buffer, sizeof(buffer), format, args);
    va_end(args);
    
    if (g_logCallback)
    {
        g_logCallback(buffer);
    }
    else
    {
        OutputDebugStringA("[StreamlineReflex] ");
        OutputDebugStringA(buffer);
        OutputDebugStringA("\n");
    }
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

// ============================================================================
// Export Macro
// ============================================================================

#define EXPORT extern "C" __declspec(dllexport)

// ============================================================================
// Initialization
// ============================================================================

EXPORT void SLReflex_SetLogCallback(LogCallback callback)
{
    g_logCallback = callback;
}

EXPORT bool SLReflex_IsAvailable()
{
    // Check if we can load the Streamline interposer
    // This is a quick check without full initialization
    HMODULE hModule = LoadLibraryA("sl.interposer.dll");
    if (hModule)
    {
        FreeLibrary(hModule);
        return true;
    }
    return false;
}

EXPORT bool SLReflex_Initialize(void* d3dDevice)
{
    std::lock_guard<std::mutex> lock(g_mutex);
    
    if (g_initialized)
    {
        LogMessage("Already initialized");
        return true;
    }
    
    LogMessage("Initializing Streamline Reflex...");
    
    // Set up Streamline preferences
    sl::Preferences prefs{};
    prefs.showConsole = false;
    prefs.logLevel = sl::LogLevel::eDefault;
    prefs.logMessageCallback = SLLogCallback;
    prefs.flags = sl::PreferenceFlags::eDisableCLStateTracking | 
                  sl::PreferenceFlags::eUseManualHooking;
    
    // We don't need an NVIDIA application ID for basic Reflex
    // But if you have one, set it here:
    // prefs.applicationId = YOUR_NVIDIA_APP_ID;
    
    prefs.engine = sl::EngineType::eUnity;
    prefs.engineVersion = "6000.0"; // Unity version
    
    // Request all features we need (Reflex, PCL, DLSS, Frame Generation)
    sl::Feature features[] = { 
        sl::kFeatureReflex, 
        sl::kFeaturePCL,
        sl::kFeatureDLSS,
        sl::kFeatureDLSS_G
    };
    prefs.featuresToLoad = features;
    prefs.numFeaturesToLoad = 4;
    
    // Initialize Streamline
    sl::Result result = slInit(prefs);
    if (result != sl::Result::eOk)
    {
        LogMessage("slInit failed with error: %d", (int)result);
        return false;
    }
    
    // Set the D3D device if provided
    if (d3dDevice)
    {
        result = slSetD3DDevice(d3dDevice);
        if (result != sl::Result::eOk)
        {
            LogMessage("slSetD3DDevice failed: %d", (int)result);
            slShutdown();
            return false;
        }
    }
    
    // Check if Reflex is supported
    sl::AdapterInfo adapterInfo{};
    result = slIsFeatureSupported(sl::kFeatureReflex, adapterInfo);
    g_reflexSupported = (result == sl::Result::eOk);
    
    result = slIsFeatureSupported(sl::kFeaturePCL, adapterInfo);
    g_pclSupported = (result == sl::Result::eOk);
    
    LogMessage("Reflex supported: %s", g_reflexSupported ? "YES" : "NO");
    LogMessage("PCL Stats supported: %s", g_pclSupported ? "YES" : "NO");
    
    g_initialized = true;
    g_frameId = 0;
    
    LogMessage("Streamline Reflex initialized successfully");
    return true;
}

EXPORT void SLReflex_Shutdown()
{
    std::lock_guard<std::mutex> lock(g_mutex);
    
    if (!g_initialized) return;
    
    LogMessage("Shutting down Streamline Reflex...");
    
    slShutdown();
    
    g_initialized = false;
    g_reflexSupported = false;
    g_pclSupported = false;
    g_currentMode = sl::ReflexMode::eOff;
    
    LogMessage("Streamline Reflex shut down");
}

// ============================================================================
// Reflex Control
// ============================================================================

EXPORT bool SLReflex_IsSupported()
{
    return g_reflexSupported;
}

EXPORT bool SLReflex_IsPCLSupported()
{
    return g_pclSupported;
}

EXPORT bool SLReflex_SetMode(int mode)
{
    if (!g_initialized) return false;
    
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
    options.frameLimitUs = 0; // No frame limit from Reflex
    
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
        result = slReflexSleep(*frameToken);
        if (result != sl::Result::eOk)
        {
            // Sleep failed - not critical, just log
        }
    }
}

// ============================================================================
// PCL Markers (Latency Measurement)
// ============================================================================

// Marker types matching sl::PCLMarker
// 0 = SimulationStart
// 1 = SimulationEnd
// 2 = RenderSubmitStart
// 3 = RenderSubmitEnd
// 4 = PresentStart
// 5 = PresentEnd
// 7 = TriggerFlash (for flash indicator)
// 8 = PCLatencyPing

EXPORT void SLReflex_SetMarker(int marker)
{
    if (!g_initialized) return;
    if (marker < 0 || marker >= (int)sl::PCLMarker::eMaximum) return;
    
    sl::FrameToken* frameToken = nullptr;
    uint32_t frameIndex = (uint32_t)(g_frameId & 0xFFFFFFFF);
    sl::Result result = slGetNewFrameToken(frameToken, &frameIndex);
    
    if (result == sl::Result::eOk && frameToken)
    {
        sl::PCLMarker pclMarker = static_cast<sl::PCLMarker>(marker);
        slPCLSetMarker(pclMarker, *frameToken);
    }
}

EXPORT void SLReflex_MarkSimulationStart()
{
    SLReflex_SetMarker((int)sl::PCLMarker::eSimulationStart);
}

EXPORT void SLReflex_MarkSimulationEnd()
{
    SLReflex_SetMarker((int)sl::PCLMarker::eSimulationEnd);
}

EXPORT void SLReflex_MarkRenderSubmitStart()
{
    SLReflex_SetMarker((int)sl::PCLMarker::eRenderSubmitStart);
}

EXPORT void SLReflex_MarkRenderSubmitEnd()
{
    SLReflex_SetMarker((int)sl::PCLMarker::eRenderSubmitEnd);
}

EXPORT void SLReflex_MarkPresentStart()
{
    SLReflex_SetMarker((int)sl::PCLMarker::ePresentStart);
}

EXPORT void SLReflex_MarkPresentEnd()
{
    SLReflex_SetMarker((int)sl::PCLMarker::ePresentEnd);
}

EXPORT void SLReflex_TriggerFlash()
{
    SLReflex_SetMarker((int)sl::PCLMarker::eTriggerFlash);
}

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
    
    // Average the last few frames of reports
    memset(stats, 0, sizeof(ReflexLatencyStats));
    int validFrames = 0;
    
    for (int i = 0; i < sl::kReflexFrameReportCount; i++)
    {
        const auto& report = state.frameReport[i];
        if (report.frameID == 0) continue;
        
        // Convert timestamps to durations (us to ms)
        if (report.simEndTime > report.simStartTime)
        {
            stats->simulationMs += (report.simEndTime - report.simStartTime) / 1000.0f;
        }
        if (report.renderSubmitEndTime > report.renderSubmitStartTime)
        {
            stats->renderSubmitMs += (report.renderSubmitEndTime - report.renderSubmitStartTime) / 1000.0f;
        }
        if (report.presentEndTime > report.presentStartTime)
        {
            stats->presentMs += (report.presentEndTime - report.presentStartTime) / 1000.0f;
        }
        if (report.driverEndTime > report.driverStartTime)
        {
            stats->driverMs += (report.driverEndTime - report.driverStartTime) / 1000.0f;
        }
        if (report.osRenderQueueEndTime > report.osRenderQueueStartTime)
        {
            stats->osRenderQueueMs += (report.osRenderQueueEndTime - report.osRenderQueueStartTime) / 1000.0f;
        }
        if (report.gpuRenderEndTime > report.gpuRenderStartTime)
        {
            stats->gpuRenderMs += (report.gpuRenderEndTime - report.gpuRenderStartTime) / 1000.0f;
        }
        
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
            // Clean up if still initialized
            if (g_initialized)
            {
                SLReflex_Shutdown();
            }
            break;
    }
    return TRUE;
}

// ============================================================================
// Unity Native Plugin Render Event (Optional)
// ============================================================================
// If you want to hook into Unity's render thread for precise timing,
// implement UnityRenderingEvent and call GL.IssuePluginEvent from C#

typedef void (*UnityRenderingEvent)(int eventID);

EXPORT UnityRenderingEvent SLReflex_GetRenderEventFunc()
{
    return [](int eventID) {
        switch (eventID)
        {
            case 0: // Frame begin
                SLReflex_BeginFrame();
                SLReflex_Sleep();
                SLReflex_MarkSimulationStart();
                break;
            case 1: // Simulation end
                SLReflex_MarkSimulationEnd();
                break;
            case 2: // Render submit start
                SLReflex_MarkRenderSubmitStart();
                break;
            case 3: // Render submit end
                SLReflex_MarkRenderSubmitEnd();
                break;
            case 4: // Present start
                SLReflex_MarkPresentStart();
                break;
            case 5: // Present end
                SLReflex_MarkPresentEnd();
                break;
        }
    };
}
