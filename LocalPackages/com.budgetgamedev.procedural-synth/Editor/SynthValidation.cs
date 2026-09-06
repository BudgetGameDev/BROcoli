using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
namespace BudgetGameDev.Synth.Editor
{
    /// <summary>Offline evidence renderer. Allocations/file IO here are explicitly outside real-time processing.</summary>
    public static class SynthValidation
    {
        public static readonly int[] BassNotes={36,36,43,36,39,36,46,43,36,48,43,39,36,34,31,34};
        public static float[] Render(SynthParameters preset,int rate=48000,int seconds=8,uint seed=7301)
        {
            var engine=new SynthEngine(); engine.Initialize(rate,seed,preset); var samples=new float[rate*seconds];
            int step=0; double next=0; int previous=-1;
            for(int i=0;i<samples.Length;i++) {
                if(i>=(long)next && i<samples.Length-rate/2) {
                    int note=BassNotes[step%BassNotes.Length];
                    // Every fourth step overlaps by 12ms to exercise legato and glide.
                    if(previous>=0) engine.Enqueue(SynthEvent.Off(i+(step%4==0?(long)(.012*rate):0),previous));
                    engine.Enqueue(SynthEvent.On(i,note,step%4==0?1:.78f));
                    if(step%4!=3) engine.Enqueue(SynthEvent.Off(i+(long)(rate*.15),note));
                    previous=note; step++; next=step*rate*.25;
                }
                if(i==samples.Length-rate/2) engine.Enqueue(new SynthEvent{Sample=i,Type=SynthEventType.AllNotesOff});
                samples[i]=engine.ProcessSample();
            }
            return samples;
        }
        public static double Rms(float[] data) { double sum=0; foreach(float x in data) sum+=x*x; return Math.Sqrt(sum/data.Length); }
        public static float Peak(float[] data) { float peak=0; foreach(float x in data) peak=Math.Max(peak,Math.Abs(x)); return peak; }
        public static void WriteWave(string path,float[] data,int rate,float gain=1)
        {
            using(var w=new BinaryWriter(File.Create(path))) {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36+data.Length*2); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
                w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate*2); w.Write((short)2); w.Write((short)16); w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(data.Length*2);
                foreach(float x in data) w.Write((short)Math.Round(Math.Max(-1,Math.Min(1,x*gain))*32767));
            }
        }
        [UnityEditor.MenuItem("Tools/Brocoli Synth/Render validation audio")]
        public static void RenderEvidence()
        {
            string dir=Path.GetFullPath("Artifacts/Synth"); Directory.CreateDirectory(dir);
            var heavy=Render(SynthParameters.HeavyBass); var clean=Render(SynthParameters.CleanBass);
            double target=Math.Min(.12,Math.Min(Rms(heavy)*.9/Math.Max(.001,Peak(heavy)),Rms(clean)*.9/Math.Max(.001,Peak(clean))));
            WriteWave(Path.Combine(dir,"heavy-bass.wav"),heavy,48000,(float)(target/Math.Max(1e-9,Rms(heavy))));
            WriteWave(Path.Combine(dir,"clean-bass-matched.wav"),clean,48000,(float)(target/Math.Max(1e-9,Rms(clean))));
            WriteWave(Path.Combine(dir,"acid.wav"),Render(SynthParameters.Acid),48000);
            WriteWave(Path.Combine(dir,"metallic-growl.wav"),Render(SynthParameters.MetallicGrowl),48000);
            var e=new SynthEngine(); e.Initialize(48000,1,SynthParameters.MetallicGrowl); e.Enqueue(SynthEvent.On(0,36));
            for(int i=0;i<8192;i++) e.ProcessSample();
            var timings=new double[200]; var timer=new Stopwatch(); float checksum=0;
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int block=0;block<timings.Length;block++) { timer.Restart(); for(int i=0;i<1024;i++) checksum+=e.ProcessSample(); timer.Stop(); timings[block]=timer.Elapsed.TotalMilliseconds; }
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before; Array.Sort(timings);
            File.WriteAllText(Path.Combine(dir,"offline-report.json"),JsonUtility.ToJson(new Report {unity=Application.unityVersion,cpu=SystemInfo.processorType,os=SystemInfo.operatingSystem,rate=48000,buffer=1024,managedBytes=allocated,p50ms=timings[100],p95ms=timings[190],maxMs=timings[199],heavyRms=Rms(heavy),cleanRms=Rms(clean),matchedRms=target,heavyPeak=Peak(heavy),cleanPeak=Peak(clean),checksum=checksum},true));
        }
        [Serializable] class Report { public string unity,cpu,os; public int rate,buffer; public long managedBytes; public double p50ms,p95ms,maxMs,heavyRms,cleanRms,matchedRms; public float heavyPeak,cleanPeak,checksum; }
    }
}
