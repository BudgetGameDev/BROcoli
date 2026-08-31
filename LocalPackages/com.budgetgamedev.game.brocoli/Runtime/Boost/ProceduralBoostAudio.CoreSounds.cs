using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralBoostAudio
    {
        private static AudioClip GenerateClip(BoostSoundType type)
        {
            System.Array.Clear(filterState, 0, filterState.Length);

            switch (type)
            {
                case BoostSoundType.Health:
                    return GenerateHealthSound();
                case BoostSoundType.Damage:
                    return GenerateDamageSound();
                case BoostSoundType.AttackSpeed:
                    return GenerateAttackSpeedSound();
                case BoostSoundType.MovementSpeed:
                    return GenerateMovementSpeedSound();
                case BoostSoundType.Experience:
                    return GenerateExperienceSound();
                case BoostSoundType.DetectionRadius:
                    return GenerateDetectionRadiusSound();
                case BoostSoundType.SprayRange:
                    return GenerateSprayRangeSound();
                case BoostSoundType.SprayWidth:
                    return GenerateSprayWidthSound();
                case BoostSoundType.Magnet:
                    return GenerateMagnetSound();
                case BoostSoundType.TimeSlow:
                    return GenerateTimeSlowSound();
                default:
                    return GenerateHealthSound();
            }
        }

        /// <summary>
        /// Health boost - Warm, soothing healing chime with soft harmonics.
        /// Evokes restoration and comfort.
        /// </summary>
        private static AudioClip GenerateHealthSound()
        {
            float duration = 0.4f;
            int numSamples = Mathf.CeilToInt(duration * sampleRate);
            numSamples = Mathf.Min(numSamples, audioBuffer.Length);

            // Warm minor 7th chord frequencies (healing feel)
            float[] freqs = { 392f, 466.16f, 587.33f, 698.46f }; // G4, Bb4, D5, F5

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;

                // Staggered warm tones
                for (int n = 0; n < freqs.Length; n++)
                {
                    float noteDelay = n * 0.04f;
                    float noteT = t - noteDelay;
                    if (noteT < 0f)
                        continue;

                    float env = GetHealEnvelope(noteT, duration - noteDelay);
                    float phase = noteT * freqs[n] * Mathf.PI * 2f;

                    // Soft sine with gentle harmonics
                    float tone = Mathf.Sin(phase) * 0.6f;
                    tone += Mathf.Sin(phase * 2f) * 0.2f;
                    tone += Mathf.Sin(phase * 0.5f) * 0.15f; // Sub-harmonic warmth

                    sample += tone * env * 0.3f;
                }

                // Soft shimmer overlay
                float shimmerEnv = Mathf.Exp(-t * 6f) * Mathf.Sin(t * 8f) * 0.5f + 0.5f;
                float shimmer = Mathf.Sin(t * 1200f * Mathf.PI * 2f) * shimmerEnv * 0.08f;
                sample += shimmer;

                // Gentle whoosh
                float whooshEnv = Mathf.Exp(-t * 4f);
                float noise = Lowpass(Random.Range(-1f, 1f), 800f, 0);
                sample += noise * whooshEnv * 0.06f;

                audioBuffer[i] = SoftClip(sample);
            }

            return FinalizeClip("HealthBoost", numSamples, duration);
        }

        /// <summary>
        /// Damage boost - Powerful impact with bass punch and metallic edge.
        /// Evokes strength and power.
        /// </summary>
        private static AudioClip GenerateDamageSound()
        {
            float duration = 0.3f;
            int numSamples = Mathf.CeilToInt(duration * sampleRate);
            numSamples = Mathf.Min(numSamples, audioBuffer.Length);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;

                // Heavy sub-bass punch
                float punchEnv = Mathf.Exp(-t * 25f);
                float punch = Mathf.Sin(t * 60f * Mathf.PI * 2f) * punchEnv * 0.5f;
                punch += Mathf.Sin(t * 90f * Mathf.PI * 2f) * punchEnv * 0.3f;
                sample += punch;

                // Aggressive mid attack
                float attackEnv = Mathf.Exp(-t * 15f);
                float attack = Mathf.Sin(t * 200f * Mathf.PI * 2f) * attackEnv * 0.4f;
                attack += Mathf.Sin(t * 350f * Mathf.PI * 2f) * attackEnv * 0.25f;
                sample += attack;

                // Metallic transient
                if (t < 0.05f)
                {
                    float transientEnv = Mathf.Exp(-t * 80f);
                    float transient = Mathf.Sin(t * 2500f * Mathf.PI * 2f) * transientEnv * 0.3f;
                    transient += Mathf.Sin(t * 3800f * Mathf.PI * 2f) * transientEnv * 0.15f;
                    sample += transient;
                }

                // Distorted noise burst
                float noiseEnv = Mathf.Exp(-t * 30f);
                float noise = Lowpass(Random.Range(-1f, 1f), 3000f, 0);
                sample += noise * noiseEnv * 0.15f;

                // Power chord undertone
                float chordEnv = Mathf.Exp(-t * 8f);
                sample += Mathf.Sin(t * 110f * Mathf.PI * 2f) * chordEnv * 0.2f; // A2
                sample += Mathf.Sin(t * 165f * Mathf.PI * 2f) * chordEnv * 0.15f; // E3

                audioBuffer[i] = HardClip(sample * 1.2f);
            }

            return FinalizeClip("DamageBoost", numSamples, duration);
        }

        /// <summary>
        /// Attack Speed boost - Rapid staccato tones accelerating upward.
        /// Evokes quickness and rapid-fire.
        /// </summary>
        private static AudioClip GenerateAttackSpeedSound()
        {
            float duration = 0.35f;
            int numSamples = Mathf.CeilToInt(duration * sampleRate);
            numSamples = Mathf.Min(numSamples, audioBuffer.Length);

            int numClicks = 6;
            float clickDuration = 0.025f;

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;

                // Accelerating clicks
                for (int c = 0; c < numClicks; c++)
                {
                    // Exponentially decreasing gaps (accelerating)
                    float clickTime = c * (0.06f - c * 0.008f);
                    float clickT = t - clickTime;

                    if (clickT >= 0f && clickT < clickDuration)
                    {
                        float clickEnv = Mathf.Exp(-clickT * 100f);
                        float freq = 1200f + c * 150f; // Rising pitch
                        float click = Mathf.Sin(clickT * freq * Mathf.PI * 2f) * clickEnv;
                        click += Mathf.Sin(clickT * freq * 2f * Mathf.PI * 2f) * clickEnv * 0.3f;
                        sample += click * 0.4f;
                    }
                }

                // Fast arpeggio sweep
                float sweepEnv = Mathf.Exp(-t * 10f);
                float sweepFreq = 800f + t * 3000f; // Rising sweep
                sample += Mathf.Sin(t * sweepFreq * Mathf.PI * 2f) * sweepEnv * 0.2f;

                // Mechanical whir
                float whirEnv = Mathf.Sin(t * 30f) * 0.5f + 0.5f;
                whirEnv *= Mathf.Exp(-t * 8f);
                sample += Mathf.Sin(t * 400f * Mathf.PI * 2f) * whirEnv * 0.1f;

                audioBuffer[i] = SoftClip(sample);
            }

            return FinalizeClip("AttackSpeedBoost", numSamples, duration);
        }
    }
}
