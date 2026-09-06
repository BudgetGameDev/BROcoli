#pragma once
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d12.h>
#include <dxgi1_6.h>
#include <atomic>
#include <mutex>
#include <string>
#include <unordered_map>
#include "IUnityGraphics.h"
#include "IUnityGraphicsD3D12.h"
#include "sl.h"
#include "sl_dlss_g.h"
#include "sl_dlss.h"
#include "sl_reflex.h"
#include "Diagnostics.h"

#define EXPORT extern "C" __declspec(dllexport)

namespace bgd
{
constexpr uint32_t AbiVersion = 2;
constexpr int CaptureEvent = 0x425200;
constexpr int SubmitStartEvent = CaptureEvent + 1;
constexpr int SubmitEndEvent = CaptureEvent + 2;
constexpr int SuperResolutionEvent = CaptureEvent + 3;
inline const sl::ViewportHandle Viewport{0};

// Sequential, 8-byte packed C ABI. Unity matrices are column-major; their memory
// representation is the row-major transpose required by Streamline's row vectors.
struct FrameData
{
    sl::FrameToken* token;
    ID3D12Resource* depth;
    ID3D12Resource* motion;
    ID3D12Resource* ui;
    ID3D12Resource* hudless;
    sl::float4x4 viewToClip, clipToView, clipToPrevious, previousToClip;
    sl::float3 position, up, right, forward;
    sl::float2 jitter, motionScale;
    float nearPlane, farPlane, fieldOfView, aspect;
    uint32_t width, height, outputWidth, outputHeight, reset, invertedDepth;
};
struct Status
{
    uint32_t abi = AbiVersion;
    uint32_t initialized{}, reflexAvailable{}, frameGenerationAvailable{};
    uint32_t maxGeneratedFrames{}, generatedFrames{}, lastError{}, frameGenerationStatus{};
    uint32_t swapchainHooked{};
    uint32_t requirementsResult = UINT32_MAX, featureSupportResult = UINT32_MAX;
    uint32_t integrationWarnings{};
};
static_assert(sizeof(FrameData) == 400, "Update the shared C# ABI if FrameData changes.");
static_assert(sizeof(Status) == 48, "Update the shared C# ABI if Status changes.");
struct Api
{
    HMODULE module{};
    PFun_slInit* init{};
    PFun_slShutdown* shutdown{};
    PFun_slIsFeatureSupported* supported{};
    PFun_slSetD3DDevice* setDevice{};
    PFun_slGetFeatureFunction* featureFunction{};
    PFun_slGetNewFrameToken* newFrame{};
    PFun_slSetConstants* constants{};
    PFun_slSetTagForFrame* tag{};
    PFun_slGetNativeInterface* nativeInterface{};
    PFun_slGetFeatureRequirements* requirements{};
    PFun_slEvaluateFeature* evaluate{};
    PFun_slDLSSSetOptions* srOptions{};
    PFun_slDLSSGetOptimalSettings* srOptimal{};
    PFun_slReflexSleep* sleep{};
    PFun_slReflexSetOptions* reflexOptions{};
    PFun_slReflexGetState* reflexState{};
    PFun_slPCLSetMarker* marker{};
    PFun_slPCLSetOptions* pclOptions{};
    PFun_slPCLGetState* pclState{};
    PFun_slDLSSGSetOptions* fgOptions{};
    PFun_slDLSSGGetState* fgState{};
};
extern Api api;
extern IUnityGraphicsD3D12v8* graphics;
extern std::recursive_mutex mutex;
extern Status status;
extern sl::FrameToken* submittedFrame;
extern std::unordered_map<sl::FrameToken*, uint32_t> readyFrames;
extern bool reflexSupported, pclSupported, fgSupported, srSupported;
bool SetFrameConstants(const FrameData& data);
void EvaluateSuperResolution(void* packet);
void ConfigureSuperResolution(const sl::AdapterInfo& adapter);
extern std::atomic<uint32_t> requestedFrames, requestedReflex;
extern std::atomic<bool> focused;
extern std::atomic<uint32_t> integrationWarnings;
bool Check(sl::Result result);
void Log(const char* message);
bool LoadStreamline();
bool InstallImports();
void RestoreImports();
bool HookSwapchain(IDXGISwapChain* swapchain);
void RestoreSwapchains();
void DisableFrameGeneration();
void BeforePresent(IDXGISwapChain* swapchain, UINT flags);
void AfterPresent(IDXGISwapChain* swapchain, UINT flags, HRESULT result);
void Capture(const FrameData& data);
void Marker(sl::PCLMarker marker, sl::FrameToken* frame);
void ConfigureDevice();
void ApplyOptions();
void BindPclWindow(HWND window);
void RestorePclWindow();
void SetSimulationFrame(sl::FrameToken* frame);
}
