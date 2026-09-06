using Unity.Mathematics;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("BudgetGameDev.Synth.Tests")]

namespace BudgetGameDev.Synth
{
    // Time parameters are stage durations. Exponential stages traverse 99.9% of
    // their distance in that duration, then reach their exact endpoint.
    internal struct VoiceEnvelope
    {
        public float Level;
        private int stage, remaining, stageTotal;
        private float increment, coefficient, endpoint;
        public void Trigger(in EnvelopeParameters p, float rate)
        {
            stage=1; remaining=stageTotal=math.max(1,(int)(p.Attack*rate));
            increment=(1-Level)/remaining;
        }
        public void Release(in EnvelopeParameters p,float rate)
        {
            stage=4; remaining=stageTotal=math.max(1,(int)(p.Release*rate)); endpoint=0;
            coefficient=math.exp(-6.907755279f/remaining);
        }
        public float Process(in EnvelopeParameters p,float rate)
        {
            if(stage==1 || stage==2 || stage==4)
            {
                int duration=math.max(1,(int)((stage==1?p.Attack:stage==2?p.Decay:p.Release)*rate));
                if(duration!=stageTotal)
                {
                    remaining=math.max(1,(int)(remaining*(float)duration/stageTotal)); stageTotal=duration;
                    increment=(1-Level)/remaining; coefficient=math.exp(-6.907755279f/duration);
                }
            }
            if(stage==2) endpoint=p.Sustain;
            if(stage==1)
            {
                Level+=increment;
                if(--remaining<=0) { Level=1; stage=2; remaining=stageTotal=math.max(1,(int)(p.Decay*rate)); coefficient=math.exp(-6.907755279f/remaining); endpoint=p.Sustain; }
            }
            else if(stage==2 || stage==4)
            {
                Level=endpoint+(Level-endpoint)*coefficient;
                if(--remaining<=0) { Level=endpoint; stage=stage==2?3:0; }
            }
            else if(stage==3) Level=p.Sustain;
            return Level;
        }
    }

    internal struct VoiceOscillator
    {
        public float Phase;
        private float drift, driftTarget;
        private int driftCountdown;
        private uint random;
        private Waveform currentWave, previousWave;
        private float waveformBlend;
        private bool waveformInitialized;
        public void Initialize(uint seed)
        {
            random=seed==0?1u:seed;
            Phase=NextRandom(ref random)*.5f+.5f;
            driftCountdown=0; drift=driftTarget=0;
        }
        public static float NextRandom(ref uint state)
        {
            state^=state<<13; state^=state>>17; state^=state<<5;
            return (state>>8)*(1f/8388608f)-1;
        }
        public float UpdateDrift(float sampleRate)
        {
            if(--driftCountdown<=0) { driftTarget=NextRandom(ref random); driftCountdown=(int)(sampleRate*(1.25f+.75f*NextRandom(ref random))); }
            drift+=(driftTarget-drift)*(1f/(.35f*sampleRate));
            return drift;
        }
        public float Process(Waveform waveform,float frequency,float rate,float phaseOffset=0,float wavetablePosition=0,float bandwidthHz=0)
        {
            float delta=math.clamp(frequency/rate,0,.45f);
            float p=Phase+phaseOffset; p-=math.floor(p);
            if(!waveformInitialized) { currentWave=previousWave=waveform; waveformBlend=1; waveformInitialized=true; }
            // Finish the current fade before accepting the latest target. Dropping
            // a partially audible mixture during rapid edits would introduce a
            // discontinuity. No queue: the caller's latest desired waveform wins.
            if(waveform!=currentWave && waveformBlend>=1) { previousWave=currentWave; currentWave=waveform; waveformBlend=0; }
            float bandwidth=math.max(frequency,bandwidthHz);
            float value=Evaluate(currentWave,p,delta,wavetablePosition,bandwidth,rate);
            if(waveformBlend<1)
            {
                waveformBlend=math.min(1,waveformBlend+1f/(.005f*rate));
                value=math.lerp(Evaluate(previousWave,p,delta,wavetablePosition,bandwidth,rate),value,waveformBlend);
            }
            Phase+=delta; Phase-=math.floor(Phase);
            return value;
        }
        private static float Evaluate(Waveform waveform,float p,float delta,float wavetablePosition,float bandwidth,float rate)
        {
            float value;
            switch(waveform)
            {
                case Waveform.Wavetable: value=VoiceWavetable.Sample(p,wavetablePosition,bandwidth,rate); break;
                case Waveform.Saw: value=2*p-1-PolyBlep(p,delta); break;
                case Waveform.Square:
                    float q=p+.5f; q-=math.floor(q);
                    value=(p<.5f?1:-1)+PolyBlep(p,delta)-PolyBlep(q,delta); break;
                case Waveform.Triangle:
                    // Bounded additive triangle; at most 8 odd partials. Unlike
                    // integrating a square, this has no low-frequency drift.
                    value=0;
                    for(int h=1;h<=15;h+=2)
                        if(h*delta<.48f) value+=(h%4==1?1:-1)*math.sin(2*math.PI*p*h)/(h*h);
                    value*=.810569469f; break;
                default: value=math.sin(2*math.PI*p); break;
            }
            return value;
        }
        private static float PolyBlep(float t,float dt)
        {
            if(dt<=0) return 0;
            if(t<dt) { t/=dt; return 2*t-t*t-1; }
            if(t>1-dt) { t=(t-1)/dt; return t*t+2*t+1; }
            return 0;
        }
    }

    // 31-tap normalized Blackman-windowed halfband FIR. Pass every input sample
    // to Push, then Read after each pair. Used as the first stage of 4:1
    // decimation; the longer final-stage lowpass below protects output Nyquist.
    internal unsafe struct VoiceHalfband
    {
        private fixed float history[31];
        private int head;
        public void Push(float value) { history[head]=value; if(++head==31) head=0; }
        private float At(int delay) { int i=head-1-delay; if(i<0) i+=31; return history[i]; }
        public float Read()
        {
            return .000410322871f*(At(2)+At(28))
                -.002230285519f*(At(4)+At(26))
                +.007100857132f*(At(6)+At(24))
                -.017917030422f*(At(8)+At(22))
                +.040107417651f*(At(10)+At(20))
                -.090106922080f*(At(12)+At(18))
                +.312633321621f*(At(14)+At(16))
                +.500004637491f*At(15);
        }
    }
    // Final 2:1 decimator: 127-tap Blackman-windowed sinc, cutoff .225 of
    // input rate (21.6kHz at 48kHz output). This moves the transition below
    // output Nyquist, unlike a halfband whose -6dB point sits at Nyquist.
    // Symmetry halves the multiply count; 508 bytes state per voice.
    internal unsafe struct VoiceOutputDecimator
    {
        private fixed float history[127];
        private int head;
        public static float Warmup() => Coefficients[0];
        public void Push(float value) { history[head]=value; if(++head==127) head=0; }
        private float At(int delay) { int i=head-1-delay; if(i<0) i+=127; return history[i]; }
        public float Read()
        {
            float result=Coefficients[63]*At(63);
            for(int i=0;i<63;i++) result+=Coefficients[i]*(At(i)+At(126-i));
            return result;
        }
        private static readonly float[] Coefficients = new float[] {
            -6.247559151e-20f,-3.553785776e-07f,-4.63036385e-06f,7.388560755e-20f,1.935289676e-05f,9.699080203e-06f,-4.136165287e-05f,-3.821218609e-05f,
            6.187967383e-05f,9.24906591e-05f,-6.623694823e-05f,-0.0001737871481f,3.525447347e-05f,0.0002743891779f,5.169455034e-05f,-0.0003749598798f,
            -0.0002118053047f,0.0004434323253f,0.0004524257349f,-0.0004364871993f,-0.0007639420423f,0.0003044571833f,0.001113366274f,-2.831618639e-18f,
            -0.001440436234f,-0.0005098989199f,0.001658130078f,0.001229970109f,-0.001659156326f,-0.002123022851f,0.001329174747f,0.003099525714f,
            -0.0005663168548f,-0.00401344516f,-0.0006947989278f,0.004666900157f,0.002457641664f,-0.00482506325f,-0.004640881539f,0.004241087064f,
            0.00706214477f,-0.002688915285f,-0.009431721774f,1.15489714e-17f,0.01135766123f,0.003901425657f,-0.01236057498f,-0.008970446199f,
            0.0118917342f,0.01502642913f,-0.009340136f,-0.02175488385f,0.003997792408f,0.02872790669f,0.005092126381f,-0.03544205735f,
            -0.01963533626f,0.04136963057f,0.04388122065f,-0.04601688615f,-0.09367469069f,0.04898133611f,0.3140703373f,0.4499996601f,
        };
    }
}
