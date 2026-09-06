using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using AOT;
using Unity.Burst;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Synth.Editor
{
    /// <summary>Reproducible synchronous native DSP-kernel benchmark. Does not measure
    /// SAP callback scheduling, pipe processing, AudioSource routing, or mixer cost.</summary>
    [BurstCompile]
    public static unsafe class SynthBurstBenchmark
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void RenderKernel(SynthEngine* engine, float* output, int frames, int* nativeMarker);

        [BurstCompile(CompileSynchronously=true)]
        [MonoPInvokeCallback(typeof(RenderKernel))]
        private static void RenderBlock(SynthEngine* engine, float* output, int frames, int* nativeMarker)
        {
            int native=1;
            MarkManagedFallback(ref native);
            *nativeMarker=native;
            for(int i=0;i<frames;i++)output[i]=engine->ProcessSample();
        }
        [BurstDiscard]
        private static void MarkManagedFallback(ref int native) { native=0; }

        [MenuItem("Tools/Brocoli Synth/Benchmark Burst DSP kernel")]
        private static void RunMenu() => Run();

        public static string Run()
        {
            const int rate=48000, frames=1024, iterations=200, warmupBlocks=32;
            // Compilation, managed delegate construction, buffers, and reporting are outside measurement.
            var pointer=BurstCompiler.CompileFunctionPointer<RenderKernel>(RenderBlock);
            var render=pointer.Invoke;
            var engine=new SynthEngine();
            engine.Initialize(rate,1979,SynthParameters.MetallicGrowl);
            engine.Enqueue(SynthEvent.On(0,36));
            var samples=new float[frames];
            var elapsedTicks=new long[iterations];
            int nativeMarker=0;
            long allocated;
            double checksum=0;
            fixed(float* output=samples)
            {
                for(int i=0;i<warmupBlocks;i++)render(&engine,output,frames,&nativeMarker);
                // Warm timestamp/runtime allocation counters as well, outside the measured loop.
                Stopwatch.GetTimestamp(); GC.GetAllocatedBytesForCurrentThread();
                long before=GC.GetAllocatedBytesForCurrentThread();
                for(int block=0;block<iterations;block++)
                {
                    long start=Stopwatch.GetTimestamp();
                    render(&engine,output,frames,&nativeMarker);
                    elapsedTicks[block]=Stopwatch.GetTimestamp()-start;
                }
                allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            }
            float peak=0;
            bool finite=true;
            foreach(float sample in samples)
            {
                finite &= !float.IsNaN(sample) && !float.IsInfinity(sample);
                checksum+=sample;
                peak=Math.Max(peak,Math.Abs(sample));
            }
            Array.Sort(elapsedTicks);
            double tickMs=1000.0/Stopwatch.Frequency;
            double p95=elapsedTicks[(int)Math.Ceiling(iterations*.95)-1]*tickMs;
            double bufferMs=1000.0*frames/rate;
            var report=new Report {
                scope="Burst function-pointer SynthEngine DSP kernel; excludes SAP callback scheduling, pipe, AudioSource and mixer",
                unity=Application.unityVersion, cpu=SystemInfo.processorType, os=SystemInfo.operatingSystem,
                processorCount=SystemInfo.processorCount, editor=true, debugBuild=UnityEngine.Debug.isDebugBuild,
                burstPackage=UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BurstCompiler).Assembly)?.version ?? "unknown",
                burstEnabled=BurstCompiler.IsEnabled, nativeKernelVerified=nativeMarker==1,
                burstSafetyChecks=BurstCompiler.Options.EnableBurstSafetyChecks,
                floatMode="Burst default (matches SAP Realtime attribute)",
                preset="MetallicGrowl", quality="4x oversampling, cascaded FIR halfband decimation",
                sampleRate=rate, blockFrames=frames, iterations=iterations, warmupBlocks=warmupBlocks,
                managedBytes=allocated, p50Ms=elapsedTicks[iterations/2-1]*tickMs, p95Ms=p95,
                maxMs=elapsedTicks[iterations-1]*tickMs, bufferDurationMs=bufferMs, p95BufferFraction=p95/bufferMs,
                passesKernelBudget=nativeMarker==1 && finite && allocated==0 && p95<bufferMs*.25,
                finalBlockFinite=finite, finalBlockPeak=peak, finalBlockChecksum=checksum,
                timestampUtc=DateTime.UtcNow.ToString("O")
            };
            string path=Path.GetFullPath("Artifacts/Synth/burst-report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,JsonUtility.ToJson(report,true));
            return path;
        }
        [Serializable]
        private sealed class Report
        {
            public string scope,unity,cpu,os,burstPackage,floatMode,preset,quality,timestampUtc;
            public int processorCount,sampleRate,blockFrames,iterations,warmupBlocks;
            public bool editor,debugBuild,burstEnabled,nativeKernelVerified,burstSafetyChecks,passesKernelBudget,finalBlockFinite;
            public long managedBytes;
            public double p50Ms,p95Ms,maxMs,bufferDurationMs,p95BufferFraction,finalBlockChecksum;
            public float finalBlockPeak;
        }
    }
}
