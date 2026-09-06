using System;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using BudgetGameDev.Synth;
static class Program
{
    const int Rate=48000;
    static void Main(string[] args)
    {
        string output=args.Length>0?args[0]:"Artifacts/Synth/Standalone"; Directory.CreateDirectory(output);
        var heavy=Render(SynthParameters.HeavyBass); var clean=Render(SynthParameters.CleanBass);
        double target=Math.Min(.12,Math.Min(Rms(heavy)*.9/Peak(heavy),Rms(clean)*.9/Peak(clean)));
        Wave(Path.Combine(output,"heavy-bass.wav"),heavy,target/Rms(heavy)); Wave(Path.Combine(output,"clean-bass-matched.wav"),clean,target/Rms(clean));
        Wave(Path.Combine(output,"acid.wav"),Render(SynthParameters.Acid),1); Wave(Path.Combine(output,"metallic-growl.wav"),Render(SynthParameters.MetallicGrowl),1);
        Wave(Path.Combine(output,"adaptive.wav"),RenderAdaptive(),1);
        var e=new SynthEngine(); e.Initialize(Rate,1,SynthParameters.MetallicGrowl); e.Enqueue(SynthEvent.On(0,36)); for(int i=0;i<Rate;i++)e.ProcessSample();
        var clock=new Stopwatch(); var times=new double[200]; float checksum=0;
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int j=0;j<times.Length;j++){clock.Restart();for(int i=0;i<1024;i++)checksum+=e.ProcessSample();clock.Stop();times[j]=clock.Elapsed.TotalMilliseconds;}
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;Array.Sort(times);
        var report=new{runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,os=System.Runtime.InteropServices.RuntimeInformation.OSDescription,rate=Rate,frames=1024,quality=4,allocated,p50ms=times[100],p95ms=times[190],maxMs=times[199],matchedRms=target,checksum};
        File.WriteAllText(Path.Combine(output,"report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true})); Console.WriteLine(JsonSerializer.Serialize(report));
        if(allocated!=0)throw new Exception("Realtime allocation regression");
    }
    static float[] Render(SynthParameters p)
    {
        var e=new SynthEngine();e.Initialize(Rate,7301,p);var data=new float[Rate*8];int[] notes={36,36,43,36,39,36,46,43,36,48,43,39,36,34,31,34};
        for(int i=0;i<data.Length;i++) {if(i%(Rate/4)==0&&i<data.Length-Rate/2){int step=i/(Rate/4),note=notes[step%notes.Length];e.Enqueue(SynthEvent.On(i,note,step%4==0?1:.78f));e.Enqueue(SynthEvent.Off(i+(long)(Rate*.19),note));}data[i]=e.ProcessSample();}return data;
    }
    static float[] RenderAdaptive()
    {
        var data=new float[Rate*16];var p=SynthParameters.HeavyBass;p.Oscillator1.Waveform=Waveform.Wavetable;
        var e=new SynthEngine();e.Initialize(Rate,731,p);var c=new MonoComposer(Rate,731);var events=new SynthEvent[32];
        for(int i=0;i<data.Length;i++) {if(i%480==0){float t=(float)i/data.Length;c.SetState(new GameMusicState{Danger=t,EnemyProximity=t,PlayerHealth=1-t,MovementSpeed=t,Weather=.4f*t,NarrativeState=t>.5f?1:0});e.SetParameters(c.AdaptPreset(p,.01f));int n=c.Fill(i+479,events);for(int j=0;j<n;j++)e.Enqueue(events[j]);}data[i]=e.ProcessSample();}return data;
    }
    static double Rms(float[] d){double sum=0;foreach(var x in d){if(!float.IsFinite(x))throw new Exception("Nonfinite render");sum+=x*x;}return Math.Sqrt(sum/d.Length);}
    static double Peak(float[] d){double max=1e-9;foreach(var x in d)max=Math.Max(max,Math.Abs(x));return max;}
    static void Wave(string path,float[] data,double gain)
    {
        using var w=new BinaryWriter(File.Create(path));w.Write("RIFF"u8);w.Write(36+data.Length*2);w.Write("WAVEfmt "u8);w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(Rate);w.Write(Rate*2);w.Write((short)2);w.Write((short)16);w.Write("data"u8);w.Write(data.Length*2);
        foreach(float x in data)w.Write((short)Math.Round(Math.Clamp(x*gain,-1,1)*32767));
    }
}
