using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralEnemyProjectileHitAudio
    {
        private AudioClip GenerateHitClip(EnemyHitPreset p)
        {
            int samples = Mathf.CeilToInt(p.duration * sampleRate);
            samples = Mathf.Min(samples, audioBuffer.Length);

            // Reset filter states
            for (int i = 0; i < lpState.Length; i++)
                lpState[i] = 0;
            for (int i = 0; i < hpState.Length; i++)
                hpState[i] = 0;

            float phase1 = 0f,
                phase2 = 0f,
                phase3 = 0f;
            float wetPhase = Random.Range(0f, Mathf.PI * 2f);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;

                // Flutter modulation
                float flutter = 1f;
                if (p.hasFlutter)
                {
                    flutter = 1f + 0.2f * Mathf.Sin(t * p.flutterRate * 2f * Mathf.PI);
                }

                // Impact thump (pitch drops)
                float impactEnv = Mathf.Exp(-t * p.impactDecay);
                float impactFreq = p.impactFreq * (1f - t * 0.5f) * flutter; // Pitch drops
                phase1 += impactFreq * 2f * Mathf.PI / sampleRate;
                sample += Mathf.Sin(phase1) * p.impactAmount * impactEnv;

                // Body resonance
                float bodyEnv = Mathf.Exp(-t * p.bodyDecay);
                phase2 += p.bodyFreq * flutter * 2f * Mathf.PI / sampleRate;
                sample += Mathf.Sin(phase2) * p.bodyAmount * bodyEnv;

                // High frequency component
                float highEnv = Mathf.Exp(-t * p.highDecay);
                phase3 += p.highFreq * 2f * Mathf.PI / sampleRate;
                sample += Mathf.Sin(phase3) * p.highAmount * highEnv;

                // Noise (colored)
                float noiseEnv = Mathf.Exp(-t * p.noiseDecay);
                float noise = Random.Range(-1f, 1f);

                // Apply color (brown noise = low pass filtered)
                if (p.noiseColor > 0)
                {
                    noise = LowPassFilter(noise, Mathf.Lerp(8000f, 500f, p.noiseColor), 0);
                }
                noise = LowPassFilter(noise, p.noiseCutoff, 1);
                sample += noise * p.noiseAmount * noiseEnv;

                // Wet/splatter effect
                if (p.hasWet)
                {
                    float wetEnv = Mathf.Exp(-t * 8f) * (1f - Mathf.Exp(-t * 50f));
                    wetPhase += (600f + Random.Range(-100f, 100f)) * 2f * Mathf.PI / sampleRate;
                    float wet = Mathf.Sin(wetPhase) * 0.5f + Random.Range(-0.5f, 0.5f);
                    wet = LowPassFilter(wet, 2000f, 2);
                    sample += wet * p.wetAmount * wetEnv;
                }

                // Distortion
                if (p.hasDistortion)
                {
                    sample = ApplyDistortion(sample, p.distortionAmount);
                }

                // Soft clip
                sample = SoftClip(sample * 0.9f);

                audioBuffer[i] = sample;
            }

            AudioClip clip = AudioClip.Create("EnemyProjectileHit", samples, 1, sampleRate, false);
            float[] finalBuffer = new float[samples];
            System.Array.Copy(audioBuffer, finalBuffer, samples);
            clip.SetData(finalBuffer, 0);
            return clip;
        }

        private float LowPassFilter(float input, float cutoff, int stateIndex)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / sampleRate;
            float alpha = dt / (rc + dt);
            lpState[stateIndex] = lpState[stateIndex] + alpha * (input - lpState[stateIndex]);
            return lpState[stateIndex];
        }

        private float ApplyDistortion(float x, float amount)
        {
            // Waveshaping distortion
            float k = 2f * amount / (1f - amount + 0.001f);
            return (1f + k) * x / (1f + k * Mathf.Abs(x));
        }

        private float SoftClip(float x)
        {
            if (x > 1f)
                return 1f - Mathf.Exp(-(x - 1f));
            if (x < -1f)
                return -1f + Mathf.Exp(-(-x - 1f));
            return x;
        }

        private static float StaticSoftClip(float x)
        {
            if (x > 1f)
                return 1f - Mathf.Exp(-(x - 1f));
            if (x < -1f)
                return -1f + Mathf.Exp(-(-x - 1f));
            return x;
        }

        private static float StaticLowPassFilter(float input, float cutoff, int stateIndex)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / staticSampleRate;
            float alpha = dt / (rc + dt);
            staticLpState[stateIndex] =
                staticLpState[stateIndex] + alpha * (input - staticLpState[stateIndex]);
            return staticLpState[stateIndex];
        }

        private static float StaticApplyDistortion(float x, float amount)
        {
            float k = 2f * amount / (1f - amount + 0.001f);
            return (1f + k) * x / (1f + k * Mathf.Abs(x));
        }
    }
}
