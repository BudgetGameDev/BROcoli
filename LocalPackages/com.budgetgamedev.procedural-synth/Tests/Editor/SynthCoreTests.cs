using System;
using NUnit.Framework;
namespace BudgetGameDev.Synth.Tests
{
    public class SynthCoreTests
    {
        static SynthParameters TestPreset() { var p=SynthParameters.HeavyBass; p.DriftCents=0; p.GlideSeconds=0; return p; }
        [Test] public void EventsApplyBeforeExactSampleAndSameTimeOrderIsStable()
        {
            var e=new SynthEngine(); e.Initialize(48000,1,TestPreset()); e.Enqueue(SynthEvent.On(37,48));
            for(int i=0;i<37;i++) { Assert.That(e.ProcessSample(),Is.Zero); Assert.That(e.VoiceNote,Is.EqualTo(-1)); }
            e.ProcessSample(); Assert.That(e.VoiceNote,Is.EqualTo(48)); Assert.That(e.SamplePosition,Is.EqualTo(38));
            e.Enqueue(SynthEvent.On(38,52)); e.Enqueue(SynthEvent.Off(38,52)); e.ProcessSample(); Assert.That(e.VoiceNote,Is.EqualTo(48));
            e.Enqueue(SynthEvent.On(0,60)); e.ProcessSample(); Assert.That(e.LateEvents,Is.EqualTo(1)); Assert.That(e.VoiceNote,Is.EqualTo(60));
        }
        [TestCase(NotePriority.Last,60)] [TestCase(NotePriority.Low,36)] [TestCase(NotePriority.High,60)]
        public void OverlapAndInactiveReleaseRespectPriority(NotePriority priority,int expected)
        {
            var p=TestPreset(); p.Priority=priority; var v=new MonoVoice(); v.Initialize(48000,2,p);
            v.NoteOn(48,1); v.NoteOn(36,.7f); v.NoteOn(60,.8f); Assert.That(v.CurrentNote,Is.EqualTo(expected));
            v.NoteOff(48); Assert.That(v.CurrentNote,Is.EqualTo(expected)); v.NoteOff(60); Assert.That(v.CurrentNote,Is.EqualTo(36));
            v.NoteOff(36); for(int i=0;i<48000;i++) v.ProcessSample(); Assert.That(v.AmpLevel,Is.LessThan(.0001f));
        }
        [Test] public void DuplicateNoteAndUnknownReleaseDoNotStick()
        {
            var v=new MonoVoice(); v.Initialize(48000,1,TestPreset()); v.NoteOn(48,1); v.NoteOn(48,.5f); v.NoteOff(99); Assert.That(v.CurrentNote,Is.EqualTo(48));
            v.NoteOff(48); for(int i=0;i<48000;i++) v.ProcessSample(); Assert.That(v.AmpLevel,Is.LessThan(.0001f));
        }
        [Test] public void GlideIsMonotonicAndLegatoPreservesEnvelope()
        {
            var p=TestPreset(); p.GlideSeconds=.07f; var v=new MonoVoice(); v.Initialize(48000,1,p); v.NoteOn(36,1);
            for(int i=0;i<1000;i++) v.ProcessSample(); float before=v.AmpLevel; v.NoteOn(48,1); float previous=v.CurrentPitch;
            for(int i=0;i<3360;i++) { v.ProcessSample(); Assert.That(v.CurrentPitch,Is.GreaterThanOrEqualTo(previous-1e-5f)); previous=v.CurrentPitch; }
            Assert.That(previous,Is.GreaterThan(36).And.LessThanOrEqualTo(48)); Assert.That(v.AmpLevel,Is.GreaterThan(before*.75f));
        }
        [Test] public void OverflowClearsPendingPairsAndReleasesVoice()
        {
            var e=new SynthEngine(); e.Initialize(48000,1,TestPreset()); e.Enqueue(SynthEvent.On(0,48)); e.ProcessSample();
            for(int i=0;i<SynthEngine.EventCapacity;i++) Assert.That(e.Enqueue(SynthEvent.On(99999+i,48)),Is.True);
            Assert.That(e.Enqueue(SynthEvent.Off(1,48)),Is.False); e.ProcessSample(); Assert.That(e.DroppedEvents,Is.EqualTo(1)); Assert.That(e.PendingEvents,Is.Zero);
            for(int i=0;i<48000;i++) e.ProcessSample(); Assert.That(e.AmpLevel,Is.LessThan(.0001f));
        }
        [Test] public void SeedAndEventsReproduceExactlyWithinRuntime()
        {
            var a=new SynthEngine(); var b=new SynthEngine(); a.Initialize(48000,42,SynthParameters.HeavyBass); b.Initialize(48000,42,SynthParameters.HeavyBass);
            a.Enqueue(SynthEvent.On(17,36)); b.Enqueue(SynthEvent.On(17,36));
            for(int i=0;i<8192;i++) Assert.That(a.ProcessSample(),Is.EqualTo(b.ProcessSample()));
        }
        [TestCase(32000)] [TestCase(44100)] [TestCase(48000)] [TestCase(96000)]
        public void ParameterExtremesStayFiniteAndProtected(int rate)
        {
            var e=new SynthEngine(); var p=SynthParameters.HeavyBass; e.Initialize(rate,17,p); e.Enqueue(SynthEvent.On(0,127));
            for(int block=0;block<24;block++) {
                p.CutoffHz=(block%2==0)?20:18000; p.Resonance=.95f; p.PreDrive=12; p.PostDrive=6; p.PhaseModCycles=.5f; p.EnvelopePhaseModCycles=.5f; p.CutoffModOctaves=3;
                p.Oscillator1.Level=p.Oscillator2.Level=p.Oscillator3.Level=p.SubLevel=p.NoiseLevel=1;
                e.SetParameters(p); e.Enqueue(SynthEvent.On(e.SamplePosition,block*5));
                for(int i=0;i<1024;i++) { float x=e.ProcessSample(); Assert.That(float.IsNaN(x)||float.IsInfinity(x),Is.False); Assert.That(Math.Abs(x),Is.LessThanOrEqualTo(1)); }
            }
            p.CutoffHz=float.NaN; p.OutputGain=float.PositiveInfinity; p.PreDrive=float.NegativeInfinity; e.SetParameters(p);
            for(int i=0;i<1024;i++) Assert.That(float.IsNaN(e.ProcessSample()),Is.False);
        }
        [Test] public void RenderingAllocatesNoManagedBytesAfterWarmup()
        {
            var e=new SynthEngine(); e.Initialize(48000,1,SynthParameters.MetallicGrowl); e.Enqueue(SynthEvent.On(0,36)); for(int i=0;i<4096;i++) e.ProcessSample();
            long before=GC.GetAllocatedBytesForCurrentThread(); float sum=0; for(int i=0;i<48000;i++) sum+=e.ProcessSample(); long delta=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.That(delta,Is.Zero); Assert.That(float.IsNaN(sum),Is.False);
        }
    }
}
