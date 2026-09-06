using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using BudgetGameDev.Synth;
class Program {
 static void Main(string[] args) {
 string output=args.Length>0?args[0]:"Artifacts/Synth/Quality"; Directory.CreateDirectory(output); var measurements=new List<object>();
 foreach(var name in new[]{"saw_clean","sine_driven","sine_pm","wavetable_pm"}) foreach(var config in new[]{(48000,1),(48000,4),(96000,4)}) {
 var p=SynthParameters.HeavyBass;p.Oscillator1=new OscillatorParameters(name=="saw_clean"?Waveform.Saw:name=="wavetable_pm"?Waveform.Wavetable:Waveform.Sine,0,0,0,1);p.Oscillator2=new OscillatorParameters(Waveform.Sine,-1,0,0,0);p.Oscillator3.Level=0;p.NoiseLevel=p.SubLevel=p.DriftCents=0;p.CutoffHz=8000;p.FilterEnvelopeOctaves=0;p.AmpEnvelope=new EnvelopeParameters(.003f,.1f,1,.08f);p.ResetPhase=true;p.WavetablePosition=.8f;
 p.PreDrive=name=="saw_clean"?1:12;p.PostDrive=name=="saw_clean"?1:6;p.Resonance=.4f;p.PhaseModCycles=name.EndsWith("pm")?.4f:0;
 var v=new MonoVoice();v.Initialize(config.Item1,42,p,config.Item2);v.NoteOn(81,1);for(int i=0;i<config.Item1;i++)v.ProcessSample();
 var samples=new float[config.Item1*2];long before=GC.GetAllocatedBytesForCurrentThread();long start=Stopwatch.GetTimestamp();for(int i=0;i<samples.Length;i++)samples[i]=v.ProcessSample();double ms=1000.0*(Stopwatch.GetTimestamp()-start)/Stopwatch.Frequency;long allocations=GC.GetAllocatedBytesForCurrentThread()-before;
 string path=Path.Combine(output,$"{name}_{config.Item1}_{config.Item2}x.f32");using(var writer=new BinaryWriter(File.Create(path)))foreach(float x in samples)writer.Write(x);
 Console.WriteLine($"{name},{config.Item1},{config.Item2},{ms:F3}ms,{allocations}bytes");
 measurements.Add(new {fixture=name,sampleRate=config.Item1,oversampling=config.Item2,renderSeconds=2,processingMilliseconds=ms,allocations});
 }
 File.WriteAllText(Path.Combine(output,"runtime-results.json"),JsonSerializer.Serialize(new {utc=DateTime.UtcNow,framework=RuntimeInformation.FrameworkDescription,os=RuntimeInformation.OSDescription,architecture=RuntimeInformation.ProcessArchitecture.ToString(),measurements},new JsonSerializerOptions{WriteIndented=true}));
 }
}
