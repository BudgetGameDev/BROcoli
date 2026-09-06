using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>A latch snap, wooden lid creak, and brief treasure chime.</summary>
    public static class ProceduralChestAudio
    {
        private const float Duration = 0.85f;
        private static AudioClip clip;
        private static AudioSource source;

        public static AudioClip GetOrCreateClip()
        {
            if (clip != null)
                return clip;

            int rate = Mathf.Max(22050, AudioSettings.outputSampleRate);
            var samples = new float[Mathf.CeilToInt(Duration * rate)];
            var random = new System.Random(906);
            float wood = 0f;
            float phase = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                wood = Mathf.Lerp(wood, noise, 0.08f);
                float latch =
                    (noise * 0.25f + Mathf.Sin(t * 2400f * Mathf.PI * 2f) * 0.18f)
                    * Mathf.Exp(-t * 65f);
                phase += Mathf.Lerp(180f, 85f, Mathf.Clamp01(t / 0.4f)) * Mathf.PI * 2f / rate;
                float lid =
                    (wood * 0.4f + Mathf.Sin(phase) * 0.18f)
                    * Mathf.Sin(Mathf.Clamp01(t / 0.45f) * Mathf.PI);
                float chime = 0f;
                for (int note = 0; note < 3; note++)
                {
                    float age = t - 0.12f - note * 0.075f;
                    if (age < 0f)
                        continue;
                    float frequency =
                        note == 0 ? 880f
                        : note == 1 ? 1108.73f
                        : 1318.51f;
                    chime +=
                        Mathf.Sin(age * frequency * Mathf.PI * 2f)
                        * Mathf.Min(1f, age / 0.005f)
                        * Mathf.Exp(-age * 7f)
                        * 0.18f;
                }
                float fade = Mathf.Clamp01((Duration - t) / 0.08f);
                samples[i] = (latch + lid + chime) * fade;
            }

            clip = AudioClip.Create("ChestOpen", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static void Play()
        {
            if (source == null)
            {
                var host = new GameObject("ChestOpenAudio");
                if (Application.isPlaying)
                    Object.DontDestroyOnLoad(host);
                source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.priority = 32;
            }
            // The chest vanishes before the cue ends; its source must outlive it.
            source.PlayOneShot(GetOrCreateClip(), 0.8f);
        }
    }
}
