using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralEnemyMeleeAudio
    {
        private static AudioClip GenerateMeleeClipStatic(MeleeSoundType type)
        {
            MeleePreset p = GetPresetStatic(type);

            int totalSamples = Mathf.CeilToInt(p.duration * staticSampleRate);
            totalSamples = Mathf.Min(totalSamples, staticAudioBuffer.Length);

            System.Array.Clear(staticLpState, 0, staticLpState.Length);
            System.Array.Clear(staticHpState, 0, staticHpState.Length);

            float phaseImpact = 0f,
                phaseBody = 0f,
                phaseMetallic = 0f;
            float noiseState = 0f;
            uint rngState = (uint)(type + 12345);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / staticSampleRate;
                float normalizedT = Mathf.Clamp01(t / p.duration);
                float sample = 0f;

                // Whoosh
                float whooshEnv = GetWhooshEnvelopeStatic(t, p.duration, p.whooshDecay);
                float whooshFreq = Mathf.Lerp(p.whooshFreqStart, p.whooshFreqEnd, normalizedT);
                rngState = rngState * 1103515245 + 12345;
                float whooshNoise = ((rngState >> 16) & 0x7FFF) / 16383.5f - 1f;
                float whoosh = LowpassFilterStatic(whooshNoise, whooshFreq, 0);
                whoosh = HighpassFilterStatic(whoosh, whooshFreq * 0.3f, 0);
                whoosh *= whooshEnv * p.whooshAmount;

                // Impact
                float impactSample = 0f;
                if (t >= p.impactDelay)
                {
                    float impactT = t - p.impactDelay;
                    float impactEnv = GetImpactEnvelopeStatic(impactT, p.impactDecay);
                    phaseImpact += p.impactFreq / staticSampleRate;
                    impactSample = Mathf.Sin(phaseImpact * Mathf.PI * 2f);
                    impactSample += Mathf.Sin(phaseImpact * Mathf.PI * 4f) * 0.4f;
                    impactSample *= impactEnv * p.impactAmount;
                }

                // Body
                float bodyEnv = GetBodyEnvelopeStatic(t, p.duration, p.bodyDecay);
                phaseBody += p.bodyFreq / staticSampleRate;
                float body = Mathf.Sin(phaseBody * Mathf.PI * 2f);
                body += Mathf.Sin(phaseBody * Mathf.PI * 3f) * 0.3f;
                body *= bodyEnv * p.bodyAmount;

                // Noise burst
                float noiseEnv = GetNoiseBurstEnvelopeStatic(t, p.duration, p.noiseDecay);
                rngState = rngState * 1103515245 + 12345;
                float whiteNoise = ((rngState >> 16) & 0x7FFF) / 16383.5f - 1f;
                noiseState = noiseState * 0.85f + whiteNoise * 0.15f;
                float noiseBurst = LowpassFilterStatic(
                    noiseState + whiteNoise * 0.5f,
                    p.noiseCutoff * (1f - normalizedT * 0.5f),
                    1
                );
                noiseBurst *= noiseEnv * p.noiseBurst;

                // Metallic
                float metallic = 0f;
                if (p.hasMetallic && t < p.duration * 0.4f)
                {
                    float metallicEnv = Mathf.Exp(-t * 20f);
                    phaseMetallic += p.metallicFreq / staticSampleRate;
                    metallic = Mathf.Sin(phaseMetallic * Mathf.PI * 2f);
                    metallic *= metallicEnv * p.metallicAmount;
                }

                sample = whoosh + impactSample + body + noiseBurst + metallic;
                sample = SoftClipStatic(sample);
                staticAudioBuffer[i] = sample;
            }

            // Fade out
            int fadeOutSamples = Mathf.Min(totalSamples / 6, staticSampleRate / 20);
            for (int i = 0; i < fadeOutSamples; i++)
            {
                int idx = totalSamples - 1 - i;
                float fade = (float)i / fadeOutSamples;
                staticAudioBuffer[idx] *= fade * fade;
            }

            // Normalize
            float maxAmp = 0f;
            for (int i = 0; i < totalSamples; i++)
                maxAmp = Mathf.Max(maxAmp, Mathf.Abs(staticAudioBuffer[i]));
            if (maxAmp > 0.01f)
            {
                float normalize = 0.85f / maxAmp;
                for (int i = 0; i < totalSamples; i++)
                    staticAudioBuffer[i] *= normalize;
            }

            AudioClip clip = AudioClip.Create(
                "EnemyMelee_" + type,
                totalSamples,
                1,
                staticSampleRate,
                false
            );
            float[] clipData = new float[totalSamples];
            System.Array.Copy(staticAudioBuffer, clipData, totalSamples);
            clip.SetData(clipData, 0);
            return clip;
        }

        private static float GetWhooshEnvelopeStatic(float t, float duration, float decayRate)
        {
            float attack = 0.008f;
            if (t < attack)
                return Mathf.Sqrt(t / attack);
            float dt = (t - attack) / (duration * 0.9f);
            return Mathf.Exp(-dt * decayRate);
        }

        private static float GetImpactEnvelopeStatic(float t, float decayRate)
        {
            float attack = 0.001f;
            if (t < attack)
                return t / attack;
            return Mathf.Exp(-(t - attack) * decayRate);
        }

        private static float GetBodyEnvelopeStatic(float t, float duration, float decayRate)
        {
            float attack = 0.003f;
            float sustain = duration * 0.1f;
            if (t < attack)
                return t / attack;
            if (t < attack + sustain)
                return 1f;
            float dt = (t - attack - sustain) / (duration * 0.8f);
            return Mathf.Exp(-dt * decayRate);
        }

        private static float GetNoiseBurstEnvelopeStatic(float t, float duration, float decayRate)
        {
            float attack = 0.002f;
            if (t < attack)
                return t / attack;
            return Mathf.Exp(-(t - attack) * decayRate);
        }

        private static float LowpassFilterStatic(float input, float cutoff, int stateIndex)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / staticSampleRate;
            float alpha = Mathf.Clamp01(dt / (rc + dt));
            staticLpState[stateIndex] += alpha * (input - staticLpState[stateIndex]);
            return staticLpState[stateIndex];
        }

        private static float HighpassFilterStatic(float input, float cutoff, int stateIndex)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / staticSampleRate;
            float alpha = rc / (rc + dt);
            float output =
                alpha * (staticHpState[stateIndex] + input - staticLpState[stateIndex + 2]);
            staticLpState[stateIndex + 2] = input;
            staticHpState[stateIndex] = output;
            return output;
        }

        private static float SoftClipStatic(float x)
        {
            if (Mathf.Abs(x) < 0.7f)
                return x;
            if (x > 0)
                return 0.7f + (1f - 0.7f) * (float)System.Math.Tanh((x - 0.7f) * 3f);
            return -0.7f + (-1f + 0.7f) * (float)System.Math.Tanh((x + 0.7f) * 3f);
        }
    }
}
