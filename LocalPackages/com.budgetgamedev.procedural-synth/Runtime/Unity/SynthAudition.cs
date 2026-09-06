using UnityEngine;

namespace BudgetGameDev.Synth
{
    /// <summary>Main-thread audition and fixed repeatable bass pattern. Adaptive composition is separate.</summary>
    [RequireComponent(typeof(MonoSynthGenerator))]
    public sealed class SynthAudition : MonoBehaviour
    {
        public bool sequenceOnStart=true;
        [Range(40,200)] public float tempo=112;
        public int rootNote=36;
        private MonoSynthGenerator synth;
        private bool sequence;
        private bool previousRunInBackground;
        private Vector2 scroll;
        private long nextStep;
        private int step;
        private long manualLead;
        private readonly int[] pattern={0,0,12,0,7,0,10,7,0,0,12,15,7,10,7,-1};
        private readonly KeyCode[] keys={KeyCode.A,KeyCode.W,KeyCode.S,KeyCode.E,KeyCode.D,KeyCode.F,KeyCode.T,KeyCode.G,KeyCode.Y,KeyCode.H,KeyCode.U,KeyCode.J,KeyCode.K};
        private readonly bool[] held=new bool[13];
        private readonly int[] heldNotes=new int[13];
        private static readonly string[] waveformNames={"Sine","Saw","Square","Triangle","Wavetable"};
        public bool SequencePlaying => sequence;
        private void Awake() { synth=GetComponent<MonoSynthGenerator>(); }
        private void OnEnable() { previousRunInBackground=Application.runInBackground; Application.runInBackground=true; if(synth==null)synth=GetComponent<MonoSynthGenerator>(); synth.ResetTimeline+=OnReset; sequence=sequenceOnStart; }
        private void OnDisable() { Application.runInBackground=previousRunInBackground; if(synth!=null) { synth.ResetTimeline-=OnReset; synth.Panic(); } }
        private void OnReset() { AudioSettings.GetDSPBufferSize(out int frames,out int buffers); manualLead=frames+synth.SampleRate/60; nextStep=synth.SamplePosition+LookAhead; step=0; System.Array.Clear(held,0,held.Length); }
        // Two video frames can elapse while a pipe packet is acknowledged: schedule >=100ms ahead.
        private long LookAhead => (long)(synth.SampleRate*.15);
        public void SetSequence(bool enabled)
        {
            sequence=enabled; synth.Panic(); OnReset();
        }
        private void Update()
        {
            if (!synth.Ready) return;
            // Frame reads of Unity input stay outside DSP. Keyboard works with project's Both input setting.
#if ENABLE_LEGACY_INPUT_MANAGER
            for(int i=0;i<keys.Length;i++)
            {
                if(Input.GetKeyDown(keys[i]))
                {
                    // The engine intentionally tracks MIDI keys, not source-specific note IDs.
                    // Manual audition takes over the voice and cancels future sequence releases.
                    if(sequence)SetSequence(false);
                    held[i]=true; heldNotes[i]=Mathf.Clamp(rootNote+i,0,127);
                    synth.QueueEvent(SynthEvent.On(synth.SamplePosition+manualLead,heldNotes[i],.85f));
                }
                if(Input.GetKeyUp(keys[i]) && held[i]) { held[i]=false; synth.QueueEvent(SynthEvent.Off(synth.SamplePosition+manualLead,heldNotes[i])); }
            }
#endif
            if(!sequence) return;
            long duration=(long)(synth.SampleRate*60.0/Mathf.Clamp(tempo,40,200)/4);
            long horizon=synth.SamplePosition+LookAhead*2;
            if(nextStep<synth.SamplePosition) nextStep=synth.SamplePosition+LookAhead;
            // Bounded lookahead independent of frame stalls; no events scheduled from the audio callback.
            for(int n=0;n<16 && nextStep<horizon;n++)
            {
                int offset=pattern[step%pattern.Length];
                if(offset>=0)
                {
                    int note=rootNote+offset;
                    synth.QueueEvent(SynthEvent.On(nextStep,note,step%4==0?1f:.72f));
                    double gate=step%4==2?1.15:.72;
                    // Allow legato across distinct pitches. A previous same-key release must
                    // precede that key's next NoteOn because duplicate NoteOn replaces the key.
                    if(pattern[(step+1)%pattern.Length]==offset)gate=System.Math.Min(gate,.99);
                    synth.QueueEvent(SynthEvent.Off(nextStep+(long)(duration*gate),note));
                }
                nextStep+=duration; step++;
            }
        }
        private void OnGUI()
        {
            GUI.backgroundColor=new Color(.15f,.19f,.17f);
            GUILayout.BeginArea(new Rect(24,24,500,Screen.height-48),GUI.skin.box);
            scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label("BROCOLI / MODULAR MONO");
            GUILayout.Label("Three oscillators · nonlinear 24 dB low-pass · audio-rate expression");
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Heavy bass"))synth.SetPreset(SynthParameters.HeavyBass);
            if(GUILayout.Button("Cleaner"))synth.SetPreset(SynthParameters.CleanBass);
            if(GUILayout.Button("Acid"))synth.SetPreset(SynthParameters.Acid);
            if(GUILayout.Button("Metallic"))synth.SetPreset(SynthParameters.MetallicGrowl);
            GUILayout.EndHorizontal();
            var p=synth.parameters;
            bool changed=false;
            GUILayout.Label("Oscillator 1 source");
            int waveform=GUILayout.SelectionGrid((int)p.Oscillator1.Waveform,waveformNames,5);
            if(waveform!=(int)p.Oscillator1.Waveform){p.Oscillator1.Waveform=(Waveform)waveform;changed=true;}
            changed|=Slider("Wavetable position",ref p.WavetablePosition,0,1);
            changed|=Slider("Cutoff Hz",ref p.CutoffHz,20,6000);
            changed|=Slider("Resonance",ref p.Resonance,0,.95f);
            changed|=Slider("Pre-filter drive",ref p.PreDrive,1,12);
            changed|=Slider("Output",ref p.OutputGain,0,1);
            changed|=Slider("Glide seconds",ref p.GlideSeconds,0,.4f);
            changed|=Slider("Osc 2 → 1 phase modulation / cycles",ref p.PhaseModCycles,0,.5f);
            changed|=Slider("Filter envelope → PM / cycles",ref p.EnvelopePhaseModCycles,0,.5f);
            changed|=Slider("Osc 3 → cutoff / octaves",ref p.CutoffModOctaves,0,3);
            bool legato=GUILayout.Toggle(p.Legato,"Legato (retrigger when disabled)");
            if(legato!=p.Legato){p.Legato=legato;changed=true;}
            if(changed)synth.SetPreset(p);
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(sequence?"Stop sequence":"Play bass sequence"))SetSequence(!sequence);
            if(GUILayout.Button("Panic")){sequence=false;synth.Panic();}
            if(GUILayout.Button("Restart audio")){synth.StopAudio();synth.StartAudio();}
            GUILayout.EndHorizontal();
            synth.integrationSine=GUILayout.Toggle(synth.integrationSine,"Integration proof: 110 Hz sine at −22 dBFS peak");
            GUILayout.Label("Audition: A W S E D F T G Y H U J K (C2–C3). Playing a key stops the sequence.");
            GUILayout.Label($"{synth.SampleRate} Hz · sample {synth.SamplePosition} · epoch {synth.Epoch} · peak {synth.Peak:F3}");
            GUILayout.Label($"Late {synth.LateEvents} · DSP drops {synth.DroppedEvents} · control overflow {synth.ControlOverflows}");
            GUILayout.Label("Native Unity 6.5 SAP. Mono AudioSource → Brocoli Ambience mixer → listener.");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        private static bool Slider(string label,ref float value,float min,float max)
        {
            GUILayout.Label($"{label}: {value:0.###}");
            float next=GUILayout.HorizontalSlider(value,min,max);
            if(Mathf.Approximately(next,value))return false; value=next; return true;
        }
    }
}
