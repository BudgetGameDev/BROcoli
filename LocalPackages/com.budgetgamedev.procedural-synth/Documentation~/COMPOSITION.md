# Mono composition contract

`MonoComposer` belongs to one control-thread owner. It emits existing `SynthEvent` values into a caller-owned array; the Unity adapter transports them to its audio owner. It does not touch the audio engine, scene objects or audio thread. Construct/reset with the same output sample rate and epoch as `SynthEngine`.

The initial pattern is a 16-sixteenth-note phrase (four beats). Tempo requests are 40–200 BPM and apply at the next generated beat. Fractional step durations accumulate in double precision and each event is rounded to the nearest frame, avoiding per-step integer truncation drift. Notes use Minor, Dorian or minor Pentatonic intervals, root MIDI 24–60, and a phrase motif. Root and scale edits commit at the next generated phrase. Every phrase starts on the root with an accent and ends with a rest; other steps use seeded pitch, rhythm and velocity decisions. Accents use velocity 0.96; ordinary notes use 0.62–0.78.

## Scheduling and ownership

- `Fill(throughSample, destination)` writes events at or before the inclusive frame horizon, sorted by sample. Repeated calls continue from the previous position.
- Capacity one is supported: an emitted note-on retains its paired note-off internally. Empty arrays consume no state or random decisions. Notes never overlap in this first composer; note-offs precede the next onset, including repeated pitches.
- A call considers at most 4096 grid steps. Call again until it returns zero to complete a large horizon. Typical operation should use a short, bounded look-ahead such as two audio buffers and respect the engine's 128-event capacity.
- Keep any returned events the adapter has not yet transported; `Fill` cannot undo delivery. Retry pipe sends, and never silently discard note releases.
- `Reset` starts a fresh frame-zero timeline and restores defaults (112 BPM, MIDI 36, Minor, healthy neutral state). Clear old transport/engine events and release any sounding note before resetting. After device changes, reapply desired tempo, scale, root and state.
- Game-state changes affect decisions that have not yet been generated. Look-ahead is therefore an explicit responsiveness tradeoff. Reproducibility requires the same state/edit calls before the same scheduling boundaries, as well as the same seed.

## Adaptive controls

All game-state floats are clamped to 0–1. Nonfinite values become zero, except health which becomes one. Narrative is a discrete integer identifier.

| Input | Destination | Timing and bounds |
| --- | --- | --- |
| Danger | Rhythmic density | Beat boundary; probability 35%, 60% or 85%, plus mandatory phrase root/rest |
| Danger | Pre-filter drive | Immediate smoothed expression; base drive plus up to 65% of remaining headroom to 12 |
| Enemy proximity | Cutoff | Immediate smoothed expression; base cutoff multiplied by up to 16, final 20–18000 Hz clamp |
| Player health | Wavetable position | Immediate smoothed expression; lower health moves base position toward 1 |
| Movement speed | Gate duration | Beat boundary; slow/medium/fast gates 90%/65%/40% of a sixteenth |
| Weather | Noise level | Immediate smoothed expression; adds up to 0.12, final 0–1 clamp |
| Narrative | Phrase motif | Two consecutive beat observations of a candidate, then commit on a phrase boundary |

Danger and movement bands use Schmitt thresholds 0.33 and 0.67 with a +/-0.05 deadband to suppress threshold chatter. At startup, narrative initializes directly from the first state. Narrative identifiers select a deterministic motif variation; they do not define a full harmonic progression.

`AdaptPreset(basePreset, elapsedSeconds)` uses 200 ms exponential smoothing (time constant, about 95% settled after 600 ms). Seconds are clamped to 0–1, nonfinite values produce no advancement. Always pass the unadapted base preset; feeding the previous result back would compound the mappings. The synth additionally smooths its own parameter transitions. Health affects timbre only when a wavetable oscillator is enabled. Weather ambience effects remain deferred; this version adds source noise only.

## Determinism and scope

Xorshift32 uses explicitly defined unsigned shifts and wrapping motif arithmetic; seed zero maps to a fixed nonzero seed. Integer musical decisions are repeatable independently of destination buffer capacity. Timing uses double arithmetic and explicit positive nearest-frame rounding; extreme cross-platform floating-point differences at exact half-frame boundaries are not promised bit-identical. DSP transcendental math and Burst optimization have separate cross-platform determinism limits. This control layer does not claim sample-identical rendered audio across platforms.

This is a mono composer: no orchestration, polyphony, effects, sustained overlap generation, game-object polling or implicit transport clock synchronization. Manual legato/glide audition remains available in the voice. Human listening approval remains outstanding until the integrated sequence renders are reviewed.

## Unity demo adapter

`SynthAdaptation` requires the existing `MonoSynthGenerator` and `SynthAudition` on the same object. Its right-hand panel (x=550) provides an adaptive toggle and normalized state controls plus tempo, root and scale. This is a demonstrator; gameplay systems are not automatically discovered or wired. Game code can supply `GameMusicState` through `SetGameState` and call `SetAdaptive`.

Adaptive mode stops the fixed sequence and temporarily disables manual audition so two producers cannot fight over the mono key stack or presets. On exit it panics, restores the unadapted `basis`, restores the previous audition enabled state, and leaves its fixed sequence stopped. The default adaptive basis enables oscillator 1's wavetable so health changes are audible. The basis remains separate from each frame's adapted output.

The adapter subscribes to `ResetTimeline`, resets the composer with the new rate/seed, and places its frame-zero origin 150 ms ahead of the generator's reported sample position. It fills to a 300 ms horizon using one preallocated 32-event array and at most two batches per frame. The generator owns transport retry once it accepts an event. A control-queue overflow resets that queue, so this adapter explicitly panics and restarts a new composition epoch; `SchedulingRecoveries` counts the recovery and is displayed in the panel. A frame stall exceeding 300 ms of audio-clock advancement also restarts instead of dumping obsolete events into the audio engine.

Disabling the adapter releases its notes and detaches lifecycle callbacks. Re-enabling preserves the adaptive toggle request. Immediate adaptation runs on unscaled frame time; audio rendering remains owned by the generator. These policies do not guarantee uninterrupted music across a device reset or long frame stall: the deliberate recovery is a release and a fresh phrase after the lead time.
