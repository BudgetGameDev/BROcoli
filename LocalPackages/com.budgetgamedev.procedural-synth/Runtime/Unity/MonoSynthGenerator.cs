using System;
using UnityEngine;
#if UNITY_6000_5_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)
using Unity.Burst;
using Unity.IntegerTime;
using Unity.Mathematics;
using UnityEngine.Audio;
using static UnityEngine.Audio.ProcessorInstance;
#endif

namespace BudgetGameDev.Synth
{
    /// <summary>Native Unity 6.5 SAP adapter. The AudioSource owns all generator instances.
    /// All scene access and message calls occur on the main/control side. Audio state is an
    /// unmanaged value owned by SAP; no buffers shared with or disposed by this component.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class MonoSynthGenerator : MonoBehaviour
#if UNITY_6000_5_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)
        , IAudioGenerator
#endif
    {
        public bool playOnEnable = true;
        public bool integrationSine;
        public uint seed = 1979;
        public SynthParameters parameters = SynthParameters.HeavyBass;
        public int SampleRate { get; private set; }
        public long SamplePosition { get; private set; }
        public int Epoch { get; private set; }
        public bool Ready { get; private set; }
        public float Peak { get; private set; }
        public int LateEvents { get; private set; }
        public int DroppedEvents { get; private set; }
        public int ControlOverflows { get; private set; }
        public event Action ResetTimeline;
        private AudioSource source;
        private readonly SynthEvent[] pending = new SynthEvent[256];
        private int head, count;
        private bool parametersDirty = true, panic, lastSine;
        private long serial;
        private bool desiredPlaying, restartAfterConfiguration;

        public void SetPreset(SynthParameters value)
        {
            value.Sanitize(); parameters = value; parametersDirty = true;
        }
        public bool QueueEvent(in SynthEvent value)
        {
            if (count == pending.Length)
            {
                // Clearing the queue plus a delivered panic prevents a lost NoteOff sticking.
                head = count = 0; panic = true; ControlOverflows++; return false;
            }
            pending[(head + count) % pending.Length] = value; count++; return true;
        }
        public void Panic() { head = count = 0; panic = true; }
        public void StartAudio()
        {
#if UNITY_6000_5_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)
            desiredPlaying=true;
            if (source == null) source = GetComponent<AudioSource>();
            if (!source.isPlaying) { source.generator = this; source.Play(); }
#endif
        }
        public void StopAudio()
        {
            desiredPlaying=false; restartAfterConfiguration=false;
            if (source != null) source.Stop();
            Ready = false; head = count = 0;
        }
        private void OnEnable()
        {
            source = GetComponent<AudioSource>();
#if UNITY_6000_5_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)
            parameters.Sanitize(); parametersDirty = true;
            AudioSettings.OnAudioConfigurationChanged+=OnAudioConfigurationChanged;
            if (playOnEnable) StartAudio();
#else
            Debug.LogWarning("Procedural synth requires native Unity 6000.5+ Scriptable Audio Pipeline; Web builds are unsupported.", this);
            enabled = false;
#endif
        }
        private void OnDisable()
        {
#if UNITY_6000_5_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)
            AudioSettings.OnAudioConfigurationChanged-=OnAudioConfigurationChanged;
#endif
            StopAudio();
        }
        private void OnApplicationFocus(bool focused) { if (!focused) Panic(); }
        private void OnValidate() { parameters.Sanitize(); parametersDirty = true; }

#if UNITY_6000_5_OR_NEWER && (!UNITY_WEBGL || UNITY_EDITOR)
        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            // Device resets may destroy/stop an AudioSource. Recover only in response to
            // this notification and only if playback was requested before reconfiguration.
            restartAfterConfiguration=desiredPlaying;
            if(restartAfterConfiguration)Ready=false;
        }
        private ProcessorInstance previousInstance;
        private const int PacketCapacity = 32;
        // Fixed structure-of-arrays avoids managed references, serialization, or native allocation.
        private unsafe struct Packet
        {
            public fixed long Samples[PacketCapacity];
            public fixed int Types[PacketCapacity], Notes[PacketCapacity], Parameters[PacketCapacity];
            public fixed float Values[PacketCapacity];
            public int Count, Epoch;
            public long Serial;
            public bool HasParameters, Panic, Sine;
            public SynthParameters Preset;
            public void Put(int i, in SynthEvent e) { Samples[i]=e.Sample; Types[i]=(int)e.Type; Notes[i]=e.Note; Parameters[i]=(int)e.Parameter; Values[i]=e.Value; }
            public SynthEvent Get(int i) => new SynthEvent { Sample=Samples[i], Type=(SynthEventType)Types[i], Note=Notes[i], Parameter=(SynthParameterId)Parameters[i], Value=Values[i] };
        }
        private struct Telemetry
        {
            public long Sample, Acknowledged;
            public int Epoch, SampleRate, Dropped, Late;
            public float Peak;
        }
        private struct Query { public Telemetry State; public bool Busy; }
        private struct Submit { public Packet Data; public bool Accepted; }
        private struct Control : GeneratorInstance.IControl<Realtime>
        {
            public SynthParameters Initial;
            public uint Seed;
            public bool Sine;
            private Telemetry latest;
            private bool busy;
            private long awaiting;
            public void Dispose(ControlContext context, ref Realtime realtime) { }
            public void Configure(ControlContext context, ref Realtime realtime, in AudioFormat format,
                out GeneratorInstance.Setup setup, ref GeneratorInstance.Properties properties)
            {
                int epoch = latest.Epoch + 1;
                latest = new Telemetry { Epoch=epoch, SampleRate=format.sampleRate };
                // Wait for a block from this epoch before sending new events; drains old-epoch pipe data.
                busy = true; awaiting = 0;
                realtime = new Realtime { Epoch=epoch, Rate=format.sampleRate, Sine=Sine, Supported=format.sampleRate>=32000 && format.sampleRate<=96000 };
                if (realtime.Supported) realtime.Engine.Initialize(format.sampleRate, Seed, Initial);
                setup = new GeneratorInstance.Setup(AudioSpeakerMode.Mono, format.sampleRate);
            }
            public void Update(ControlContext context, Pipe pipe)
            {
                int read=0;
                foreach (var element in pipe.GetAvailableData(context))
                {
                    if (++read > 64) break;
                    if (element.TryGetData(out Telemetry state) && state.Epoch == latest.Epoch)
                    {
                        latest=state;
                        if (state.Acknowledged>=awaiting) busy=false;
                    }
                }
            }
            public Response OnMessage(ControlContext context, Pipe pipe, Message message)
            {
                if (message.Is<Query>())
                {
                    ref var query = ref message.Get<Query>(); query.State=latest; query.Busy=busy; return Response.Handled;
                }
                if (message.Is<Submit>())
                {
                    ref var request = ref message.Get<Submit>();
                    request.Accepted=false;
                    if (!busy && request.Data.Epoch==latest.Epoch && pipe.SendData(context, request.Data))
                    {
                        busy=true; awaiting=request.Data.Serial; request.Accepted=true;
                        if (request.Data.HasParameters) Initial=request.Data.Preset;
                        Sine=request.Data.Sine;
                    }
                    return Response.Handled;
                }
                return Response.Unhandled;
            }
        }
        [BurstCompile(CompileSynchronously = true)]
        private struct Realtime : GeneratorInstance.IRealtime
        {
            public SynthEngine Engine;
            public int Epoch, Rate;
            public bool Sine, Supported;
            private double sinePhase;
            private long acknowledged;
            public bool isFinite => false;
            public bool isRealtime => true;
            public DiscreteTime? length => null;
            public void Update(UpdatedDataContext context, Pipe pipe)
            {
                // One in-flight packet per acknowledged mix. At most 32 events + one preset.
                int read=0;
                foreach (var element in pipe.GetAvailableData(context))
                {
                    if (++read > 1) break;
                    if (!element.TryGetData(out Packet packet) || packet.Epoch!=Epoch) continue;
                    if (Supported)
                    {
                        if (packet.Panic) Engine.Panic();
                        if (packet.HasParameters) Engine.SetParameters(packet.Preset);
                        for (int i=0; i<packet.Count && i<PacketCapacity; i++) Engine.Enqueue(packet.Get(i));
                    }
                    Sine=packet.Sine; acknowledged=packet.Serial;
                }
            }
            public GeneratorInstance.Result Process(in RealtimeContext context, Pipe pipe, ChannelBuffer buffer, GeneratorInstance.Arguments args)
            {
                float peak=0;
                for (int frame=0; frame<buffer.frameCount; frame++)
                {
                    float sample=Supported ? Engine.ProcessSample() : 0;
                    if (Sine && Supported)
                    {
                        sample=.08f*(float)math.sin(sinePhase * (2*math.PI));
                        sinePhase += 110.0/Rate; sinePhase -= math.floor(sinePhase);
                    }
                    peak=math.max(peak,math.abs(sample));
                    for (int channel=0; channel<buffer.channelCount; channel++) buffer[channel,frame]=sample;
                }
                var state=new Telemetry { Sample=Supported?Engine.SamplePosition:0, Acknowledged=acknowledged, Epoch=Epoch, SampleRate=Rate, Peak=peak, Dropped=Supported?Engine.DroppedEvents:0, Late=Supported?Engine.LateEvents:0 };
                // Telemetry is replaceable. A failed send is retried with current state next block.
                pipe.SendData(context,state);
                return buffer.frameCount;
            }
        }
        public bool isFinite => false;
        public bool isRealtime => true;
        public DiscreteTime? length => null;
        public GeneratorInstance CreateInstance(ControlContext context, AudioFormat? nestedConfiguration, CreationParameters creationParameters)
        {
            creationParameters.controlUpdateSetting=UpdateSetting.UpdateAlways;
            creationParameters.realtimeUpdateSetting=UpdateSetting.UpdateIfDataIsAvailable;
            return context.AllocateGenerator(new Realtime(), new Control { Initial=parameters, Seed=seed, Sine=integrationSine }, nestedConfiguration, creationParameters);
        }
        private void Update()
        {
            if(restartAfterConfiguration)
            {
                if(ControlContext.builtIn.IsSystemWideReconfiguring)return;
                restartAfterConfiguration=false;
                source.Stop(); source.generator=this; source.Play();
            }
            var instance=source.generatorInstance;
            if (!ControlContext.builtIn.Exists(instance))
            {
                // Respect external stops; recovery is driven by configuration notifications.
                if(Ready && !source.isPlaying)desiredPlaying=false;
                Ready=false; return;
            }
            Query query=default; ControlContext.builtIn.SendMessage(instance,ref query);
            var state=query.State;
            bool reset=!Ready || state.Epoch!=Epoch || !instance.Equals(previousInstance);
            previousInstance=instance;
            SampleRate=state.SampleRate; SamplePosition=state.Sample; Peak=state.Peak;
            DroppedEvents=state.Dropped; LateEvents=state.Late;
            if (reset)
            {
                Epoch=state.Epoch; head=count=0; parametersDirty=true; panic=true;
                Ready=SampleRate>=32000 && SampleRate<=96000;
                ResetTimeline?.Invoke();
            }
            if (!Ready || query.Busy) return;
            if (integrationSine!=lastSine) { parametersDirty=true; lastSine=integrationSine; }
            if (count==0 && !parametersDirty && !panic) return;
            Packet packet=default;
            packet.Count=Math.Min(count,PacketCapacity); packet.Epoch=Epoch; packet.Serial=++serial;
            packet.HasParameters=parametersDirty; packet.Preset=parameters; packet.Panic=panic; packet.Sine=integrationSine;
            for (int i=0; i<packet.Count; i++) packet.Put(i,pending[(head+i)%pending.Length]);
            var submit=new Submit { Data=packet };
            ControlContext.builtIn.SendMessage(instance,ref submit);
            if (!submit.Accepted) return; // Retain all notes/releases and retry next frame.
            head=(head+packet.Count)%pending.Length; count-=packet.Count; parametersDirty=false; panic=false;
        }
#endif
    }
}
