using UnityEngine;

namespace BudgetGameDev.Synth
{
    /// <summary>Main-thread demo integration; game code may call SetGameState with normalized controls.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(MonoSynthGenerator), typeof(SynthAudition))]
    public sealed class SynthAdaptation : MonoBehaviour
    {
        public bool adaptive;
        public GameMusicState state = new GameMusicState { PlayerHealth = 1 };
        [Range(40, 200)] public float tempo = 112;
        [Range(24, 60)] public int root = 36;
        public MusicScale scale = MusicScale.Minor;
        public SynthParameters basis = DefaultBasis();
        public int SchedulingRecoveries { get; private set; }

        private static readonly string[] ScaleNames = { "Minor", "Dorian", "Pentatonic" };
        private readonly SynthEvent[] events = new SynthEvent[32];
        private MonoSynthGenerator synth;
        private SynthAudition audition;
        private MonoComposer composer;
        private bool running, auditionWasEnabled, previousRunInBackground, needsEpoch;
        private long origin, lastAudioPosition = -1;
        private int pendingStart, pendingCount;

        private static SynthParameters DefaultBasis()
        {
            var value = SynthParameters.HeavyBass;
            value.Oscillator1.Waveform = Waveform.Wavetable;
            return value;
        }

        private void OnEnable()
        {
            synth = GetComponent<MonoSynthGenerator>();
            audition = GetComponent<SynthAudition>();
            if (composer == null) composer = new MonoComposer(48000, synth.seed);
            synth.ResetTimeline += OnTimelineReset;
            // Activate on Update, after every required component has completed Awake.
        }

        private void OnDisable()
        {
            bool requested = adaptive;
            if (synth != null) synth.ResetTimeline -= OnTimelineReset;
            SetAdaptive(false);
            adaptive = requested;
        }

        public void SetGameState(GameMusicState value) { state = value; }

        public void SetAdaptive(bool enabled)
        {
            adaptive = enabled;
            if (running == enabled || synth == null || audition == null) return;
            if (enabled)
            {
                auditionWasEnabled = audition.enabled;
                audition.SetSequence(false);
                audition.enabled = false;
                // Audition restores its own previous global value when disabled.
                previousRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                running = true;
                needsEpoch = true;
                synth.integrationSine = false;
                synth.Panic();
                synth.StartAudio();
                if (synth.Ready) BeginEpoch();
            }
            else
            {
                running = false;
                pendingStart = pendingCount = 0;
                lastAudioPosition = -1;
                synth.Panic();
                synth.SetPreset(basis);
                Application.runInBackground = previousRunInBackground;
                audition.enabled = auditionWasEnabled;
                // Enabling audition can apply its startup sequence preference; keep exit quiet.
                if (auditionWasEnabled) audition.SetSequence(false);
            }
        }

        private void OnTimelineReset()
        {
            if (!running) return;
            needsEpoch = true;
            pendingStart = pendingCount = 0;
            if (synth.Ready) BeginEpoch();
        }

        private void BeginEpoch()
        {
            synth.Panic();
            composer.Reset(synth.SampleRate, synth.seed);
            composer.Tempo = tempo; composer.RootMidi = root; composer.Scale = scale;
            composer.SetState(state);
            origin = synth.SamplePosition + (long)(synth.SampleRate * .15);
            lastAudioPosition = synth.SamplePosition;
            pendingStart = pendingCount = 0;
            needsEpoch = false;
        }

        private void RecoverScheduling()
        {
            SchedulingRecoveries++;
            BeginEpoch(); // explicit panic invalidates both old notes and their releases
        }

        private void Update()
        {
            if (adaptive != running) SetAdaptive(adaptive);
            if (!running || !synth.Ready) return;
            if (needsEpoch) BeginEpoch();
            long horizonLength = (long)(synth.SampleRate * .30);
            if (lastAudioPosition >= 0 && synth.SamplePosition - lastAudioPosition > horizonLength)
                RecoverScheduling();
            lastAudioPosition = synth.SamplePosition;
            composer.Tempo = tempo; composer.RootMidi = root; composer.Scale = scale;
            composer.SetState(state);
            // Always adapt the original basis. Feeding the previous output back compounds mappings.
            synth.SetPreset(composer.AdaptPreset(basis, Time.unscaledDeltaTime));
            long horizon = synth.SamplePosition + horizonLength - origin;
            // Bounded control-side work: at most two arrays / 64 events per frame.
            for (int batch = 0; batch < 2; batch++)
            {
                if (pendingCount == 0)
                {
                    pendingStart = 0;
                    pendingCount = composer.Fill(horizon, events);
                    for (int i = 0; i < pendingCount; i++) events[i].Sample += origin;
                }
                if (pendingCount == 0) break;
                while (pendingCount > 0)
                {
                    if (!synth.QueueEvent(events[pendingStart]))
                    {
                        // QueueEvent failure clears its whole transport queue. Retrying only
                        // this event could lose an earlier release, so restart with a panic.
                        RecoverScheduling();
                        return;
                    }
                    pendingStart++;
                    pendingCount--;
                }
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(550, 24, 400, 640), GUI.skin.box);
            GUILayout.Label("ADAPTIVE MONO COMPOSER");
            bool next = GUILayout.Toggle(adaptive, "Enable adaptive composition");
            if (next != adaptive) SetAdaptive(next);
            GUILayout.Label("Seeded phrases · beat articulation · phrase motifs");
            state.Danger = Slider("Danger / density and drive", state.Danger);
            state.EnemyProximity = Slider("Enemy proximity / cutoff", state.EnemyProximity);
            state.PlayerHealth = Slider("Player health / wavetable", state.PlayerHealth);
            state.MovementSpeed = Slider("Movement / shorter gates", state.MovementSpeed);
            state.Weather = Slider("Weather / noise", state.Weather);
            GUILayout.Label($"Narrative motif: {state.NarrativeState}");
            // The demo slider offers eight IDs; gameplay may supply any integer ID.
            float displayedNarrative = Mathf.Clamp(state.NarrativeState, 0, 7);
            float nextNarrative = GUILayout.HorizontalSlider(displayedNarrative, 0, 7);
            if (!Mathf.Approximately(displayedNarrative, nextNarrative))
                state.NarrativeState = Mathf.RoundToInt(nextNarrative);
            GUILayout.Label($"Tempo: {tempo:0} BPM");
            tempo = GUILayout.HorizontalSlider(tempo, 40, 200);
            GUILayout.Label($"Root MIDI: {root}");
            root = Mathf.RoundToInt(GUILayout.HorizontalSlider(root, 24, 60));
            scale = (MusicScale)GUILayout.SelectionGrid((int)scale, ScaleNames, 3);
            GUILayout.Space(8);
            if (running)
                GUILayout.Label($"Active motif {composer.ActiveNarrativeState} · root {composer.ActiveRootMidi} · {composer.ActiveScale}");
            GUILayout.Label($"Scheduling recoveries: {SchedulingRecoveries}");
            GUILayout.Label("150–300 ms look-ahead. Game inputs are manual demo controls.");
            GUILayout.Label("Weather adds noise; ambience effects are deferred.");
            GUILayout.EndArea();
        }

        private static float Slider(string label, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = 0;
            value = Mathf.Clamp01(value);
            GUILayout.Label($"{label}: {value:0.00}");
            return GUILayout.HorizontalSlider(value, 0, 1);
        }
    }
}
