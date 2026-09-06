#include "Bridge.h"
#include "sl_security.h"
#include <filesystem>
#include <shlobj.h>

namespace bgd
{
Api api;
IUnityGraphicsD3D12v8* graphics{};
std::recursive_mutex mutex;
Status status;
sl::FrameToken* submittedFrame{};
std::unordered_map<sl::FrameToken*, uint32_t> readyFrames;
bool reflexSupported{}, pclSupported{}, fgSupported{}, srSupported{};
std::atomic<uint32_t> requestedFrames{3}, requestedReflex{1};
std::atomic<bool> focused{true};
std::atomic<uint32_t> integrationWarnings{};
static IUnityGraphics* unityGraphics{};
static IUnityInterfaces* unityInterfaces{};

void Log(const char* message)
{
    RecordLog(message);
    OutputDebugStringA("[BudgetGameDev Streamline] ");
    OutputDebugStringA(message);
    OutputDebugStringA("\n");
}
bool Check(sl::Result result)
{
    if (result == sl::Result::eOk) return true;
    status.lastError = static_cast<uint32_t>(result);
    Log(("Streamline error " + std::to_string(status.lastError)).c_str());
    return false;
}
static void Message(sl::LogType type, const char* message)
{
    if (type != sl::LogType::eInfo) ++integrationWarnings;
    Log(message);
}

bool LoadStreamline()
{
    wchar_t executable[32768]{};
    if (!GetModuleFileNameW(nullptr, executable, 32768)) return false;
    auto directory = std::filesystem::path(executable).parent_path();
    auto library = directory / L"sl.interposer.dll";
    // Verify NVIDIA's signature before executing any code from the interposer.
    const wchar_t* libraries[] = {L"sl.interposer.dll", L"sl.common.dll", L"sl.dlss.dll", L"sl.dlss_g.dll",
        L"sl.reflex.dll", L"sl.pcl.dll", L"nvngx_dlssg.dll", L"nvngx_dlss.dll"};
    for (auto name : libraries)
    if (!sl::security::verifyEmbeddedSignature((directory / name).c_str()))
    {
        Log(("Missing or invalid NVIDIA signature: " + std::filesystem::path(name).string()).c_str());
        return false;
    }
    api.module = LoadLibraryExW(library.c_str(), nullptr,
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (!api.module) return false;
#define LOAD(member, name) \
    api.member = reinterpret_cast<PFun_##name*>(GetProcAddress(api.module, #name)); \
    if (!api.member) return false
    LOAD(init, slInit); LOAD(shutdown, slShutdown);
    LOAD(supported, slIsFeatureSupported); LOAD(setDevice, slSetD3DDevice);
    LOAD(featureFunction, slGetFeatureFunction); LOAD(newFrame, slGetNewFrameToken);
    LOAD(constants, slSetConstants); LOAD(tag, slSetTagForFrame);
    LOAD(nativeInterface, slGetNativeInterface);
    LOAD(requirements, slGetFeatureRequirements); LOAD(evaluate, slEvaluateFeature);
#undef LOAD
    const std::wstring path = directory.wstring();
    std::wstring logPath;
    PWSTR appData{};
    if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &appData)))
    {
        auto logs = std::filesystem::path(appData) / L"BudgetGameDev" / L"Streamline"
            / std::filesystem::path(executable).stem();
        CoTaskMemFree(appData);
        std::error_code error;
        std::filesystem::create_directories(logs, error);
        if (!error) logPath = logs.wstring();
    }
    const wchar_t* paths[] = {path.c_str()};
    const sl::Feature features[] = {sl::kFeatureDLSS, sl::kFeatureDLSS_G, sl::kFeatureReflex, sl::kFeaturePCL};
    sl::Preferences preferences;
    preferences.featuresToLoad = features;
    preferences.numFeaturesToLoad = 4;
    preferences.pathsToPlugins = paths;
    preferences.numPathsToPlugins = 1;
    preferences.engine = sl::EngineType::eUnity;
    preferences.engineVersion = "6000.5.10f1";
    preferences.renderAPI = sl::RenderAPI::eD3D12;
    preferences.flags = sl::PreferenceFlags::eDisableCLStateTracking
        | sl::PreferenceFlags::eUseDXGIFactoryProxy
        | sl::PreferenceFlags::eUseFrameBasedResourceTagging;
    preferences.logMessageCallback = Message;
    preferences.pathToLogsAndData = logPath.empty() ? nullptr : logPath.c_str();
    if (!Check(api.init(preferences, sl::kSDKVersion))) return false;
    status.initialized = 1;
    return true;
}

template<class T> static bool Feature(sl::Feature feature, const char* name, T*& function)
{
    void* address{};
    if (!Check(api.featureFunction(feature, name, address))) return false;
    function = reinterpret_cast<T*>(address);
    return function != nullptr;
}

void ConfigureDevice()
{
    if (!graphics || !status.initialized || !graphics->GetDevice()) return;
    if (!Check(api.setDevice(graphics->GetDevice()))) return;
    LUID luid = graphics->GetDevice()->GetAdapterLuid();
    sl::AdapterInfo adapter;
    adapter.deviceLUID = reinterpret_cast<uint8_t*>(&luid);
    adapter.deviceLUIDSizeInBytes = sizeof(luid);
    ConfigureSuperResolution(adapter);
    reflexSupported = api.supported(sl::kFeatureReflex, adapter) == sl::Result::eOk;
    pclSupported = api.supported(sl::kFeaturePCL, adapter) == sl::Result::eOk;
    auto supportResult = api.supported(sl::kFeatureDLSS_G, adapter);
    status.featureSupportResult = static_cast<uint32_t>(supportResult);
    fgSupported = supportResult == sl::Result::eOk;
    sl::FeatureRequirements requirements;
    auto requirementsResult = api.requirements(sl::kFeatureDLSS_G, requirements);
    status.requirementsResult = static_cast<uint32_t>(requirementsResult);
    fgSupported &= requirementsResult == sl::Result::eOk
        && (requirements.flags & sl::FeatureRequirementFlags::eD3D12Supported);
    if (reflexSupported)
    {
        reflexSupported = Feature(sl::kFeatureReflex, "slReflexSleep", api.sleep)
            && Feature(sl::kFeatureReflex, "slReflexSetOptions", api.reflexOptions)
            && Feature(sl::kFeatureReflex, "slReflexGetState", api.reflexState);
        if (reflexSupported)
        {
            sl::ReflexOptions options;
            options.mode = sl::ReflexMode::eLowLatency;
            reflexSupported = Check(api.reflexOptions(options));
            sl::ReflexState state;
            if (reflexSupported && Check(api.reflexState(state)))
                status.reflexAvailable = state.lowLatencyAvailable;
        }
    }
    if (pclSupported)
    {
        pclSupported = Feature(sl::kFeaturePCL, "slPCLSetMarker", api.marker)
            && Feature(sl::kFeaturePCL, "slPCLSetOptions", api.pclOptions)
            && Feature(sl::kFeaturePCL, "slPCLGetState", api.pclState);
        if (pclSupported) { sl::PCLOptions options; pclSupported = Check(api.pclOptions(options)); }
    }
    fgSupported = fgSupported && reflexSupported && pclSupported && status.reflexAvailable;
    if (fgSupported)
        fgSupported = Feature(sl::kFeatureDLSS_G, "slDLSSGSetOptions", api.fgOptions)
            && Feature(sl::kFeatureDLSS_G, "slDLSSGGetState", api.fgState);
    status.frameGenerationAvailable = fgSupported;
    UnityD3D12PluginEventConfig capture{
        kUnityD3D12GraphicsQueueAccess_DontCare,
        kUnityD3D12EventConfigFlag_ModifiesCommandBuffersState, false};
    graphics->ConfigureEvent(CaptureEvent, &capture);
    graphics->ConfigureEvent(SuperResolutionEvent, &capture);
    UnityD3D12PluginEventConfig submit{
        kUnityD3D12GraphicsQueueAccess_Allow,
        kUnityD3D12EventConfigFlag_FlushCommandBuffers, false};
    graphics->ConfigureEvent(SubmitStartEvent, &submit);
    graphics->ConfigureEvent(SubmitEndEvent, &submit);
}

static void UNITY_INTERFACE_API DeviceEvent(UnityGfxDeviceEventType event)
{
    std::lock_guard lock(mutex);
    if (event == kUnityGfxDeviceEventInitialize && unityGraphics->GetRenderer() == kUnityGfxRendererD3D12)
    {
        graphics = unityInterfaces->Get<IUnityGraphicsD3D12v8>();
        ConfigureDevice();
    }
    if (event == kUnityGfxDeviceEventShutdown && status.initialized)
    {
        DisableFrameGeneration();
        RestorePclWindow();
        RestoreSwapchains();
        RestoreImports();
        Check(api.shutdown());
        status.initialized = 0;
        reflexSupported = pclSupported = fgSupported = srSupported = false;
    }
}
}

EXPORT void UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* interfaces)
{
    using namespace bgd;
    unityInterfaces = interfaces;
    unityGraphics = interfaces->Get<IUnityGraphics>();
    // Renderer-specific interfaces need not exist at graphics-plugin preload.
    // Install interposition first; obtain D3D12v8 when Unity creates the device.
    if (!unityGraphics) return;
    if (LoadStreamline() && !InstallImports())
    {
        Log("UnityPlayer graphics imports were not found. Frame generation disabled.");
        api.shutdown(); status.initialized = 0;
    }
    unityGraphics->RegisterDeviceEventCallback(DeviceEvent);
}
EXPORT void UNITY_INTERFACE_API UnityPluginUnload()
{
    using namespace bgd;
    if (unityGraphics) unityGraphics->UnregisterDeviceEventCallback(DeviceEvent);
    RestoreImports();
    // The interposer stays loaded until process exit: Unity may still own its proxies.
}
