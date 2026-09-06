# Windows HDRP DLSS and Streamline

This shared-package integration targets Unity **6000.5.10f1**, HDRP **17.5.0**,
Windows x64 and DX12, including SDR and the project's HDR10 output path. Unity's NVIDIA module
provides DLSS Super Resolution, defaulting to **Quality / preset K**. Streamline
**2.12.0** provides DLSS Frame Generation and **sl.reflex**, with three generated
frames requested (4× total), clamped to the device's reported maximum. Reflex
defaults to **On**, without Boost. Unsupported devices retain ordinary rendering.

**Status:** the bridge can be cross-compiled, its production payload can be checked,
and its managed code can be tested on macOS. These checks do not validate Unity's
DXGI interposition, image quality, actual generated frames, or Reflex timing.
Windows RTX acceptance below is required before treating this as production-ready.
The full player must be built with the Windows Editor: the macOS Editor cannot
compile HDRP's DXC/STP and ray-tracing shaders for DirectX. The attempted macOS
cross-build passed managed compilation but failed at that shader compilation step.

## Prepare

Resolve the project's packages in Unity once. Install CMake and Visual Studio's
x64 C++ tools on Windows, then run from the project root:

```powershell
python LocalPackages/com.budgetgamedev.shared/Tools~/Streamline/setup.py `
  --unity-plugin-api "C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data\PluginAPI"
```

On macOS, the same script accepts the Editor's
`Unity.app/Contents/Resources/PluginAPI` directory and cross-compiles with
`mingw-w64`. `--sdk PATH` uses an already-extracted NVIDIA SDK. The script checks
the pinned production hashes even with this option. On Windows it additionally
checks NVIDIA Authenticode signatures. Refresh Unity after setup.

Setup builds `Native~/Streamline/artifacts/win-x64`, stages the graphics plugin in
`Runtime/Plugins/Streamline`, and creates an ignored, embedded HDRP package from the
resolved 17.5.0 source. The installer checks the unmodified final-pass source hash
before inserting the small callback in `HDRenderPipeline.PostProcess.cs`; it
refuses unknown versions or local edits. `--hdrp-only` prepares just that hook.
The patch and native artifacts are reproducible build inputs, not committed SDKs.
The patch also installs its linker rules inside the embedded HDRP package, so
projects without that optional hook do not acquire unresolved linker references.
The committed plugin metadata limits loading to Windows x64 and preloads it.

Use **Tools > Build > Native > Windows HDRP HDR10 Player**, or the existing native
release command with `--pipeline hdrp`. Release staging carries the patched HDRP
and production payload into the isolated project. Build callbacks reject missing
hooks, incorrect importer settings, non-x64 DLLs, and payloads that differ from
the pinned production release. Libraries and licenses are copied beside the EXE.
The Windows HDRP build uses DX12 exclusively; the URP build keeps its API policy.

Press **F10** for the reusable rendering settings panel. Hosts can instead bind
their own UI to `StreamlineSettings`. Disabling Reflex also disables FG in this
panel; a programmatic FG request forces effective Reflex On. FG pauses while the
IMGUI panel is open because that UI is composed outside HDRP's tagged UI buffer.

## Settings menu and live diagnostics

Open **Settings > NVIDIA** from either the main menu or pause menu. Both use the
shared `NvidiaSettingsPage`; game code only links the page into its navigation.
Other hosts can create the same page and use the pipeline-independent
`NvidiaRendering.IBackend` contract without referencing HDRP assemblies.

The controls toggle DLSS Quality/K, cycle supported FG multipliers, and select
Reflex Off/On/On+Boost. Selecting Reflex Off also disables FG. **Defaults** restores
Quality/K, a 4x request, and Reflex On. **Copy Debug** copies the entire report,
including text outside the visible scroll area. Scroll with the mouse wheel,
touch, right stick, or Page Up/Down; Escape/B returns to Settings.

The page refreshes at 4 Hz using unscaled time, including while paused. Unlike the
F10 IMGUI window, this ordinary overlay canvas does not itself suspend capture.
An orthographic menu camera can still make FG ineligible.

The report separates requested preferences, accepted native options, and observed
runtime evidence:

- **DLSS:** HDRP asset configuration and NVIDIA module/NGX versions, feature
  validity, execution dimensions, initialized quality and preset. Feature validity
  is not a GPU completion measurement; NVIDIA App overrides are not exposed.
- **FG:** actual proxy attachment, support/requirements results, decoded status
  errors, per-frame input mask, dimensions, token IDs, successful real Presents,
  and `numFramesActuallyPresented` from the existing present-thread state query.
  Extra-present evidence expires after 1.5 seconds. An enabled option alone never
  produces an observed-working label. SDK counts do not prove monitor scan-out.
- **Reflex:** accepted mode, sleep and marker results/counters, PCL window binding,
  driver report frame IDs, report age, and PC/simulation/submission/GPU latency.
  PC latency is simulation start to GPU render end, not click-to-photon latency.
- **Debug:** platform, adapter, API/driver, HDR format, focus, load/ABI failures,
  sticky native errors and the latest 64 native/Streamline messages. Full log files
  remain in the path shown in the report.

Native telemetry is cached under the bridge lock; opening the page does not add
DLSS-G state reads that would consume/reset its presented-frame counters. Rebuild
the shared native bridge when updating to this diagnostics interface.

## Actual integration points

1. **Early DXGI/D3D12:** `GfxPluginBudgetGameDevStreamline` uses Unity's documented
   graphics-plugin preload convention. `UnityPluginLoad` validates NVIDIA DLL
   signatures, calls `slInit`, and redirects UnityPlayer's own graphics imports
   and dynamically resolved DXGI/D3D12 entry points to `sl.interposer`.
   This is a native Unity-version-specific adapter, not a C# initialization call.
   It does not modify system DLLs or other processes. Unity's resulting device,
   queue, command-list, and swapchain interfaces must be Streamline proxies;
   actual interception still requires validation against the Windows player.
2. **Actual Present:** submission-thread plugin events obtain Unity's swapchain
   through `IUnityGraphicsD3D12v8`. `slGetNativeInterface` must return a different
   underlying interface before FG can run. Native callbacks wrap that proxy's
   `Present`, `Present1`, fullscreen, and resize calls. Streamline's proxy handles
   `GetBuffer`, backbuffer indexing and the actual FG presentation. FG is turned
   off before resize/fullscreen transitions, on occlusion, or without current inputs.
3. **HDRP inputs:** `StreamlineInputsPass` captures the actual pre-postprocess
   depth and motion textures, non-jittered camera matrices, negative HDRP jitter,
   normalized current-to-previous motion scale, and the rendering extent. The
   final-pass hook repeats HDRP's actual final shader with transparent overlay UI
   to capture a full-resolution HUD-less image in the output format and encoding.
   A separate R8 texture contains the real HDRP overlay alpha. For SDR, the overlay
   renderer list is drawn to a separate color/depth target before alpha extraction;
   HUD-less SDR color uses the final shader and an sRGB render target. HDRP overlay canvases
   stay separate; the existing camera-space HDR UI workaround is restricted to URP.
   Screen-space overlay UI is separated; world-space UI and after-post scene
   geometry remain part of the scene image.
4. **Lifetime/state:** render events request D3D12 resource states using Unity's
   v8 API. The source textures are tagged `eOnlyValidNow` because HDRP can alias or
   reuse them before Present; Streamline records copies and owns their lifetime.
   Depth/motion/constants, HUD-less color and UI alpha tags must all arrive for the same token.
5. **Reflex:** a PlayerLoop callback obtains one token and sleeps before EarlyUpdate
   input processing. The same token flows through SimulationStart/End, submission
   events, tagging, and the actual PresentStart/End callbacks. Markers use
   `slPCLSetMarker`, the current Streamline Reflex/PCL API. PCL latency-ping and
   mouse-flash messages are handled on the real player window. Sleep and markers
   continue when low-latency mode is Off, as required by NVIDIA.

The implementation currently enables FG for **one fullscreen perspective camera
on display 0**. Orthographic menus, render textures, XR,
multiple output cameras, and intermediate final passes do not provide a complete tagged frame,
so FG stays off for them. DLSS SR and Reflex are independent of the final-color
hook. Capture is skipped when FG is disabled or unsupported. No fabricated empty
HUD mask is used for SDR or HDR.

## Windows RTX acceptance

Use Windows 10 2004+ or Windows 11, a supported current NVIDIA driver, and
Hardware-accelerated GPU Scheduling enabled (restart after changing it).
These are evaluated through `slIsFeatureSupported` for Unity's actual adapter;
the bridge also calls `slGetFeatureRequirements` and requires DX12 support.
It does not infer support merely from GPU-name text or an RTX product number.

1. Verify `Player.log` reports `proxy=1`, `reflex=1`, successful feature/requirements
   results and `status=0`. On a supported MFG GPU, the default should report
   `generated=3`; on a 2×-only GPU it should report `generated=1`. These are accepted
   options, not an FPS measurement. Check the actual generated/presented frames
   separately with FrameView and NVIDIA's tools.
2. Inspect Streamline logs under
   `%LOCALAPPDATA%\BudgetGameDev\Streamline\<executable-name>`. Resolve integration
   warnings, including frame-index mismatches. Initialization failures are also
   sent to OutputDebugString. A DLL existing on disk is not attachment evidence.
3. Run NVIDIA's Reflex Test Utility with FG On/Off and Reflex Off/On/On+Boost.
   Confirm nonzero PC latency, ping/flash operation, continuously updated markers,
   and matching rendered/presented frame indices. Verify low-latency sleep remains
   active when Reflex is Off. Observe GPU-presented FPS rather than Unity's
   simulation FPS counter when checking MFG.
4. Inspect tagged depth, motion, HUD-less color and UI alpha using a development
   validation package and NVIDIA's DLSS-G visualization tests. Check camera cuts,
   animated transparency, HUD edges, exposure changes, dithering, native-resolution
   UI, HDR luminance and encoding. Development binaries must never replace the
   pinned production payload in a release build.
5. Test resize, minimize/restore, Alt-Tab, HDR output changes, quality changes,
   scene transitions and shutdown, plus unsupported adapters/drivers/HAGS-off.
   Check for device loss, deadlocks, lingering interpolation, and VRAM growth.
6. Test real 2× and MFG-capable devices and compare baseline GPU/CPU frametimes.
   Capturing the final color and requesting native texture pointers has a cost
   that needs measurement on Windows hardware.

## Validation performed

- Windows x64 native bridge cross-compiled against Streamline 2.12.0 and Unity's v8 headers.
- All 31 Windows player script assemblies compiled in an isolated Windows-targeted Editor run.
- Six Streamline Editor tests, 38 existing display-settings tests, and 16 Python
  release/setup tests passed. Live Editor compilation had no first-party warnings.
- Five diagnostics regression tests and two shared-page tests passed. Main-menu
  and pause-menu rendering, compact layout, paused refresh, scrolling, and a
  single Escape returning only to Settings were checked in the live Editor.
- Production SHA-256/x64 checks and Unity plugin import/preload validation passed.
- Full player build failed on macOS at Unity's DXC/STP and ray-tracing shader
  compilation; no successful Windows GPU execution is claimed.
- The repository-wide source-size check still fails on two unchanged files:
  `VoiceWavetable.cs` (1984 lines) and `GameDisplaySettingsTests.cs` (309 lines).

## References

- [NVIDIA DLSS-G integration guide](https://github.com/NVIDIA-RTX/Streamline/blob/v2.12.0/docs/ProgrammingGuideDLSS_G.md)
- [NVIDIA Streamline Reflex guide](https://github.com/NVIDIA-RTX/Streamline/blob/v2.12.0/docs/ProgrammingGuideReflex.md)
- [Streamline device and DXGI integration](https://github.com/NVIDIA-RTX/Streamline/blob/v2.12.0/docs/ProgrammingGuide.md)
- [Unity graphics-plugin preloading](https://docs.unity3d.com/6000.5/Documentation/Manual/low-level-native-plugin-rendering-extensions.html)
- [HDRP DLSS documentation](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.high-definition/Documentation~/deep-learning-super-sampling-in-hdrp.md)
