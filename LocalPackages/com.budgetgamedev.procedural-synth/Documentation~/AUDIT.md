# Repository and API audit — 2026-09-06

**Historical implementation audit.** Repository paths, BROcoli mixer measurements and release policies below describe the original host project before extraction into a portable UPM package. Current setup is documented in the [manual](README.md); those host assets and build hooks are not package dependencies.

Clean git working tree before work; root AGENTS.md is empty; no nested project instructions found. Runtime packages live in LocalPackages. Synth was initially isolated under `Assets/ProceduralSynth` to avoid unrelated gameplay changes. Existing shared AudioSettings routes to Brocoli mixer Master/Ambience/SFX. The original demo selected Ambience; the packaged demo now defaults to direct AudioListener output with an optional consumer-assigned mixer.

Installed/running editor 6000.5.10f1 (3bd4f66ad299), macOS arm64, Apple M4, 16GiB RAM; current target StandaloneOSX. Audio configuration at audit: 48000 Hz, 1024 DSP frames, stereo, 32 real voices. Installed desktop Linux/Windows/Web modules. Repository native scripts target macOS/Windows64/Linux64; separate WebGL build script. Root game scene is Packages/com.budgetgamedev.hub/Scenes/GameLauncher.unity, clean before demo work.

Packages: com.unity.modules.audio 1.0.0 (built-in), Burst1.8.30, Collections6.5.0 built-in, Mathematics1.4.0 built-in; TestFramework1.7.0; HDRP/URP17.5.0. com.unity.pipeline0.5.0-exp.1 supplies editor automation and is unrelated to the Scriptable Audio Pipeline.

## Verified against installed assembly and official 6.5 docs

Runtime reflection queried actual UnityEngine.AudioModule.dll under editor Contents/Resources/Scripting/Managed/UnityEngine and its installed XML. Unity.Mathematics.math actually resides in UnityEngine.MathematicsModule.dll in this version. Public signatures confirmed: IAudioGenerator.CreateInstance(ControlContext,AudioFormat?,ProcessorInstance.CreationParameters); AllocateGenerator<TRealtime,TControl>; IControl.Configure/Update/OnMessage/Dispose; IRealtime.Update/Process(in RealtimeContext,Pipe,ChannelBuffer,Arguments); Pipe.SendData returns bool, GetAvailableData, Element.TryGetData; AudioSource.generator and generatorInstance; ChannelBuffer[channel,frame]. The implementation is compiled against these exact installed APIs, not a guessed package API.

Official sources, checked September6:
- [Pipeline overview and Web restriction](https://docs.unity3d.com/6000.5/Documentation/Manual/audio-scriptable-processors.html)
- [Generator lifecycle and format negotiation](https://docs.unity3d.com/6000.5/Documentation/Manual/audio-scriptable-processors-generators.html)
- [Concrete generator and pipe example](https://docs.unity3d.com/6000.5/Documentation/Manual/audio-scriptable-processors-example-creating-a-generator.html)
- [Pipe/message threading and lifetime](https://docs.unity3d.com/6000.5/Documentation/Manual/audio-scriptable-processors-concepts.html)

Pipes are fixed-memory realtime transport. Messages are blocking and therefore only invoked on main/control side. Realtime state is unmanaged and audio-owned. Pipe payloads are valid only for the stated mix/update cycle; adapter uses acknowledged packets and retries unsuccessful sends. No realtime object disposal: structs contain fixed inline state only, with SAP owning lifetime.

Web platform explicitly unsupported by SAP. Component compile guard disables generation with one control-side warning on Web; no live synth support claimed there. Native desktop intended, actual platform evidence tracked in [VALIDATION.md](VALIDATION.md). Supported output rate32–96kHz; unsupported rates yield silence. Mono source lets host route/pan/spatialize; source pitch should remain1 because real-time timing uses output frames.

## Build policy follow-up

`LocalPackages/com.budgetgamedev.shared/Editor/Build/BuildContentPolicy.cs` requires isolated staging for release builds and explicitly allows development builds in the source workspace. `Assets/Editor/BuildWarningGate.cs` treats unexpected build warnings as errors. The first connector request lost its array-valued options (connector schema exposes strings while installed BuildCommand expects string arrays), so the release gate correctly rejected it. The retry calls the installed BuildCommand with typed `new[]{"Development","DetailedBuildReport"}` and the explicit demo scene. Neither policy is bypassed or modified. Native build hooks temporarily select compatible render quality and are expected to restore project settings afterward.

The historically verified pre-package invocation was (the old scene path below is not the current build recipe):

```csharp
Unity.Pipeline.Editor.Commands.Build.BuildCommand.Build(
    target: "StandaloneOSX",
    outputPath: "Artifacts/Synth/Build/MonoSynth.app",
    options: new[] { "Development", "DetailedBuildReport" },
    scenes: new[] { "Assets/ProceduralSynth/Demo/MonoSynthDemo.unity" },
    confirm: true);
```

This development build still includes source-project code and Resources. Its cold Metal shader compilation is substantial even with only the audition scene selected. Shipping BROcoli or launcher releases must use the repository's `scripts/release-build.py --product brocoli` or `--product launcher` isolation workflow. Selecting the synth scene does not create an isolated release product.
