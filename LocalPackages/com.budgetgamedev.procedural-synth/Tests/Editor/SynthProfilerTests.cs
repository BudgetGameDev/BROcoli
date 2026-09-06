using System.Collections;
using BudgetGameDev.Synth.Editor;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine.TestTools;
namespace BudgetGameDev.Synth.Tests
{
    public class SynthProfilerTests
    {
        [UnityTest] public IEnumerator LiveReaderSurvivesMoreFramesThanRecorderCapacity()
        {
            var marker=new ProfilerMarker("SynthProfilerCapacityRegression");
            using(var recorder=new ProfilerRecorder(ProfilerCategory.Scripts,"SynthProfilerCapacityRegression",8,
                ProfilerRecorderOptions.StartImmediately|ProfilerRecorderOptions.SumAllSamplesInFrame|ProfilerRecorderOptions.WrapAroundWhenCapacityReached))
            {
                Assert.That(recorder.Valid,Is.True);
                for(int frame=0;frame<160;frame++)
                {
                    using(marker.Auto()) { for(int i=0;i<100;i++) System.Math.Sqrt(i); }
                    yield return null;
                    // The previous helper indexed Count-1 and threw once capacity was exceeded.
                    Assert.DoesNotThrow(()=>SynthLiveValidation.TryReadLatest(recorder,out double ignored));
                }
                Assert.That(SynthLiveValidation.TryReadLatest(recorder,out double time),Is.True);
                Assert.That(time,Is.GreaterThanOrEqualTo(0));
            }
            Assert.That(SynthLiveValidation.TryReadLatest(default,out double empty),Is.False);
        }
    }
}
