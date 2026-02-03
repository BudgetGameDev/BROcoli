// StreamlineDLSSPlugin.cpp
// DLSS and Frame Generation functions for GfxPluginStreamline
//
// This file contains the DLSS Super Resolution and Frame Generation API.
// It shares initialization state with StreamlineReflexPlugin.cpp.

#include <windows.h>
#include <cstdint>
#include <cstring>
#include <cstdio>
#include <ctime>
#include <mutex>
#include <cstdarg>

// Streamline SDK headers
#include "sl.h"
#include "sl_dlss.h"
#include "sl_dlss_g.h"

// ============================================================================
// External state from StreamlineReflexPlugin.cpp
// ============================================================================

// These are defined in StreamlineReflexPlugin.cpp and shared across the plugin
extern bool g_initialized;
extern bool g_dlssSupported;
extern bool g_dlssgSupported;
extern sl::DLSSMode g_dlssMode;
extern sl::DLSSGMode g_dlssgMode;
extern uint32_t g_numFramesToGenerate;
extern std::mutex g_mutex;

// External logging infrastructure from StreamlineReflexPlugin.cpp
extern FILE* g_logFile;
typedef void(*LogCallback)(const char*);
extern LogCallback g_logCallback;

// ============================================================================
// Export Macro
// ============================================================================

#define EXPORT extern "C" __declspec(dllexport)

// ============================================================================
// Logging (writes to shared log file)
// ============================================================================

static void LogDLSS(const char* format, ...)
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
    
    // Write to shared log file
    if (g_logFile)
    {
        fprintf(g_logFile, "%s[DLSS] %s\n", timestamp, buffer);
        fflush(g_logFile);
    }
    
    // C# callback
    if (g_logCallback)
    {
        char fullMsg[1100];
        snprintf(fullMsg, sizeof(fullMsg), "[DLSS] %s", buffer);
        g_logCallback(fullMsg);
    }
    
    // Debug output
    OutputDebugStringA("[GfxPluginStreamline/DLSS] ");
    OutputDebugStringA(buffer);
    OutputDebugStringA("\n");
}

// ============================================================================
// DLSS Structures for C# interop
// ============================================================================

struct DLSSSettingsExport
{
    uint32_t optimalRenderWidth;
    uint32_t optimalRenderHeight;
    uint32_t minRenderWidth;
    uint32_t minRenderHeight;
    uint32_t maxRenderWidth;
    uint32_t maxRenderHeight;
    float optimalSharpness;
};

struct DLSSGStateExport
{
    uint64_t estimatedVRAMUsage;
    uint32_t status;
    uint32_t minWidthOrHeight;
    uint32_t numFramesActuallyPresented;
    uint32_t numFramesToGenerateMax;
};

// ============================================================================
// DLSS Super Resolution (Legacy API - use functions in StreamlineReflexPlugin.cpp instead)
// ============================================================================

// Note: The main DLSS APIs (SLDLSS_SetOptions, SLDLSS_GetOptimalSettings, SLDLSS_TagResourceD3D12,
// SLDLSS_SetConstants, SLDLSS_Evaluate) are now in StreamlineReflexPlugin.cpp for better integration
// with buffer tagging. The functions below are legacy wrappers.

EXPORT bool SLDLSS_GetOptimalSettingsLegacy(
    int mode, 
    uint32_t targetWidth, 
    uint32_t targetHeight,
    DLSSSettingsExport* outSettings)
{
    if (!outSettings || !g_initialized || !g_dlssSupported) return false;
    
    std::lock_guard<std::mutex> lock(g_mutex);
    
    sl::DLSSOptions options{};
    options.mode = static_cast<sl::DLSSMode>(mode);
    options.outputWidth = targetWidth;
    options.outputHeight = targetHeight;
    
    sl::DLSSOptimalSettings settings{};
    
    using PFN_slDLSSGetOptimalSettings = sl::Result(*)(const sl::DLSSOptions&, sl::DLSSOptimalSettings&);
    PFN_slDLSSGetOptimalSettings fn = nullptr;
    sl::Result result = slGetFeatureFunction(sl::kFeatureDLSS, "slDLSSGetOptimalSettings", (void*&)fn);
    
    if (result != sl::Result::eOk || !fn)
    {
        LogDLSS("Failed to get slDLSSGetOptimalSettings function");
        return false;
    }
    
    result = fn(options, settings);
    if (result != sl::Result::eOk)
    {
        LogDLSS("slDLSSGetOptimalSettings failed: %d", (int)result);
        return false;
    }
    
    outSettings->optimalRenderWidth = settings.optimalRenderWidth;
    outSettings->optimalRenderHeight = settings.optimalRenderHeight;
    outSettings->minRenderWidth = settings.renderWidthMin;
    outSettings->minRenderHeight = settings.renderHeightMin;
    outSettings->maxRenderWidth = settings.renderWidthMax;
    outSettings->maxRenderHeight = settings.renderHeightMax;
    outSettings->optimalSharpness = settings.optimalSharpness;
    
    return true;
}

EXPORT bool SLDLSS_SetMode(int mode)
{
    if (!g_initialized || !g_dlssSupported) 
    {
        LogDLSS("Cannot set DLSS mode - not initialized or not supported");
        return false;
    }
    
    std::lock_guard<std::mutex> lock(g_mutex);
    
    sl::DLSSOptions options{};
    options.mode = static_cast<sl::DLSSMode>(mode);
    
    using PFN_slDLSSSetOptions = sl::Result(*)(const sl::ViewportHandle&, const sl::DLSSOptions&);
    PFN_slDLSSSetOptions fn = nullptr;
    sl::Result result = slGetFeatureFunction(sl::kFeatureDLSS, "slDLSSSetOptions", (void*&)fn);
    
    if (result != sl::Result::eOk || !fn)
    {
        LogDLSS("Failed to get slDLSSSetOptions function");
        return false;
    }
    
    sl::ViewportHandle viewport(0);
    result = fn(viewport, options);
    
    if (result == sl::Result::eOk)
    {
        g_dlssMode = static_cast<sl::DLSSMode>(mode);
        LogDLSS("DLSS mode set to: %d", mode);
        return true;
    }
    
    LogDLSS("Failed to set DLSS mode: %d", (int)result);
    return false;
}

EXPORT int SLDLSS_GetMode()
{
    return static_cast<int>(g_dlssMode);
}

// ============================================================================
// DLSS Frame Generation
// ============================================================================

EXPORT bool SLDLSSG_SetMode(int mode, int numFramesToGenerate)
{
    if (!g_initialized || !g_dlssgSupported)
    {
        LogDLSS("Cannot set Frame Gen mode - not initialized or not supported");
        return false;
    }
    
    std::lock_guard<std::mutex> lock(g_mutex);
    
    sl::DLSSGOptions options{};
    options.mode = static_cast<sl::DLSSGMode>(mode);
    options.numFramesToGenerate = static_cast<uint32_t>(numFramesToGenerate);
    
    using PFN_slDLSSGSetOptions = sl::Result(*)(const sl::ViewportHandle&, const sl::DLSSGOptions&);
    PFN_slDLSSGSetOptions fn = nullptr;
    sl::Result result = slGetFeatureFunction(sl::kFeatureDLSS_G, "slDLSSGSetOptions", (void*&)fn);
    
    if (result != sl::Result::eOk || !fn)
    {
        LogDLSS("Failed to get slDLSSGSetOptions function");
        return false;
    }
    
    sl::ViewportHandle viewport(0);
    result = fn(viewport, options);
    
    if (result == sl::Result::eOk)
    {
        g_dlssgMode = static_cast<sl::DLSSGMode>(mode);
        g_numFramesToGenerate = numFramesToGenerate;
        LogDLSS("Frame Gen mode set to: %d, frames: %d", mode, numFramesToGenerate);
        return true;
    }
    
    LogDLSS("Failed to set Frame Gen mode: %d", (int)result);
    return false;
}

EXPORT int SLDLSSG_GetMode()
{
    return static_cast<int>(g_dlssgMode);
}

EXPORT int SLDLSSG_GetNumFramesToGenerate()
{
    return static_cast<int>(g_numFramesToGenerate);
}

EXPORT bool SLDLSSG_GetStateLegacy(DLSSGStateExport* outState)
{
    if (!outState || !g_initialized || !g_dlssgSupported) return false;
    
    std::lock_guard<std::mutex> lock(g_mutex);
    
    sl::DLSSGState state{};
    sl::DLSSGOptions options{};
    options.flags = sl::DLSSGFlags::eRequestVRAMEstimate;
    
    using PFN_slDLSSGGetState = sl::Result(*)(const sl::ViewportHandle&, sl::DLSSGState&, const sl::DLSSGOptions*);
    PFN_slDLSSGGetState fn = nullptr;
    sl::Result result = slGetFeatureFunction(sl::kFeatureDLSS_G, "slDLSSGGetState", (void*&)fn);
    
    if (result != sl::Result::eOk || !fn)
    {
        return false;
    }
    
    sl::ViewportHandle viewport(0);
    result = fn(viewport, state, &options);
    
    if (result == sl::Result::eOk)
    {
        outState->estimatedVRAMUsage = state.estimatedVRAMUsageInBytes;
        outState->status = static_cast<uint32_t>(state.status);
        outState->minWidthOrHeight = state.minWidthOrHeight;
        outState->numFramesActuallyPresented = state.numFramesActuallyPresented;
        outState->numFramesToGenerateMax = state.numFramesToGenerateMax;
        return true;
    }
    
    return false;
}

// ============================================================================
// Convenience Presets
// ============================================================================

EXPORT bool SLStreamline_EnableDLSSQualityWithFrameGen2x()
{
    LogDLSS("Enabling DLSS Quality + Frame Gen 2x preset");
    
    // Enable DLSS Quality mode (67% render scale)
    if (!SLDLSS_SetMode(3)) // 3 = MaxQuality
    {
        LogDLSS("Failed to enable DLSS Quality mode");
        return false;
    }
    
    // Enable Frame Generation with 2x multiplier (1 generated frame)
    if (!SLDLSSG_SetMode(1, 1)) // 1 = On, 1 generated frame = 2x
    {
        LogDLSS("DLSS enabled but Frame Gen failed - partial success");
        return true;
    }
    
    LogDLSS("DLSS Quality + Frame Gen 2x enabled successfully");
    return true;
}

EXPORT bool SLStreamline_EnableDLSSPerformanceWithFrameGen3x()
{
    LogDLSS("Enabling DLSS Performance + Frame Gen 3x preset");
    
    // Enable DLSS Performance mode (50% render scale)
    if (!SLDLSS_SetMode(1)) // 1 = MaxPerformance
    {
        LogDLSS("Failed to enable DLSS Performance mode");
        return false;
    }
    
    // Enable Frame Generation with 3x multiplier (2 generated frames)
    if (!SLDLSSG_SetMode(1, 2)) // 1 = On, 2 generated frames = 3x
    {
        LogDLSS("DLSS enabled but Frame Gen failed - partial success");
        return true;
    }
    
    LogDLSS("DLSS Performance + Frame Gen 3x enabled successfully");
    return true;
}

EXPORT bool SLStreamline_DisableDLSSAndFrameGen()
{
    LogDLSS("Disabling DLSS and Frame Gen");
    
    bool success = true;
    
    // Disable Frame Generation first
    if (!SLDLSSG_SetMode(0, 0))
    {
        success = false;
    }
    
    // Disable DLSS
    if (!SLDLSS_SetMode(0))
    {
        success = false;
    }
    
    return success;
}
