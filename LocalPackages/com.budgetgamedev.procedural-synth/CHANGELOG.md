# Changelog

## 0.1.0 — 2026-09-06

- Extracted the mono synthesizer and adaptive composer into the portable `com.budgetgamedev.procedural-synth` UPM package for Unity 6.5, minimum 6000.5.10f1.
- Added an importable mono audition sample, a writable-scene builder, and optional consumer mixer routing with direct AudioListener output by default.
- Included independently renderable DSP: three tuned oscillators, sub/noise, bounded drift, envelopes, note handling, glide, nonlinear filtering/drive, audio-rate modulation, mipmapped wavetables and oversampling.
- Included seeded phrase composition and bounded game-state adaptation with separate beat/phrase scheduling and immediate expression.
- Moved offline render/quality tools into `Tools~` and technical documentation into `Documentation~`; retained the original measurement record with its host-project context.

Initial technical verification does not establish listening approval, alias-free modulation, Web support, or untested target-platform compatibility.
