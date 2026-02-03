// StreamlineInit.cpp - Unity plugin interface and Streamline SDK initialization

#include "StreamlineCommon.h"
#include <shlobj.h>
#include <d3d12.h>
#include <dxgi1_4.h>
#include <string>
#include "Unity/IUnityInterface.h"
#include "Unity/IUnityGraphics.h"
#include "Unity/IUnityGraphicsD3D11.h"
#include "Unity/IUnityGraphicsD3D12.h"

// Unity Plugin Interface Globals
static IUnityInterfaces* s_UnityInterfaces = nullptr;
static IUnityGraphics* s_Graphics = nullptr;
static UnityGfxRenderer s_RendererType = kUnityGfxRendererNull;

// D3D12 interfaces
static IUnityGraphicsD3D12v7* s_D3D12v7 = nullptr;
static IUnityGraphicsD3D12v6* s_D3D12v6 = nullptr;
static ID3D12Device* s_D3D12Device = nullptr;
static ID3D12CommandQueue* s_D3D12CommandQueue = nullptr;

// Expose D3D12v7 for DLSS render callback
IUnityGraphicsD3D12v7* s_D3D12v7_ptr = nullptr;
IUnityGraphicsD3D12v7* GetUnityD3D12v7() { return s_D3D12v7_ptr; }

// D3D11 interface
static IUnityGraphicsD3D11* s_D3D11 = nullptr;
static ID3D11Device* s_D3D11Device = nullptr;

// Error tracking
static int g_lastErrorCode = 0;
static const char* g_lastErrorMessage = "Not initialized yet";

// Device Accessors
ID3D12Device* GetD3D12Device() { return s_D3D12Device; }
ID3D11Device* GetD3D11Device() { return s_D3D11Device; }
ID3D12CommandQueue* GetD3D12CommandQueue() { return s_D3D12CommandQueue; }
int GetRendererType() { return (int)s_RendererType; }

// Streamline Log Callback
static void SLLogCallback(sl::LogType type, const char* msg)
{
    const char* t = (type == sl::LogType::eInfo) ? "INFO" : 
                    (type == sl::LogType::eWarn) ? "WARN" : 
                    (type == sl::LogType::eError) ? "ERROR" : "DEBUG";
    LogMessage("[SL_%s] %s", t, msg);
}

// Setup plugin search paths for Streamline
static int SetupPluginPaths(const wchar_t** paths, wchar_t* appDir, wchar_t* unityDir, wchar_t* ngxDir)
{
    int n = 0;
    char exePath[MAX_PATH];
    if (GetModuleFileNameA(NULL, exePath, MAX_PATH))
    {
        char* lastSlash = strrchr(exePath, '\\');
        if (lastSlash)
        {
            *lastSlash = '\0';
            MultiByteToWideChar(CP_UTF8, 0, exePath, -1, appDir, MAX_PATH);
            paths[n++] = appDir;
            
            char fullExePath[MAX_PATH];
            GetModuleFileNameA(NULL, fullExePath, MAX_PATH);
            char* exeName = strrchr(fullExePath, '\\');
            if (exeName) exeName++; else exeName = fullExePath;
            char* ext = strrchr(exeName, '.');
            if (ext) *ext = '\0';
            
            char unityPlugins[MAX_PATH];
            snprintf(unityPlugins, MAX_PATH, "%s\\%s_Data\\Plugins\\x86_64", exePath, exeName);
            MultiByteToWideChar(CP_UTF8, 0, unityPlugins, -1, unityDir, MAX_PATH);
            paths[n++] = unityDir;
        }
    }
    
    char programData[MAX_PATH];
    if (SUCCEEDED(SHGetFolderPathA(NULL, CSIDL_COMMON_APPDATA, NULL, 0, programData)))
    {
        MultiByteToWideChar(CP_UTF8, 0, programData, -1, ngxDir, MAX_PATH);
        wcscat_s(ngxDir, MAX_PATH, L"\\NVIDIA\\NGX\\models");
        paths[n++] = ngxDir;
    }
    return n;
}

bool InitializeStreamline()
{
    if (g_initialized) { LogMessage("Streamline already initialized"); return true; }
    
    LogMessage("--- InitializeStreamline() ---");
    
    void* d3dDevice = s_D3D12Device ? (void*)s_D3D12Device : (void*)s_D3D11Device;
    if (!d3dDevice)
    {
        g_lastErrorCode = -999;
        g_lastErrorMessage = "No D3D device available";
        LogMessage("ERROR: No D3D device!");
        return false;
    }
    LogMessage("Using D3D%s device: 0x%p", s_D3D12Device ? "12" : "11", d3dDevice);
    
    // Setup preferences
    sl::Preferences prefs{};
    prefs.showConsole = true;
    prefs.logLevel = sl::LogLevel::eVerbose;
    prefs.logMessageCallback = SLLogCallback;
    prefs.flags = sl::PreferenceFlags::eDisableCLStateTracking | 
                  sl::PreferenceFlags::eAllowOTA | sl::PreferenceFlags::eLoadDownloadedPlugins;
    prefs.renderAPI = sl::RenderAPI::eD3D12;
    prefs.applicationId = 0x0E658700;
    prefs.engine = sl::EngineType::eUnity;
    prefs.engineVersion = "6000.0";
    
    // Plugin paths
    static wchar_t appDir[MAX_PATH], unityDir[MAX_PATH], ngxDir[MAX_PATH];
    static const wchar_t* paths[4] = {nullptr};
    prefs.numPathsToPlugins = SetupPluginPaths(paths, appDir, unityDir, ngxDir);
    prefs.pathsToPlugins = paths;
    
    // Features
    sl::Feature features[] = { sl::kFeatureReflex, sl::kFeaturePCL, sl::kFeatureDLSS, sl::kFeatureDLSS_G };
    prefs.featuresToLoad = features;
    prefs.numFeaturesToLoad = 4;
    
    LogMessage("Calling slInit()...");
    sl::Result result = slInit(prefs);
    if (result != sl::Result::eOk)
    {
        g_lastErrorCode = -(int)result;
        g_lastErrorMessage = "slInit failed";
        LogMessage("slInit FAILED: %d", (int)result);
        return false;
    }
    
    result = slSetD3DDevice(d3dDevice);
    if (result != sl::Result::eOk)
    {
        g_lastErrorCode = -100 - (int)result;
        g_lastErrorMessage = "slSetD3DDevice failed";
        LogMessage("slSetD3DDevice FAILED: %d", (int)result);
        slShutdown();
        return false;
    }
    
    // Check feature support
    sl::AdapterInfo ai{};
    g_reflexSupported = (slIsFeatureSupported(sl::kFeatureReflex, ai) == sl::Result::eOk);
    g_pclSupported = (slIsFeatureSupported(sl::kFeaturePCL, ai) == sl::Result::eOk);
    g_dlssSupported = (slIsFeatureSupported(sl::kFeatureDLSS, ai) == sl::Result::eOk);
    g_dlssgSupported = (slIsFeatureSupported(sl::kFeatureDLSS_G, ai) == sl::Result::eOk);
    
    LogMessage("Reflex:%s PCL:%s DLSS:%s DLSS-G:%s",
               g_reflexSupported ? "Y" : "N", g_pclSupported ? "Y" : "N",
               g_dlssSupported ? "Y" : "N", g_dlssgSupported ? "Y" : "N");
    
    g_initialized = true;
    g_frameId = 0;
    g_lastErrorCode = 0;
    g_lastErrorMessage = "OK";
    LogMessage("=== Streamline initialized! ===");
    return true;
}

void ShutdownStreamline()
{
    if (!g_initialized) return;
    LogMessage("Shutting down Streamline...");
    slShutdown();
    g_initialized = g_reflexSupported = g_pclSupported = g_dlssSupported = g_dlssgSupported = false;
    g_currentMode = sl::ReflexMode::eOff;
    g_dlssMode = sl::DLSSMode::eOff;
    g_dlssgMode = sl::DLSSGMode::eOff;
}

// Unity Graphics Device Event
static void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
{
    if (eventType == kUnityGfxDeviceEventInitialize)
    {
        s_RendererType = s_Graphics->GetRenderer();
        LogMessage("Graphics init, renderer: %d", (int)s_RendererType);
        
        if (s_RendererType == kUnityGfxRendererD3D12)
        {
            s_D3D12v7 = s_UnityInterfaces->Get<IUnityGraphicsD3D12v7>();
            if (s_D3D12v7) { s_D3D12Device = s_D3D12v7->GetDevice(); s_D3D12CommandQueue = s_D3D12v7->GetCommandQueue(); s_D3D12v7_ptr = s_D3D12v7; }
            else { s_D3D12v6 = s_UnityInterfaces->Get<IUnityGraphicsD3D12v6>();
                   if (s_D3D12v6) { s_D3D12Device = s_D3D12v6->GetDevice(); s_D3D12CommandQueue = s_D3D12v6->GetCommandQueue(); } }
        }
        else if (s_RendererType == kUnityGfxRendererD3D11)
        {
            s_D3D11 = s_UnityInterfaces->Get<IUnityGraphicsD3D11>();
            if (s_D3D11) s_D3D11Device = s_D3D11->GetDevice();
        }
        if (s_D3D12Device || s_D3D11Device) InitializeStreamline();
    }
    else if (eventType == kUnityGfxDeviceEventShutdown)
    {
        ShutdownStreamline();
        s_D3D12Device = nullptr; s_D3D12CommandQueue = nullptr; s_D3D11Device = nullptr;
        s_RendererType = kUnityGfxRendererNull;
    }
}

// Unity Plugin Entry Points
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* ui)
{
    InitLogFile();
    LogMessage("=== GfxPluginStreamline Loading (v1.0.0, SDK 2.10.3) ===");
    s_UnityInterfaces = ui;
    s_Graphics = ui->Get<IUnityGraphics>();
    if (s_Graphics) { s_Graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent); OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize); }
    LogMessage("=== GfxPluginStreamline Loaded ===");
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload()
{
    LogMessage("=== GfxPluginStreamline Unloading ===");
    if (s_Graphics) s_Graphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);
    ShutdownStreamline();
    s_UnityInterfaces = nullptr; s_Graphics = nullptr;
    CloseLogFile();
}

// Exported Availability APIs
EXPORT bool SLReflex_IsAvailable() { HMODULE h = LoadLibraryA("sl.interposer.dll"); if (h) { FreeLibrary(h); return true; } return false; }
EXPORT bool SLReflex_IsInitialized() { return g_initialized; }
EXPORT int SLReflex_GetLastErrorCode() { return g_lastErrorCode; }
EXPORT const char* SLReflex_GetLastErrorMessage() { return g_lastErrorMessage; }
EXPORT bool SLReflex_HasD3D12Device() { return s_D3D12Device != nullptr; }
EXPORT bool SLReflex_HasD3D11Device() { return s_D3D11Device != nullptr; }
EXPORT int SLReflex_GetRendererType() { return (int)s_RendererType; }
EXPORT bool SLReflex_TryInitialize() { if (g_initialized) return true; if (s_D3D12Device || s_D3D11Device) return InitializeStreamline(); g_lastErrorCode = -999; g_lastErrorMessage = "No device"; return false; }
EXPORT bool SLReflex_Initialize(void*) { if (g_initialized) return true; return (s_D3D12Device || s_D3D11Device) ? InitializeStreamline() : false; }
EXPORT void SLReflex_Shutdown() { std::lock_guard<std::mutex> lock(g_mutex); ShutdownStreamline(); }
EXPORT bool SLReflex_IsSupported() { return g_reflexSupported; }
EXPORT bool SLReflex_IsPCLSupported() { return g_pclSupported; }
EXPORT bool SLDLSS_IsSupported() { return g_dlssSupported; }
EXPORT bool SLDLSS_IsFrameGenSupported() { return g_dlssgSupported; }
