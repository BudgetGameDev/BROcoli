# Streamline (DLSS / Frame Gen) Integration Review + Fix Plan

Target: Standalone Windows 11, DirectX 12, RTX 5070.

This repo currently has a custom Streamline native plugin (`GfxPluginStreamline.dll`) plus URP integration code, but several critical gaps can explain:
- crash-on-launch / crash-on-first-frame
- DLSS not actually upscaling
- Frame Generation not working (and required buffers not tagged correctly)

---

## What Exists Today (Quick Map)

Native (Windows-only):
- `native/NvidiaReflex/StreamlineReflexPlugin.cpp` (Streamline init + Reflex + DLSS tagging/eval + DLSSG options/tag helpers)
- `native/NvidiaReflex/StreamlineDLSSPlugin.cpp` (legacy DLSS/DLSSG wrappers + presets)
- Builds to: `Assets/Plugins/x86_64/GfxPluginStreamline.dll` (Unity loads early because `GfxPlugin*` and isPreloaded)

Managed:
- `Assets/Scripts/StreamlineReflexPlugin.cs` (C# P/Invoke wrapper)
- `Assets/Scripts/StreamlineDLSSPlugin.cs` (C# P/Invoke wrapper incl. tagging APIs)

URP integration:
- `Assets/Scripts/Rendering/DLSSRenderFeature.cs`
- `Assets/Scripts/Rendering/DLSSUpscalePass.cs`
- Renderer asset includes feature: `Assets/3dRenderer_Renderer.asset` (Frame Gen enabled right now)

Packaging:
- `Assets/Plugins/x86_64/` currently contains only `.meta` placeholders + README in this checkout; the actual DLLs must be present in the Windows build output (`*_Data/Plugins/x86_64/`) for the player to run.

---

## Highest-Probability Crash Causes

### 1) P/Invoke signature mismatches (can hard-crash if called)

In `Assets/Scripts/StreamlineDLSSPlugin.cs`:
- `SLDLSS_GetOptimalSettings(..., out DLSSSettings outSettings)` **does not match** the native export in `native/NvidiaReflex/StreamlineReflexPlugin.cpp` (native takes multiple `uint32_t*` outputs, not a struct pointer).
- `SLDLSSG_GetState(out DLSSGState outState)` **does not match** the native export in `native/NvidiaReflex/StreamlineReflexPlugin.cpp` (native takes multiple output pointers, not a struct).

If *any* code path calls `StreamlineDLSSPlugin.GetOptimalSettings(...)` or `GetFrameGenState(...)`, you can get immediate access violations.

Fix options:
- **Preferred:** Update the C# `DllImport` signatures to match the native exports, and then populate the structs in managed code.
- Alternative: Add native wrapper exports that exactly match the existing C# struct signatures (and rebuild the DLL).
- Alternative: Point C# to the `*Legacy` exports in `StreamlineDLSSPlugin.cpp` using `DllImport(EntryPoint=...)`.

### 2) DLSS evaluation is invoked with a null/invalid command buffer

In `Assets/Scripts/Rendering/DLSSUpscalePass.cs`, the pass calls:
- `StreamlineDLSSPlugin.Evaluate(IntPtr.Zero);`

But the native `SLDLSS_Evaluate(void* commandBuffer)` passes this through to:
- `slEvaluateFeature(..., static_cast<sl::CommandBuffer*>(commandBuffer))`

On D3D12, Streamline generally needs an actual command list (and the call must happen on the correct thread / correct point in the frame). A null command buffer can lead to failure and may crash depending on internal validation.

### 3) Work is executed on the wrong thread (main thread vs render/submission thread)

Current flow calls `slSetConstants`, `slSetTagForFrame`, and `slEvaluateFeature` directly from C# render pass code.

For Unity D3D12 integration, you typically need to:
- schedule native work via a plugin event (`CommandBuffer.IssuePluginEvent*`)
- in native code, use `IUnityGraphicsD3D12v7::CommandRecordingState(...)` to obtain the current `ID3D12GraphicsCommandList*`

The repo already includes Unity’s D3D12 interface header showing:
- `IUnityGraphicsD3D12v7::ConfigureEvent(...)`
- `IUnityGraphicsD3D12v7::CommandRecordingState(...) -> ID3D12GraphicsCommandList*`

File: `native/NvidiaReflex/Unity/IUnityGraphicsD3D12.h`

### 4) Frame index / frame token is not managed unless Reflex BeginFrame is called

Native code uses `g_frameId` for `slGetNewFrameToken(...)`, but `g_frameId++` only happens in:
- `native/NvidiaReflex/StreamlineReflexPlugin.cpp` -> `SLReflex_BeginFrame()`

And in this checkout, the only managed caller is:
- `Assets/Scripts/FrameRateOptimizer.cs` (but it does not appear to be referenced by scenes in this repo snapshot).

If `SLReflex_BeginFrame()` is not called once per presented frame, tags/constants/evaluations all reuse the same frame index, which is not a valid Streamline integration pattern.

---

## Buffer / Resolution Tagging Gaps (DLSS SR)

### Render resolution is likely wrong when URP renderScale != 1

`DLSSRenderScaleManager` changes `UniversalRenderPipelineAsset.renderScale`, but `DLSSUpscalePass` computes:
- `renderWidth = camera.pixelWidth`
- `renderHeight = camera.pixelHeight`

Those are usually the *output* dimensions, not the *internal* (renderScale) dimensions. If you tag the input color/depth/mvec with dimensions that do not match the actual resource, Streamline can reject tags or behave unpredictably.

Use URP’s descriptor:
- `renderingData.cameraData.cameraTargetDescriptor.width/height` for internal dimensions

### Input textures are pulled via global shader properties (may be wrong in URP 17)

`DLSSUpscalePass.TagResources` uses:
- `_CameraDepthTexture`
- `_MotionVectorTexture`
- `_CameraColorTexture`

These are not guaranteed to be set the way you expect in URP, and can change depending on renderer settings, intermediate texture mode, render graph, etc.

Prefer using the actual RTHandles from the renderer (camera color/depth handles) and only fall back to globals for debugging.

### Formats and resource states are hardcoded

The pass hardcodes DXGI format IDs and D3D12 resource state bits. In practice:
- Depth may not be `DXGI_FORMAT_D32_FLOAT` on all URP configs.
- The “current resource state” you pass to Streamline needs to match reality, or you need a robust barrier/state-tracking strategy.

At minimum for debugging: log (on Windows) the Unity texture graphics format and verify it matches your assumed DXGI format.

---

## Frame Generation Gaps (DLSS-G / MFG)

Right now, Frame Generation is *enabled* in settings (`Assets/3dRenderer_Renderer.asset`) but the integration is incomplete:

Missing pieces:
- No code creates/provides a **HUD-less color** buffer (world without UI) and a **UI color+alpha** buffer (UI only).
- No code calls `StreamlineDLSSPlugin.TagHUDLessColor(...)` or `TagUIColorAndAlpha(...)`.
- No code calls the non-legacy `StreamlineDLSSPlugin.SetFrameGenOptions(...)` to provide correct buffer sizes (colorWidth/height, mvecDepthWidth/height).
- Native side does **not** call `slEvaluateFeature(kFeatureDLSS_G, ...)` anywhere in this repo, so DLSS-G is never evaluated explicitly.

Practical requirement (from NVIDIA Streamline DLSS Frame Generation integration guidance):
- HUD-less color and UI buffers must match the backbuffer resolution exactly.
- UI must be split out (otherwise generated frames smear/ghost UI).

Reference (integration checklist): https://developer.nvidia.com/rtx/dlss/streamline/get-started#dlss-multi-frame-generation

---

## Suggested “Make It Work” Implementation Path (Minimize Risk)

### Phase 0: Get the game to launch reliably
1) Ensure all required DLLs ship in the Windows build:
   - `GfxPluginStreamline.dll`
   - `sl.interposer.dll`, `sl.common.dll`
   - `sl.dlss.dll`, `sl.dlss_g.dll`
   - `sl.reflex.dll`, `sl.pcl.dll`
   - `nvngx_dlss.dll`, `nvngx_dlssg.dll`
2) Temporarily set `DLSSRenderFeature.settings.dlssMode = Off` and `enableFrameGen = false` in `Assets/3dRenderer_Renderer.asset` to confirm stability without Streamline evaluation.
3) Fix the P/Invoke mismatches in `Assets/Scripts/StreamlineDLSSPlugin.cs` so accidental calls can’t crash the player.

### Phase 1: DLSS Super Resolution only (no Frame Gen)
Goal: render at lower res, DLSS upscales to full res.
1) Correct internal render dimensions in `DLSSUpscalePass` to use `cameraTargetDescriptor.width/height`.
2) Stop calling Streamline from the main thread with null command buffers.
3) Implement a render-thread plugin-event path:
   - In native: add a `UnityRenderingEventAndData` callback for DLSS that:
     - grabs `ID3D12GraphicsCommandList*` via `IUnityGraphicsD3D12v7::CommandRecordingState`
     - calls `slGetNewFrameToken` once for the frame index
     - calls `slSetConstants` + `slSetTagForFrame` for required buffers
     - calls `slEvaluateFeature(kFeatureDLSS, ...)` with the real command list
   - In C#: in the URP pass, pack pointers + sizes into an unmanaged struct and call `cmd.IssuePluginEventAndData(...)`.
4) Verify in `GfxPluginStreamline.log` that:
   - `slInit` succeeded
   - DLSS is supported
   - evaluation returns `eOk`

### Phase 2: Add Frame Generation (DLSS-G / MFG)
Goal: generated frames with correct UI composition.
1) Produce two full-resolution buffers:
   - `HUDLessColor` = full-res color **without UI** (likely the output of DLSS SR pass before UI)
   - `UIColorAndAlpha` = UI-only color with alpha (render UI into a separate RT)
2) Tag DLSS-G resources each frame:
   - `Depth`, `MotionVectors` (usually at render resolution)
   - `HUDLessColor`, `UIColorAndAlpha` (must match backbuffer resolution)
3) Call `slDLSSGSetOptions` with correct sizes:
   - `colorWidth/height` = backbuffer dims
   - `mvecDepthWidth/height` = depth/mvec dims
4) Evaluate DLSS-G on the render thread with the same frame token / correct frame index.
5) Decide compositing responsibility:
   - Either let Streamline composite UI on generated frames (recommended if you provide UI buffers correctly),
   - or composite UI yourself after FG output (harder to get perfect for generated frames).

---

## Windows Build Constraints (Working From macOS)

Because the native plugin is Windows-only, you cannot validate the full path on macOS.
Recommended workflow:
1) Keep the native source in-repo (`native/NvidiaReflex/**`).
2) Build the DLLs on a Windows machine or a Windows CI runner.
3) Copy the DLLs into `Assets/Plugins/x86_64/` (or inject them into the build output as a post-build step).
4) Commit the `.meta` files for *all* shipped DLLs so Unity import settings stay correct (Windows x86_64 only, exclude Editor).

---

## “Next Concrete Steps” Checklist

1) Fix `Assets/Scripts/StreamlineDLSSPlugin.cs` P/Invoke signatures for:
   - `SLDLSS_GetOptimalSettings`
   - `SLDLSSG_GetState`
2) Update `Assets/Scripts/Rendering/DLSSUpscalePass.cs` to:
   - use URP internal render dimensions
   - **skip** evaluation when required textures/pointers are missing (avoid crashing)
3) Add a render-thread evaluation path using `IUnityGraphicsD3D12v7::CommandRecordingState` (native) + `IssuePluginEventAndData` (C#).
4) Implement HUD-less + UI buffer creation and tagging for Frame Gen.
5) Add native DLSS-G evaluation (`slEvaluateFeature(kFeatureDLSS_G, ...)`).

