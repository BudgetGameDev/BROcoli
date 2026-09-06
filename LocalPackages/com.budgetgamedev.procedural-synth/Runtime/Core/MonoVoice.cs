using Unity.Mathematics;

namespace BudgetGameDev.Synth
{
    /// <summary>Unmanaged mono voice. Events are applied by the owning engine before
    /// ProcessSample. All frequencies are Hz, pitch is MIDI semitones, PM is cycles.
    /// Default 4x processing includes oscillators, filter and both drive stages.
    /// Sub pitch follows the gliding played key minus one octave, with no osc tuning.
    /// No feedback modulation routes: osc2/3 are evaluated before osc1/filter.</summary>
    public unsafe struct MonoVoice
    {
        private SynthParameters target, smooth;
        private VoiceOscillator osc1, osc2, osc3, sub;
        private VoiceEnvelope amp, filterEnvelope;
        private NonlinearFilter24 filter;
        private DcBlocker dc;
        private VoiceHalfband halfband1;
        private VoiceOutputDecimator halfband2;
        private fixed ulong heldOrder[128];
        private fixed float heldVelocity[128];
        private ulong order;
        private uint noiseRandom;
        private int currentNote, sampleRate, oversampling;
        private float pitch, velocity, velocityTarget, smoothing;
        private float tune1, tune2, tune3;
        public int CurrentNote => currentNote;
        public float CurrentPitch => pitch;
        public float AmpLevel => amp.Level;
        public int Oversampling => oversampling;
        // FIR group delay is 35.25 / 31.5 base-rate periods; because each call
        // returns the last phase of its 4/2-sample block, the output-index delay
        // of an impulse on its first internal sample is 34.5 / 31 samples.
        public float OutputLatencySamples => oversampling==4?34.5f:oversampling==2?31:0;

        public void Initialize(int sampleRate,uint seed,SynthParameters parameters) => Initialize(sampleRate,seed,parameters,4);
        // Quality is initialization-only, never changed while rendering. 1x is
        // exposed for validation and deliberately permits greater aliasing.
        public void Initialize(int rate,uint seed,SynthParameters parameters,int quality)
        {
            this=default;
            sampleRate=math.clamp(rate,32000,96000);
            oversampling=quality==1?1:quality==2?2:4;
            currentNote=-1; pitch=48;
            parameters.Sanitize(); target=smooth=parameters;
            smoothing=1-math.exp(-1f/(.005f*sampleRate));
            tune1=Tuning(parameters.Oscillator1); tune2=Tuning(parameters.Oscillator2); tune3=Tuning(parameters.Oscillator3);
            osc1.Initialize(seed^0xD1B54A35u); osc2.Initialize(seed^0x94D049BBu);
            osc3.Initialize(seed^0x8538ECB5u); sub.Initialize(seed^0xA341316Cu);
            noiseRandom=seed^0xC8013EA4u; if(noiseRandom==0) noiseRandom=1;
            filter.Reset(); dc.Reset();
            VoiceWavetable.Warmup();
            VoiceOutputDecimator.Warmup();
        }
        public void SetParameters(in SynthParameters parameters)
        {
            var previousPriority=target.Priority;
            target=parameters; target.Sanitize();
            if(target.Priority!=previousPriority && currentNote>=0) SelectNote(false);
        }
        public void NoteOn(int midi,float noteVelocity)
        {
            if((uint)midi>127) return;
            noteVelocity=SynthParameters.Safe(noteVelocity,0,1);
            if(noteVelocity<=0) { NoteOff(midi); return; }
            bool wasSilent=currentNote<0;
            heldOrder[midi]=++order; heldVelocity[midi]=noteVelocity;
            SelectNote(wasSilent, midi);
        }
        public void NoteOff(int midi)
        {
            if((uint)midi>127 || heldOrder[midi]==0) return;
            heldOrder[midi]=0;
            if(currentNote==midi) SelectNote(false);
        }
        public void AllNotesOff()
        {
            for(int i=0;i<128;i++) heldOrder[i]=0;
            if(currentNote>=0) Release();
            currentNote=-1;
        }
        private void SelectNote(bool initial,int pressedNote=-1)
        {
            int selected=-1; ulong newest=0;
            for(int i=0;i<128;i++)
            {
                ulong stamp=heldOrder[i]; if(stamp==0) continue;
                if(target.Priority==NotePriority.Low) { selected=i; break; }
                if(target.Priority==NotePriority.High || stamp>newest) { selected=i; newest=stamp; }
            }
            if(selected<0) { if(currentNote>=0) Release(); currentNote=-1; return; }
            bool changed=selected!=currentNote;
            // Inactive key presses/releases do not affect the sounding envelope.
            bool retrigger=initial || (!target.Legato && (changed || selected==pressedNote));
            currentNote=selected; velocityTarget=heldVelocity[selected];
            if(initial) pitch=selected;
            if(retrigger)
            {
                float rate=sampleRate*oversampling;
                amp.Trigger(target.AmpEnvelope,rate); filterEnvelope.Trigger(target.FilterEnvelope,rate);
                if(target.ResetPhase) { osc1.Phase=0; osc2.Phase=0; osc3.Phase=0; sub.Phase=0; }
            }
        }
        private void Release()
        {
            amp.Release(target.AmpEnvelope,sampleRate*oversampling);
            filterEnvelope.Release(target.FilterEnvelope,sampleRate*oversampling);
        }
        private static float Tuning(OscillatorParameters p) => p.Octave*12+p.Semitone+p.Cents*.01f;
        private float Slew(float current,float desired) => current+(desired-current)*smoothing;
        private void SmoothControls()
        {
            smooth.Oscillator1.Level=Slew(smooth.Oscillator1.Level,target.Oscillator1.Level);
            smooth.Oscillator2.Level=Slew(smooth.Oscillator2.Level,target.Oscillator2.Level);
            smooth.Oscillator3.Level=Slew(smooth.Oscillator3.Level,target.Oscillator3.Level);
            smooth.SubLevel=Slew(smooth.SubLevel,target.SubLevel); smooth.NoiseLevel=Slew(smooth.NoiseLevel,target.NoiseLevel);
            smooth.DriftCents=Slew(smooth.DriftCents,target.DriftCents); smooth.CutoffHz=Slew(smooth.CutoffHz,target.CutoffHz);
            smooth.Resonance=Slew(smooth.Resonance,target.Resonance); smooth.PreDrive=Slew(smooth.PreDrive,target.PreDrive);
            smooth.PostDrive=Slew(smooth.PostDrive,target.PostDrive); smooth.OutputGain=Slew(smooth.OutputGain,target.OutputGain);
            smooth.FilterEnvelopeOctaves=Slew(smooth.FilterEnvelopeOctaves,target.FilterEnvelopeOctaves);
            smooth.PhaseModCycles=Slew(smooth.PhaseModCycles,target.PhaseModCycles);
            smooth.EnvelopePhaseModCycles=Slew(smooth.EnvelopePhaseModCycles,target.EnvelopePhaseModCycles);
            smooth.CutoffModOctaves=Slew(smooth.CutoffModOctaves,target.CutoffModOctaves);
            smooth.WavetablePosition=Slew(smooth.WavetablePosition,target.WavetablePosition);
            smooth.GlideSeconds=Slew(smooth.GlideSeconds,target.GlideSeconds);
            smooth.AmpEnvelope.Attack=target.AmpEnvelope.Attack; smooth.AmpEnvelope.Decay=target.AmpEnvelope.Decay; smooth.AmpEnvelope.Release=target.AmpEnvelope.Release;
            smooth.FilterEnvelope.Attack=target.FilterEnvelope.Attack; smooth.FilterEnvelope.Decay=target.FilterEnvelope.Decay; smooth.FilterEnvelope.Release=target.FilterEnvelope.Release;
            smooth.AmpEnvelope.Sustain=Slew(smooth.AmpEnvelope.Sustain,target.AmpEnvelope.Sustain);
            smooth.FilterEnvelope.Sustain=Slew(smooth.FilterEnvelope.Sustain,target.FilterEnvelope.Sustain);
            tune1=Slew(tune1,Tuning(target.Oscillator1)); tune2=Slew(tune2,Tuning(target.Oscillator2)); tune3=Slew(tune3,Tuning(target.Oscillator3));
            velocity=Slew(velocity,velocityTarget);
        }
        public float ProcessSample()
        {
            SmoothControls();
            // GlideSeconds is a 99% settling duration in MIDI pitch space.
            if(currentNote>=0)
            {
                float coefficient=smooth.GlideSeconds<.0001f?1:1-math.exp(-4.605170186f/(smooth.GlideSeconds*sampleRate));
                pitch+=(currentNote-pitch)*coefficient;
            }
            float drift1=osc1.UpdateDrift(sampleRate)*smooth.DriftCents*.01f;
            float drift2=osc2.UpdateDrift(sampleRate)*smooth.DriftCents*.01f;
            float drift3=osc3.UpdateDrift(sampleRate)*smooth.DriftCents*.01f;
            float f1=Frequency(pitch+tune1+drift1), f2=Frequency(pitch+tune2+drift2), f3=Frequency(pitch+tune3+drift3);
            float fSub=Frequency(pitch-12), internalRate=sampleRate*oversampling;
            float result=0;
            for(int s=0;s<oversampling;s++)
            {
                float filterEnv=filterEnvelope.Process(smooth.FilterEnvelope,internalRate);
                float ampEnv=amp.Process(smooth.AmpEnvelope,internalRate);
                float source2=osc2.Process(target.Oscillator2.Waveform,f2,internalRate,0,smooth.WavetablePosition);
                float source3=osc3.Process(target.Oscillator3.Waveform,f3,internalRate,0,smooth.WavetablePosition);
                float depth=math.min(.5f,smooth.PhaseModCycles+filterEnv*smooth.EnvelopePhaseModCycles);
                // Carson-style PM bandwidth estimate, with extra headroom for
                // harmonically rich modulators. Arbitrary saw/square PM has wider
                // sidebands; oversampling reduces them but cannot eliminate them.
                float modulatorBandwidth=target.Oscillator2.Waveform==Waveform.Sine?f2:f2*8;
                float pmBandwidth=depth<=0?f1:f1+(1+2*math.PI*depth)*modulatorBandwidth;
                float source1=osc1.Process(target.Oscillator1.Waveform,f1,internalRate,source2*depth,smooth.WavetablePosition,pmBandwidth);
                float subValue=sub.Process(Waveform.Sine,fSub,internalRate);
                float noise=VoiceOscillator.NextRandom(ref noiseRandom);
                // Fixed gain compensation preserves individual mixer gain controls
                // without normalizing away level changes or pulling down the sub.
                float mix=.45f*(source1*smooth.Oscillator1.Level+source2*smooth.Oscillator2.Level+source3*smooth.Oscillator3.Level+subValue*smooth.SubLevel+noise*smooth.NoiseLevel);
                float octaves=filterEnv*smooth.FilterEnvelopeOctaves+source3*smooth.CutoffModOctaves;
                float cutoff=math.clamp(smooth.CutoffHz*math.exp2(octaves),20,math.min(18000,sampleRate*.4f));
                // Saturating input feedback solver supplies the pre-filter drive
                // nonlinearity; do not double-clip its input here.
                float filtered=filter.Process(mix*smooth.PreDrive,cutoff,smooth.Resonance,internalRate);
                float driven=Saturation.SoftClip(filtered*smooth.PostDrive);
                float value=driven*ampEnv*velocity*smooth.OutputGain;
                if(oversampling==1) result=value;
                else if(oversampling==2)
                {
                    halfband2.Push(value);
                    if(s==1) result=halfband2.Read();
                }
                else
                {
                    halfband1.Push(value);
                    if((s&1)==1)
                    {
                        float down=halfband1.Read();
                        halfband2.Push(down); if(s==3) result=halfband2.Read();
                    }
                }
            }
            result=dc.Process(result,sampleRate);
            // Normal signal is already saturated upstream. This last guard only
            // bounds FIR/DC overshoot and corrupt output, without extra distortion.
            return math.isfinite(result)?math.clamp(result,-1,1):0;
        }
        private float Frequency(float midi) => math.min(440*math.exp2((midi-69)/12),sampleRate*.45f);
    }
}
