using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralEnemyGunAudio
    {
        private static AudioClip GenerateGunClipStatic(EnemyGunSoundType type)
        {
            EnemyGunPreset p = GetPresetStatic(type);

            float dur = p.duration;
            float roomR = p.roomSize;
            int numSamples = Mathf.CeilToInt(dur * staticSampleRate);
            int totalSamples = Mathf.CeilToInt((dur + roomR * 0.4f) * staticSampleRate);
            totalSamples = Mathf.Min(totalSamples, staticAudioBuffer.Length);
            numSamples = Mathf.Min(numSamples, totalSamples);

            System.Array.Clear(staticLpState, 0, staticLpState.Length);
            System.Array.Clear(staticHpState, 0, staticHpState.Length);
            System.Array.Clear(staticBpState, 0, staticBpState.Length);
            ClearReverbStatic();

            float phase1 = 0f,
                phase2 = 0f;
            float phaseSub = 0f,
                phaseMid = 0f;
            float phaseMod = 0f,
                phaseRes = 0f;
            float noiseState = 0f;
            uint rngState = (uint)(type + 54321);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / staticSampleRate;
                float normalizedT = Mathf.Clamp01(t / dur);
                float sample = 0f;

                if (i < numSamples)
                {
                    float pitchMod = 1f + p.pitchBend * normalizedT;
                    phaseMod += p.modFreq / staticSampleRate;
                    float lfo = Mathf.Sin(phaseMod * Mathf.PI * 2f);
                    float glitchMod = 1f;

                    // Transient
                    float transientEnv = GetTransientEnvelopeStatic(t, p.transientDecay);
                    float tf1 = p.transientFreq1 * pitchMod;
                    float tf2 = p.transientFreq2 * pitchMod;
                    if (p.hasChorus)
                    {
                        tf1 *= 1f + lfo * p.modDepth * 0.1f;
                        tf2 *= 1f - lfo * p.modDepth * 0.08f;
                    }
                    phase1 += tf1 / staticSampleRate;
                    phase2 += tf2 / staticSampleRate;
                    float trans1 = Mathf.Sin(phase1 * Mathf.PI * 2f);
                    float trans2 = Mathf.Sin(phase2 * Mathf.PI * 2f);
                    float transient =
                        (trans1 * 0.6f + trans2 * 0.4f) * transientEnv * p.transientAmount;

                    // Body
                    float bodyEnv = GetBodyEnvelopeStatic(t, dur, p.bodyDecay);
                    float subF = p.subFreq * pitchMod;
                    if (p.hasChorus)
                        subF *= 1f + lfo * p.modDepth * 0.05f;
                    phaseSub += subF / staticSampleRate;
                    float sub = Mathf.Sin(phaseSub * Mathf.PI * 2f);
                    sub += Mathf.Sin(phaseSub * Mathf.PI * 3f) * 0.3f;
                    float midF = p.midFreq * pitchMod;
                    phaseMid += midF / staticSampleRate;
                    float mid = Mathf.Sin(phaseMid * Mathf.PI * 2f);
                    mid += Mathf.Sin(phaseMid * Mathf.PI * 4f) * 0.25f;
                    mid *= 1f + lfo * p.modDepth;
                    float body = (sub * p.subAmount + mid * p.midAmount) * bodyEnv;

                    // Resonance
                    float resEnv = GetResonanceEnvelopeStatic(t, dur);
                    float resF = p.resonanceFreq * pitchMod;
                    phaseRes += resF / staticSampleRate;
                    float res = Mathf.Sin(phaseRes * Mathf.PI * 2f);
                    res = BandpassFilterStatic(res, resF, p.resonanceQ, 0);
                    res *= resEnv * p.resonanceAmount;

                    // Noise
                    float noiseEnv = GetNoiseEnvelopeStatic(t, dur, p.noiseDecay);
                    rngState = rngState * 1103515245 + 12345;
                    float whiteNoise = ((rngState >> 16) & 0x7FFF) / 16383.5f - 1f;
                    noiseState =
                        noiseState * (0.95f + p.noiseColor * 0.04f)
                        + whiteNoise * (0.05f - p.noiseColor * 0.04f);
                    float coloredNoise =
                        noiseState * p.noiseColor + whiteNoise * (1f - p.noiseColor);
                    float noise = LowpassFilterStatic(
                        coloredNoise,
                        p.noiseCutoff * (1f - normalizedT * 0.4f),
                        0
                    );
                    noise *= noiseEnv * p.noiseAmount;

                    sample = transient + body + res + noise;
                    sample = DistortStatic(sample, p.distortion);
                    sample *= glitchMod;
                }

                float wet = ProcessReverbStatic(sample) * roomR;
                sample = sample * (1f - roomR * 0.3f) + wet;
                sample = FinalLimitStatic(sample);
                staticAudioBuffer[i] = sample;
            }

            // Fade out
            int fadeOutSamples = Mathf.Min(totalSamples / 5, staticSampleRate / 12);
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
                float normalize = 0.7f / maxAmp;
                for (int i = 0; i < totalSamples; i++)
                    staticAudioBuffer[i] *= normalize;
            }

            AudioClip clip = AudioClip.Create(
                "EnemyGun_" + type,
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

        private static float GetTransientEnvelopeStatic(float t, float decayRate)
        {
            float attack = 0.001f;
            float decay = 0.025f;
            if (t < attack)
                return t / attack;
            if (t < attack + decay)
                return Mathf.Exp(-((t - attack) / decay) * decayRate);
            return Mathf.Exp(-(t - attack - decay) * 30f) * 0.08f;
        }

        private static float GetBodyEnvelopeStatic(float t, float duration, float decayRate)
        {
            float attack = 0.003f;
            float sustain = duration * 0.15f;
            if (t < attack)
                return t / attack;
            if (t < attack + sustain)
                return 1f - (t - attack) / sustain * 0.1f;
            float dt = (t - attack - sustain) / (duration * 0.85f);
            return 0.9f * Mathf.Exp(-dt * decayRate);
        }

        private static float GetResonanceEnvelopeStatic(float t, float duration)
        {
            float delay = 0.002f;
            float attack = 0.005f;
            if (t < delay)
                return 0f;
            t -= delay;
            if (t < attack)
                return t / attack;
            return Mathf.Exp(-((t - attack) / (duration * 0.7f)) * 5f);
        }

        private static float GetNoiseEnvelopeStatic(float t, float duration, float decayRate)
        {
            float attack = 0.001f;
            float hold = 0.01f;
            if (t < attack)
                return t / attack;
            if (t < attack + hold)
                return 1f;
            float dt = (t - attack - hold) / (duration * 0.75f);
            return Mathf.Exp(-dt * decayRate);
        }

        private static float LowpassFilterStatic(float input, float cutoff, int stateIndex)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / staticSampleRate;
            float alpha = Mathf.Clamp01(dt / (rc + dt));
            staticLpState[stateIndex] += alpha * (input - staticLpState[stateIndex]);
            return staticLpState[stateIndex];
        }

        private static float BandpassFilterStatic(
            float input,
            float centerFreq,
            float q,
            int stateIndex
        )
        {
            float w0 = 2f * Mathf.PI * centerFreq / staticSampleRate;
            float alpha = Mathf.Sin(w0) / (2f * q);
            float b0 = alpha;
            float a1 = -2f * Mathf.Cos(w0);
            float a2 = 1f - alpha;
            float norm = 1f + alpha;
            b0 /= norm;
            a1 /= norm;
            a2 /= norm;
            float output =
                b0 * input - a1 * staticBpState[stateIndex] - a2 * staticBpState[stateIndex + 1];
            staticBpState[stateIndex + 1] = staticBpState[stateIndex];
            staticBpState[stateIndex] = output;
            return output;
        }

        private static float DistortStatic(float x, float amount)
        {
            if (amount <= 0f)
                return x;
            float drive = 1f + amount * 5f;
            x *= drive;
            if (x > 0)
                return (1f - Mathf.Exp(-x * 1.8f)) / 1.1f;
            return (-1f + Mathf.Exp(x * 1.4f)) / 1.1f;
        }

        private static float ProcessReverbStatic(float input)
        {
            float combOut = 0f;
            float[] combFeedback = { 0.82f, 0.80f, 0.79f, 0.77f, 0.76f, 0.75f };
            for (int i = 0; i < staticCombBuffers.Length; i++)
            {
                int idx = staticCombIndices[i];
                float delayed = staticCombBuffers[i][idx];
                staticCombBuffers[i][idx] = input + delayed * combFeedback[i];
                staticCombIndices[i] = (idx + 1) % staticCombBuffers[i].Length;
                combOut += delayed;
            }
            combOut /= staticCombBuffers.Length;

            float allpassOut = combOut;
            float allpassFeedback = 0.5f;
            for (int i = 0; i < staticAllpassBuffers.Length; i++)
            {
                int idx = staticAllpassIndices[i];
                float delayed = staticAllpassBuffers[i][idx];
                float temp = -allpassFeedback * allpassOut + delayed;
                staticAllpassBuffers[i][idx] = allpassOut + allpassFeedback * temp;
                staticAllpassIndices[i] = (idx + 1) % staticAllpassBuffers[i].Length;
                allpassOut = temp;
            }
            return allpassOut;
        }

        private static float FinalLimitStatic(float x)
        {
            float threshold = 0.75f;
            float knee = 0.15f;
            float absX = Mathf.Abs(x);
            if (absX < threshold - knee)
                return x;
            if (absX < threshold + knee)
            {
                float t = (absX - (threshold - knee)) / (2f * knee);
                float gain = 1f - t * t * 0.35f;
                return Mathf.Sign(x) * absX * gain;
            }
            float over = absX - threshold;
            return Mathf.Sign(x) * (threshold + over * 0.08f);
        }
    }
}
