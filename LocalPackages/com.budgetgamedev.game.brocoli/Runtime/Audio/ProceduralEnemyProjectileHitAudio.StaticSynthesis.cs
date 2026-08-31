using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralEnemyProjectileHitAudio
    {
        private static AudioClip GenerateStaticHitClip(EnemyHitPreset p)
        {
            int samples = Mathf.CeilToInt(p.duration * staticSampleRate);
            samples = Mathf.Min(samples, staticAudioBuffer.Length);

            // Reset filter states
            for (int i = 0; i < staticLpState.Length; i++)
                staticLpState[i] = 0;

            float phase1 = 0f,
                phase2 = 0f,
                phase3 = 0f;
            float wetPhase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / staticSampleRate;
                float sample = 0f;

                // Flutter modulation
                float flutter = 1f;
                if (p.hasFlutter)
                {
                    flutter = 1f + 0.2f * Mathf.Sin(t * p.flutterRate * 2f * Mathf.PI);
                }

                // Impact thump (pitch drops)
                float impactEnv = Mathf.Exp(-t * p.impactDecay);
                float impactFreq = p.impactFreq * (1f - t * 0.5f) * flutter;
                phase1 += impactFreq * 2f * Mathf.PI / staticSampleRate;
                sample += Mathf.Sin(phase1) * p.impactAmount * impactEnv;

                // Body resonance
                float bodyEnv = Mathf.Exp(-t * p.bodyDecay);
                phase2 += p.bodyFreq * flutter * 2f * Mathf.PI / staticSampleRate;
                sample += Mathf.Sin(phase2) * p.bodyAmount * bodyEnv;

                // High frequency component
                float highEnv = Mathf.Exp(-t * p.highDecay);
                phase3 += p.highFreq * 2f * Mathf.PI / staticSampleRate;
                sample += Mathf.Sin(phase3) * p.highAmount * highEnv;

                // Noise (colored)
                float noiseEnv = Mathf.Exp(-t * p.noiseDecay);
                float noise = Random.Range(-1f, 1f);

                // Apply color (brown noise = low pass filtered)
                if (p.noiseColor > 0)
                {
                    noise = StaticLowPassFilter(noise, Mathf.Lerp(8000f, 500f, p.noiseColor), 0);
                }
                noise = StaticLowPassFilter(noise, p.noiseCutoff, 1);
                sample += noise * p.noiseAmount * noiseEnv;

                // Wet/splatter effect
                if (p.hasWet)
                {
                    float wetEnv = Mathf.Exp(-t * 8f) * (1f - Mathf.Exp(-t * 50f));
                    wetPhase += 600f * 2f * Mathf.PI / staticSampleRate;
                    float wet = Mathf.Sin(wetPhase) * 0.5f + Random.Range(-0.5f, 0.5f);
                    wet = StaticLowPassFilter(wet, 2000f, 2);
                    sample += wet * p.wetAmount * wetEnv;
                }

                // Distortion
                if (p.hasDistortion)
                {
                    sample = StaticApplyDistortion(sample, p.distortionAmount);
                }

                // Soft clip
                sample = StaticSoftClip(sample * 0.9f);

                staticAudioBuffer[i] = sample;
            }

            AudioClip clip = AudioClip.Create(
                "EnemyProjectileHitCached",
                samples,
                1,
                staticSampleRate,
                false
            );
            float[] finalBuffer = new float[samples];
            System.Array.Copy(staticAudioBuffer, finalBuffer, samples);
            clip.SetData(finalBuffer, 0);
            return clip;
        }

        // Static helper to play hit sound at position
        public static void PlayHit(
            Vector3 position,
            EnemyHitSoundType type = EnemyHitSoundType.PlasmaImpact,
            float vol = 0.45f
        )
        {
            EnsureStaticInitialized();

            // Use cached clip directly if available (avoids GameObject/Component overhead)
            AudioClip clip;
            if (cachedClips.TryGetValue(type, out clip) && clip != null)
            {
                // Play directly using AudioSource.PlayClipAtPoint for minimal overhead
                AudioSource.PlayClipAtPoint(clip, position, vol);
                return;
            }

            // Fallback: Create temporary audio source (shouldn't happen after prewarming)
            GameObject temp = new GameObject("EnemyProjectileHitSound");
            temp.transform.position = position;

            AudioSource source = temp.AddComponent<AudioSource>();
            source.spatialBlend = 0.5f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = 30f;

            ProceduralEnemyProjectileHitAudio hitAudio =
                temp.AddComponent<ProceduralEnemyProjectileHitAudio>();
            hitAudio.volume = vol;
            hitAudio.soundType = type;
            hitAudio.PlayHitSound();

            Destroy(temp, 0.5f);
        }
    }
}
