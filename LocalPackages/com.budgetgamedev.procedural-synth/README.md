# Procedural Synth

`com.budgetgamedev.procedural-synth` **0.1.0** is a portable package for a driven, expressive mono instrument and seeded adaptive composition. It requires **Unity 6.5, minimum 6000.5.10f1**, and uses Unity's Scriptable Audio Pipeline through an AudioSource, with independently renderable C# DSP. No BROcoli gameplay assets or mixer are required.

Install this folder as a local UPM package, select **Procedural Synth** in Package Manager, and import its included sample. Open `Assets/Samples/Procedural Synth/0.1.0/MonoSynth/MonoSynthDemo.unity` and enter Play. The sample routes directly to the AudioListener; assign your own AudioMixerGroup on the AudioSource when mixer routing is desired. Unity documents [package layout](https://docs.unity3d.com/6000.5/Documentation/Manual/cus-layout.html) and [sample import conventions](https://docs.unity3d.com/6000.5/Documentation/Manual/cus-samples.html).

The instrument includes three tuned oscillators, sub/noise, seeded drift, envelopes, note priorities, legato/glide, a nonlinear four-pole lowpass, drive, audio-rate phase/cutoff modulation, mipmapped wavetable morphing and 4× oversampling. The composer provides tempo/scale/phrase scheduling and bounded game-state mappings. Human listening acceptance remains open; feature count is not a sound-quality claim.

- [Manual and audition controls](Documentation~/README.md)
- [Architecture and contracts](Documentation~/CONTRACTS.md)
- [Adaptive composition API](Documentation~/COMPOSITION.md)
- [Validation evidence and limitations](Documentation~/VALIDATION.md)
- [Verified Unity APIs and historical host audit](Documentation~/AUDIT.md)
- [Spectral fixtures and offline analysis](Tools~/SynthQuality/README.md)
- [Changelog](CHANGELOG.md)

From this repository root, the standalone renderer can be built/run with .NET 10 and the installed Unity mathematics module:

```sh
dotnet run --project LocalPackages/com.budgetgamedev.procedural-synth/Tools~/SynthOffline -c Release \
  -p:EditorManaged=/path/to/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine
```

In other checkouts, substitute the package folder. Renders default to `Artifacts/Synth/Standalone` under the current working directory. The editor demo builder creates a writable scene at `Assets/ProceduralSynthDemo/MonoSynthDemo.unity`.

Native desktop is the intended integration target. macOS was exercised in the original host project; Windows/Linux runtime qualification remains open. DSP rates span 32–96 kHz. Unity SAP does not support Web, and the adapter explicitly disables itself there. Multichannel/spatializer qualification and automatic game-state hookups remain consumer integration work.

To expose package tests in a consuming project's Test Runner, install `com.unity.test-framework` and add `"com.budgetgamedev.procedural-synth"` to the top-level `testables` array in `Packages/manifest.json`, preserving any existing entries. Run the `BudgetGameDev.Synth.Tests` Editor assembly. This repository already has that configuration.
