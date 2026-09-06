# Listening and measurement artifacts

- `Standalone/heavy-bass.wav` and `Standalone/clean-bass-matched.wav`: final core, same sequence and0.12RMS level target.
- `Standalone/acid.wav`, `Standalone/metallic-growl.wav`, `Standalone/adaptive.wav`: final contrasting presets and16-second adaptation sweep.
- Root `heavy-bass.wav`, `clean-bass-matched.wav`, `acid.wav`, `metallic-growl.wav`: Unity-hosted offline render with explicit overlap/glide examples.
- `Quality/*matched.wav`: controlled 1x/4x/reference comparisons, each normalized to−18dBFS RMS. Numerical/spectral limits explained in `LocalPackages/com.budgetgamedev.procedural-synth/Tools~/SynthQuality/README.md`.
- `burst-report.json`: final native kernel cost/allocation report; excludes pipeline/mixer overhead.
- `live-profile.json`: bounded600-update Editor capture; unavailable synth-marker timing is explicitly flagged.
- `unity-tests.json`:39 passing Unity tests, including the profiler capacity regression.
- `routing-*.txt`, `reconfigure-recovery.txt`, `adaptive-live.txt`: actual Editor integration observations.
- `reconfigure-44100.txt`: preserved initial failed recovery observation, superseded by recovery fix; actual device stayed48000Hz.

These WAVs are for human listening review. No listening approval or perceptual equivalence has been inferred from signal measurements.
