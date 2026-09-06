# Unity 6.5 upgrade — 2026-09-06

The project moves from Unity **6000.3.6f1** to the installed **6000.5.10f1**
(revision **3bd4f66ad299**). `ProjectSettings/ProjectVersion.txt` remains the
editor version source for CI, bootstrap, and build scripts. Linux Mono support
was installed alongside the existing WebGL and Windows Mono modules.

Package manifests and Unity's resolved lockfile now use URP/HDRP **17.5.0**,
Unity UI **2.5.0**, Input System **1.20.0**, AI Navigation **2.0.14**, and Test
Framework **1.7.0**. Dependent render packages and editor-required dependencies
were resolved together. The game, shared, hub, and BROcoli autoplay packages
declare Unity **6000.5** as their minimum. Credits list the resolved versions.

Object searches use the unsorted `FindObjectsByType` overloads and
`FindAnyObjectByType` for singleton lookups. Occlusion registries, selectors,
and state machines preserve full `EntityId` keys from `GetEntityId()`, including
their ordering. Only integer random seeds and animation phases use
`EntityId.GetHashCode()`. Mathematical test fixtures explicitly round-trip
synthetic IDs through `FromULong`/`ToULong`; these values never identify scene
objects. No obsolete-API warnings were suppressed.

The Editor migrated serialized rendering globals and graphics/player/VFX
settings, and generated Physics Core 2D and Project Auditor settings. Invalid
scene navigation references now point to Selectable components. Test fixtures
were updated for input lifecycle, active physics bodies, pipeline isolation,
object-prefixed diagnostics, and the existing five-layer torch effect.
Authored quality settings, timestep, and material queues are preserved by the
upgrade; transient pipeline-test normalization is excluded from the migration.
The preexisting `BROcoli.slnx` changes were preserved byte for byte.

## Verification

- Passed: **51 Python tests**, CSharpier check of **736 C# files**, the
  **300-line source limit**, no-emoji check, and `git diff --check`.
- **EditMode: 1,138 passed, zero failed or skipped.** Results:
  `build/verification/unity65-editmode.xml`; editor log beside it.
- All **22 direct package pins** and local dependency snapshots agree with the
  **46-package lockfile**.
- **WebGL: not completed.** Both build attempts were canceled during shader
  compilation. Evidence: `build/verification/unity65-webgl-validation-status.txt`
  and adjacent logs.

The initial cold WebGL mesh-optimization pass queued **27,648 shader variants**;
its estimated **1–2 hours** prompted cancellation. A temporary staged attempt
disabled `PlayerSettings.stripUnusedMeshComponents` and
`URPTerrainShaderSetting.includeTerrainShaders`, preserving production settings.
Despite the serialized terrain setting being false, `WavingDoublePass` retained
**13,824 ForwardLit variants**, and the bounded validation attempt was canceled.
No WebGL player, release content audit, desktop browser smoke, or iOS-profile
smoke completed. Player compilation, optimized size, performance, and terrain
shader coverage remain unverified.

The full Editor suite was run with:

```sh
unity test . --mode EditMode --timeout 3600 \
  --output build/verification/unity65-editmode.xml -- \
  -buildTarget StandaloneOSX \
  -logFile build/verification/unity65-editmode.log
```

Native player builds and real-device Safari testing were not run.
