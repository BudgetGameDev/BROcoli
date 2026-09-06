using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>Quiet burning fuel, with transient sounds driven only by visible ember events.</summary>
    internal sealed partial class ProceduralTorchFireAudio : MonoBehaviour
    {
        private AudioSource bed;
        private AudioSource[] crackles;
        private readonly double[] busyUntil = new double[2];
        private int nextVariant;
        private float phaseOffset;
        private float gain;
        private bool bedRunning;
        private bool pausedByTime;
        private double pauseStarted;
        private static readonly AnimationCurve ConstantRolloff = AnimationCurve.Linear(0, 1, 1, 1);

        private void Awake()
        {
            EnsureClips();
            bed = CreateSource(true);
            bed.clip = bedClip;
            crackles = new[] { CreateSource(false), CreateSource(false) };
            // Stable local variation neither consumes gameplay randomness nor allocates on emission.
            uint identity = unchecked((uint)GetEntityId().GetHashCode() * 2654435761u);
            phaseOffset = identity % SampleRate / (float)SampleRate * BedSeconds;
            nextVariant = (int)(identity % CrackleVariants);
        }

        private AudioSource CreateSource(bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.spread = 25f;
            source.minDistance = 1f;
            source.maxDistance = 1000f;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, ConstantRolloff);
            source.ignoreListenerPause = false;
            source.priority = loop ? 210 : 180;
            source.outputAudioMixerGroup = FindSfxGroup();
            source.volume = 0f;
            return source;
        }

        private void Update()
        {
            // Subsystem reset can clear shared clips when scene reload is disabled.
            if (bed.clip == null)
            {
                EnsureClips();
                bed.clip = bedClip;
                bedRunning = false;
            }
            if (AudioListener.pause || Time.timeScale <= 0f)
            {
                if (!AudioListener.pause && !pausedByTime)
                {
                    pauseStarted = AudioSettings.dspTime;
                    pausedByTime = true;
                    bed.Pause();
                    foreach (AudioSource source in crackles)
                        source.Pause();
                }
                return;
            }
            if (pausedByTime)
            {
                double pausedSeconds = AudioSettings.dspTime - pauseStarted;
                for (int index = 0; index < crackles.Length; index++)
                {
                    busyUntil[index] += pausedSeconds;
                    crackles[index].UnPause();
                }
                bed.UnPause();
                pausedByTime = false;
            }
            float target = PlayerGain();
            gain = Mathf.MoveTowards(gain, target, Time.deltaTime * 4f);
            if (target <= 0f && gain <= 0.0001f)
            {
                StopSources();
                return;
            }
            if (!bedRunning)
            {
                bed.timeSamples = (int)((Time.time + phaseOffset) % BedSeconds * SampleRate);
                bed.Play();
                bedRunning = true;
            }
            else
                bed.UnPause();
            bed.volume = gain * 0.13f * UnroutedVolume(bed);
            foreach (AudioSource source in crackles)
                source.volume = target * 0.24f * UnroutedVolume(source);
        }

        internal void PlayCrackle(float strength)
        {
            if (!isActiveAndEnabled || AudioListener.pause || Time.timeScale <= 0f)
                return;
            if (float.IsNaN(strength) || float.IsInfinity(strength))
                return;
            float audible = PlayerGain();
            strength = Mathf.Clamp01(strength);
            if (audible <= 0f || strength <= 0f)
                return;
            double now = AudioSettings.dspTime;
            for (int index = 0; index < crackles.Length; index++)
            {
                if (busyUntil[index] > now)
                    continue;
                AudioClip clip = crackleClips[nextVariant];
                nextVariant = (nextVariant + 1) % CrackleVariants;
                AudioSource source = crackles[index];
                source.volume = audible * 0.24f * UnroutedVolume(source);
                source.PlayOneShot(clip, strength);
                busyUntil[index] = now + clip.length;
                return;
            }
        }

        private float PlayerGain()
        {
            Transform player = GameContext.Instance?.PlayerTransform;
            return player == null
                ? 0f
                : DistanceGain(GroundPlane.GroundDistance(transform.position, player.position));
        }

        internal static float DistanceGain(float distance)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance))
                return 0f;
            float t = Mathf.Clamp01((distance - 1f) / 11f);
            float smooth = t * t * (3f - 2f * t);
            return (1f - smooth) * (1f - smooth);
        }

        private static float UnroutedVolume(AudioSource source) =>
            source.outputAudioMixerGroup == null
                ? GameAudioSettings.MasterVolume * GameAudioSettings.SfxVolume
                : 1f;

        private void StopSources()
        {
            if (bed != null)
                bed.Stop();
            bedRunning = false;
            pausedByTime = false;
            if (crackles == null)
                return;
            for (int index = 0; index < crackles.Length; index++)
            {
                crackles[index].Stop();
                busyUntil[index] = 0;
            }
        }

        private void OnDisable()
        {
            gain = 0f;
            StopSources();
        }
    }
}
