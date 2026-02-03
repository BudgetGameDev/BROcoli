// StreamlineDLSSPlugin.cpp
// Legacy DLSS API wrappers and convenience presets
//
// This file provides backward-compatible API wrappers. New code should
// use the functions in StreamlineDLSSCore.cpp directly.

#include "StreamlineCommon.h"
#include <cstdarg>
#include <ctime>
#include <cstring>

// ============================================================================
// Legacy Logging (for backward compatibility)
// ============================================================================

static void LogDLSS(const char* format, ...)
{
    char buffer[1024];
    va_list args;
    va_start(args, format);
    vsnprintf(buffer, sizeof(buffer), format, args);
    va_end(args);
    
    time_t now = time(NULL);
    struct tm* t = localtime(&now);
    char timestamp[32];
    snprintf(timestamp, sizeof(timestamp), "[%02d:%02d:%02d] ", t->tm_hour, t->tm_min, t->tm_sec);
    
    if (g_logFile)
    {
        fprintf(g_logFile, "%s[DLSS] %s\n", timestamp, buffer);
        fflush(g_logFile);
    }
    
    if (g_logCallback)
    {
        char fullMsg[1100];
        snprintf(fullMsg, sizeof(fullMsg), "[DLSS] %s", buffer);
        g_logCallback(fullMsg);
    }
    
    OutputDebugStringA("[GfxPluginStreamline/DLSS] ");
    OutputDebugStringA(buffer);
    OutputDebugStringA("\n");
}

// ============================================================================
// C# Interop Structures
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
// Legacy DLSS APIs
// ============================================================================

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
// Legacy Frame Generation APIs
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
    
    if (result != sl::Result::eOk || !fn) return false;
    
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
    
    if (!SLDLSS_SetMode(3))
    {
        LogDLSS("Failed to enable DLSS Quality mode");
        return false;
    }
    
    if (!SLDLSSG_SetMode(1, 1))
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
    
    if (!SLDLSS_SetMode(1))
    {
        LogDLSS("Failed to enable DLSS Performance mode");
        return false;
    }
    
    if (!SLDLSSG_SetMode(1, 2))
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
    if (!SLDLSSG_SetMode(0, 0)) success = false;
    if (!SLDLSS_SetMode(0)) success = false;
    
    return success;
}
