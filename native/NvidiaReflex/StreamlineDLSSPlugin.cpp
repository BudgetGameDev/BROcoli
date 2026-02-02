// StreamlineDLSSPlugin.cpp
// Unity Native Plugin wrapping NVIDIA Streamline SDK for DLSS and Frame Generation
//
// This plugin provides DLSS Super Resolution and Frame Generation for Unity.
//
// Required DLLs (copy to Assets/Plugins/x86_64/):
//   - StreamlineReflexPlugin.dll (our wrapper - handles both Reflex and DLSS)
//   - sl.interposer.dll
//   - sl.dlss.dll
//   - sl.dlss_g.dll  
//   - nvngx_dlss.dll
//   - nvngx_dlssg.dll

#include <windows.h>
#include <cstdint>
#include <cstring>
#include <mutex>
#include <string>
#include <cstdarg>

// Streamline SDK headers
#include "sl.h"
#include "sl_dlss.h"
#include "sl_dlss_g.h"

// ============================================================================
// Plugin State  
// ============================================================================

static bool g_dlssInitialized = false;
static bool g_dlssSupported = false;
static bool g_dlssgSupported = false;
static sl::DLSSMode g_dlssMode = sl::DLSSMode::eOff;
static sl::DLSSGMode g_dlssgMode = sl::DLSSGMode::eOff;
static uint32_t g_numFramesToGenerate = 2; // Default to 3x (2 generated frames)
static std::mutex g_dlssMutex;

// Logging function (shared with main plugin)
static void LogMessageDLSS(const char* format, ...)
{
    char buffer[1024];
    va_list args;
    va_start(args, format);
    vsnprintf(buffer, sizeof(buffer), format, args);
    va_end(args);
    
    OutputDebugStringA("[StreamlineDLSS] ");
    OutputDebugStringA(buffer);
    OutputDebugStringA("\n");
}

// ============================================================================
// Export Macro
// ============================================================================

#define EXPORT extern "C" __declspec(dllexport)

// ============================================================================
// DLSS Enums for C# interop
// ============================================================================

// Matches sl::DLSSMode
enum DLSSModeExport : int32_t
{
    DLSS_Off = 0,
    DLSS_MaxPerformance = 1,    // Render at 50% resolution
    DLSS_Balanced = 2,          // Render at 58% resolution
    DLSS_MaxQuality = 3,        // Render at 67% resolution
    DLSS_UltraPerformance = 4,  // Render at 33% resolution
    DLSS_UltraQuality = 5,      // Render at 77% resolution
    DLSS_DLAA = 6               // Native resolution with AA
};

// Matches sl::DLSSGMode  
enum DLSSGModeExport : int32_t
{
    DLSSG_Off = 0,
    DLSSG_On = 1,
    DLSSG_Auto = 2
};

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
// DLSS Functions
// ============================================================================

EXPORT bool SLDLSS_IsSupported()
{
    sl::AdapterInfo adapterInfo{};
    sl::Result result = slIsFeatureSupported(sl::kFeatureDLSS, adapterInfo);
    g_dlssSupported = (result == sl::Result::eOk);
    return g_dlssSupported;
}

EXPORT bool SLDLSS_IsFrameGenSupported()
{
    sl::AdapterInfo adapterInfo{};
    sl::Result result = slIsFeatureSupported(sl::kFeatureDLSS_G, adapterInfo);
    g_dlssgSupported = (result == sl::Result::eOk);
    return g_dlssgSupported;
}

EXPORT bool SLDLSS_GetOptimalSettings(
    int mode, 
    uint32_t targetWidth, 
    uint32_t targetHeight,
    DLSSSettingsExport* outSettings)
{
    if (!outSettings) return false;
    
    std::lock_guard<std::mutex> lock(g_dlssMutex);
    
    // Set up options
    sl::DLSSOptions options{};
    options.mode = static_cast<sl::DLSSMode>(mode);
    options.outputWidth = targetWidth;
    options.outputHeight = targetHeight;
    
    // Get optimal settings
    sl::DLSSOptimalSettings settings{};
    
    // Use slDLSSGetOptimalSettings via function pointer
    using PFN_slDLSSGetOptimalSettings = sl::Result(*)(const sl::DLSSOptions&, sl::DLSSOptimalSettings&);
    PFN_slDLSSGetOptimalSettings fn = nullptr;
    sl::Result result = slGetFeatureFunction(sl::kFeatureDLSS, "slDLSSGetOptimalSettings", (void*&)fn);
    
    if (result != sl::Result::eOk || !fn)
    {
        return false;
    }
    
    result = fn(options, settings);
    if (result != sl::Result::eOk)
    {
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
    std::lock_guard<std::mutex> lock(g_dlssMutex);
    
    sl::DLSSOptions options{};
    options.mode = static_cast<sl::DLSSMode>(mode);
    
    // Use slDLSSSetOptions via function pointer
    using PFN_slDLSSSetOptions = sl::Result(*)(const sl::ViewportHandle&, const sl::DLSSOptions&);
    PFN_slDLSSSetOptions fn = nullptr;
    sl::Result result = slGetFeatureFunction(sl::kFeatureDLSS, "slDLSSSetOptions", (void*&)fn);
    
    if (result != sl::Result::eOk || !fn)
    {
        return false;
    }
    
    sl::ViewportHandle viewport(0);
    result = fn(viewport, options);
    
    if (result == sl::Result::eOk)
    {
        g_dlssMode = static_cast<sl::DLSSMode>(mode);
        return true;
    }
    
    return false;
}

EXPORT int SLDLSS_GetMode()
{
    return static_cast<int>(g_dlssMode);
}

// ============================================================================
// DLSS Frame Generation Functions
// ============================================================================

EXPORT bool SLDLSSG_SetMode(int mode, int numFramesToGenerate)
{
    std::lock_guard<std::mutex> lock(g_dlssMutex);
    
    sl::DLSSGOptions options{};
    options.mode = static_cast<sl::DLSSGMode>(mode);
    options.numFramesToGenerate = static_cast<uint32_t>(numFramesToGenerate);
    
    // Use slDLSSGSetOptions via function pointer
    using PFN_slDLSSGSetOptions = sl::Result(*)(const sl::ViewportHandle&, const sl::DLSSGOptions&);
    PFN_slDLSSGSetOptions fn = nullptr;
    sl::Result result = slGetFeatureFunction(sl::kFeatureDLSS_G, "slDLSSGSetOptions", (void*&)fn);
    
    if (result != sl::Result::eOk || !fn)
    {
        return false;
    }
    
    sl::ViewportHandle viewport(0);
    result = fn(viewport, options);
    
    if (result == sl::Result::eOk)
    {
        g_dlssgMode = static_cast<sl::DLSSGMode>(mode);
        g_numFramesToGenerate = numFramesToGenerate;
        return true;
    }
    
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

EXPORT bool SLDLSSG_GetState(DLSSGStateExport* outState)
{
    if (!outState) return false;
    
    std::lock_guard<std::mutex> lock(g_dlssMutex);
    
    sl::DLSSGState state{};
    sl::DLSSGOptions options{};
    options.flags = sl::DLSSGFlags::eRequestVRAMEstimate;
    
    // Use slDLSSGGetState via function pointer
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
// Combined DLSS + Frame Gen Preset
// ============================================================================

EXPORT bool SLStreamline_EnableDLSSPerformanceWithFrameGen3x()
{
    // Enable DLSS Performance mode
    if (!SLDLSS_SetMode(DLSS_MaxPerformance))
    {
        return false;
    }
    
    // Enable Frame Generation with 3x multiplier (2 generated frames)
    if (!SLDLSSG_SetMode(DLSSG_On, 2))
    {
        // DLSS enabled but Frame Gen failed - still partial success
        return true;
    }
    
    return true;
}

EXPORT bool SLStreamline_DisableDLSSAndFrameGen()
{
    bool success = true;
    
    // Disable Frame Generation first
    if (!SLDLSSG_SetMode(DLSSG_Off, 0))
    {
        success = false;
    }
    
    // Disable DLSS
    if (!SLDLSS_SetMode(DLSS_Off))
    {
        success = false;
    }
    
    return success;
}
