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

That fixed exposure is **EV100 10.9**, and it is measured against the Universal build
rather than derived from the ladder. The measurement is worth repeating whenever the
dungeon's lighting moves: render the same seed from the same camera on both pipelines
with the tone map turned off, and compare the scene-linear picture the grade is handed.
At 10.9 the two agree within 0.03 stop from the fifth percentile to the brightest flame
core, and that core lands at four times paper white -- 800 nits against 200 -- which is
where the ladder puts it.

Measure it that way rather than by eye or by screenshot. Both pipelines resolve their
post stack through a target the Editor clamps, so a picture of the game says nothing
about the range above paper white, which is the whole of what HDR adds. It was exactly
that range that a wrong exposure erased: at EV100 12.5, three times too dark, nothing in
the dungeon reached paper white at all and the flames topped out at a quarter of the
peak they are authored for.

`SceneLuminanceBudget.Ev100For` computes what the ladder implies -- 9.4 -- and that is
still a stop and a half below the measured number, because the dungeon's lights were
authored in Universal's arbitrary units and then converted to preserve that look rather
than to sit on the ladder. Both numbers are kept, and the gap is the honest size of the
remaining tuning job: bringing the lights onto the ladder should pull the exposure the
rest of the way down to 9.4. Until then, matching the two pipelines beats matching the
theory.

Both pipelines tone map through the ACES 1000 nit preset, which is what
`AcesToneScale.SelectPreset` returns for the calibrated peak the game ships with.

## Bloom, and why only one pipeline has it

Universal's bloom is additive: it adds the thresholded, blurred image on top of the
picture, so the torches gain a halo and the flame cores themselves get brighter.
Measured on a torch, its authored settings put about 8% more light in the ring around
the flames and 6% more on the flame pixels.

High Definition's bloom is a veiling glare -- a mix between the picture and its blurred
self, with intensity as the mix weight. It can only move light around, never add any,
and around a small bright source it moves light *off* it. Measured on the same torch on
the same frame, every intensity took light away and put almost none back: at 0.2 the
flame cores lost 6% and the ring 3%; at 0.7 the cores lost 24%, the ring 12%, and the
brightest pixel in the frame lost 46%. Scatter, threshold and pyramid resolution changed
none of that.

So the High Definition profiles carry no bloom override. Adding one costs exactly the
peak the HDR grade exists to reach, and returns a halo too small to measure. If the
dungeon's glow is to be matched on both pipelines, it has to come from something that
adds light -- brighter flame emissive, or a bloom written for the purpose -- not from
turning this one up.

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
