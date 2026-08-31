using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralEnemyMeleeAudio
    {
        public void PlayMeleeSound()
        {
            float distAtten = GetDistanceAttenuation();
            if (distAtten < 0.01f)
                return;

            AudioClip clip;
            if (cachedClips != null && cachedClips.TryGetValue(soundType, out clip))
            {
                audioSource.PlayOneShot(clip, volume * distAtten);
            }
            else
            {
                currentPreset = GetPreset(soundType);
                clip = GenerateMeleeClip();
                audioSource.PlayOneShot(clip, volume * distAtten);
            }
        }

        public void PlayMeleeSound(float volumeMultiplier)
        {
            float distAtten = GetDistanceAttenuation();
            if (distAtten < 0.01f)
                return;

            AudioClip clip;
            if (cachedClips != null && cachedClips.TryGetValue(soundType, out clip))
            {
                audioSource.PlayOneShot(clip, volume * volumeMultiplier * distAtten);
            }
            else
            {
                currentPreset = GetPreset(soundType);
                clip = GenerateMeleeClip();
                audioSource.PlayOneShot(clip, volume * volumeMultiplier * distAtten);
            }
        }

        private AudioClip GenerateMeleeClip()
        {
            MeleePreset p = currentPreset;
            float rnd = randomization;

            float dur = p.duration * (1f + Random.Range(-rnd * 0.3f, rnd * 0.3f));
            int totalSamples = Mathf.CeilToInt(dur * sampleRate);
            totalSamples = Mathf.Min(totalSamples, audioBuffer.Length);

            System.Array.Clear(lpState, 0, lpState.Length);
            System.Array.Clear(hpState, 0, hpState.Length);

            float phaseImpact = 0f;
            float phaseBody = 0f;
            float phaseMetallic = 0f;
            float noiseState = 0f;

            float freqOffset = Random.Range(0.92f, 1.08f);
            float impactDelayRnd = p.impactDelay * (1f + Random.Range(-rnd, rnd));

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float normalizedT = Mathf.Clamp01(t / dur);

                float sample = 0f;

                // ===== WHOOSH (filtered noise sweep) =====
                float whooshEnv = GetWhooshEnvelope(t, dur, p.whooshDecay);
                float whooshFreq =
                    Mathf.Lerp(p.whooshFreqStart, p.whooshFreqEnd, normalizedT) * freqOffset;

                float whooshNoise = Random.Range(-1f, 1f);
                float whoosh = LowpassFilter(whooshNoise, whooshFreq, 0);
                whoosh = HighpassFilter(whoosh, whooshFreq * 0.3f, 0);
                whoosh *= whooshEnv * p.whooshAmount;

                // ===== IMPACT =====
                float impactSample = 0f;
                if (t >= impactDelayRnd)
                {
                    float impactT = t - impactDelayRnd;
                    float impactEnv = GetImpactEnvelope(impactT, p.impactDecay);

                    float impactF = p.impactFreq * freqOffset;
                    phaseImpact += impactF / sampleRate;
                    impactSample = Mathf.Sin(phaseImpact * Mathf.PI * 2f);
                    impactSample += Mathf.Sin(phaseImpact * Mathf.PI * 4f) * 0.4f;
                    impactSample *= impactEnv * p.impactAmount;
                }

                // ===== BODY RESONANCE =====
                float bodyEnv = GetBodyEnvelope(t, dur, p.bodyDecay);
                float bodyF = p.bodyFreq * freqOffset;
                phaseBody += bodyF / sampleRate;
                float body = Mathf.Sin(phaseBody * Mathf.PI * 2f);
                body += Mathf.Sin(phaseBody * Mathf.PI * 3f) * 0.3f;
                body *= bodyEnv * p.bodyAmount;

                // ===== NOISE BURST =====
                float noiseEnv = GetNoiseBurstEnvelope(t, dur, p.noiseDecay);
                float whiteNoise = Random.Range(-1f, 1f);
                noiseState = noiseState * 0.85f + whiteNoise * 0.15f;
                float noiseBurst = LowpassFilter(
                    noiseState + whiteNoise * 0.5f,
                    p.noiseCutoff * (1f - normalizedT * 0.5f),
                    1
                );
                noiseBurst *= noiseEnv * p.noiseBurst;

                // ===== METALLIC (optional) =====
                float metallic = 0f;
                if (p.hasMetallic && t < dur * 0.4f)
                {
                    float metallicEnv = Mathf.Exp(-t * 20f);
                    phaseMetallic += p.metallicFreq * freqOffset / sampleRate;
                    metallic = Mathf.Sin(phaseMetallic * Mathf.PI * 2f);
                    metallic *= metallicEnv * p.metallicAmount;
                }

                // ===== COMBINE =====
                sample = whoosh + impactSample + body + noiseBurst + metallic;

                // Soft clipping
                sample = SoftClip(sample);

                audioBuffer[i] = sample;
            }

            // Fade out
            int fadeOutSamples = Mathf.Min(totalSamples / 6, sampleRate / 20);
            for (int i = 0; i < fadeOutSamples; i++)
            {
                int idx = totalSamples - 1 - i;
                float fade = (float)i / fadeOutSamples;
                fade = fade * fade;
                audioBuffer[idx] *= fade;
            }

            // Normalize
            float maxAmp = 0f;
            for (int i = 0; i < totalSamples; i++)
                maxAmp = Mathf.Max(maxAmp, Mathf.Abs(audioBuffer[i]));

            if (maxAmp > 0.01f)
            {
                float normalize = 0.85f / maxAmp;
                for (int i = 0; i < totalSamples; i++)
                    audioBuffer[i] *= normalize;
            }

            AudioClip clip = AudioClip.Create("EnemyMelee", totalSamples, 1, sampleRate, false);
            float[] clipData = new float[totalSamples];
            System.Array.Copy(audioBuffer, clipData, totalSamples);
            clip.SetData(clipData, 0);

            return clip;
        }

        // =============== ENVELOPES ===============

        private float GetWhooshEnvelope(float t, float duration, float decayRate)
        {
            float attack = 0.008f;
            float decay = duration * 0.9f;

            if (t < attack)
                return Mathf.Sqrt(t / attack);
            else
            {
                float dt = (t - attack) / decay;
                return Mathf.Exp(-dt * decayRate);
            }
        }

        private float GetImpactEnvelope(float t, float decayRate)
        {
            float attack = 0.001f;

            if (t < attack)
                return t / attack;
            else
            {
                return Mathf.Exp(-(t - attack) * decayRate);
            }
        }

        private float GetBodyEnvelope(float t, float duration, float decayRate)
        {
            float attack = 0.003f;
            float sustain = duration * 0.1f;

            if (t < attack)
                return t / attack;
            else if (t < attack + sustain)
                return 1f;
            else
            {
                float dt = (t - attack - sustain) / (duration * 0.8f);
                return Mathf.Exp(-dt * decayRate);
            }
        }

        private float GetNoiseBurstEnvelope(float t, float duration, float decayRate)
        {
            float attack = 0.002f;

            if (t < attack)
                return t / attack;
            else
            {
                return Mathf.Exp(-(t - attack) * decayRate);
            }
        }

        // =============== FILTERS ===============

        private float LowpassFilter(float input, float cutoff, int stateIndex)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / sampleRate;
            float alpha = dt / (rc + dt);
            alpha = Mathf.Clamp01(alpha);

            lpState[stateIndex] += alpha * (input - lpState[stateIndex]);
            return lpState[stateIndex];
        }

        private float HighpassFilter(float input, float cutoff, int stateIndex)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / sampleRate;
            float alpha = rc / (rc + dt);

            float output = alpha * (hpState[stateIndex] + input - lpState[stateIndex + 2]);
            lpState[stateIndex + 2] = input;
            hpState[stateIndex] = output;
            return output;
        }

        private float SoftClip(float x)
        {
            if (Mathf.Abs(x) < 0.7f)
                return x;
            else if (x > 0)
                return 0.7f + (1f - 0.7f) * (float)System.Math.Tanh((x - 0.7f) * 3f);
            else
                return -0.7f + (-1f + 0.7f) * (float)System.Math.Tanh((x + 0.7f) * 3f);
        }
    }
}
