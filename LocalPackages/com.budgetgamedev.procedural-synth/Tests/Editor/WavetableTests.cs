using System;
using NUnit.Framework;

namespace BudgetGameDev.Synth.Tests
{
    public class WavetableTests
    {
        [Test] public void InterruptedWaveformTransitionsRetainTheAudibleBlend()
        {
            var oscillator=new VoiceOscillator { Phase=.25f };
            float previous=oscillator.Process(Waveform.Sine,0,48000);
            for(int i=0;i<1000;i++)
            {
                // Freeze phase to isolate control continuity from natural waveform
                // motion; replace the requested shape during two incomplete fades.
                Waveform requested=i<48?Waveform.Saw:i<93?Waveform.Triangle:Waveform.Square;
                float value=oscillator.Process(requested,0,48000);
                Assert.That(Math.Abs(value-previous),Is.LessThan(.0084f)); previous=value;
            }
            Assert.That(previous,Is.EqualTo(1).Within(.00001f));
        }
        [TestCase(2,31)] [TestCase(4,34.5)]
        public void DecimatorImpulseHasDocumentedLatencyAndUnityDcGain(int factor,double expectedDelay)
        {
            VoiceHalfband first=default; VoiceOutputDecimator final=default;
            double sum=0,weighted=0;
            for(int i=0;i<200;i++)
            {
                for(int s=0;s<factor;s++)
                {
                    float impulse=i==0&&s==0?1:0;
                    if(factor==2)final.Push(impulse);
                    else { first.Push(impulse); if((s&1)==1)final.Push(first.Read()); }
                }
                float output=final.Read(); sum+=output; weighted+=i*output;
            }
            Assert.That(sum*factor,Is.EqualTo(1).Within(.00002));
            Assert.That(weighted/sum,Is.EqualTo(expectedDelay).Within(.0001));
        }
        [Test] public void InterpolationWrapsAtBothEnds()
        {
            for(int i=0;i<100;i++)
            {
                float phase=i/100f;
                Assert.That(VoiceWavetable.Sample(phase,.73f,110,48000),Is.EqualTo(VoiceWavetable.Sample(phase+1,.73f,110,48000)).Within(.00002f));
                Assert.That(VoiceWavetable.Sample(phase,.73f,110,48000),Is.EqualTo(VoiceWavetable.Sample(phase-1,.73f,110,48000)).Within(.00002f));
            }
            Assert.That(Math.Abs(VoiceWavetable.Sample(-.000001f,1,100,48000)-VoiceWavetable.Sample(.000001f,1,100,48000)),Is.LessThan(.002f));
        }
        [Test] public void MorphJoinsAndMipBoundariesAreContinuous()
        {
            for(int i=0;i<100;i++)
            {
                float phase=i/100f;
                Assert.That(Math.Abs(VoiceWavetable.Sample(phase,.500001f,440,48000)-VoiceWavetable.Sample(phase,.499999f,440,48000)),Is.LessThan(.00002f));
                foreach(int harmonics in new[]{127,63,31,15,7,3,1})
                {
                    float edge=.45f*48000/harmonics;
                    Assert.That(Math.Abs(VoiceWavetable.Sample(phase,.8f,edge*.99999f,48000)-VoiceWavetable.Sample(phase,.8f,edge*1.00001f,48000)),Is.LessThan(.0001f));
                }
            }
        }
        static double Harmonic(int harmonic,float position,float bandwidth)
        {
            double sum=0;
            for(int i=0;i<1024;i++)sum+=VoiceWavetable.Sample(i/1024f,position,bandwidth,48000)*Math.Sin(2*Math.PI*harmonic*i/1024);
            return sum*2/1024;
        }
        [Test] public void BrightMipContainsPartialsAndHighMipRemovesThem()
        {
            Assert.That(Math.Abs(Harmonic(7,.5f,55)),Is.GreaterThan(.02));
            Assert.That(Math.Abs(Harmonic(7,.5f,10000)),Is.LessThan(.000001));
            Assert.That(Math.Abs(Harmonic(1,.5f,10000)),Is.GreaterThan(.1));
            Assert.That(Math.Abs(Harmonic(2,1,55)),Is.LessThan(.000001));
            Assert.That(Math.Abs(Harmonic(3,1,55)),Is.GreaterThan(.02));
        }
        [Test] public void FrequencyMorphAndMalformedInputSweepsRemainFiniteAndBounded()
        {
            foreach(float rate in new[]{32000f,44100f,48000f,96000f,384000f})
            for(int i=0;i<2000;i++)
            {
                float value=VoiceWavetable.Sample(i*.0317f,(i%101)*.01f,(float)Math.Pow(2,i*.008),rate);
                Assert.That(float.IsNaN(value)||float.IsInfinity(value),Is.False);
                Assert.That(Math.Abs(value),Is.LessThanOrEqualTo(1.001f));
            }
            Assert.That(VoiceWavetable.Sample(float.NaN,float.PositiveInfinity,float.NaN,float.NegativeInfinity),Is.EqualTo(0));
        }
        [Test] public void WarmedTableReadsAllocateNothing()
        {
            VoiceWavetable.Warmup(); float sum=0;
            for(int i=0;i<1000;i++)sum+=VoiceWavetable.Sample(i*.001f,.7f,110,48000);
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<10000;i++)sum+=VoiceWavetable.Sample(i*.001f,.7f,110,48000);
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.That(allocated,Is.Zero); Assert.That(float.IsNaN(sum),Is.False);
        }
    }
}
