using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralEnemyWalkAudio
    {
        private static float GetImpactEnvelopeStatic(float t, float decayRate)
        {
            float attack = 0.002f;
            if (t < attack)
                return t / attack;
            return Mathf.Exp(-(t - attack) * decayRate);
        }

        private static float GetBodyEnvelopeStatic(float t, float duration, float decayRate)
        {
            float attack = 0.004f;
            float sustain = duration * 0.1f;
            if (t < attack)
                return t / attack;
            if (t < attack + sustain)
                return 1f;
            float dt = (t - attack - sustain) / (duration * 0.8f);
            return Mathf.Exp(-dt * decayRate);
        }

        private static float GetSecondaryEnvelopeStatic(float t, float duration, float decayRate)
        {
            float attack = 0.003f;
            if (t < attack)
                return t / attack;
            return Mathf.Exp(-(t - attack) * decayRate);
        }

        private static float GetNoiseEnvelopeStatic(float t, float duration, float decayRate)
        {
            float attack = 0.001f;
            if (t < attack)
                return t / attack;
            return Mathf.Exp(-(t - attack) * decayRate);
        }

        private static float GetWetEnvelopeStatic(float t, float duration)
        {
            float delay = 0.02f;
            if (t < delay)
                return 0f;
            float wetT = t - delay;
            float attack = 0.008f;
            if (wetT < attack)
                return wetT / attack;
            return Mathf.Exp(-(wetT - attack) * 12f);
        }

        private static float LowpassFilterStatic(float input, float cutoff, int stateIndex)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / _staticSampleRate;
            float alpha = Mathf.Clamp01(dt / (rc + dt));
            _staticLpState[stateIndex] += alpha * (input - _staticLpState[stateIndex]);
            return _staticLpState[stateIndex];
        }

        private static float HighpassFilterStatic(float input, float cutoff, int stateIndex)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / _staticSampleRate;
            float alpha = rc / (rc + dt);
            float output =
                alpha * (_staticHpState[stateIndex + 2] + input - _staticHpState[stateIndex]);
            _staticHpState[stateIndex] = input;
            _staticHpState[stateIndex + 2] = output;
            return output;
        }

        private static float SoftClipStatic(float x)
        {
            if (x > 1f)
                return 1f;
            if (x < -1f)
                return -1f;
            return x - (x * x * x) / 3f;
        }

        // Track which clip variation to use next for randomization
        private int _clipVariationIndex = 0;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;

            rb = GetComponent<Rigidbody>();

            sampleRate = AudioSettings.outputSampleRate;
            int maxSamples = Mathf.CeilToInt(0.4f * sampleRate);
            audioBuffer = new float[maxSamples];

            stepTimer = Random.Range(0f, baseStepInterval); // Randomize initial offset

            activeEnemyCount++;

            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerTransform = player.transform;
            }
        }

        void OnDestroy()
        {
            activeEnemyCount = Mathf.Max(0, activeEnemyCount - 1);
        }
    }
}
