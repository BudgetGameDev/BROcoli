#pragma once
#include <cstdint>
namespace bgd
{
struct Diagnostics
{
    uint64_t simulationId{}, submissionId{}, presentId{}, simulatedFrames{}, presentedFrames{};
    uint64_t sleepCalls{}, sleepSuccesses{}, markers{}, completeTags{}, slPresentedFrames{}, slStateSamples{};
    uint64_t reflexReportFrame{}, reflexReportUpdates{}, presentTick{}, reportTick{}, generatedTick{}, snapshotTick{};
    uint32_t activeReflex = UINT32_MAX;
    uint32_t tagMask{}, renderWidth{}, renderHeight{}, outputWidth{}, outputHeight{};
    uint32_t actualPresentedLast{}, fgStateResult = UINT32_MAX, reflexStateResult = UINT32_MAX;
    uint32_t latencyValid{}, pclWindowBound{}, presentResult{};
    uint32_t pcLatencyUs{}, simulationLatencyUs{}, renderLatencyUs{}, gpuLatencyUs{};
    uint32_t markerResult = UINT32_MAX, sleepResult = UINT32_MAX;
};
static_assert(sizeof(Diagnostics) == 208, "Diagnostics C ABI changed");
extern Diagnostics diagnostics;
void RecordLog(const char* message);
void ReadReflexReport();
}
