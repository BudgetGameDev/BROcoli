// StreamlineDLSSCore.cpp
// DLSS Super Resolution - Buffer Tagging, Constants, and Evaluation
//
// Core DLSS functionality for upscaling.

#include "StreamlineCommon.h"
#include "Unity/IUnityInterface.h"
#include "Unity/IUnityGraphicsD3D12.h"
#include <cstring>

// ============================================================================
// DLSS Constants Storage
// ============================================================================

static sl::Constants g_dlssConstants{};
static bool g_dlssConstantsValid = false;

// ============================================================================
// DLSS Frame Management
// ============================================================================

// Increment frame ID - call once per frame before DLSS evaluation
EXPORT void SLDLSS_BeginFrame()
{
    if (!g_initialized) return;
    g_frameId++;
    LogMessage("DLSS BeginFrame: frameId=%llu", g_frameId);
}

// Get current frame ID for debugging
EXPORT uint64_t SLDLSS_GetFrameId()
{
    return g_frameId;
}

// ============================================================================
// DLSS Viewport
// ============================================================================

EXPORT void SLDLSS_SetViewport(uint32_t viewportId)
{
    g_dlssViewport = sl::ViewportHandle(viewportId);
    LogMessage("DLSS viewport set to: %u", viewportId);
}

// ============================================================================
// DLSS Constants (Camera Matrices, Jitter, etc.)
// ============================================================================

EXPORT bool SLDLSS_SetConstants(
    const float* cameraViewToClip,
    const float* clipToCameraView,
    const float* clipToPrevClip,
    const float* prevClipToClip,
    float jitterOffsetX, float jitterOffsetY,
    float mvecScaleX, float mvecScaleY,
    float cameraNear, float cameraFar,
    float cameraFOV, float cameraAspectRatio,
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
    
    memcpy(&g_dlssConstants.cameraViewToClip, cameraViewToClip, sizeof(float) * 16);
    memcpy(&g_dlssConstants.clipToCameraView, clipToCameraView, sizeof(float) * 16);
    memcpy(&g_dlssConstants.clipToPrevClip, clipToPrevClip, sizeof(float) * 16);
    memcpy(&g_dlssConstants.prevClipToClip, prevClipToClip, sizeof(float) * 16);
    
    g_dlssConstants.jitterOffset = {jitterOffsetX, jitterOffsetY};
    g_dlssConstants.mvecScale = {mvecScaleX, mvecScaleY};
    g_dlssConstants.cameraNear = cameraNear;
    g_dlssConstants.cameraFar = cameraFar;
    g_dlssConstants.cameraFOV = cameraFOV;
    g_dlssConstants.cameraAspectRatio = cameraAspectRatio;
    
    g_dlssConstants.depthInverted = depthInverted ? sl::Boolean::eTrue : sl::Boolean::eFalse;
    g_dlssConstants.cameraMotionIncluded = cameraMotionIncluded ? sl::Boolean::eTrue : sl::Boolean::eFalse;
    g_dlssConstants.motionVectors3D = sl::Boolean::eFalse;
    g_dlssConstants.reset = reset ? sl::Boolean::eTrue : sl::Boolean::eFalse;
    g_dlssConstants.orthographicProjection = sl::Boolean::eFalse;
    g_dlssConstants.motionVectorsDilated = sl::Boolean::eFalse;
    g_dlssConstants.motionVectorsJittered = sl::Boolean::eFalse;
    
    g_dlssConstantsValid = true;
    
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

// ============================================================================
// Buffer Tagging
// ============================================================================

EXPORT bool SLDLSS_TagResourceD3D12(
    void* d3d12Resource,
    uint32_t bufferType,
    uint32_t width,
    uint32_t height,
    uint32_t nativeFormat,
    uint32_t state
)
{
    LogMessage("SLDLSS_TagResourceD3D12 called: type=%u, %ux%u, format=%u, state=%u, ptr=%p",
               bufferType, width, height, nativeFormat, state, d3d12Resource);
    
    if (!g_initialized)
    {
        LogMessage("SLDLSS_TagResourceD3D12 failed: not initialized");
        return false;
    }
    
    if (!d3d12Resource)
    {
        LogMessage("SLDLSS_TagResourceD3D12 failed: null resource");
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
    
    result = slSetTagForFrame(*frameToken, g_dlssViewport, &tag, 1, nullptr);
    
    if (result != sl::Result::eOk)
    {
        LogMessage("slSetTagForFrame failed for buffer type %u: %d", bufferType, (int)result);
        return false;
    }
    
    return true;
}

// ============================================================================
// DLSS Options
// ============================================================================

EXPORT bool SLDLSS_SetOptions(
    int mode,
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

// ============================================================================
// Optimal Settings Query
// ============================================================================

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
    // CRITICAL: Check both initialized AND DLSS is supported
    // slDLSSGetOptimalSettings will crash if DLSS feature isn't loaded
    if (!g_initialized || !g_dlssSupported)
    {
        LogMessage("SLDLSS_GetOptimalSettings: skipped (init=%d, dlss=%d)", 
                   g_initialized ? 1 : 0, g_dlssSupported ? 1 : 0);
        return false;
    }
    
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
        
        LogMessage("DLSS optimal for mode %d @ %ux%u: render=%ux%u", 
                   mode, outputWidth, outputHeight, 
                   settings.optimalRenderWidth, settings.optimalRenderHeight);
        return true;
    }
    
    return false;
}

// ============================================================================
// DLSS Evaluation
// ============================================================================

// Pending evaluation state
DLSSPendingEval g_dlssPending = { false, 0 };

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
// DLSS Evaluation via Render Callback (for IssuePluginEvent)
// ============================================================================

// Forward declaration of Unity interface access
extern IUnityGraphicsD3D12v7* s_D3D12v7_ptr;

// Track callback invocations for debugging
static int g_callbackInvocationCount = 0;

// Mark DLSS ready for evaluation - call this from C# before IssuePluginEvent
EXPORT void SLDLSS_PrepareEvaluate()
{
    LogMessage(">>> PrepareEvaluate ENTRY, frame %u, initialized=%d, dlssSupported=%d", 
               (uint32_t)(g_frameId & 0xFFFFFFFF), g_initialized ? 1 : 0, g_dlssSupported ? 1 : 0);
    g_dlssPending.ready = true;
    g_dlssPending.frameIndex = (uint32_t)(g_frameId & 0xFFFFFFFF);
    LogMessage("<<< PrepareEvaluate EXIT, ready=%d", g_dlssPending.ready ? 1 : 0);
}

// The actual render callback invoked by Unity on the render thread
static void UNITY_INTERFACE_API OnDLSSRenderEvent(int eventID)
{
    g_callbackInvocationCount++;
    
    // Log first few frames and then periodically
    bool shouldLog = (g_callbackInvocationCount <= 5) || (g_callbackInvocationCount % 300 == 1);
    
    if (shouldLog)
    {
        LogMessage("OnDLSSRenderEvent #%d: eventID=0x%X (expected 0x%X), ready=%d, init=%d, supported=%d, frameId=%llu",
                   g_callbackInvocationCount, eventID, kDLSSEventID_Evaluate, 
                   g_dlssPending.ready ? 1 : 0, g_initialized ? 1 : 0, 
                   g_dlssSupported ? 1 : 0, g_frameId);
    }
    
    if (eventID != kDLSSEventID_Evaluate)
    {
        if (shouldLog) LogMessage("OnDLSSRenderEvent: wrong event ID, ignoring");
        return;
    }
    if (!g_dlssPending.ready)
    {
        if (shouldLog) LogMessage("OnDLSSRenderEvent: not ready, ignoring");
        return;
    }
    if (!g_initialized || !g_dlssSupported)
    {
        LogMessage("DLSS render callback: not initialized or DLSS not supported");
        g_dlssPending.ready = false;
        return;
    }
    
    // Get current command list from Unity
    IUnityGraphicsD3D12v7* d3d12 = GetUnityD3D12v7();
    if (!d3d12)
    {
        LogMessage("DLSS render callback: no D3D12 interface");
        g_dlssPending.ready = false;
        return;
    }
    
    UnityGraphicsD3D12RecordingState recordingState;
    if (!d3d12->CommandRecordingState(&recordingState) || !recordingState.commandList)
    {
        LogMessage("DLSS render callback: no active command list");
        g_dlssPending.ready = false;
        return;
    }
    
    // Get frame token
    sl::FrameToken* frameToken = nullptr;
    sl::Result result = slGetNewFrameToken(frameToken, &g_dlssPending.frameIndex);
    
    if (result != sl::Result::eOk || !frameToken)
    {
        LogMessage("DLSS render callback: failed to get frame token, result=%d", (int)result);
        g_dlssPending.ready = false;
        return;
    }
    
    LogMessage("DLSS render callback: evaluating with cmdList=%p, frame=%u", 
               recordingState.commandList, g_dlssPending.frameIndex);
    
    // Evaluate DLSS with actual command list
    result = slEvaluateFeature(sl::kFeatureDLSS, *frameToken, nullptr, 0,
                               reinterpret_cast<sl::CommandBuffer*>(recordingState.commandList));
    
    if (result != sl::Result::eOk)
    {
        LogMessage("DLSS render callback: slEvaluateFeature failed: %d", (int)result);
    }
    else
    {
        // Log success periodically
        static int successCount = 0;
        if (++successCount % 60 == 1)
        {
            LogMessage("DLSS evaluate SUCCESS (count: %d)", successCount);
        }
    }
    
    g_dlssPending.ready = false;
}

// Return the render callback function pointer for Unity
EXPORT UnityRenderingEvent SLDLSS_GetRenderCallback()
{
    LogMessage(">>> GetRenderCallback ENTRY");
    LogMessage("    OnDLSSRenderEvent address: %p", (void*)OnDLSSRenderEvent);
    LogMessage("<<< GetRenderCallback EXIT, returning %p", (void*)OnDLSSRenderEvent);
    return OnDLSSRenderEvent;
}

// Get the DLSS event ID
EXPORT int SLDLSS_GetEvaluateEventID()
{
    LogMessage("GetEvaluateEventID called, returning 0x%X", kDLSSEventID_Evaluate);
    return kDLSSEventID_Evaluate;
}
