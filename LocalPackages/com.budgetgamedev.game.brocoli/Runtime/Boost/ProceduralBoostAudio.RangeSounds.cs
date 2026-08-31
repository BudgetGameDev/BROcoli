using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralBoostAudio
    {
        /// <summary>
        /// Movement Speed boost - Whooshing wind with doppler-like effect.
        /// Evokes motion and velocity.
        /// </summary>
        private static AudioClip GenerateMovementSpeedSound()
        {
            float duration = 0.4f;
            int numSamples = Mathf.CeilToInt(duration * sampleRate);
            numSamples = Mathf.Min(numSamples, audioBuffer.Length);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;

                // Main whoosh - filtered noise with sweeping cutoff
                float whooshEnv = GetWhooshEnvelope(t, duration);
                float cutoff = 800f + 2500f * Mathf.Sin(t * 4f * Mathf.PI);
                cutoff = Mathf.Max(cutoff, 400f);

                float noise = Random.Range(-1f, 1f);
                float whoosh = Lowpass(noise, cutoff, 0);
                whoosh = Highpass(whoosh, 200f, 1);
                sample += whoosh * whooshEnv * 0.4f;

                // Wind whistle harmonics
                float whistleEnv = Mathf.Exp(-Mathf.Abs(t - 0.15f) * 8f);
                float whistleFreq = 2000f + Mathf.Sin(t * 20f) * 300f;
                sample += Mathf.Sin(t * whistleFreq * Mathf.PI * 2f) * whistleEnv * 0.1f;

                // Speed streaks (high frequency bursts)
                for (int s = 0; s < 3; s++)
                {
                    float streakT = t - s * 0.1f;
                    if (streakT > 0f && streakT < 0.08f)
                    {
                        float streakEnv = Mathf.Exp(-streakT * 40f);
                        float streakFreq = 3000f + s * 500f;
                        sample +=
                            Mathf.Sin(streakT * streakFreq * Mathf.PI * 2f) * streakEnv * 0.08f;
                    }
                }

                // Low rumble undertone
                float rumbleEnv = Mathf.Exp(-t * 5f);
                sample += Mathf.Sin(t * 80f * Mathf.PI * 2f) * rumbleEnv * 0.15f;

                audioBuffer[i] = SoftClip(sample);
            }

            return FinalizeClip("MovementSpeedBoost", numSamples, duration);
        }

        /// <summary>
        /// Experience boost - Bright, sparkling ascending tones.
        /// Evokes growth and enlightenment.
        /// </summary>
        private static AudioClip GenerateExperienceSound()
        {
            float duration = 0.35f;
            int numSamples = Mathf.CeilToInt(duration * sampleRate);
            numSamples = Mathf.Min(numSamples, audioBuffer.Length);

            // Ascending major scale snippet
            float[] notes = { 523.25f, 659.25f, 783.99f, 880f, 1046.5f }; // C5, E5, G5, A5, C6

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;

                // Quick ascending arpeggio
                for (int n = 0; n < notes.Length; n++)
                {
                    float noteStart = n * 0.035f;
                    float noteT = t - noteStart;
                    if (noteT < 0f)
                        continue;

                    float noteEnv = Mathf.Exp(-noteT * 12f);
                    float phase = noteT * notes[n] * Mathf.PI * 2f;

                    float tone = Mathf.Sin(phase) * 0.5f;
                    tone += Mathf.Sin(phase * 2f) * 0.2f;
                    tone += Mathf.Sin(phase * 3f) * 0.1f;

                    sample += tone * noteEnv * 0.25f;
                }

                // Sparkle layer
                float sparkleEnv = Mathf.Exp(-t * 8f);
                float sparkleFreq = 2800f + Mathf.Sin(t * 25f) * 400f;
                sample += Mathf.Sin(t * sparkleFreq * Mathf.PI * 2f) * sparkleEnv * 0.12f;

                // Bright shimmer
                float shimmerMod = Mathf.Sin(t * 40f) * 0.5f + 0.5f;
                sample +=
                    Mathf.Sin(t * 4000f * Mathf.PI * 2f) * shimmerMod * Mathf.Exp(-t * 15f) * 0.06f;

                audioBuffer[i] = SoftClip(sample);
            }

            return FinalizeClip("ExperienceBoost", numSamples, duration);
        }

        /// <summary>
        /// Detection Radius boost - Radar ping with sonar-like resonance.
        /// Evokes scanning and awareness.
        /// </summary>
        private static AudioClip GenerateDetectionRadiusSound()
        {
            float duration = 0.5f;
            int numSamples = Mathf.CeilToInt(duration * sampleRate);
            numSamples = Mathf.Min(numSamples, audioBuffer.Length);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;

                // Main sonar ping
                float pingEnv = Mathf.Exp(-t * 6f);
                float pingFreq = 1400f;
                float ping = Mathf.Sin(t * pingFreq * Mathf.PI * 2f) * pingEnv * 0.5f;

                // Resonant harmonics (sonar character)
                ping += Mathf.Sin(t * pingFreq * 2f * Mathf.PI * 2f) * pingEnv * 0.2f;
                ping += Mathf.Sin(t * pingFreq * 0.5f * Mathf.PI * 2f) * pingEnv * 0.15f;
                sample += ping;

                // Expanding wave effect (echo)
                for (int e = 1; e <= 3; e++)
                {
                    float echoT = t - e * 0.1f;
                    if (echoT > 0f)
                    {
                        float echoEnv = Mathf.Exp(-echoT * 8f) * (1f - e * 0.25f);
                        float echoFreq = pingFreq * (1f - e * 0.05f); // Slight pitch drop
                        sample += Mathf.Sin(echoT * echoFreq * Mathf.PI * 2f) * echoEnv * 0.2f;
                    }
                }

                // Electronic blip at start
                if (t < 0.03f)
                {
                    float blipEnv = Mathf.Exp(-t * 150f);
                    sample += Mathf.Sin(t * 3000f * Mathf.PI * 2f) * blipEnv * 0.25f;
                }

                // Subtle static hum
                float humEnv = Mathf.Exp(-t * 4f);
                float noise = Lowpass(Random.Range(-1f, 1f), 600f, 0);
                sample += noise * humEnv * 0.04f;

                audioBuffer[i] = SoftClip(sample);
            }

            return FinalizeClip("DetectionRadiusBoost", numSamples, duration);
        }

        /// <summary>
        /// Spray Range boost - Extending reach sound with stretching quality.
        /// Evokes distance and projection.
        /// </summary>
        private static AudioClip GenerateSprayRangeSound()
        {
            float duration = 0.4f;
            int numSamples = Mathf.CeilToInt(duration * sampleRate);
            numSamples = Mathf.Min(numSamples, audioBuffer.Length);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;

                // Extending tone (pitch rises then settles)
                float extendEnv = GetExtendEnvelope(t, duration);
                float pitchCurve = 1f + 0.3f * Mathf.Exp(-t * 8f); // Starts high, settles
                float baseFreq = 600f * pitchCurve;

                sample += Mathf.Sin(t * baseFreq * Mathf.PI * 2f) * extendEnv * 0.35f;
                sample += Mathf.Sin(t * baseFreq * 1.5f * Mathf.PI * 2f) * extendEnv * 0.2f;

                // Spray hiss (extends outward)
                float hissEnv = Mathf.Max(0f, 1f - t * 3f) * Mathf.Exp(-t * 4f);
                float hissCutoff = 4000f + 2000f * t; // Opening up
                float hiss = Lowpass(Random.Range(-1f, 1f), hissCutoff, 0);
                hiss = Highpass(hiss, 1500f, 1);
                sample += hiss * hissEnv * 0.2f;

                // Pressure release burst at start
                if (t < 0.05f)
                {
                    float burstEnv = Mathf.Exp(-t * 60f);
                    float burst = Lowpass(Random.Range(-1f, 1f), 5000f, 2);
                    sample += burst * burstEnv * 0.25f;
                }

                // Reaching tone
                float reachEnv = Mathf.Exp(-Mathf.Abs(t - 0.1f) * 10f);
                sample += Mathf.Sin(t * 1000f * Mathf.PI * 2f) * reachEnv * 0.15f;

                audioBuffer[i] = SoftClip(sample);
            }

            return FinalizeClip("SprayRangeBoost", numSamples, duration);
        }
    }
}
