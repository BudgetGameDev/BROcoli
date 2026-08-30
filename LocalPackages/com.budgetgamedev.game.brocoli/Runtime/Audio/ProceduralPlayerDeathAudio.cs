using System;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Synthesizes a short broccoli defeat cue: leafy rustle, a descending wilt
    /// tone, and a soft landing thump. No legacy game audio assets are used.
    /// </summary>
    public static class ProceduralPlayerDeathAudio
    {
        private const float Duration = 1.05f;
        private static AudioClip cachedClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            cachedClip = null;
        }

        public static AudioClip GetOrCreateClip()
        {
            if (cachedClip != null)
                return cachedClip;

            int sampleRate = Mathf.Max(22050, AudioSettings.outputSampleRate);
            int sampleCount = Mathf.CeilToInt(Duration * sampleRate);
            float[] samples = new float[sampleCount];
            System.Random random = new System.Random(240617);
            float rustle = 0f;
            float wiltPhase = 0f;
            float thumpPhase = 0f;
            float peak = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                rustle = Mathf.Lerp(rustle, noise, 0.28f);

                float snapEnvelope = Mathf.Exp(-time * 30f);
                float snapPitch = Mathf.Lerp(920f, 420f, Mathf.Clamp01(time / 0.14f));
                float snap = Mathf.Sin(time * snapPitch * Mathf.PI * 2f) * snapEnvelope;

                float leafEnvelope =
                    Mathf.Exp(-time * 5.2f)
                    * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / 0.025f));
                float leaves = rustle * leafEnvelope;

                float wiltPitch = Mathf.Lerp(285f, 82f, Mathf.Pow(time / Duration, 0.72f));
                wiltPhase += wiltPitch * Mathf.PI * 2f / sampleRate;
                float wiltEnvelope =
                    Mathf.Sin(Mathf.Min(1f, time / 0.09f) * Mathf.PI * 0.5f)
                    * Mathf.Exp(-time * 2.7f);
                float wilt =
                    (Mathf.Sin(wiltPhase) + 0.22f * Mathf.Sin(wiltPhase * 2.01f)) * wiltEnvelope;

                float landingTime = time - 0.58f;
                float thump = 0f;
                if (landingTime >= 0f)
                {
                    float thumpPitch = Mathf.Lerp(92f, 46f, Mathf.Clamp01(landingTime / 0.3f));
                    thumpPhase += thumpPitch * Mathf.PI * 2f / sampleRate;
                    float thumpEnvelope = Mathf.Exp(-landingTime * 13f);
                    thump = (Mathf.Sin(thumpPhase) * 0.8f + noise * 0.16f) * thumpEnvelope;
                }

                float sample = snap * 0.18f + leaves * 0.24f + wilt * 0.34f + thump * 0.5f;
                float fadeOut =
                    1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - Duration + 0.12f) / 0.12f));
                samples[i] = sample * fadeOut;
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }

            float gain = peak > 0f ? 0.82f / peak : 1f;
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= gain;

            cachedClip = AudioClip.Create("BroccoliDefeat", sampleCount, 1, sampleRate, false);
            cachedClip.SetData(samples, 0);
            return cachedClip;
        }
    }
}
