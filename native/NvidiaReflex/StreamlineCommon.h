// StreamlineCommon.h
// Shared declarations for the Streamline plugin modules
#pragma once

#include <windows.h>
#include <cstdint>
#include <cstdio>
#include <mutex>

// Streamline SDK headers
#include "sl.h"
#include "sl_reflex.h"
#include "sl_pcl.h"
#include "sl_dlss.h"
#include "sl_dlss_g.h"

// ============================================================================
// Export Macro
// ============================================================================

#define EXPORT extern "C" __declspec(dllexport)

// ============================================================================
// Shared State (defined in StreamlineCommon.cpp)
// ============================================================================

// Initialization state
extern bool g_initialized;
extern bool g_reflexSupported;
extern bool g_pclSupported;
extern bool g_dlssSupported;
extern bool g_dlssgSupported;

// Current modes
extern sl::ReflexMode g_currentMode;
extern sl::DLSSMode g_dlssMode;
extern sl::DLSSGMode g_dlssgMode;
extern uint32_t g_numFramesToGenerate;
extern uint64_t g_frameId;

// Thread safety
extern std::mutex g_mutex;

// Logging
typedef void (*LogCallback)(const char* message);
extern LogCallback g_logCallback;
extern FILE* g_logFile;

// DLSS viewport
extern sl::ViewportHandle g_dlssViewport;

// ============================================================================
// DLSS Render Callback State (for IssuePluginEvent pattern)
// ============================================================================

// Event IDs for IssuePluginEvent
constexpr int kDLSSEventID_Evaluate = 0xD155E001;  // DLSS Evaluate

// DLSS pending evaluation data
struct DLSSPendingEval
{
    bool ready;
    uint32_t frameIndex;
};
extern DLSSPendingEval g_dlssPending;

// Unity interfaces for command list access
struct IUnityGraphicsD3D12v7;
extern IUnityGraphicsD3D12v7* GetUnityD3D12v7();

// Render callback for IssuePluginEvent
typedef void (*UnityRenderingEvent)(int eventID);
UnityRenderingEvent GetDLSSRenderCallback();

// ============================================================================
// Logging Functions (defined in StreamlineCommon.cpp)
// ============================================================================

void InitLogFile();
void CloseLogFile();
void LogMessage(const char* format, ...);

// ============================================================================
// D3D Device Access (defined in StreamlineInit.cpp)
// ============================================================================

struct ID3D12Device;
struct ID3D11Device;
struct ID3D12CommandQueue;

ID3D12Device* GetD3D12Device();
ID3D11Device* GetD3D11Device();
ID3D12CommandQueue* GetD3D12CommandQueue();
int GetRendererType();

// ============================================================================
// Initialization (defined in StreamlineInit.cpp)
// ============================================================================

bool InitializeStreamline();
void ShutdownStreamline();
