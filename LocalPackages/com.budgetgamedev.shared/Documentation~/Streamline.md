# NVIDIA Streamline for URP and HDRP

The shared package uses **NVIDIA Streamline 2.12.0** for DLSS Super Resolution,
DLSS Frame Generation and Reflex in **Unity 6000.5.10f1 / URP and HDRP 17.5.0**.
The native backend targets **Windows x64 / Direct3D 12**. macOS, Linux, WebGL,
unsupported adapters and unavailable native features retain ordinary rendering.
Unity's separate NVIDIA module is no longer a package dependency.

Defaults remain **DLSS Quality / preset K**, up to **three generated frames**
(4x total, clamped to hardware support), and **Reflex On** without Boost.
Existing `Rendering.Streamline.*` preferences survive the migration. Shared APIs
now live in `BudgetGameDev.Shared.Rendering`; only pipeline capture adapters live
in `.Universal` and `.HighDefinition`.

**Validation boundary:** native cross-compilation and player script compilation
are useful integration checks, but do not establish GPU execution or image quality.
Windows RTX acceptance below is required before calling either pipeline production-ready.

## Prepare and build

Resolve the project's packages once, then run from the project root:

```powershell
python LocalPackages/com.budgetgamedev.shared/Tools~/Streamline/setup.py `
  --unity-plugin-api "C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data\PluginAPI"
```

The default prepares **both** pipelines. `--pipeline urp` supports a URP-only host;
HDRP builds of this shared package also need the URP framework fixes, so use the
default `both`. `--hdrp-source PATH` and `--urp-source PATH` accept resolved 17.5.0
sources, including the Editor's built-in packages. `--hooks-only` skips the native
build. The legacy `--hdrp-only` switch only installs the HDRP final-color hook.

On macOS, pass the Editor's `Unity.app/Contents/Resources/PluginAPI` directory and
install `mingw-w64` for the Windows cross-compile. `--sdk PATH` uses an extracted
SDK; setup still verifies the production payload hashes. Windows additionally
verifies NVIDIA Authenticode signatures.

Setup creates ignored embedded render pipeline packages and stages the bridge in
`Runtime/Plugins/Streamline`. The engine edits are hash-pinned and repeatable;
unknown source changes are rejected. URP's patch also fixes two obsolete API
references exposed by Unity's optional upscaler framework. Refresh/resolve packages
in Unity after setup. Do not edit the generated packages as the source of a fix.

Unity's optional `ENABLE_UPSCALER_FRAMEWORK` define changes serialized pipeline
fields, so **Editor and player must both compile with it**. Once hooks are imported,
shared Editor setup enables it for Standalone. Do not enable it only through
`BuildPlayerOptions.extraScriptingDefines`. Run setup (or `--hooks-only` for a
non-Windows development machine) before opening a fresh checkout with the define
enabled. macOS/Linux release staging carries the URP framework fixes when this
define is configured; their runtime still uses ordinary rendering. WebGL uses its
own build-target defines and does not require the framework.

The shared build callbacks cover **URP and HDRP**, validate capture hooks, native
import/preload settings, x64 PE files and pinned production payloads, and copy DLLs
and licenses beside the EXE. Both Windows pipelines use DX12. Release isolation
carries the selected pipeline hooks and native payload; HDRP staging also carries
the URP framework fixes needed by the shared package's URP dependency.

## Runtime integration

- **One shared SR backend:** `StreamlineUpscaler` implements Unity's `AbstractUpscaler`
  for both pipelines. It negotiates the Quality render resolution with
  `slDLSSGetOptimalSettings`, provides temporal jitter, normalized motion vectors,
  depth, camera constants and pre-exposed color, and dispatches `slEvaluateFeature`
  on Unity's recording command list. The native backend selects Streamline's
  `ePresetK` enum, not Unity's differently numbered preset enum.
- **Failure behavior:** output has a spatial fallback before native evaluation.
  Unsupported or failed native SR selects ordinary pipeline rendering. Unity's
  built-in DLSS names are excluded from the shared configuration.
- **Pipeline capture:** HDRP retains its pre-postprocess CustomPass and final-color
  hook. URP requests resolved depth/object motion independently of SR and uses
  version-pinned hooks before postprocessing and in both final compositor paths.
  The final compositor is replayed with UI compositing disabled into a separate
  full-resolution target. HDR uses the existing overlay texture; SDR renders the
  UI renderer list separately, including stencil masks, to obtain UI alpha.
- **UI:** overlay canvases stay separate when Streamline capture is installed,
  including URP's HDR path. Camera-space/world-space UI remains scene content.
  The F10 IMGUI panel suspends FG because it is outside the tagged overlay.
- **Tokens and lifetime:** one simulation token flows through Reflex, SR, capture,
  submission and real Present. Volatile RenderGraph resources are tagged
  `eOnlyValidNow`; native packets retain their COM resources until consumed.
  The bridge requests D3D12 read/UAV states through Unity's v8 plugin API.
- **Presentation and Reflex:** the existing early graphics-plugin preload,
  signature-checked interposer, UnityPlayer import adapter, proxy-swapchain hooks,
  PCL markers and low-latency sleep are shared by both pipelines. FG requires
  depth/motion/constants, HUD-less color and UI alpha for the same token. Resize,
  focus loss, missing inputs and unsupported configurations suspend FG.

The integration supports one fullscreen perspective Game camera on the primary
display. Camera stacks, multiple output cameras, orthographic cameras, render
textures, XR and split-screen are excluded. RenderGraph and URP's UniversalRenderer
are required; the 2D renderer is not an FG adapter. SR resets history after camera
changes/cuts, output resize, pipeline transitions and interrupted execution.

## Settings and diagnostics

**Settings > NVIDIA** uses the pipeline-independent `NvidiaRendering.IBackend`.
The shared F10 panel is also available. Reflex Off disables FG in the UI; a
programmatic FG request forces effective Reflex On. Defaults and saved preference
keys are unchanged.

SR telemetry reports adapter support, accepted options, evaluation results,
input/output dimensions and successful dispatch counts. Only recent successful
native dispatches produce an observed label; this is not GPU completion or an
image-quality measurement. FG still requires recent real-Present and extra-Present
evidence. Reflex reports accepted mode, sleep/marker results and driver latency
reports. NVIDIA App overrides and monitor scan-out are not measured.

Full logs remain under
`%LOCALAPPDATA%\BudgetGameDev\Streamline\<executable-name>`.

## Windows RTX acceptance

For **both URP and HDRP**, test an actual Windows player on compatible hardware:

1. Confirm the shared bridge ABI, SR support, successful SR dispatches and the
   actual proxy swapchain attachment. A DLL on disk or an enabled setting is not
   execution evidence.
2. Compare SR off/on at Quality/K and inspect depth/motion tags, camera cuts,
   animated geometry, fine detail, transparency, exposure and resize transitions.
   Verify native-resolution UI in SDR and HDR and check for double gamma encoding.
3. Test FG independently with SR on/off and with Reflex Off/On/On+Boost. Use the
   NVIDIA Reflex Test Utility, verify continuously updated frame IDs and measure
   GPU-presented FPS rather than Unity simulation FPS. Exercise 2x and MFG devices.
4. Test minimize/restore, focus, HDR changes, quality and scene changes, unsupported
   adapters/drivers, HAGS off, and shutdown. Check device loss and VRAM growth.
5. Measure the cost of final-compositor replay, UI capture, fallback blits and native
   texture pointer access. Production builds must retain the pinned production DLLs.

The macOS Editor cannot complete the project's Windows HDRP shader build because
its DXC/STP/ray-tracing shader toolchain is unavailable. Use the Windows Editor
for full player and RTX acceptance.

## References

- [Streamline DLSS Super Resolution guide](https://github.com/NVIDIA-RTX/Streamline/blob/v2.12.0/docs/ProgrammingGuideDLSS.md)
- [Streamline DLSS-G guide](https://github.com/NVIDIA-RTX/Streamline/blob/v2.12.0/docs/ProgrammingGuideDLSS_G.md)
- [Streamline Reflex guide](https://github.com/NVIDIA-RTX/Streamline/blob/v2.12.0/docs/ProgrammingGuideReflex.md)
- [Streamline device and DXGI integration](https://github.com/NVIDIA-RTX/Streamline/blob/v2.12.0/docs/ProgrammingGuide.md)
- [Unity graphics-plugin preloading](https://docs.unity3d.com/6000.5/Documentation/Manual/low-level-native-plugin-rendering-extensions.html)
