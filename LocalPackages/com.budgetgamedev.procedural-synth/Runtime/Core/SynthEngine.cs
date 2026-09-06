using Unity.Mathematics;
namespace BudgetGameDev.Synth
{
    /// <summary>Single audio-owner engine. Only the owner calls these methods; the adapter transports value messages.</summary>
    public unsafe struct SynthEngine
    {
        public const int EventCapacity=128;
        private MonoVoice voice;
        private SynthParameters parameters;
        private fixed long times[EventCapacity];
        private fixed int types[EventCapacity], notes[EventCapacity], ids[EventCapacity];
        private fixed float values[EventCapacity];
        private int count;
        private bool panic;
        public long SamplePosition { get; private set; }
        public int DroppedEvents { get; private set; }
        public int LateEvents { get; private set; }
        public int PendingEvents => count;
        public int VoiceNote => voice.CurrentNote;
        public float VoicePitch => voice.CurrentPitch;
        public float AmpLevel => voice.AmpLevel;
        public void Initialize(int sampleRate,uint seed,SynthParameters preset)
        {
            this=default;
            parameters=preset; parameters.Sanitize();
            voice.Initialize(math.clamp(sampleRate,32000,96000),seed,parameters);
        }
        public void SetParameters(in SynthParameters preset) { parameters=preset; parameters.Sanitize(); voice.SetParameters(parameters); }
        public bool Enqueue(in SynthEvent e)
        {
            if(count==EventCapacity) { DroppedEvents++; panic=true; return false; }
            long time=e.Sample;
            if(time<SamplePosition) { time=SamplePosition; LateEvents++; }
            int i=count++;
            while(i>0 && times[i-1]>time) { Copy(i-1,i); i--; }
            times[i]=time; types[i]=(int)e.Type; notes[i]=e.Note; values[i]=e.Value; ids[i]=(int)e.Parameter;
            return true;
        }
        public void Panic() { count=0; panic=false; voice.AllNotesOff(); }
        private void Copy(int from,int to) { times[to]=times[from]; types[to]=types[from]; notes[to]=notes[from]; values[to]=values[from]; ids[to]=ids[from]; }
        public float ProcessSample()
        {
            // Overflow invalidates future note pairs too: discard bounded queue and release the voice.
            if(panic) { panic=false; count=0; voice.AllNotesOff(); }
            int consumed=0;
            while(consumed<count && times[consumed]<=SamplePosition)
            {
                switch((SynthEventType)types[consumed]) {
                    case SynthEventType.NoteOn: voice.NoteOn(notes[consumed],values[consumed]); break;
                    case SynthEventType.NoteOff: voice.NoteOff(notes[consumed]); break;
                    case SynthEventType.AllNotesOff: voice.AllNotesOff(); break;
                    case SynthEventType.Parameter: parameters.Set((SynthParameterId)ids[consumed],values[consumed]); voice.SetParameters(parameters); break;
                }
                consumed++;
            }
            if(consumed>0) { count-=consumed; for(int i=0;i<count;i++) Copy(i+consumed,i); }
            float value=voice.ProcessSample(); SamplePosition++; return value;
        }
    }
}
