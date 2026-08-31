using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralProjectileHitAudio
    {
        private AudioClip GenerateHitClip(HitPreset p)
        {
            int samples = Mathf.CeilToInt(p.duration * sampleRate);
            samples = Mathf.Min(samples, audioBuffer.Length);

            // Reset filter state
            for (int i = 0; i < lpState.Length; i++)
                lpState[i] = 0;

            float phase1 = 0f,
                phase2 = 0f,
                phase3 = 0f,
                phase4 = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;

                // Initial thump (sub bass)
                float thumpEnv = Mathf.Exp(-t * 40f);
                phase1 += p.thumpFreq * 2f * Mathf.PI / sampleRate;
                sample += Mathf.Sin(phase1) * p.thumpAmount * thumpEnv;

                // Impact pop
                float impactEnv = Mathf.Exp(-t * p.impactDecay);
                float impactFreq = p.impactFreq * (1f + t * 2f); // Slight pitch rise
                phase2 += impactFreq * 2f * Mathf.PI / sampleRate;
                sample += Mathf.Sin(phase2) * p.impactAmount * impactEnv;

                // Body resonance
                float bodyEnv = Mathf.Exp(-t * p.bodyDecay);
                phase3 += p.bodyFreq * 2f * Mathf.PI / sampleRate;
                sample += Mathf.Sin(phase3) * p.bodyAmount * bodyEnv;

                // High sizzle
                float sizzleEnv = Mathf.Exp(-t * p.sizzleDecay);
                phase4 += p.sizzleFreq * 2f * Mathf.PI / sampleRate;
                sample += Mathf.Sin(phase4) * p.sizzleAmount * sizzleEnv;
                // Add some harmonics to sizzle
                sample += Mathf.Sin(phase4 * 2.5f) * p.sizzleAmount * 0.3f * sizzleEnv;

                // Noise burst
                float noiseEnv = Mathf.Exp(-t * p.noiseDecay);
                float noise = Random.Range(-1f, 1f);
                noise = LowPassFilter(noise, p.noiseCutoff, 0);
                sample += noise * p.noiseAmount * noiseEnv;

                // Soft clip for punch
                sample = SoftClip(sample);

                audioBuffer[i] = sample;
            }

            AudioClip clip = AudioClip.Create("PlayerHit", samples, 1, sampleRate, false);
            float[] finalBuffer = new float[samples];
            System.Array.Copy(audioBuffer, finalBuffer, samples);
            clip.SetData(finalBuffer, 0);
            return clip;
        }

        /// <summary>
        /// Static version of GenerateHitClip for prewarming (doesn't use instance fields).
        /// </summary>
        private static AudioClip GenerateStaticHitClip(HitPreset p)
        {
            int samples = Mathf.CeilToInt(p.duration * staticSampleRate);
            samples = Mathf.Min(samples, staticAudioBuffer.Length);

            // Reset filter state
            for (int i = 0; i < staticLpState.Length; i++)
                staticLpState[i] = 0;

            float phase1 = 0f,
                phase2 = 0f,
                phase3 = 0f,
                phase4 = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / staticSampleRate;
                float sample = 0f;

                // Initial thump (sub bass)
                float thumpEnv = Mathf.Exp(-t * 40f);
                phase1 += p.thumpFreq * 2f * Mathf.PI / staticSampleRate;
                sample += Mathf.Sin(phase1) * p.thumpAmount * thumpEnv;

                // Impact pop
                float impactEnv = Mathf.Exp(-t * p.impactDecay);
                float impactFreq = p.impactFreq * (1f + t * 2f); // Slight pitch rise
                phase2 += impactFreq * 2f * Mathf.PI / staticSampleRate;
                sample += Mathf.Sin(phase2) * p.impactAmount * impactEnv;

                // Body resonance
                float bodyEnv = Mathf.Exp(-t * p.bodyDecay);
                phase3 += p.bodyFreq * 2f * Mathf.PI / staticSampleRate;
                sample += Mathf.Sin(phase3) * p.bodyAmount * bodyEnv;

                // High sizzle
                float sizzleEnv = Mathf.Exp(-t * p.sizzleDecay);
                phase4 += p.sizzleFreq * 2f * Mathf.PI / staticSampleRate;
                sample += Mathf.Sin(phase4) * p.sizzleAmount * sizzleEnv;
                // Add some harmonics to sizzle
                sample += Mathf.Sin(phase4 * 2.5f) * p.sizzleAmount * 0.3f * sizzleEnv;

                // Noise burst
                float noiseEnv = Mathf.Exp(-t * p.noiseDecay);
                float noise = Random.Range(-1f, 1f);
                noise = StaticLowPassFilter(noise, p.noiseCutoff, 0);
                sample += noise * p.noiseAmount * noiseEnv;

                // Soft clip for punch
                sample = SoftClip(sample);

                staticAudioBuffer[i] = sample;
            }

            AudioClip clip = AudioClip.Create("PlayerHit", samples, 1, staticSampleRate, false);
            float[] finalBuffer = new float[samples];
            System.Array.Copy(staticAudioBuffer, finalBuffer, samples);
            clip.SetData(finalBuffer, 0);
            return clip;
        }
    }
}
