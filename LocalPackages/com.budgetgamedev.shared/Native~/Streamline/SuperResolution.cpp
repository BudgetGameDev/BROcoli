#include "Bridge.h"
#include <memory>
#include <new>

namespace bgd
{
struct SuperResolutionData
{
    FrameData frame;
    ID3D12Resource *input, *output;
    float preExposure;
    uint32_t hdr, motionWidth, motionHeight;
};
struct SuperResolutionStatus
{
    uint32_t available{}, supportResult = UINT32_MAX, optionsResult = UINT32_MAX,
        evaluationResult = UINT32_MAX, width{}, height{}, outputWidth{}, outputHeight{};
    uint64_t attempts{}, evaluations{}, evaluationTick{}, snapshotTick{};
};
static SuperResolutionStatus srStatus;
static_assert(sizeof(SuperResolutionData) == 432);
static_assert(sizeof(SuperResolutionStatus) == 64);

void ConfigureSuperResolution(const sl::AdapterInfo& adapter)
{
    auto support = api.supported(sl::kFeatureDLSS, adapter);
    srStatus.supportResult = static_cast<uint32_t>(support);
    srSupported = support == sl::Result::eOk;
    void *options{}, *optimal{};
    if (srSupported)
        srSupported = Check(api.featureFunction(sl::kFeatureDLSS, "slDLSSSetOptions", options))
            && Check(api.featureFunction(sl::kFeatureDLSS, "slDLSSGetOptimalSettings", optimal));
    api.srOptions = reinterpret_cast<PFun_slDLSSSetOptions*>(options);
    api.srOptimal = reinterpret_cast<PFun_slDLSSGetOptimalSettings*>(optimal);
    srStatus.available = srSupported;
}

void EvaluateSuperResolution(void* packet)
{
    auto release = [](SuperResolutionData* data)
    {
        if (!data) return;
        for (auto resource : {data->input, data->output, data->frame.depth, data->frame.motion})
            if (resource) resource->Release();
        delete data;
    };
    std::unique_ptr<SuperResolutionData, decltype(release)> data(
        static_cast<SuperResolutionData*>(packet), release);
    std::lock_guard lock(mutex);
    if (!data || !srSupported || !srStatus.available || !graphics || !data->frame.token) return;
    auto& f = data->frame;
    UnityGraphicsD3D12RecordingState recording{};
    if (!graphics->CommandRecordingState(&recording) || !recording.commandList) return;
    ++srStatus.attempts;
    if (srStatus.attempts == 1)
    {
        for (auto resource : {data->input, data->output, f.depth, f.motion})
        {
            auto desc = resource->GetDesc();
            char line[192];
            sprintf_s(line, "DLSS resource %p: %llu x %u, format=%u, flags=%u",
                resource, desc.Width, desc.Height, desc.Format, desc.Flags);
            Log(line);
        }
        char line[128];
        sprintf_s(line, "DLSS input: preExposure=%g, HDR=%u", data->preExposure, data->hdr);
        Log(line);
    }
    sl::DLSSOptions options;
    options.mode = sl::DLSSMode::eMaxQuality;
    options.qualityPreset = sl::DLSSPreset::ePresetK;
    options.outputWidth = f.outputWidth; options.outputHeight = f.outputHeight;
    options.colorBuffersHDR = data->hdr ? sl::eTrue : sl::eFalse;
    options.preExposure = data->preExposure;
    // Both pipelines provide pre-exposed color; SL estimates exposure from that color.
    options.useAutoExposure = sl::eTrue;
    auto result = api.srOptions(Viewport, options);
    srStatus.optionsResult = static_cast<uint32_t>(result);
    if (!Check(result)) { srStatus.available = 0; return; }
    if (!SetFrameConstants(f)) { srStatus.evaluationResult = status.lastError; srStatus.available = 0; return; }
    constexpr auto read = D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE;
    constexpr auto write = D3D12_RESOURCE_STATE_UNORDERED_ACCESS;
    for (auto resource : {data->input, f.depth, f.motion}) graphics->RequestResourceState(resource, read);
    graphics->RequestResourceState(data->output, write);
    sl::Resource input{sl::ResourceType::eTex2d, data->input, read};
    sl::Resource output{sl::ResourceType::eTex2d, data->output, write};
    sl::Resource depth{sl::ResourceType::eTex2d, f.depth, read};
    sl::Resource motion{sl::ResourceType::eTex2d, f.motion, read};
    sl::Extent render{0, 0, f.width, f.height}, display{0, 0, f.outputWidth, f.outputHeight};
    sl::Extent vectors{0, 0, data->motionWidth, data->motionHeight};
    sl::ResourceTag tags[] = {
        {&input, sl::kBufferTypeScalingInputColor, sl::ResourceLifecycle::eOnlyValidNow, &render},
        {&output, sl::kBufferTypeScalingOutputColor, sl::ResourceLifecycle::eOnlyValidNow, &display},
        {&depth, sl::kBufferTypeDepth, sl::ResourceLifecycle::eOnlyValidNow, &render},
        {&motion, sl::kBufferTypeMotionVectors, sl::ResourceLifecycle::eOnlyValidNow, &vectors}
    };
    result = api.tag(*f.token, Viewport, tags, 4, recording.commandList);
    if (Check(result))
    {
        const sl::BaseStructure* inputs[] = {&Viewport};
        result = api.evaluate(sl::kFeatureDLSS, *f.token, inputs, 1, recording.commandList);
    }
    for (auto resource : {data->input, f.depth, f.motion}) graphics->NotifyResourceState(resource, read, false);
    graphics->NotifyResourceState(data->output, write, true);
    srStatus.evaluationResult = static_cast<uint32_t>(result);
    if (Check(result))
    {
        ++srStatus.evaluations;
        srStatus.evaluationTick = GetTickCount64();
        srStatus.width = f.width; srStatus.height = f.height;
        srStatus.outputWidth = f.outputWidth; srStatus.outputHeight = f.outputHeight;
    }
    // A failed dispatch leaves the managed spatial fallback in place, and disables SR next frame.
    else srStatus.available = 0;
}
}

EXPORT uint32_t __cdecl BgdSL_GetSuperResolutionStatus(bgd::SuperResolutionStatus* output, uint32_t size)
{
    if (!output || size != sizeof(*output)) return 0;
    std::lock_guard lock(bgd::mutex);
    *output = bgd::srStatus;
    output->snapshotTick = GetTickCount64();
    return 1;
}
EXPORT uint32_t __cdecl BgdSL_GetOptimalResolution(uint32_t width, uint32_t height, uint32_t* x, uint32_t* y)
{
    using namespace bgd;
    std::lock_guard lock(mutex);
    if (!srSupported || !x || !y || !width || !height) return 0;
    // Quality and preset are fixed. Avoid querying NGX every rendered frame:
    // its verbose callback otherwise floods the useful diagnostics with identical settings.
    static uint32_t cachedWidth{}, cachedHeight{}, cachedX{}, cachedY{};
    if (width == cachedWidth && height == cachedHeight)
    {
        *x = cachedX; *y = cachedY;
        return 1;
    }
    sl::DLSSOptions options;
    options.mode = sl::DLSSMode::eMaxQuality;
    options.qualityPreset = sl::DLSSPreset::ePresetK;
    options.outputWidth = width; options.outputHeight = height;
    sl::DLSSOptimalSettings optimal;
    if (!Check(api.srOptimal(options, optimal))) { srStatus.available = 0; return 0; }
    *x = optimal.optimalRenderWidth; *y = optimal.optimalRenderHeight;
    if (!*x || !*y || *x > width || *y > height) return 0;
    cachedWidth = width; cachedHeight = height; cachedX = *x; cachedY = *y;
    return 1;
}
EXPORT void* __cdecl BgdSL_CopySuperResolution(const bgd::SuperResolutionData* source, uint32_t size)
{
    if (!source || size != sizeof(*source) || !source->input || !source->output
        || !source->frame.depth || !source->frame.motion) return nullptr;
    auto data = new (std::nothrow) bgd::SuperResolutionData(*source);
    if (data)
        for (auto resource : {data->input, data->output, data->frame.depth, data->frame.motion}) resource->AddRef();
    return data;
}
