#include "Bridge.h"
#include <deque>
#include <cstring>
#include <algorithm>
#include <filesystem>
#include <fstream>
#include <shlobj.h>
namespace bgd
{
Diagnostics diagnostics;
static std::mutex logMutex;
static std::deque<std::string> recentLogs;
static std::wstring logDirectory;
static std::ofstream bridgeLog;
const std::wstring& LogDirectory() { return logDirectory; }
void InitializeLogs(const wchar_t* executable)
{
    PWSTR appData{};
    if (FAILED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &appData)))
    { Log("Cannot resolve LOCALAPPDATA for native logs."); return; }
    auto logs = std::filesystem::path(appData) / L"BudgetGameDev" / L"Streamline"
        / std::filesystem::path(executable).stem()
        / (std::to_wstring(GetCurrentProcessId()) + L"-" + std::to_wstring(GetTickCount64()));
    CoTaskMemFree(appData);
    std::error_code error;
    std::filesystem::create_directories(logs, error);
    if (error) { Log(("Cannot create native log directory: " + error.message()).c_str()); return; }
    logDirectory = logs.wstring();
    bridgeLog.open(logs / L"bridge.log", std::ios::out | std::ios::binary);
    Log("Native log session started (timestamps are system uptime milliseconds).");
}
void RecordLog(const char* message)
{
    std::lock_guard lock(logMutex);
    std::string line = std::to_string(GetTickCount64()) + "ms " + (message ? message : "");
    if (bridgeLog.is_open()) { bridgeLog << line << '\n'; bridgeLog.flush(); }
    recentLogs.push_back(line.substr(0, 2048));
    if (recentLogs.size() > 64) recentLogs.pop_front();
}
void ReadReflexReport()
{
    // Sample on the presentation thread, at 4 Hz. UI only reads this cached POD.
    static uint64_t nextRead{};
    auto now = GetTickCount64();
    if (!reflexSupported || now < nextRead) return;
    nextRead = now + 250;
    sl::ReflexState state;
    auto result = api.reflexState(state);
    diagnostics.reflexStateResult = static_cast<uint32_t>(result);
    diagnostics.latencyValid = result == sl::Result::eOk && state.latencyReportAvailable;
    if (!diagnostics.latencyValid) return;
    const sl::ReflexReport* latest{};
    for (const auto& report : state.frameReport)
        if (report.gpuRenderEndTime && report.simStartTime && report.gpuRenderEndTime >= report.simStartTime
            && (!latest || report.frameID > latest->frameID)) latest = &report;
    if (!latest) { diagnostics.latencyValid = 0; return; }
    if (latest->frameID != diagnostics.reflexReportFrame)
    {
        ++diagnostics.reflexReportUpdates;
        diagnostics.reportTick = now;
    }
    diagnostics.reflexReportFrame = latest->frameID;
    auto duration = [](uint64_t start, uint64_t end) -> uint32_t {
        return start && end >= start ? static_cast<uint32_t>(std::min<uint64_t>(end - start, UINT32_MAX)) : 0;
    };
    diagnostics.pcLatencyUs = duration(latest->simStartTime, latest->gpuRenderEndTime);
    diagnostics.simulationLatencyUs = duration(latest->simStartTime, latest->simEndTime);
    diagnostics.renderLatencyUs = duration(latest->renderSubmitStartTime, latest->renderSubmitEndTime);
    diagnostics.gpuLatencyUs = duration(latest->gpuRenderStartTime, latest->gpuRenderEndTime);
}
}
EXPORT uint32_t __cdecl BgdSL_GetLogDirectory(char* output, uint32_t capacity)
{
    if (!output || !capacity) return 0;
    const auto& path = bgd::LogDirectory();
    int size = WideCharToMultiByte(CP_UTF8, 0, path.c_str(), -1, output, static_cast<int>(capacity), nullptr, nullptr);
    if (!size) output[0] = 0;
    return size ? static_cast<uint32_t>(size - 1) : 0;
}
EXPORT uint32_t __cdecl BgdSL_GetDiagnostics(bgd::Diagnostics* output, uint32_t size)
{
    if (!output || size != sizeof(bgd::Diagnostics)) return 0;
    std::lock_guard lock(bgd::mutex);
    *output = bgd::diagnostics;
    output->snapshotTick = GetTickCount64();
    return 1;
}
EXPORT uint32_t __cdecl BgdSL_GetRecentLog(char* output, uint32_t capacity)
{
    if (!output || !capacity) return 0;
    std::lock_guard lock(bgd::logMutex);
    std::string result;
    for (const auto& line : bgd::recentLogs) result += line + "\n";
    if (result.size() >= capacity) result.erase(0, result.size() - (capacity - 1));
    std::memcpy(output, result.data(), result.size());
    output[result.size()] = 0;
    return static_cast<uint32_t>(result.size());
}
