using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>Scene-owned dungeon air and sparse distant details, independent of gameplay RNG.</summary>
    internal sealed partial class ProceduralDungeonAmbience : MonoBehaviour
    {
        internal const float BedVolume = 0.16f;
        internal const float DetailVolume = 0.3f;
        private AudioSource bed;
        private AudioSource[] details;
        private System.Random random;
        private double activeSeconds;
        private double nextDetail;
        private float fade;
        private bool playing;
        private bool pausedByTime;

        private void Awake()
        {
            EnsureClips();
            random = new System.Random(unchecked(GetEntityId().GetHashCode() * 397) ^ 61793);
            bed = CreateSource(true);
            bed.clip = bedClip;
            details = new[] { CreateSource(false), CreateSource(false) };
        }

        private void OnEnable()
        {
            activeSeconds = 0;
            nextDetail = random == null ? 5 : NextDelay(random.NextDouble());
            fade = 0;
        }

        private AudioSource CreateSource(bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            source.ignoreListenerPause = false;
            source.priority = loop ? 220 : 205;
            source.outputAudioMixerGroup = FindAmbienceGroup();
            source.volume = 0;
            return source;
        }

        private void Update()
        {
            if (bed.clip == null)
            {
                EnsureClips();
                bed.clip = bedClip;
                playing = false;
            }
            if (AudioListener.pause || Time.timeScale <= 0)
            {
                if (!AudioListener.pause && !pausedByTime)
                {
                    bed.Pause();
                    foreach (AudioSource source in details)
                        source.Pause();
                    pausedByTime = true;
                }
                return;
            }
            if (pausedByTime)
            {
                bed.UnPause();
                foreach (AudioSource source in details)
                    source.UnPause();
                pausedByTime = false;
            }
            // Sound plays in real time, even in accelerated development simulations.
            // This clock advances only while gameplay is active, so pauses cannot queue bursts.
            activeSeconds += Time.unscaledDeltaTime;
            fade = Mathf.MoveTowards(fade, 1, Time.unscaledDeltaTime / 2.5f);
            if (!playing)
            {
                bed.Play();
                playing = true;
            }
            bed.volume = fade * BedVolume * UnroutedVolume(bed);
            foreach (AudioSource source in details)
                source.volume = fade * DetailVolume * UnroutedVolume(source);
            if (activeSeconds < nextDetail)
                return;
            nextDetail = activeSeconds + NextDelay(random.NextDouble());
            foreach (AudioSource source in details)
            {
                if (source.isPlaying)
                    continue;
                source.clip = detailClips[random.Next(DetailVariants)];
                source.panStereo = (float)(random.NextDouble() * 1.3 - 0.65);
                source.Play();
                break;
            }
        }

        internal static double NextDelay(double unitRandom) =>
            3 + System.Math.Clamp(unitRandom, 0, 1) * 6;

        private static float UnroutedVolume(AudioSource source) =>
            source.outputAudioMixerGroup == null
                ? GameAudioSettings.MasterVolume * GameAudioSettings.AmbienceVolume
                : 1;

        private void OnDisable()
        {
            if (bed != null)
                bed.Stop();
            if (details != null)
                foreach (AudioSource source in details)
                    source.Stop();
            playing = pausedByTime = false;
            fade = 0;
        }
    }
}
