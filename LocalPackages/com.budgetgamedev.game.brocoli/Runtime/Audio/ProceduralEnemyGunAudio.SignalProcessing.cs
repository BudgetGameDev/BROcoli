using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralEnemyGunAudio
    {
        // =============== ENVELOPES ===============

        private float GetTransientEnvelope(float t, float decayRate)
        {
            float attack = 0.001f;
            float decay = 0.025f;

            if (t < attack)
                return t / attack;
            else if (t < attack + decay)
            {
                float dt = (t - attack) / decay;
                return Mathf.Exp(-dt * decayRate);
            }
            return Mathf.Exp(-(t - attack - decay) * 30f) * 0.08f;
        }

        private float GetBodyEnvelope(float t, float duration, float decayRate)
        {
            float attack = 0.003f;
            float sustain = duration * 0.15f;
            float decay = duration * 0.85f;

            if (t < attack)
                return t / attack;
            else if (t < attack + sustain)
                return 1f - (t - attack) / sustain * 0.1f;
            else
            {
                float dt = (t - attack - sustain) / decay;
                return 0.9f * Mathf.Exp(-dt * decayRate);
            }
        }

        private float GetResonanceEnvelope(float t, float duration)
        {
            float delay = 0.002f;
            float attack = 0.005f;
            float decay = duration * 0.7f;

            if (t < delay)
                return 0f;
            t -= delay;

            if (t < attack)
                return t / attack;
            else
            {
                float dt = (t - attack) / decay;
                return Mathf.Exp(-dt * 5f);
            }
        }

        private float GetNoiseEnvelope(float t, float duration, float decayRate)
        {
            float attack = 0.001f;
            float hold = 0.01f;
            float decay = duration * 0.75f;

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

        private float Distort(float x, float amount)
        {
            if (amount <= 0f)
                return x;

            float drive = 1f + amount * 5f;
            x *= drive;

            // Asymmetric waveshaping for alien character
            if (x > 0)
                return (1f - Mathf.Exp(-x * 1.8f)) / 1.1f;
            else
                return (-1f + Mathf.Exp(x * 1.4f)) / 1.1f;
        }

        private float ProcessReverb(float input)
        {
            float combOut = 0f;
            float[] combFeedback = { 0.82f, 0.80f, 0.79f, 0.77f, 0.76f, 0.75f };

            for (int i = 0; i < combBuffers.Length; i++)
            {
                int idx = combIndices[i];
                float delayed = combBuffers[i][idx];
                combBuffers[i][idx] = input + delayed * combFeedback[i];
                combIndices[i] = (idx + 1) % combBuffers[i].Length;
                combOut += delayed;
            }
            combOut /= combBuffers.Length;

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

        private float FinalLimit(float x)
        {
            float threshold = 0.75f;
            float knee = 0.15f;

            float absX = Mathf.Abs(x);
            if (absX < threshold - knee)
                return x;
            else if (absX < threshold + knee)
            {
                float t = (absX - (threshold - knee)) / (2f * knee);
                float gain = 1f - t * t * 0.35f;
                return Mathf.Sign(x) * absX * gain;
            }
            else
            {
                float over = absX - threshold;
                return Mathf.Sign(x) * (threshold + over * 0.08f);
            }
        }
    }
}
