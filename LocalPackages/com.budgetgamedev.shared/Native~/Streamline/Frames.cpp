#include "Bridge.h"
#include <memory>
#include <new>
#include <deque>
#include <unordered_set>

namespace bgd
{
void Marker(sl::PCLMarker marker, sl::FrameToken* frame)
{
    if (pclSupported && frame)
    {
        auto result = api.marker(marker, *frame);
        diagnostics.markerResult = static_cast<uint32_t>(result);
        if (Check(result)) ++diagnostics.markers;
    }
}
bool SetFrameConstants(const FrameData& data)
{
    // SR and FG share one viewport and token. Their passes may disagree about
    // history reset during a resolution transition; SL permits only one common
    // constants packet for that frame. Use the first render submission for both.
    static std::unordered_set<uint32_t> submitted;
    static std::deque<uint32_t> order;
    if (!data.token) return false;
    uint32_t frame = static_cast<uint32_t>(*data.token);
    if (submitted.count(frame)) return true;
    sl::Constants constants;
    constants.cameraViewToClip = data.viewToClip;
    constants.clipToCameraView = data.clipToView;
    constants.clipToPrevClip = data.clipToPrevious;
    constants.prevClipToClip = data.previousToClip;
    constants.cameraPos = data.position;
    constants.cameraUp = data.up;
    constants.cameraRight = data.right;
    constants.cameraFwd = data.forward;
    constants.jitterOffset = data.jitter;
    constants.cameraPinholeOffset = {0.0f, 0.0f};
    constants.mvecScale = data.motionScale;
    constants.cameraNear = data.nearPlane;
    constants.cameraFar = data.farPlane;
    constants.cameraFOV = data.fieldOfView;
    constants.cameraAspectRatio = data.aspect;
    constants.depthInverted = data.invertedDepth ? sl::eTrue : sl::eFalse;
    constants.cameraMotionIncluded = sl::eTrue;
    constants.motionVectors3D = sl::eFalse;
    constants.reset = data.reset ? sl::eTrue : sl::eFalse;
    constants.motionVectorsDilated = sl::eFalse;
    constants.motionVectorsJittered = sl::eFalse;
    if (!Check(api.constants(constants, *data.token, Viewport))) return false;
    submitted.insert(frame); order.push_back(frame);
    if (order.size() > 64) { submitted.erase(order.front()); order.pop_front(); }
    return true;
}
void Capture(const FrameData& data)
{
    std::lock_guard lock(mutex);
    if (!fgSupported || !graphics || !data.token || !requestedFrames.load() || !focused.load()) return;
    UnityGraphicsD3D12RecordingState recording{};
    if (!graphics->CommandRecordingState(&recording) || !recording.commandList) return;
    constexpr auto readState = D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE;
    if (data.hudless || data.ui)
    {
        diagnostics.outputWidth = data.outputWidth; diagnostics.outputHeight = data.outputHeight;
        if (data.hudless) graphics->RequestResourceState(data.hudless, readState);
        if (data.ui) graphics->RequestResourceState(data.ui, readState);
        sl::Resource color{sl::ResourceType::eTex2d, data.hudless, readState};
        sl::Resource ui{sl::ResourceType::eTex2d, data.ui, readState};
        sl::Extent output{0, 0, data.outputWidth, data.outputHeight};
        sl::ResourceTag tags[2];
        uint32_t count = 0, mask = 0;
        if (data.hudless)
        {
            tags[count++] = {&color, sl::kBufferTypeHUDLessColor, sl::ResourceLifecycle::eOnlyValidNow, &output};
            mask |= 2;
        }
        if (data.ui)
        {
            tags[count++] = {&ui, sl::kBufferTypeUIAlpha, sl::ResourceLifecycle::eOnlyValidNow, &output};
            mask |= 4;
        }
        if (Check(api.tag(*data.token, Viewport, tags, count, recording.commandList)))
            readyFrames[data.token] |= mask;
        return;
    }
    if (!data.depth || !data.motion) return;
    diagnostics.renderWidth = data.width; diagnostics.renderHeight = data.height;
    if (!SetFrameConstants(data)) return;
    graphics->RequestResourceState(data.depth, readState);
    graphics->RequestResourceState(data.motion, readState);
    sl::Resource depth{sl::ResourceType::eTex2d, data.depth, readState};
    sl::Resource motion{sl::ResourceType::eTex2d, data.motion, readState};
    sl::Extent render{0, 0, data.width, data.height};
    // HDRP aliases RenderGraph textures and may overwrite them before Present.
    // eOnlyValidNow makes SL copy on this command list; it owns those copies.
    sl::ResourceTag tags[] = {
        {&depth, sl::kBufferTypeDepth, sl::ResourceLifecycle::eOnlyValidNow, &render},
        {&motion, sl::kBufferTypeMotionVectors, sl::ResourceLifecycle::eOnlyValidNow, &render}
    };
    if (Check(api.tag(*data.token, Viewport, tags, 2, recording.commandList)))
        readyFrames[data.token] |= 1;
}
static void UNITY_INTERFACE_API RenderEvent(int event, void* pointer)
{
    if (event == SuperResolutionEvent) { EvaluateSuperResolution(pointer); return; }
    if (event == CaptureEvent)
    {
        std::unique_ptr<FrameData> data(static_cast<FrameData*>(pointer));
        if (!data) return;
        Capture(*data);
        if (data->depth) data->depth->Release();
        if (data->motion) data->motion->Release();
        if (data->ui) data->ui->Release();
        if (data->hudless) data->hudless->Release();
        return;
    }
    std::lock_guard lock(mutex);
    if (!graphics || !status.initialized) return;
    auto frame = static_cast<sl::FrameToken*>(pointer);
    if (event == SubmitStartEvent)
    {
        ApplyOptions();
        HookSwapchain(graphics->GetSwapChain());
        Marker(sl::PCLMarker::eRenderSubmitStart, frame);
    }
    else if (event == SubmitEndEvent)
    {
        Marker(sl::PCLMarker::eRenderSubmitEnd, frame);
        submittedFrame = frame;
        if (frame) diagnostics.submissionId = static_cast<uint32_t>(*frame);
    }
}
}

EXPORT uint32_t __cdecl BgdSL_GetStatus(bgd::Status* output, uint32_t size)
{
    using namespace bgd;
    if (!output || size != sizeof(Status)) return 0;
    std::lock_guard lock(mutex);
    *output = status;
    output->integrationWarnings = integrationWarnings.load();
    return AbiVersion;
}
EXPORT uint32_t __cdecl BgdSL_FrameDataSize() { return sizeof(bgd::FrameData); }
EXPORT void* __cdecl BgdSL_BeginFrame()
{
    using namespace bgd;
    sl::FrameToken* frame{};
    {
        std::lock_guard lock(mutex);
        if (!status.initialized || (!reflexSupported && !pclSupported && !srSupported)) return nullptr;
        if (!Check(api.newFrame(frame, nullptr))) return nullptr;
        readyFrames[frame] = 0;
        diagnostics.simulationId = static_cast<uint32_t>(*frame);
        ++diagnostics.simulatedFrames;
        SetSimulationFrame(frame);
    }
    // Never hold the bridge mutex while sleeping: presentation must be able to progress.
    auto sleepResult = reflexSupported ? api.sleep(*frame) : sl::Result::eOk;
    {
        std::lock_guard lock(mutex);
        diagnostics.sleepResult = static_cast<uint32_t>(sleepResult);
        if (reflexSupported) { ++diagnostics.sleepCalls; if (sleepResult == sl::Result::eOk) ++diagnostics.sleepSuccesses; }
        Check(sleepResult);
        Marker(sl::PCLMarker::eSimulationStart, frame);
    }
    return frame;
}
EXPORT void __cdecl BgdSL_EndSimulation(void* pointer)
{
    std::lock_guard lock(bgd::mutex);
    bgd::Marker(sl::PCLMarker::eSimulationEnd, static_cast<sl::FrameToken*>(pointer));
}
EXPORT void __cdecl BgdSL_Configure(uint32_t frames, uint32_t reflex, uint32_t hasFocus)
{
    using namespace bgd;
    std::lock_guard lock(mutex);
    requestedFrames = frames <= 3 ? frames : 3;
    requestedReflex = reflex <= 2 ? reflex : 1;
    focused = hasFocus != 0;
}

namespace bgd
{
void ApplyOptions()
{
    static uint32_t previousMode = UINT32_MAX;
    const auto frames = requestedFrames.load();
    const auto reflex = requestedReflex.load();
    uint32_t mode = frames && reflex == 0 ? 1 : reflex;
    if (reflexSupported && mode != previousMode)
    {
        sl::ReflexOptions options;
        options.mode = static_cast<sl::ReflexMode>(mode);
        if (Check(api.reflexOptions(options))) { previousMode = mode; diagnostics.activeReflex = mode; }
    }
}
}
EXPORT void* __cdecl BgdSL_CopyFrame(const bgd::FrameData* source, uint32_t size)
{
    if (!source || size != sizeof(bgd::FrameData)) return nullptr;
    auto data = new (std::nothrow) bgd::FrameData(*source);
    if (data)
    {
        if (data->depth) data->depth->AddRef();
        if (data->motion) data->motion->AddRef();
        if (data->ui) data->ui->AddRef();
        if (data->hudless) data->hudless->AddRef();
    }
    return data;
}
EXPORT UnityRenderingEventAndData __cdecl BgdSL_GetRenderEvent() { return bgd::RenderEvent; }
