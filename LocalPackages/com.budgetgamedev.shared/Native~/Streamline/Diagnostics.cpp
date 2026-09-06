#include "Bridge.h"
#include <deque>
#include <cstring>
#include <algorithm>
namespace bgd
{
Diagnostics diagnostics;
static std::mutex logMutex;
static std::deque<std::string> recentLogs;
void RecordLog(const char* message)
{
    std::lock_guard lock(logMutex);
    recentLogs.push_back(std::to_string(GetTickCount64()) + "ms " + std::string(message ? message : "").substr(0, 2048));
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
