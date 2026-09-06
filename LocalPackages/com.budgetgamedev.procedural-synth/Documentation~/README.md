# Procedural Synth manual

A native Unity 6.5 mono instrument with three independent oscillators, sub/noise, nonlinear resonant lowpass, driven VCA path, phase modulation and wavetable morphing. Includes an isolated audition scene and a seeded mono composer. This is an implemented first instrument, not a claim of Vital-equivalent sound quality. Human listening approval remains open.

## Play

Install `com.budgetgamedev.procedural-synth` version `0.1.0` in Unity 6.5 (minimum 6000.5.10f1), then import its sample from Package Manager. Open `Assets/Samples/Procedural Synth/0.1.0/MonoSynth/MonoSynthDemo.unity` and enter Play. The source sample lives at `Samples~/MonoSynth/MonoSynthDemo.unity` inside the package; import it before editing. The `Tools > Brocoli Synth > Create isolated demo scene` command creates a writable scene at `Assets/ProceduralSynthDemo/MonoSynthDemo.unity`. The existing host-project copy at that path retains its original mixer; freshly created demos and the packaged sample use direct listener output. Turn off Unity's Game View audio mute to hear output. The portable demo routes its mono AudioSource directly to the AudioListener by default. Assign an AudioMixerGroup on the AudioSource to use your own mixer; no BROcoli mixer or game package is required.

- The bass sequence starts automatically. Stop it to audition `A W S E D F T G Y H U J K` (C2–C3); a manual note also stops the fixed sequence. Keyboard requires Game View focus.
- Select Heavy bass, Cleaner, Acid or Metallic; edit cutoff, drive, resonance, output, glide, waveform/morph and audio-rate depths. All oscillator tuning and envelope settings are editable on the generator component.
- Panic clears queued notes and releases the voice. Restart audio recreates the generator.
- Enable adaptive composition in the right panel to give the composer exclusive control. Its normalized game-state sliders demonstrate mappings; `SynthAdaptation.SetGameState(GameMusicState)` is the game integration entry point. Gameplay systems must supply this state explicitly; the package does not discover game objects automatically.
- The 110Hz sine switch is an integration diagnostic.

Supported DSP rates: 32, 44.1, 48, 96kHz and intermediate rates in32–96kHz. The native adapter reports mono and follows the host rate. The host handles stereo/surround routing. Web builds explicitly disable the generator because Unity SAP does not support Web. macOS is tested here; Windows/Linux runtime audition remains unverified.

## Architecture and musical semantics

Control/composition produces absolute **engine-local sample frames**. Unity's adapter sends acknowledged fixed-size value packets through verified SAP pipes. The audio owner processes a bounded, stable-sorted event queue before each sample. DSP is unmanaged state; rendering performs no heap allocation, scene access, logging, locks or I/O. Static wavetable data is initialized before rendering. The AudioSource owns generator lifetime; configuration changes recreate/reset state and reschedule the control timeline.

Duplicate note-ons replace a held key; one matching off releases that key. Last/low/high priority are explicit. Releasing an inactive key does not retrigger. Legato keeps envelopes; nonlegato retriggers from current level. Glide is exponential in semitones; the configured duration reaches99% of the pitch change. Phases run continuously even while the VCA is closed. Reset-phase acts on envelope retrigger and resets oscillators only; it does not clear filter memory. Sub follows the gliding played note minus12 semitones, independent of oscillator tuning. Seeded oscillator drift is independently bounded, default±2cents.

The four-pole filter uses trapezoidal one-poles with a saturating global feedback junction and a bounded implicit Newton solve. Stages are linear; it is a documented ladder-inspired approximation, not a transistor circuit simulation. There is no hidden sample delay in its feedback. High resonance reduces bass/DC gain; no loudness compensation conceals this behavior. Signal path: summed oscillators/sub/noise → pre-drive gain into nonlinear filter → post saturation → amp envelope/velocity → filtered downsampling →8Hz DC removal → peak protection.

PM is phase modulation in **cycles**, not frequency modulation. Oscillators2 and3 are evaluated first; oscillator2 offsets oscillator1 phase, oscillator3 offsets cutoff in **octaves**, and the filter envelope adds PM depth. Sum PM depth is limited to0.5cycle. Cutoff is evaluated every internal sample and clamped. There are no circular modulation routes. Panel target controls smooth over5ms; game expression adds200ms smoothing. Bandlimited basic sources and mipmapped tables do not eliminate distortion/modulation aliasing; see measurements.

## Verification and reproducibility

[Validation record](VALIDATION.md), [API/repository audit](AUDIT.md), [shared contracts](CONTRACTS.md), [composition details](COMPOSITION.md).

Run the `BudgetGameDev.Synth.Tests` Editor test assembly. Menu `Tools > Brocoli Synth > Benchmark Burst DSP kernel` writes its measured report. `Tools > Brocoli Synth > Render validation audio` renders audition WAVs. Outputs are under `Artifacts/Synth`.

For DSP rendering without a Unity Editor process, run from the repository root:

```sh
dotnet run --project LocalPackages/com.budgetgamedev.procedural-synth/Tools~/SynthOffline -c Release \
  -p:EditorManaged=/path/to/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine
```

Uses .NET10 and the installed Unity6.5 mathematics DLL; no scene or Unity audio device. It writes matched heavy/clean WAVs, acid, metallic, adaptive output and zero-allocation/timing evidence. File I/O and buffers live in the offline host, outside rendering. [Quality tools](../Tools~/SynthQuality/README.md) contain reproducible oversampling fixtures and spectral analysis. The example command runs from this repository root; in another project replace the project path with the installed package folder. Output paths are relative to the current working directory and default to `Artifacts/Synth`.

Seeded composer integer draws and schedule order reproduce for identical sampled control inputs. Floating-point DSP may differ across Burst/Mono/.NET, CPU architectures and math implementations; bitwise cross-platform audio equality is not promised. Look-ahead decisions already queued cannot be revised retrospectively.

## Scope

Initial instrument and adaptive demonstration are implemented. Human listening comparisons, target-device qualification and automatic gameplay-state hookups remain product integration work. Hard sync, ring modulation, wavefolding, arbitrary feedback, a modulation matrix, extra filters, unison/polyphony and spatial/time effects are deferred until listening and measured priorities justify them. Weather currently changes noise, not a separate ambience/reverb engine.

## Package contents

`Runtime/Core` contains the independently renderable DSP, `Runtime/Composition` the control-layer composer, and `Runtime/Unity` the SAP adapter and audition components. `Editor` contains scene creation, render and profiling tools. `Tests/Editor` contains the NUnit checks. The developer command-line projects live in `Tools~`, documentation in `Documentation~`, and the importable scene in `Samples~`. See Unity 6.5's [package layout](https://docs.unity3d.com/6000.5/Documentation/Manual/cus-layout.html) and [package samples](https://docs.unity3d.com/6000.5/Documentation/Manual/cus-samples.html) documentation.
