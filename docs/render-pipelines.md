# Render pipelines

BROcoli renders through two pipelines. Universal drives the web build, which is the one
most players see. High Definition drives the Windows build, which is where the ray
traced tiers live. They are two front ends over one game, not two games.

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
nothing in a project without the package.

## Scenes

A level is authored as three scenes:

```
Brocoli_Dungeon_Common   game content: geometry, gameplay, navigation, audio, interface
Brocoli_Dungeon_URP      rendering data only: volumes, probes, light settings
Brocoli_Dungeon_HDRP     the same, in High Definition's terms
```

`RenderingSceneLoader` sits in the common scene and additively loads whichever
rendering scene matches the running pipeline. Only two of the three are ever loaded
together, so neither pipeline can see the other's data.

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

## Luminance

The dungeon is authored against a fixed ladder, in nits, written down in
`SceneLuminanceBudget.Dungeon`:

| what | nits |
| --- | --- |
| pitch-black recesses | ~0.05 |
| distant cobblestones | ~1.75 |
| shadow side of near objects | ~6 |
| torch-lit stone | ~30 |
| ordinary bright diffuse surface | ~85 |
| flame body | ~300 |
| hottest core, specular, sparks | ~800 |

Two things on that ladder are deliberately separate and must stay separate:

- **The flame's emissive material** decides how bright the fire looks. It is the
  `BROcoli/Flame` graph, driven in HDR.
- **The torch's point light** decides how much light reaches the cobblestones. It is a
  `PunctualLightSpec`, converted to lumens on High Definition and to graded units on
  Universal.

Neither is derived from the other. Pushing the flame makes the fire hotter without
washing out the floor; raising the light brightens the room without turning the fire
into a white blob. Wiring one to the other would collapse that, and is the most likely
way for this to be broken by accident.

High Definition meters the scene itself, so every tier's volume profile pins exposure to
Fixed. An automatic exposure would chase the torches and undo the ladder.

That fixed exposure is **EV100 12.5**, and it is measured against the Universal build
rather than derived from the ladder. `SceneLuminanceBudget.Ev100For` computes what the
ladder implies -- 9.4 -- and the two disagree by about three stops, because the dungeon's
lights were authored in Universal's arbitrary units and then converted to preserve that
look rather than to sit on the ladder. Both numbers are kept, and the gap is the honest
size of the remaining tuning job: bringing the lights onto the ladder should pull the
exposure down towards 9.4. Until then, matching the two pipelines beats matching the
theory.

Both pipelines tone map through the ACES 1000 nit preset, which is what
`AcesToneScale.SelectPreset` returns for the calibrated peak the game ships with.

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

No platform default points at them yet. Switching Windows over is one deliberate change
-- `Standalone` in `m_PerPlatformDefaultQuality`, or a build profile that pins the
quality level -- and it is left undone on purpose: the editor's active build target is
Windows, so changing it switches the editor itself to High Definition, and that wants
someone looking at the result rather than a commit doing it quietly. Until then the
tiers exist, are selectable, and are what a graphics menu would offer.

## The web build

The web build renders through Universal and must stay cheap. Adding High Definition to
the project did not change it: `m_CustomRenderPipeline` in the graphics settings still
points at `3dRenderer`, and High Definition merely registered its global settings
alongside Universal's. Anything that would make the web build pay for the Windows one is
a bug.
