#include "Bridge.h"
#include <map>
#include <algorithm>

namespace bgd
{
using Present = HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain*, UINT, UINT);
using Present1 = HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain1*, UINT, UINT, const DXGI_PRESENT_PARAMETERS*);
using Resize = HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain*, UINT, UINT, UINT, DXGI_FORMAT, UINT);
using Resize1 = HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain3*, UINT, UINT, UINT, DXGI_FORMAT, UINT, const UINT*, IUnknown* const*);
using Fullscreen = HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain*, BOOL, IDXGIOutput*);
struct Slot { void* original; void* replacement; };
static std::map<void**, Slot> slots;
static IDXGISwapChain* activeSwapchain{};
static sl::FrameToken* presentingFrame{};

template<class T> static T Original(void* object, size_t index)
{
    std::lock_guard lock(mutex);
    return reinterpret_cast<T>(slots.at(*reinterpret_cast<void***>(object) + index).original);
}
static HRESULT STDMETHODCALLTYPE OnPresent(IDXGISwapChain* chain, UINT sync, UINT flags)
{
    BeforePresent(chain, flags);
    auto result = Original<Present>(chain, 8)(chain, sync, flags);
    AfterPresent(chain, flags, result);
    return result;
}
static HRESULT STDMETHODCALLTYPE OnPresent1(IDXGISwapChain1* chain, UINT sync, UINT flags, const DXGI_PRESENT_PARAMETERS* parameters)
{
    BeforePresent(chain, flags);
    auto result = Original<Present1>(chain, 22)(chain, sync, flags, parameters);
    AfterPresent(chain, flags, result);
    return result;
}
static HRESULT STDMETHODCALLTYPE OnResize(IDXGISwapChain* chain, UINT count, UINT width, UINT height, DXGI_FORMAT format, UINT flags)
{
    { std::lock_guard lock(mutex); DisableFrameGeneration(); readyFrames.clear(); }
    return Original<Resize>(chain, 13)(chain, count, width, height, format, flags);
}
static HRESULT STDMETHODCALLTYPE OnResize1(IDXGISwapChain3* chain, UINT count, UINT width, UINT height, DXGI_FORMAT format, UINT flags, const UINT* masks, IUnknown* const* queues)
{
    { std::lock_guard lock(mutex); DisableFrameGeneration(); readyFrames.clear(); }
    return Original<Resize1>(chain, 39)(chain, count, width, height, format, flags, masks, queues);
}
static HRESULT STDMETHODCALLTYPE OnFullscreen(IDXGISwapChain* chain, BOOL fullscreen, IDXGIOutput* output)
{
    { std::lock_guard lock(mutex); DisableFrameGeneration(); readyFrames.clear(); }
    return Original<Fullscreen>(chain, 10)(chain, fullscreen, output);
}
static bool Patch(void* object, size_t index, void* replacement)
{
    void** slot = *reinterpret_cast<void***>(object) + index;
    if (slots.count(slot)) return *slot == replacement;
    DWORD protect{};
    if (!VirtualProtect(slot, sizeof(void*), PAGE_READWRITE, &protect)) return false;
    slots.emplace(slot, Slot{*slot, replacement});
    InterlockedExchangePointer(slot, replacement);
    DWORD unused{}; VirtualProtect(slot, sizeof(void*), protect, &unused);
    return true;
}
bool HookSwapchain(IDXGISwapChain* chain)
{
    if (!chain || !status.initialized) return false;
    if (chain == activeSwapchain) return status.swapchainHooked != 0;
    void* native{};
    if (api.nativeInterface(chain, &native) != sl::Result::eOk || native == chain || !native)
    {
        if (native) static_cast<IUnknown*>(native)->Release();
        Log("Unity swapchain is not a Streamline proxy; frame generation stays off.");
        activeSwapchain = chain;
        status.swapchainHooked = 0;
        return false;
    }
    // slGetNativeInterface returns an AddRef'ed interface.
    static_cast<IUnknown*>(native)->Release();
    DisableFrameGeneration();
    bool hooked = Patch(chain, 8, reinterpret_cast<void*>(OnPresent))
        && Patch(chain, 10, reinterpret_cast<void*>(OnFullscreen))
        && Patch(chain, 13, reinterpret_cast<void*>(OnResize));
    IDXGISwapChain1* chain1{};
    if (SUCCEEDED(chain->QueryInterface(IID_PPV_ARGS(&chain1))))
    {
        hooked &= Patch(chain1, 22, reinterpret_cast<void*>(OnPresent1));
        chain1->Release();
    }
    IDXGISwapChain3* chain3{};
    if (SUCCEEDED(chain->QueryInterface(IID_PPV_ARGS(&chain3))))
    {
        hooked &= Patch(chain3, 39, reinterpret_cast<void*>(OnResize1));
        chain3->Release();
    }
    activeSwapchain = chain;
    status.swapchainHooked = hooked;
    DXGI_SWAP_CHAIN_DESC description{};
    if (SUCCEEDED(chain->GetDesc(&description))) BindPclWindow(description.OutputWindow);
    return hooked;
}
void RestoreSwapchains()
{
    for (const auto& [slot, value] : slots)
    {
        DWORD protect{};
        if (*slot == value.replacement && VirtualProtect(slot, sizeof(void*), PAGE_READWRITE, &protect))
        {
            *slot = value.original;
            DWORD unused{}; VirtualProtect(slot, sizeof(void*), protect, &unused);
        }
    }
    slots.clear(); activeSwapchain = nullptr; status.swapchainHooked = 0;
}
void DisableFrameGeneration()
{
    if (!fgSupported || !status.generatedFrames) return;
    sl::DLSSGOptions options;
    if (Check(api.fgOptions(Viewport, options))) status.generatedFrames = 0;
}
void BeforePresent(IDXGISwapChain* chain, UINT flags)
{
    std::lock_guard lock(mutex);
    if (chain != activeSwapchain || (flags & DXGI_PRESENT_TEST)) return;
    presentingFrame = submittedFrame;
    diagnostics.tagMask = presentingFrame ? readyFrames[presentingFrame] : 0;
    if (diagnostics.tagMask == 7) ++diagnostics.completeTags;
    if (presentingFrame) diagnostics.presentId = static_cast<uint32_t>(*presentingFrame);
    if (fgSupported)
    {
        sl::DLSSGState state;
        auto stateResult = api.fgState(Viewport, state, nullptr);
        diagnostics.fgStateResult = static_cast<uint32_t>(stateResult);
        bool valid = Check(stateResult);
        if (valid)
        {
            ++diagnostics.slStateSamples;
            diagnostics.actualPresentedLast = state.numFramesActuallyPresented;
            diagnostics.slPresentedFrames += state.numFramesActuallyPresented;
            // Exactly one state read per real Present. >1 is SDK evidence of extra presents.
            if (state.numFramesActuallyPresented > 1 && status.generatedFrames)
                diagnostics.generatedTick = GetTickCount64();
        }
        status.maxGeneratedFrames = valid ? state.numFramesToGenerateMax : 0;
        status.frameGenerationStatus = static_cast<uint32_t>(state.status);
        DXGI_SWAP_CHAIN_DESC desc{};
        valid &= SUCCEEDED(chain->GetDesc(&desc));
        valid &= state.status == sl::DLSSGStatus::eOk && status.swapchainHooked
            && presentingFrame && readyFrames[presentingFrame] == 7 && focused.load()
            && desc.BufferDesc.Width >= state.minWidthOrHeight
            && desc.BufferDesc.Height >= state.minWidthOrHeight;
        uint32_t frames = valid ? std::min(requestedFrames.load(), status.maxGeneratedFrames) : 0;
        if (frames != status.generatedFrames)
        {
            sl::DLSSGOptions options;
            options.mode = frames ? sl::DLSSGMode::eOn : sl::DLSSGMode::eOff;
            options.numFramesToGenerate = frames ? frames : 1;
            if (Check(api.fgOptions(Viewport, options))) status.generatedFrames = frames;
        }
    }
    Marker(sl::PCLMarker::ePresentStart, presentingFrame);
}
void AfterPresent(IDXGISwapChain* chain, UINT flags, HRESULT result)
{
    std::lock_guard lock(mutex);
    if (chain != activeSwapchain || (flags & DXGI_PRESENT_TEST)) return;
    Marker(sl::PCLMarker::ePresentEnd, presentingFrame);
    diagnostics.presentResult = static_cast<uint32_t>(result);
    if (SUCCEEDED(result) && result != DXGI_STATUS_OCCLUDED)
    {
        ++diagnostics.presentedFrames;
        diagnostics.presentTick = GetTickCount64();
    }
    ReadReflexReport();
    if (FAILED(result) || result == DXGI_STATUS_OCCLUDED) DisableFrameGeneration();
    readyFrames.erase(presentingFrame);
    submittedFrame = nullptr;
    presentingFrame = nullptr;
}
}
