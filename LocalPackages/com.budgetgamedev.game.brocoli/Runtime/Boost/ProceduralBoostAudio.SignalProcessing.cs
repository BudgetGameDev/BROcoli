using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralBoostAudio
    {
        /// <summary>
        /// Spray Width boost - Spreading expansion sound with widening quality.
        /// Evokes spreading and coverage.
        /// </summary>
        private static AudioClip GenerateSprayWidthSound()
        {
            float duration = 0.4f;
            int numSamples = Mathf.CeilToInt(duration * sampleRate);
            numSamples = Mathf.Min(numSamples, audioBuffer.Length);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;

                // Spreading chord (notes diverge from center)
                float spreadEnv = GetSpreadEnvelope(t, duration);
                float centerFreq = 700f;
                float spread = Mathf.Min(t * 3f, 1f) * 200f; // Frequencies spread apart

                sample += Mathf.Sin(t * centerFreq * Mathf.PI * 2f) * spreadEnv * 0.25f;
                sample += Mathf.Sin(t * (centerFreq + spread) * Mathf.PI * 2f) * spreadEnv * 0.2f;
                sample +=
                    Mathf.Sin(t * (centerFreq - spread * 0.8f) * Mathf.PI * 2f) * spreadEnv * 0.2f;
                sample +=
                    Mathf.Sin(t * (centerFreq + spread * 1.5f) * Mathf.PI * 2f) * spreadEnv * 0.15f;

                // Fan-out hiss
                float fanEnv = Mathf.Exp(-t * 5f);
                float fanCutoff = 2000f + 3000f * Mathf.Min(t * 4f, 1f);
                float fan = Lowpass(Random.Range(-1f, 1f), fanCutoff, 0);
                sample += fan * fanEnv * 0.15f;

                // Opening whoosh
                if (t < 0.15f)
                {
                    float openEnv = Mathf.Sin(t / 0.15f * Mathf.PI);
                    float openNoise = Lowpass(Random.Range(-1f, 1f), 3000f, 1);
                    sample += openNoise * openEnv * 0.2f;
                }

                // Wide shimmer
                float shimmerEnv = Mathf.Exp(-t * 6f);
                float shimmerMod = Mathf.Sin(t * 15f) * 0.5f + 0.5f;
                sample += Mathf.Sin(t * 2200f * Mathf.PI * 2f) * shimmerEnv * shimmerMod * 0.08f;

                audioBuffer[i] = SoftClip(sample);
            }

            return FinalizeClip("SprayWidthBoost", numSamples, duration);
        }

        #region Helper Methods

        private static float GetHealEnvelope(float t, float duration)
        {
            float attack = 0.02f;
            float sustain = 0.3f;

            if (t < attack)
                return t / attack;
            else if (t < sustain)
                return 1f;
            else
                return Mathf.Exp(-(t - sustain) * 6f);
        }

        private static float GetWhooshEnvelope(float t, float duration)
        {
            float peak = 0.12f;
            if (t < peak)
                return Mathf.Sin(t / peak * Mathf.PI * 0.5f);
            else
                return Mathf.Exp(-(t - peak) * 5f);
        }

        private static float GetExtendEnvelope(float t, float duration)
        {
            float attack = 0.01f;
            if (t < attack)
                return t / attack;
            else
                return Mathf.Exp(-(t - attack) * 4f);
        }

        private static float GetSpreadEnvelope(float t, float duration)
        {
            float attack = 0.03f;
            float hold = 0.1f;

            if (t < attack)
                return t / attack;
            else if (t < attack + hold)
                return 1f;
            else
                return Mathf.Exp(-(t - attack - hold) * 5f);
        }

        private static float Lowpass(float input, float cutoff, int stateIdx)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / sampleRate;
            float alpha = Mathf.Clamp01(dt / (rc + dt));

            filterState[stateIdx] += alpha * (input - filterState[stateIdx]);
            return filterState[stateIdx];
        }

        private static float Highpass(float input, float cutoff, int stateIdx)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / sampleRate;
            float alpha = Mathf.Clamp01(rc / (rc + dt));

            float output = alpha * (filterState[stateIdx + 4] + input - filterState[stateIdx]);
            filterState[stateIdx] = input;
            filterState[stateIdx + 4] = output;
            return output;
        }

        private static float SoftClip(float x)
        {
            if (x > 1f)
                return 1f;
            if (x < -1f)
                return -1f;
            return x - (x * x * x) / 3f;
        }

        private static float HardClip(float x)
        {
            return Mathf.Clamp(x, -0.95f, 0.95f);
        }

        private static AudioClip FinalizeClip(string name, int numSamples, float duration)
        {
            // Normalize
            float maxAmp = 0f;
            for (int i = 0; i < numSamples; i++)
                maxAmp = Mathf.Max(maxAmp, Mathf.Abs(audioBuffer[i]));

            if (maxAmp > 0.01f)
            {
                float normalize = 0.8f / maxAmp;
                for (int i = 0; i < numSamples; i++)
                    audioBuffer[i] *= normalize;
            }

            // Fade out
            int fadeOut = Mathf.Min(numSamples / 5, sampleRate / 20);
            for (int i = 0; i < fadeOut; i++)
            {
                int idx = numSamples - 1 - i;
                float fade = (float)i / fadeOut;
                audioBuffer[idx] *= fade * fade;
            }

            AudioClip clip = AudioClip.Create(name, numSamples, 1, sampleRate, false);
            float[] clipData = new float[numSamples];
            System.Array.Copy(audioBuffer, clipData, numSamples);
            clip.SetData(clipData, 0);
            return clip;
        }

        /// <summary>
        /// Magnet boost - Magnetic pull whoosh with swirling resonance.
        /// Evokes attraction and gathering.
        /// </summary>
        private static AudioClip GenerateMagnetSound()
        {
            float duration = 0.45f;
            int numSamples = Mathf.CeilToInt(duration * sampleRate);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float norm = t / duration;

                // Swirling magnetic pull effect - frequency rises then falls
                float freqCurve = Mathf.Sin(norm * Mathf.PI); // Peaks at middle
                float baseFreq = Mathf.Lerp(150f, 400f, freqCurve);
                float phase = t * baseFreq * Mathf.PI * 2f;

                // Magnetic hum with modulation
                float hum = Mathf.Sin(phase);
                float modulation = 1f + 0.3f * Mathf.Sin(t * 25f * Mathf.PI * 2f);
                hum *= modulation;

                // Swirling overtones
                float swirl1 = Mathf.Sin(phase * 1.5f + t * 8f) * 0.3f;
                float swirl2 = Mathf.Sin(phase * 2.01f - t * 12f) * 0.2f;

                // Whoosh component - filtered noise
                float noise = Mathf.PerlinNoise(t * 50f, 0f) * 2f - 1f;
                noise *= freqCurve * 0.4f;

                // Envelope - quick attack, sustain, smooth decay
                float envelope;
                if (norm < 0.1f)
                    envelope = norm / 0.1f;
                else if (norm < 0.7f)
                    envelope = 1f;
                else
                    envelope = (1f - norm) / 0.3f;

                audioBuffer[i] = (hum * 0.5f + swirl1 + swirl2 + noise) * envelope;
            }

            return FinalizeClip("MagnetBoost", numSamples, duration);
        }

        private static AudioClip GenerateTimeSlowSound()
        {
            float duration = 0.5f;
            int numSamples = Mathf.Min(Mathf.CeilToInt(duration * sampleRate), audioBuffer.Length);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float norm = t / duration;
                float frequency = Mathf.Lerp(1100f, 220f, norm * norm);
                float envelope = Mathf.Sin(norm * Mathf.PI) * Mathf.Exp(-norm * 0.8f);
                float tick = Mathf.Sin(t * frequency * Mathf.PI * 2f);
                float overtone = Mathf.Sin(t * frequency * 2.01f * Mathf.PI * 2f) * 0.25f;
                audioBuffer[i] = SoftClip((tick + overtone) * envelope * 0.65f);
            }

            return FinalizeClip("TimeSlowBoost", numSamples, duration);
        }

        #endregion
    }
}
