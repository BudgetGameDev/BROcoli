# Mono synth quality fixtures

Run from the repository root with .NET 10 and the installed Unity 6.5 mathematics module:

```sh
DOTNET_TieredCompilation=0 dotnet run --project LocalPackages/com.budgetgamedev.procedural-synth/Tools~/SynthQuality/SynthQuality.csproj -c Release -p:EditorManaged=/path/to/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine
python3 -m venv /tmp/synth-quality-env
/tmp/synth-quality-env/bin/pip install numpy scipy
/tmp/synth-quality-env/bin/python LocalPackages/com.budgetgamedev.procedural-synth/Tools~/SynthQuality/analyze.py
```

In another project replace the command paths with the actual package directory. The renderer and analyzer accept an optional output directory argument, resolved from the current working directory. Their default is `Artifacts/Synth/Quality`. Files include raw float renders, RMS-matched WAV comparisons (-18 dBFS), `runtime-results.json`, and `quality-results.json`. Initialization and file writing occur outside the measured rendering loop. The runtime results describe standalone .NET execution; Unity/Burst callback profiling remains a separate acceptance check. These recordings have not received human listening validation.

Fixtures use an 880 Hz carrier, a 440 Hz sine PM source where enabled, no drift/noise, an 8 kHz filter cutoff with zero filter-envelope depth, and one second of envelope/filter settling. They compare 48 kHz output at 1x/4x against the same core at 96 kHz/4x, subsequently resampled by a 1025-tap Kaiser FIR. The higher-rate version is a numerical reference, not ground truth. The analyzer uses a one-second Hann-windowed segment and masks +/-5 Hz around legitimate harmonic locations. Remaining spectral energy includes aliases and numerical/leakage error; alias components that land on legitimate harmonic bins are not counted. Both full-band and below-20-kHz figures are provided. The tests do not establish perceptual transparency or cover arbitrary musical modulation.

At the final matched-cutoff settings, changing from 1x to 4x reduced below-20-kHz off-harmonic energy (dBc) as follows:

| Fixture | 1x | 4x | Higher-rate reference |
| --- | ---: | ---: | ---: |
| Saw, cleaner drive | -49.85 | -66.31 | -72.19 |
| Driven sine | -41.81 | -88.34 | -84.10 |
| Sine PM with drive | -31.77 | -83.81 | -82.02 |
| Wavetable PM with drive | -31.76 | -54.25 | -58.61 |

Differences near the numerical floor need not improve monotonically with rate. Residual wavetable PM aliasing remains measurable. Saw/square PM can produce wider spectra than the conservative mip estimate covers.

The final realtime decimator was increased from a 31-tap halfband to a 127-tap Blackman-windowed sinc after the initial PM fixtures revealed near-Nyquist aliasing. Its cutoff is 0.45 times output rate (21.6 kHz at 48 kHz); the first 4x-to-2x stage remains a 31-tap halfband. In the original 4x-only 14 kHz-cutoff fixture, the sine-PM full-band result improved from -38.22 to -83.32 dBc. That initial fixture cannot support a matched 1x-vs-4x comparison because the 1x filter safety cap lowers 14 kHz to 9.6 kHz. The final comparison table above uses 8 kHz in every configuration. This costs approximately 56 additional multiply-accumulates per output sample. Observable output-index FIR latency is 34.5 samples at 4x and 31 at 2x, independently checked by impulse centroid; the corresponding filter group delays before accounting for decimation phase are 35.25 and 31.5 sample periods.

`generate_wavetable.py` resolves the package root relative to its own location and deterministically regenerates `Runtime/Core/VoiceWavetable.cs`. Run it only in a writable development checkout of the package; it edits runtime source. Its 15 x 1024 floats occupy 60 KiB shared across voices; per-voice storage contains no table arrays. Seven harmonic mips use 127/63/31/15/7/3/1 partials. Mip blends leave an octave of headroom, and waveform position is smoothed in the voice. Table and decimator static storage is warmed during initialization, before realtime rendering. The initializer uses literal primitive arrays supported by Burst; final Unity/Burst compilation must be checked after regenerating it.
