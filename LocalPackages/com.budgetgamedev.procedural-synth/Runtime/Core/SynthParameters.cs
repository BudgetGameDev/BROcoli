using System;
using Unity.Mathematics;
namespace BudgetGameDev.Synth
{
    public enum Waveform { Sine, Saw, Square, Triangle, Wavetable }
    public enum NotePriority { Last, Low, High }
    public enum SynthEventType { NoteOn, NoteOff, AllNotesOff, Parameter }
    public enum SynthParameterId { CutoffHz, Resonance, PreDrive, PostDrive, OutputGain, PhaseModCycles, EnvelopePhaseModCycles, CutoffModOctaves, WavetablePosition, NoiseLevel, GlideSeconds, FilterEnvelopeOctaves }
    [Serializable] public struct EnvelopeParameters
    {
        public float Attack, Decay, Sustain, Release;
        public EnvelopeParameters(float attack,float decay,float sustain,float release) { Attack=attack; Decay=decay; Sustain=sustain; Release=release; }
        public void Sanitize() { Attack=SynthParameters.Safe(Attack,.001f,10); Decay=SynthParameters.Safe(Decay,.001f,10); Sustain=SynthParameters.Safe(Sustain,0,1); Release=SynthParameters.Safe(Release,.001f,10); }
    }
    [Serializable] public struct OscillatorParameters
    {
        public Waveform Waveform;
        public int Octave, Semitone;
        public float Cents, Level;
        public OscillatorParameters(Waveform waveform,int octave,int semitone,float cents,float level) { Waveform=waveform; Octave=octave; Semitone=semitone; Cents=cents; Level=level; }
        public void Sanitize() { Octave=math.clamp(Octave,-3,3); Semitone=math.clamp(Semitone,-12,12); Cents=SynthParameters.Safe(Cents,-100,100); Level=SynthParameters.Safe(Level,0,1); if((uint)Waveform>4) Waveform=Waveform.Sine; }
    }
    [Serializable] public struct SynthParameters
    {
        public OscillatorParameters Oscillator1, Oscillator2, Oscillator3;
        public EnvelopeParameters AmpEnvelope, FilterEnvelope;
        public float SubLevel, NoiseLevel, DriftCents, CutoffHz, Resonance, PreDrive, PostDrive, OutputGain;
        public float FilterEnvelopeOctaves, GlideSeconds, PhaseModCycles, EnvelopePhaseModCycles, CutoffModOctaves, WavetablePosition;
        public bool Legato, ResetPhase;
        public NotePriority Priority;
        public static float Safe(float x,float min,float max) => math.isfinite(x)?math.clamp(x,min,max):min;
        public void Sanitize()
        {
            Oscillator1.Sanitize(); Oscillator2.Sanitize(); Oscillator3.Sanitize(); AmpEnvelope.Sanitize(); FilterEnvelope.Sanitize();
            SubLevel=Safe(SubLevel,0,1); NoiseLevel=Safe(NoiseLevel,0,1); DriftCents=Safe(DriftCents,0,5); CutoffHz=Safe(CutoffHz,20,18000);
            Resonance=Safe(Resonance,0,.95f); PreDrive=Safe(PreDrive,1,12); PostDrive=Safe(PostDrive,1,6); OutputGain=Safe(OutputGain,0,1);
            FilterEnvelopeOctaves=Safe(FilterEnvelopeOctaves,-4,6); GlideSeconds=Safe(GlideSeconds,0,2); PhaseModCycles=Safe(PhaseModCycles,0,.5f);
            EnvelopePhaseModCycles=Safe(EnvelopePhaseModCycles,0,.5f); CutoffModOctaves=Safe(CutoffModOctaves,0,3); WavetablePosition=Safe(WavetablePosition,0,1);
            if((uint)Priority>2) Priority=NotePriority.Last;
        }
        public void Set(SynthParameterId id,float value)
        {
            switch(id) { case SynthParameterId.CutoffHz: CutoffHz=value; break; case SynthParameterId.Resonance: Resonance=value; break;
            case SynthParameterId.PreDrive: PreDrive=value; break; case SynthParameterId.PostDrive: PostDrive=value; break; case SynthParameterId.OutputGain: OutputGain=value; break;
            case SynthParameterId.PhaseModCycles: PhaseModCycles=value; break; case SynthParameterId.EnvelopePhaseModCycles: EnvelopePhaseModCycles=value; break;
            case SynthParameterId.CutoffModOctaves: CutoffModOctaves=value; break; case SynthParameterId.WavetablePosition: WavetablePosition=value; break;
            case SynthParameterId.NoiseLevel: NoiseLevel=value; break; case SynthParameterId.GlideSeconds: GlideSeconds=value; break; case SynthParameterId.FilterEnvelopeOctaves: FilterEnvelopeOctaves=value; break; }
            Sanitize();
        }
        public static SynthParameters HeavyBass => new SynthParameters {
            Oscillator1=new OscillatorParameters(Waveform.Saw,0,0,0,.55f), Oscillator2=new OscillatorParameters(Waveform.Saw,-1,0,-7,.42f),
            Oscillator3=new OscillatorParameters(Waveform.Square,0,7,0,.12f), SubLevel=.35f, NoiseLevel=.006f, DriftCents=2,
            CutoffHz=180, Resonance=.25f, PreDrive=4, PostDrive=1.4f, OutputGain=.65f, FilterEnvelopeOctaves=2.5f,
            AmpEnvelope=new EnvelopeParameters(.003f,.4f,.85f,.08f), FilterEnvelope=new EnvelopeParameters(.002f,.18f,.2f,.09f),
            GlideSeconds=.07f, Legato=true, Priority=NotePriority.Last };
        public static SynthParameters CleanBass { get { var p=HeavyBass; p.PreDrive=1; p.PostDrive=1; return p; } }
        public static SynthParameters Acid { get { var p=HeavyBass; p.Oscillator2.Level=.1f; p.Oscillator3.Level=0; p.SubLevel=.08f; p.CutoffHz=330; p.Resonance=.82f; p.FilterEnvelopeOctaves=3.5f; p.FilterEnvelope.Decay=.12f; p.FilterEnvelope.Sustain=0; p.Legato=false; return p; } }
        public static SynthParameters MetallicGrowl { get { var p=HeavyBass; p.Oscillator1.Waveform=Waveform.Wavetable; p.WavetablePosition=.75f; p.CutoffHz=850; p.PhaseModCycles=.12f; p.EnvelopePhaseModCycles=.22f; p.CutoffModOctaves=1.2f; p.PreDrive=5; return p; } }
    }
    public struct SynthEvent
    {
        public long Sample;
        public SynthEventType Type;
        public int Note;
        public float Value;
        public SynthParameterId Parameter;
        public static SynthEvent On(long sample,int note,float velocity=1) => new SynthEvent {Sample=sample,Type=SynthEventType.NoteOn,Note=note,Value=velocity};
        public static SynthEvent Off(long sample,int note) => new SynthEvent {Sample=sample,Type=SynthEventType.NoteOff,Note=note};
    }
}
