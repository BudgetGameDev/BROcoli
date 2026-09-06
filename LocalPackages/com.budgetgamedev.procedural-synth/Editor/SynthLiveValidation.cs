using System;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
namespace BudgetGameDev.Synth.Editor
{
    /// <summary>Bounded, explicitly started editor-only capture. Never runs on the audio thread.</summary>
    [InitializeOnLoad]
    public static class SynthLiveValidation
    {
        public const int CaptureLimit=600;
        static ProfilerRecorder audio, synth;
        static readonly List<double> audioMs=new List<double>(CaptureLimit), synthMs=new List<double>(CaptureLimit);
        public static int CapturedUpdates { get; private set; }
        public static bool Running { get; private set; }
        static SynthLiveValidation()
        {
            AssemblyReloadEvents.beforeAssemblyReload+=StopRecorders;
            EditorApplication.quitting+=StopRecorders;
            EditorApplication.playModeStateChanged+=state=> { if(state==PlayModeStateChange.ExitingPlayMode)StopRecorders(); };
        }
        public static void Start()
        {
            StopRecorders(); audioMs.Clear();synthMs.Clear();CapturedUpdates=0;
            var options=ProfilerRecorderOptions.StartImmediately|ProfilerRecorderOptions.SumAllSamplesInFrame|ProfilerRecorderOptions.WrapAroundWhenCapacityReached;
            audio=new ProfilerRecorder(ProfilerCategory.Audio,"Audio.Thread",128,options);
            synth=new ProfilerRecorder("Burst Jobs","MonoSynthGenerator:Realtime.Header.Processor.m_Control (Burst)",128,options);
            Running=true;EditorApplication.update+=Sample;
        }
        // Count is not a safe GetSample index: a non-wrapping recorder may report a
        // lifetime count beyond capacity. LastValue directly reads the latest aggregate.
        public static bool TryReadLatest(ProfilerRecorder recorder,out double milliseconds)
        {
            milliseconds=0;
            if(!recorder.Valid || recorder.Count==0)return false;
            milliseconds=recorder.LastValue/1e6;return true;
        }
        static void Sample()
        {
            if(!EditorApplication.isPlaying)return;
            if(TryReadLatest(audio,out double a))audioMs.Add(a);
            if(TryReadLatest(synth,out double s))synthMs.Add(s);
            if(++CapturedUpdates>=CaptureLimit)Finish();
        }
        public static string Finish()
        {
            bool av=audio.Valid,sv=synth.Valid;StopRecorders();audioMs.Sort();synthMs.Sort();
            var report=new Report{audioMarkerValid=av,synthMarkerValid=sv,synthTimingAvailable=P95(synthMs)>0,updates=CapturedUpdates,frames=audioMs.Count,synthFrames=synthMs.Count,audioFrameP95ms=P95(audioMs),synthFrameP95ms=P95(synthMs),configuration="Unity Editor Play, Burst enabled, Apple M4, 48000Hz /1024 frames; marker values aggregate per video frame. Zero synth timing means unavailable, not zero processing cost. Kernel timing is recorded separately."};
            string json=JsonUtility.ToJson(report,true);Directory.CreateDirectory("Artifacts/Synth");File.WriteAllText("Artifacts/Synth/live-profile.json",json);return json;
        }
        static double P95(List<double> data)=>data.Count==0?0:data[Math.Min(data.Count-1,(int)(data.Count*.95))];
        static void StopRecorders(){EditorApplication.update-=Sample;Running=false;if(audio.Valid)audio.Dispose();if(synth.Valid)synth.Dispose();audio=default;synth=default;}
        [Serializable] class Report{public bool audioMarkerValid,synthMarkerValid,synthTimingAvailable;public int updates,frames,synthFrames;public double audioFrameP95ms,synthFrameP95ms;public string configuration;}
    }
}
