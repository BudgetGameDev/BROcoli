# Render pipelines

BROcoli renders through two pipelines. Universal is the default for web and native
builds. High Definition is an explicit native build option, where the ray traced
tiers live. They are two front ends over one game, not two games.

That distinction is the whole design. Gameplay code never asks which pipeline is
running, never spells a pipeline's shader name, and never sets a light's intensity in a
pipeline's own units. Everything that differs is pushed to one of three places: the
front-end assemblies, the per-pipeline rendering scenes, and the shader graphs' second
target. If you find yourself adding `#if` or a pipeline check anywhere else, the thing
you are reaching for belongs in one of those three.

## The seam

`BudgetGameDev.Shared.Rendering` holds the pipeline-agnostic side:

- `RenderPipelineProbe` names the active pipeline from the render pipeline asset's own
  type, so the shared runtime references neither pipeline's assembly.
- `RenderPipelineFrontEnd` finds the front end whose pipeline is actually rendering.
- `HdrGradeRequest` states the HDR grade in display terms -- ACES preset, paper white,
  brightness limits, the toe -- rather than in either pipeline's volume overrides.
- `PunctualLightSpec` states a light by what it does: how bright it makes a reference
  surface at a reference distance.

Two assemblies implement it, `…Rendering.Universal` and `…Rendering.HighDefinition`.
The High Definition one is gated behind a `BROCOLI_HDRP` version define, so it costs
nothing in a project without the package. URP player builds also exclude this assembly
with `BROCOLI_URP_PLAYER`, supplied through the build options before compilation.

## Scenes

A level is authored as three scenes:

```
Brocoli_Dungeon_Common   game content: geometry, gameplay, navigation, audio, interface
Brocoli_Dungeon_URP      rendering data only: volumes, probes, light settings
Brocoli_Dungeon_HDRP     the same, in High Definition's terms
```

`RenderingSceneLoader` sits in the common scene and additively loads whichever
rendering scene matches the running pipeline. Only two of the three are ever loaded
together. Player builds filter out the unused rendering scenes before calling
`BuildPipeline.BuildPlayer`, so their dependencies are not packaged.

Nothing that affects gameplay may live in a `_URP` or `_HDRP` scene. If a change to one
of them can alter what the player can do rather than what they see, it is in the wrong
scene.

## Shaders

Ordinary materials use BROcoli's own dual-target Shader Graphs, in
`Resources/Brocoli/Shaders/`. Each graph carries a Universal subshader and a High
Definition one, so a surface is authored once. `BrocoliShaders` names them; nothing else
in the game spells a shader name.

Two shaders stay hand written, and should:

- `DungeonOcclusionFade.shader` has a shadow pass that deliberately ignores the fade, so
  a wall hidden from the camera still casts its whole shadow. A graph cannot express
  "transparent in the forward pass, opaque in the shadow pass".
- `XpEnergyGlow.shader` already carries subshaders for both pipelines, and its falloff
  and band maths are tuned by hand.

`Surface` and `WaterVolume` are marked for ray tracing. A material without that flag is
absent from High Definition's acceleration structure entirely: no traced shadow, no
appearance in a traced reflection, no contribution to traced global illumination. Set it
on anything opaque that the player should see reflected. Leave it off transparent
effects, where tracing costs more than it returns.

## Lighting reference and units

The lighting reference is the URP setup at `6fb6c401baa0c4baa05a5231817bf71b37a20c40`.
Its compact flame highlights target **1.3 times the calibrated display peak** (30% above,
not 130% above). This is a highlight target, not a target for every diffuse surface.
The point lights, flame texture and additive bloom together create the bright cores,
colored glow and dark surroundings; ACES shapes their shoulder and toe.

Keep these two controls separate:

- The flame material controls the visible fire, including the calibrated HDR highlight.
- The point light controls illumination reaching the room and player.

`PunctualLightSpec` describes incident illumination through a reference Lambertian
surface. `SceneLuminanceBudget.AuthoringPaperWhiteNits` is fixed at **200** for converting
these authored lights. Changing display paper white must not also change the light's
physical intensity. The historical torch intensity is 7.5 in URP; its specification is
67.5 reference nits on an 18% surface at two meters, or 4712.389 candela.

URP's diffuse BRDF omits the `1/pi` normalization that HDRP includes. The front ends
therefore convert the same specification as follows:

```
URP intensity = referenceNits * distance² / (reflectance * 200)
HDRP candela  = pi * referenceNits * distance² / reflectance
```

In Unity 6 HDRP, `Light.intensity` for a point light stores **candela**. Setting
`Light.lightUnit` to Lumen only changes the authoring/display unit; it does not make a
subsequent raw intensity assignment a lumen conversion. The earlier adapter wrote
lumens there, introducing a factor of `4*pi` before exposure. `HDAdditionalLightData`
is now added before assigning the final intensity so initialization cannot overwrite it.

All HDRP tier, default and game profiles use fixed **EV100 7.380822**, with zero exposure
compensation. The project's imperfect-lens setting gives an exposure multiplier of
`1 / (1.2 * 2^EV)`, so this exposure equals `1/200`. Combined with the BRDF normalization,
it puts the corrected lights in the same scene-linear range as URP. Automatic exposure
would chase the fire and change the authored look, so it remains disabled.

The previous 10.9/13.2 exposure values compensated for incorrect units and are no longer
valid references. Similar lighting does not imply identical pixels: URP and HDRP have
different diffuse/specular models, shadow filtering and indirect-lighting features.

The Flame graph uses the original texture RGB multiplied by particle color and material
color. It no longer recolors the texture through a red-channel ramp. In HDRP its unlit
BaseColor carries that same authored scene-linear signal; wiring it to Emission as well
would add a second, exposure-dependent contribution. HDR highlight presentation changes
the compact bright particles and preserves their authored fade when calibration changes.

## HDR grade and additive bloom

Native HDR retains the reference contrast **+17**, saturation **+12**, and compact
highlight target **peak * 1.3**. The subsequent near-black correction of -0.0008 remains.
At the default calibration of 600-nit peak / 200-nit paper white, the ACES 1000 preset
still solves for a 780-nit highlight. Preset selection also checks paper white and the
reachable grading range: it promotes the preset when necessary to retain that overshoot.
An extreme calibration can exceed the available LUT range even with the 4000 preset;
for example, 2000/80 cannot achieve the whole 2600-nit target. Do not compensate for that
by flattening the rest of the lighting.

The runtime grade follows pipeline changes and disposes the old pipeline's volume and
profile components. This matters when comparing URP and HDRP in one Editor session.

Universal keeps its historical bloom: gamma-space threshold **0.85**, intensity **1.35**,
scatter **0.72**, high-quality Gaussian/bicubic filtering, at most six half-resolution
pyramid levels. HDRP's `ImpressionistBloom` implements the same additive operations in
half-float buffers before the color grade and ACES. Its source signal is preserved and
the thresholded halo is added to it; values above 1 remain available to the tone mapper.

Native HDRP bloom redistributes highlight energy (`source - thresholded source + blur`)
and cannot reproduce that additive operation. It is explicitly zeroed in the default
and game profiles. Merely omitting a scene override was insufficient because the default
profile still enabled intensity 0.2. The custom effect is registered in HDRP Global
Settings under **Before Post Process**, and enabled in both game rendering profiles.
Its shader is retained in Resources for player builds.

## Validate HDR without an 8-bit screenshot

Use **Tools > BROcoli > Rendering > Log HDR Lighting Diagnostics** in a running game.
It records the actual output availability, activity, graphics format, gamut, display
limits, calibration, camera target format and resolved volume values in the Console
and `Temp/BrocoliHdrLighting.txt`. A reported HDR render buffer does not by itself mean
that the monitor is receiving native HDR.

**Tools > BROcoli > Rendering > Show Values Above Paper White** opens Unity's Rendering
Debugger and enables its HDR luminance overlay. On an HDR-capable Windows Editor/player,
verify native HDR is active, then inspect HDRP Rendering > HDR Output > DebugMode
(or URP Lighting > HDR Debug Mode) > Values Above Paper White and the HDRP exposure
histogram/color picker. HDRP advises using the play-mode debug UI (Ctrl+Backspace) for
accurate HDR display readings. Use Hide HDR Debug Overlay afterward when judging the artwork. See [Unity's Rendering Debugger documentation](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/manual/Render-Pipeline-Debug.html).

For numerical comparisons, render the same reference surface or deterministic dungeon
view into floating-point targets, disable tone mapping only for the scene-linear
comparison, and read back RGBAFloat values. Re-enable the production grade/bloom to test
the output transform. The bloom GPU regression tests verify a signal of 8 remains above
1, bright cores retain energy, halos reach nearby dark pixels, and resized targets do
not sample stale bright backing pixels. ACES tests exercise the highlight target over
multiple peak/paper-white calibrations.

Do not use an 8-bit PNG or a model's screenshot preview to calibrate native HDR nits.
A float scene-linear capture proves the input range; a native HDR display/debug capture
is still needed to verify the display transform, panel clipping and final appearance.

## Remaining atmosphere difference

The reference URP scene uses Exp2 fog at density 0.016; the HDRP dungeon's native Fog
component remains disabled. At the authored camera's visible floor depths, URP fog
contributes about 3–14% (about 6% at the center). This is a remaining visual difference,
not covered by the direct-light parity assertion. The Surface graph has no replacement
distance-fog function.

HDRP's saved mean free path 60 uses a different distance/height curve and would be much
stronger. A uniform native approximation around 259 m matches the center but differs at
the edges. Exact parity would also need to preserve final-lighting fog on transparent
occlusion walls, water and lit/unlit particles; an opaque-only post-process plus a flame
adjustment is insufficient. The lighting restoration does not claim to solve that
separate atmosphere mismatch or to certify final 10-bit display appearance.

## Verification recorded on 2026-09-05

Unity 6000.3.6f1 on Apple M4 / Metal compiled the changes without errors.
The six HDRP GPU/profile checks and seven flame-presentation checks passed. The paired
18%-linear reference surface agrees within 5% between pipelines, and both preserve an
unlit `(8, 4, 1)` signal in float readback. The broader shared suite passed 274/276;
`ForceLandscapeAspectLoggingTests.ASceneLoadNamesTheSceneItIsCorrecting` and
`VirtualControllerTests.EnhancedTouchInputIsForwardedToTheJoystickProcessor` still fail
on this Editor's portrait-layout/touch-input expectations.

A live dungeon camera resolved fixed EV100 7.380822, ACES, active additive bloom 1.35,
native bloom 0, and enabled Postprocess / CustomPostProcess / ExposureControl frame
settings. The HDR debug selector was exercised and read back as Values Above Paper
White, then reset. Native HDR availability and activity were both false on this host,
so these checks establish scene-linear behavior and configuration, not final panel nits
or a visually certified match on a 10-bit monitor. No 8-bit screenshot was used to tune
luminance. The Editor was returned to GameLauncher / Ultra (URP).

Changed C# files pass the pinned formatter and stay within 300 lines. The repository-wide
source-size gate still reports unrelated oversized files elsewhere in the existing tree.

## Windows tiers

Four quality tiers, in `Assets/Settings/Rendering/HDRP/`. Each is an HDRP asset paired
with a volume profile: the asset decides what the pipeline is *built* with, the profile
decides how each term is *solved*.

| tier | GI | reflections | shadows | AO |
| --- | --- | --- | --- | --- |
| Medium | APV / baked | probes | raster | SSAO |
| High | SSGI / APV | SSR | raster | SSAO |
| RT High | RTGI | RT reflections | selective RT | RTAO |
| RT Ultra | RTGI | RT reflections | RT shadows | RTAO |

Adaptive probe volumes are on at every tier. They carry the bounce light in the parts of
the dungeon that a screen space or traced effect cannot see, and the higher tiers add to
them rather than replace them.

Ray traced shadows are screen space shadows underneath, and each light that casts one
takes a slot. RT High gets four, so the torches nearest the player trace and the rest
stay on the shadow map -- that is what "selective" means. RT Ultra gets eight, enough for
a lit room.

Ray tracing needs DX12 and a GPU that supports it. The Medium and High tiers do not, and
are the fallback when it is missing.

### Selecting one

The four appear as quality levels named `HDRP Medium` through `HDRP RT Ultra`, each
carrying its pipeline asset. They are excluded from WebGL, Android, iOS and tvOS, so the
web build cannot land on one even by accident.

The player starts on `HDRP High`, which is what `m_CurrentQuality` in the quality
settings names.

### One pipeline per player

A player carries one pipeline, and which one is decided per build target rather than by
whatever the project last had selected. `NativePlayerBuildScript.ConfigureTarget` points
Graphics Settings at the pipeline the target ships with -- High Definition for the
Windows player, Universal for everything else, the web build included -- and puts the
authored value back when the build is done, so building leaves the project as it found
it.

That is not a preference. High Definition refuses to build a target whose quality levels
and Graphics Settings name different pipelines, and the six levels from `Very Low` to
`Ultra` carry no pipeline of their own, so they follow the graphics default. With the
default on Universal, a Windows build mixes six Universal levels with four High
Definition ones and fails before it compiles a shader:

```
BuildFailedException: The current build target has assets in its associated Quality
levels and Graphics Settings that belong to different render pipelines.
```

One caveat is still open. Unity's per-quality platform exclusions work on the build
target *group*, and Windows, macOS and Linux share `Standalone`, so the Windows tiers
cannot be hidden from the other two desktop players. A macOS or Linux build therefore
still mixes pipelines and still fails; separating them needs per-profile quality
overrides, not exclusions.

## The web build

The web build renders through Universal and must stay cheap. Adding High Definition to
the project did not change it: `m_CustomRenderPipeline` in the graphics settings still
points at `3dRenderer`, and High Definition merely registered its global settings
alongside Universal's. Anything that would make the web build pay for the Windows one is
a bug.
