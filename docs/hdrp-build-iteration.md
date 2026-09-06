# Faster HDRP builds

The project uses Unity 6000.5.10f1 and HDRP 17.5.0. Windows HDRP builds use
Direct3D 12 and retain all four HDRP quality assets, including both ray-traced tiers.

## Shader variants

All four HDRP assets now disable features absent from the authored materials,
Shader Graphs, scenes and runtime code:

- Distortion vectors and transparent backface, depth-prepass and depth-postpass passes.
- Subsurface scattering.
- Decals and decal surface-gradient blending.
- Screen-space and data-driven HDRP lens flares.

The graphs do not request the disabled transparent passes or subsurface materials;
there are no HDRP decal projectors or data-driven lens flare components. BROcoli's
water uses its own shader, and torch/pickup glow uses additive shaders and bloom.
Re-enable a feature in the relevant HDRP assets when introducing content that needs it.

Deferred-only rendering, variant stripping and runtime-debug shader stripping were
already enabled. These remain enabled. Motion vectors, custom passes, HDR output,
volumetrics, screen-space effects and the authored ray-tracing modes remain available,
including the inputs required by Streamline. Unity's built-in HDRP stripper now
rejects the unused passes; no broad custom keyword blacklist is installed.

Per-shader variant logging is enabled in HDRP Global Settings. Release builds copy
`Temp/shader-stripping.json` and `Temp/compute-shader-stripping.json` beside
`unity-build.log`; the Windows nightly uploads them with the log. Compare the same
product, target, pipeline and quality configuration in before/after full builds.
Inspect `Compiling shader`, retained variants, cache hits and compilation duration.
A stripped-variant count is not a measured build-time speedup.

## Local C# iteration

On a Windows Editor, use **Tools > Build > Iteration**:

1. **Windows HDRP Full Development Player** creates a baseline in
   `build/iteration/windows-hdrp/BROcoli.exe`.
2. After C# method-body changes, use **Windows HDRP Scripts Only**.
3. After scene, material, shader, HDRP setting or serialized-field changes, run the
   full development build again. Keep `Library` and the player output between builds.

The second entry uses `BuildOptions.BuildScriptsOnly`. A receipt under
`Library/BuildIteration` checks that the output has a completed full build for the
same project, Unity version, pipeline, target, scenes, defines and build options.
Unity still validates serialization compatibility. The receipt does not detect
arbitrary content changes: scripts-only deliberately reuses the previous data.
The development flag provides debugging; it does not skip shader compilation.
Direct native build entry points also accept `-scriptsOnly` after a matching full build.

This loop runs in the source project, so it can include launcher and development
content. Ship through the isolated release workflow instead.

## Cached isolated releases

Fresh staging remains the default. Opt into a stable, isolated workspace with:

```powershell
.\scripts\native-builds.ps1 -Product brocoli -RenderPipeline hdrp -ReuseStage
```

```bash
./scripts/native-builds.sh --product brocoli --targets windows --pipeline hdrp --reuse-stage
```

`release-build.py` also accepts `--reuse-stage`. It keeps a workspace under
`build/release-staging/incremental-*`, partitioned by source path, product, pipeline,
target selection, development mode and Unity version. Each invocation stages the
allowlisted inputs, removes stale inputs, and preserves identical files' timestamps.
`Library` stays in that workspace; it is never copied from the source Editor.
The source Editor can remain open. A lease prevents concurrent reuse, and a workspace
with an open Unity Editor is rejected. After a hard process crash, verify no build is
running before removing an abandoned sibling `.lock` file.

Player output and release artifacts remain fresh. The full build still runs content
validation and the excluded-code audit; `--reuse-stage` never forces scripts-only.
The Windows nightly uses this mode and caches its workspace Library between runs.
Normal builds do not request `CleanBuildCache` or invoke `unity-clean`.
Build callbacks also avoid repeating identical pipeline/quality configuration within
one build, avoiding redundant pipeline-dependent imports.

The first build and changed shader combinations still need compilation. No Windows
shader build-time comparison has been measured on this macOS development machine;
validate the savings using consecutive Windows builds and the retained reports.

## Unity references

- [Scripts-only builds and serialization restrictions](https://docs.unity.cn/ja/current/Manual/build-scripts-only.html)
- [Shader variant stripping](https://docs.unity3d.com/cn/current/Manual/shader-variant-stripping.html)
- [Build cache locations](https://docs.unity3d.com/ja/current/Manual/build-cache-location-reference.html)
