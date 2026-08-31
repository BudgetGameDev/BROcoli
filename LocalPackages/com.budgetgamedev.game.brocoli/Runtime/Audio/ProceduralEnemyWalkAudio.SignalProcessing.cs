using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralEnemyWalkAudio
    {
        // =============== ENVELOPES ===============

        private float GetImpactEnvelope(float t, float decayRate)
        {
            float attack = 0.002f;

            if (t < attack)
                return t / attack;
            else
                return Mathf.Exp(-(t - attack) * decayRate);
        }

        private float GetBodyEnvelope(float t, float duration, float decayRate)
        {
            float attack = 0.004f;
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

        private float GetSecondaryEnvelope(float t, float duration, float decayRate)
        {
            float attack = 0.003f;

            if (t < attack)
                return t / attack;
            else
                return Mathf.Exp(-(t - attack) * decayRate);
        }

        private float GetNoiseEnvelope(float t, float duration, float decayRate)
        {
            float attack = 0.001f;

            if (t < attack)
                return t / attack;
            else
                return Mathf.Exp(-(t - attack) * decayRate);
        }

        private float GetWetEnvelope(float t, float duration)
        {
            // Delayed wet squelch
            float delay = 0.02f;
            if (t < delay)
                return 0f;

            float wetT = t - delay;
            float attack = 0.008f;

            if (wetT < attack)
                return wetT / attack;
            else
                return Mathf.Exp(-(wetT - attack) * 12f);
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
            alpha = Mathf.Clamp01(alpha);

            float output = alpha * (hpState[stateIndex + 2] + input - hpState[stateIndex]);
            hpState[stateIndex] = input;
            hpState[stateIndex + 2] = output;
            return output;
        }

        private float SoftClip(float x)
        {
            if (x > 1f)
                return 1f;
            if (x < -1f)
                return -1f;
            return x - (x * x * x) / 3f;
        }
    }
}
