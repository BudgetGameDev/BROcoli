# HDRP 16 FPS investigation — 2026-09-06

The main cause was rebuilding the entire HDRP pipeline every frame. The Streamline
adapter's `Configure()` runs from `StreamlineRuntime.Update()` and unconditionally
assigned `HDRenderPipelineAsset.currentPlatformRenderPipelineSettings`, even when
the desired settings were identical. HDRP explicitly documents this setter as
rebuilding the whole pipeline from scratch. Its `OnValidate()` invalidates the
active pipeline. This also discards temporal histories and recreates GPU resources.

The fix compares the desired dynamic-resolution settings with the current settings
before using that setter. Upscaler priority lists compare by contents, so a fresh
but equivalent list does not invalidate the pipeline. Real resolution, upscaler,
quality and enable/disable changes still apply; disposal still restores the original
settings. Other HDRP asset settings are preserved.

## Standalone evidence

Unity 6000.5.10f1, RTX 5070, 2560×1440, HDRP RT Ultra (quality 9), DLSS Quality,
Reflex On + Boost, 240 Hz, VSync off, uncapped. Both players were **Development +
Autoconnect Profiler**, connected to a separate Unity Editor. Deep profiling and
allocation call stacks were off. The system-readiness route runs without saving,
with 5 seconds of warm-up and 20 seconds of gameplay measurement.

| Measurement | Before | After |
|---|---:|---:|
| 20-second benchmark, rendered FPS | 14.8 | 86.1–86.3 |
| Benchmark mean rendered frame | 67.4 ms | 11.6 ms |
| Profiler sample, mean frame (120 gameplay frames) | 68.245 ms | 11.840 ms |
| Main-thread `Semaphore.WaitForSignal` | 37.122 ms/frame | 0.066 ms/frame |
| `VolumeManager.Initialize` | 1 call/frame | 0 calls |
| Render-thread committed resource creation | 36.758 calls/frame | 0.067 calls/frame |
| Committed resource creation CPU time | 6.054 ms/frame | 0.018 ms/frame |
| Other GPU-resource creation calls | 145.767/frame | 27.442/frame |
| Main-thread `DXGI.WaitOnSwapChain` | 16.829 ms/frame | 9.257 ms/frame |

The disappearance of per-frame initialization and resource churn, together with
the controlled code change and approximately 5.8× rendered-FPS improvement,
identifies pipeline recreation as the dominant regression. Marker durations can
overlap across threads and hierarchy levels; do not add them together.

The remaining roughly 9 ms swapchain wait is not evidence that VSync is enabled.
The native Reflex report in the fixed gameplay run measured 9.14 ms GPU duration
and 11.14 ms simulation-start-to-GPU-end latency. The current workload is consistent
with waiting for rendered work rather than the former pipeline-recreation churn.
Unity's raw GPU-frame field returned zero in this capture; that means unavailable,
not a zero-cost GPU frame. We have not attributed the remaining GPU time to individual
HDRP passes.

### Development frame-generation limitation

FG was requested at 4× but **suspended in both development captures**: depth/motion
arrived, but the final-color/UI tags did not. HDRP's `HDUtils.PostProcessIsFinalPass`
explicitly returns false when `Debug.isDebugBuild`. Development players perform
their final output through another blit so debug rendering can follow postprocessing.
The current shared final-frame hook intentionally excludes that intermediate buffer.
Thus the comparison measures rendered frames with FG inactive; it must not be
presented as a working-FG development benchmark. Release validation is separate.

### Fixed release validation

With the receiving Editor and development player closed, the fixed release at the
same Quality 9 settings completed the 20-second route at **71.4 rendered FPS**,
14.0 ms mean frame time, **61.1 FPS 1% low**, and 14.9 ms P99. The previous release
benchmark measured 16.1 rendered FPS. The moving player visibly reported about
72 rendered / 289 total FPS with 4× FG. Native diagnostics confirmed all required
inputs, 528 complete tagged frames at the sampled point, successful DLSS dispatches,
and SDK presentation count 4. The recorded PC latency was 17.22 ms, versus roughly
78 ms before the fix. This is simulation-to-GPU-end, not complete click-to-display latency.

The release result is separate from the development comparison because FG is active
and the profiler is detached. The benchmark save entries remained unchanged.
Evidence: `build/verification/hdrp-fixed-release-readiness.txt` and
`build/verification/hdrp-fixed-release-gameplay.txt`.

## Reproduce and inspect

Build a fresh profiling player into an empty output directory:

```powershell
python scripts/release-build.py --product brocoli --pipeline hdrp --development --connect-profiler --reuse-stage --output build/releases/brocoli-hdrp-profiler
```

Start the matching Unity Editor with the Profiler recording, then run the standalone
player with `-screen-quality "HDRP RT Ultra" -profiler-ip 127.0.0.1`. The generated
`boot.config` contains `player-connection-mode=Connect` and `profiler-enable=1`.
Use Settings → System Readiness → Run Benchmark. Keep the player focused.

The native Unity Profiler recordings are `build/verification/hdrp-before.raw` and
`hdrp-after.raw`. Open **Window → Analysis → Profiler → Load** to inspect them.
The analyzed ranges are frames 385–504 before and 3695–3814 after. The adjacent
`-summary.tsv`, `-frames.tsv` and `-samples.tsv` files provide exported marker totals,
frame times and a representative sample hierarchy. The full recordings contain
additional frames, including loading and menus; keep those outside the comparison.

Validation: 10 Unity rendering/build-iteration tests passed, including two new
regressions for unchanged settings and changed upscaler priority. All 26 Python
release-script tests passed. An earlier camera-capability test was inapplicable in
the receiver Editor launched with `-nographics`; rerunning the complete selected
test group with graphics enabled passed all tests.
