// StreamlineFrameGen.cpp
// DLSS Frame Generation (DLSS-G / MFG) APIs
//
// Provides frame generation for increased perceived frame rates.

#include "StreamlineCommon.h"

// Forward declaration from StreamlineDLSSCore.cpp
EXPORT bool SLDLSS_TagResourceD3D12(void*, uint32_t, uint32_t, uint32_t, uint32_t, uint32_t);

// ============================================================================
// Frame Generation Options
// ============================================================================

EXPORT bool SLDLSSG_SetOptions(
    int mode,
    uint32_t numFramesToGenerate,
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
    options.flags = sl::DLSSGFlags::eDynamicResolutionEnabled;
    
    sl::Result result = slDLSSGSetOptions(g_dlssViewport, options);
    
    if (result == sl::Result::eOk)
    {
        g_dlssgMode = options.mode;
        g_numFramesToGenerate = numFramesToGenerate;
        LogMessage("DLSS-G mode set to: %d, frames: %u", mode, numFramesToGenerate);
        return true;
    }
    
    LogMessage("slDLSSGSetOptions failed: %d", (int)result);
    return false;
}

// ============================================================================
// Frame Generation State
// ============================================================================

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

// ============================================================================
// Frame Generation Buffer Tagging
// ============================================================================

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
