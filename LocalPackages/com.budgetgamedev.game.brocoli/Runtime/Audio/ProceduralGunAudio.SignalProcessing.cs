using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralGunAudio
    {
        // =============== ENVELOPES ===============

        private float GetTransientEnvelope(float t, float decayRate)
        {
            float attack = 0.0006f;
            float decay = 0.02f;

            if (t < attack)
                return Mathf.Sqrt(t / attack);
            else if (t < attack + decay)
            {
                float dt = (t - attack) / decay;
                return Mathf.Exp(-dt * decayRate);
            }
            return Mathf.Exp(-(t - attack - decay) * 40f) * 0.05f;
        }

        private float GetBodyEnvelope(float t, float duration, float decayRate)
        {
            float attack = 0.002f;
            float sustain = duration * 0.1f;
            float decay = duration * 0.9f;

            if (t < attack)
                return t / attack;
            else if (t < attack + sustain)
                return 1f - (t - attack) / sustain * 0.15f;
            else
            {
                float dt = (t - attack - sustain) / decay;
                return 0.85f * Mathf.Exp(-dt * decayRate);
            }
        }

        private float GetMechEnvelope(float t, float duration)
        {
            float delay = 0.003f;
            float attack = 0.004f;
            float decay = duration * 0.6f;

            if (t < delay)
                return 0f;
            t -= delay;

            if (t < attack)
                return t / attack;
            else
            {
                float dt = (t - attack) / decay;
                return Mathf.Exp(-dt * 7f)
                    * (1f + Mathf.Sin(dt * 35f) * 0.12f * Mathf.Exp(-dt * 4f));
            }
        }

        private float GetNoiseEnvelope(float t, float duration, float decayRate)
        {
            float attack = 0.0008f;
            float hold = 0.008f;
            float decay = duration * 0.8f;

            if (t < attack)
                return t / attack;
            else if (t < attack + hold)
                return 1f;
            else
            {
                float dt = (t - attack - hold) / decay;
                return Mathf.Exp(-dt * decayRate);
            }
        }

        private float GetAirEnvelope(float t, float duration)
        {
            float attack = 0.008f;
            float decay = duration * 0.95f;

            if (t < attack)
                return Mathf.Sqrt(t / attack);
            else
            {
                float dt = (t - attack) / decay;
                return Mathf.Exp(-dt * 3.5f);
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

        private float BandpassFilter(float input, float centerFreq, float q, int stateIndex)
        {
            float w0 = 2f * Mathf.PI * centerFreq / sampleRate;
            float alpha = Mathf.Sin(w0) / (2f * q);

            float b0 = alpha;
            float a1 = -2f * Mathf.Cos(w0);
            float a2 = 1f - alpha;
            float norm = 1f + alpha;

            b0 /= norm;
            a1 /= norm;
            a2 /= norm;

            float output = b0 * input - a1 * bpState[stateIndex] - a2 * bpState[stateIndex + 1];
            bpState[stateIndex + 1] = bpState[stateIndex];
            bpState[stateIndex] = output;

            return output;
        }

        // =============== SATURATION ===============

        private float WarmSaturate(float x)
        {
            // Asymmetric saturation for analog warmth
            if (x > 0)
                return 1f - Mathf.Exp(-x * 1.5f);
            else
                return -1f + Mathf.Exp(x * 1.2f);
        }

        // =============== COMPRESSION ===============

        private float PunchCompress(float input, float punchAmount)
        {
            // Fast attack, medium release compressor for punch
            float attackTime = 0.0005f;
            float releaseTime = 0.05f;
            float threshold = 0.4f;
            float ratio = 4f + punchAmount * 4f;
            float makeupGain = 1f + punchAmount * 0.5f;

            float inputLevel = Mathf.Abs(input);

            float targetEnv = inputLevel;
            float coeff =
                inputLevel > compEnvelope
                    ? 1f - Mathf.Exp(-1f / (attackTime * sampleRate))
                    : 1f - Mathf.Exp(-1f / (releaseTime * sampleRate));

            compEnvelope += coeff * (targetEnv - compEnvelope);

            float gainReduction = 1f;
            if (compEnvelope > threshold)
            {
                float overDb = 20f * Mathf.Log10(compEnvelope / threshold);
                float reducedDb = overDb / ratio;
                gainReduction = threshold * Mathf.Pow(10f, reducedDb / 20f) / compEnvelope;
            }

            return input * gainReduction * makeupGain;
        }

        // =============== REVERB ===============

        private float ProcessReverb(float input)
        {
            // Comb filters in parallel
            float combOut = 0f;
            float[] combFeedback = { 0.84f, 0.82f, 0.81f, 0.79f, 0.78f, 0.77f };

            for (int i = 0; i < combBuffers.Length; i++)
            {
                int idx = combIndices[i];
                float delayed = combBuffers[i][idx];
                combBuffers[i][idx] = input + delayed * combFeedback[i];
                combIndices[i] = (idx + 1) % combBuffers[i].Length;
                combOut += delayed;
            }
            combOut /= combBuffers.Length;

            // Allpass filters in series
            float allpassOut = combOut;
            float allpassFeedback = 0.5f;

            for (int i = 0; i < allpassBuffers.Length; i++)
            {
                int idx = allpassIndices[i];
                float delayed = allpassBuffers[i][idx];
                float temp = -allpassFeedback * allpassOut + delayed;
                allpassBuffers[i][idx] = allpassOut + allpassFeedback * temp;
                allpassIndices[i] = (idx + 1) % allpassBuffers[i].Length;
                allpassOut = temp;
            }

            return allpassOut;
        }

        // =============== FINAL LIMITING ===============

        private float FinalLimit(float x)
        {
            // Soft knee limiter
            float threshold = 0.8f;
            float knee = 0.2f;

            float absX = Mathf.Abs(x);
            if (absX < threshold - knee)
                return x;
            else if (absX < threshold + knee)
            {
                // Soft knee region
                float t = (absX - (threshold - knee)) / (2f * knee);
                float gain = 1f - t * t * 0.3f;
                return Mathf.Sign(x) * absX * gain;
            }
            else
            {
                // Hard limit with slight compression
                float over = absX - threshold;
                return Mathf.Sign(x) * (threshold + over * 0.1f);
            }
        }
    }
}
