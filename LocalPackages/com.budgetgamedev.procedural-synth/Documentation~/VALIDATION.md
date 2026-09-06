# Verification record — 2026-09-06

This record preserves the original host-project measurements before package extraction. Historical scene paths and BROcoli mixer names identify that tested setup; they are not dependencies of the portable package. Packaging/build verification is reported separately and does not constitute fresh listening or target-platform validation.

Implementation evidence is distinct from listening acceptance. No human listening judgment is claimed. Play the scene and compare the supplied WAVs before accepting the defining sound.

## Environment

Apple M4 (10 cores),16GiB RAM, macOS26.6.2. Unity6000.5.10f1 arm64 Editor; Burst1.8.30 enabled, safety checks enabled, default floating-point mode. Native kernel benchmark uses the same SynthEngine code as the SAP adapter via a synchronously compiled function pointer; a BurstDiscard marker verifies that native code actually ran. Sample rate48000Hz, block1024, final quality4x:31-tap intermediate halfband plus127-tap output lowpass. First-order DC blocker8Hz. FIR adds34.5 output samples (0.719ms at48kHz), excluding device buffers and scheduling.

## Integration proof and lifecycle

Saved runnable `Assets/ProceduralSynth/Demo/MonoSynthDemo.unity`. Actual AudioSource and listener measured nonzero110Hz sine output. Routed through BrocoliAudioMixer/Ambience: listener RMS0.0295167 at0dB and0.00299773 at−20dB (about−19.87dB measured, short asynchronous windows). Original editor audio master mute explained the initial silent listener. Stop/start recovered with nonzero signal.

AudioSettings.Reset initially revealed source termination without recovery. Fixed main-thread configuration notification/restart ownership. Retest at512DSP frames showed Ready=true, playing=true, peak0.0799994;1024 restored. The device retained48000Hz when44100 was requested, so **live44100 was not verified**. DSP tests independently cover32/44.1/48/96kHz; filter tests additionally cover internal oversampled rates to384kHz. Other channel layouts are delegated to Unity mono-source routing; stereo verified, multichannel layouts and spatializer behavior not auditioned.

Adaptive demo ran with danger.85/proximity.6/health.25/movement.7/weather.2. Actual targets converged to cutoff950.0455Hz, pre-drive8.42, wavetable.75, noise.03; nonzero output, no dropped events. Editor eval/screenshot stalls triggered documented late-event clamping and scheduling recovery (2 late events/2 recoveries in that instrument run); these are not represented as sample-exact live scheduling success. Core dispatch tests verify sample offsets exactly. Human input timing includes roughly one DSP buffer plus video/pipe acknowledgement delay. Automated sequence uses150–300ms lookahead.

## Automated checks

39/39 Unity Editor tests passed. `Artifacts/Synth/unity-tests.json` retains the detailed result. Coverage includes sample-exact dispatch and stable simultaneous ordering; late/overflow policy; all note priorities, overlap, duplicate keys, inactive release, legato and glide; seeded repeatability; finite/protected extremes; zero allocation; nonlinear response/DC reset; table interpolation/mip boundaries; FIR impulse latency/unity gain; interrupted waveform transitions; composer capacity1 equivalence, rests/accents/scale, hysteresis, tempo and phrase boundaries.

Independent worker validation ran709,336 voice assertions and8million nonlinear stress samples against actual C# and the installed Unity mathematics module. Maximum scalar feedback-solver residual2.533e−7 in1million cases. Small-signal filter response fell23.61 and24.10dB per octave across successive test octaves. These checks supplement, not replace, the checked-in Unity tests.

The first profiler helper incorrectly used an accumulating recorder count as a physical buffer index and raised IndexOutOfRangeException. Fixed by using LastValue, explicit wrap policy, a600-update bounded capture and automatic cleanup on stop/reload. Regression test covers160 iterations with an8-slot recorder; actual final live capture completed600 updates and stopped without errors. This issue was editor-only, but invalidated the earlier live profile; final `live-profile.json` replaces it.

## Cost and real-time safety

Final native Burst metallic kernel: p50 **0.4311ms**, p95 **0.4616ms**, maximum **0.4837ms**, per1024 frames at48kHz. Buffer budget21.333ms; p95 **2.16%**.200 measured blocks after32 warmup blocks. **0 managed bytes** allocated during rendering. Initialization, compilation, reporting and buffers are outside the measurement. Standalone .NET10 Release final p95.5449ms,0allocated bytes; different host and warmup, not a Unity callback result.

The kernel benchmark excludes SAP pipe handling, callback scheduling, mixer and routing. The final live Audio.Thread recorder reported3.287ms p95 aggregate per editor update; it is **not per-buffer DSP timing**. The discovered synth-specific marker supplied no usable timings; its zero is explicitly marked unavailable. Callback code review verifies fixed bounded packets/loops and inline state, no native allocation calls, managed allocation, locks, I/O, logging or scene access. Native allocation behavior is established by structure/API contracts; no per-callback native allocator instrumentation was available.

## Spectral evidence

[Packaged quality tools](../Tools~/SynthQuality/README.md) and the original repository's `Artifacts/Synth/Quality` contain reproducible actual-core fixtures, metadata and12RMS-matchedWAVs. Matched8000Hz cutoff,880Hz carrier,440Hz PM source, no drift/noise, settled envelopes.48kHz1x versus4x and a96kHz4x reference properly resampled offline. Below20kHz off-harmonic spectral energy:

| Fixture | 1x | Final4x | Higher-rate reference |
|---|---:|---:|---:|
| Cleaner saw | −49.85dBc | −66.31dBc | −72.19dBc |
| Driven sine | −41.81 | −88.34 | −84.10 |
| Driven sinePM | −31.77 | −83.81 | −82.02 |
| Driven wavetablePM | −31.76 | −54.25 | −58.61 |

The longer final decimator was justified by excessive near-Nyquist residual in the first31-tap version. An early14kHz fixture also changed actual1x cutoff because of the filter safety clamp; final paired results use8000Hz consistently. Energy measurement includes aliasing plus numerical/leakage error and misses aliases landing on legitimate harmonic bins. It does not establish perceptual transparency. Wavetable PM and rich basic-wave PM retain measurable aliasing. No claim of alias-free extreme modulation.

## Audio review artifacts and remaining gates

`Artifacts/Synth/heavy-bass.wav` and `clean-bass-matched.wav` share an RMS target. Final standalone equivalents and acid/metallic/adaptive renders are in `Artifacts/Synth/Standalone`; quality pairs in `Quality`. Root Editor-generated renders are regenerated after final changes. RMS matching is a repeatable comparison aid, not equal perceived loudness or a listening verdict.

Milestones0–3 have implementation and technical demonstration evidence. Sound-quality acceptance remains open for human audition. Actual game-state hookups, Windows/Linux runtime qualification and multichannel/spatializer qualification remain unverified. Web is unsupported by Unity SAP and explicitly disabled. Milestone4 effects/polyphony/unison and broader modular routes remain deferred to listening and measured priorities.

## Package migration verification

The standalone projects now live at `Tools~/SynthOffline` and `Tools~/SynthQuality` inside `com.budgetgamedev.procedural-synth` version 0.1.0. Both moved projects built successfully in .NET 10 Release against the installed Unity 6000.5.10f1 `UnityEngine.MathematicsModule.dll`, with zero compiler warnings or errors. Their relative source includes resolve to the package's `Runtime` tree. The wavetable generator resolves its destination relative to its own package location; it was not executed during migration, so runtime table data was not regenerated. Output defaults remain relative to the invoking working directory under `Artifacts/Synth`.

After migration, the manager reran the Unity Editor test assembly: **39/39 passed**. Package sample import succeeded with **zero missing components** and script dependencies confined to the package. The imported sample entered Play with `Ready=true`, peak **0.3521911**, **48000 Hz**, and **0 dropped events**. These are Editor integration checks; they do not establish a fresh standalone player build or human listening approval. The existing host demo retains its historical mixer assignment; the importable package sample and newly generated demos default to direct AudioListener routing.
