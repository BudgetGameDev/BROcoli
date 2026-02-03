// StreamlineReflex.cpp
// Reflex Low Latency and PCL marker APIs
//
// Provides latency reduction and performance measurement markers.

#include "StreamlineCommon.h"
#include "Unity/IUnityInterface.h"
#include <cstring>

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
        return false;
    
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
    (void)lpvReserved;
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
