using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralEnemyProjectileHitAudio
    {
        private static EnemyHitPreset GetPresetStatic(EnemyHitSoundType type)
        {
            return GetPresetInternal(type);
        }

        private EnemyHitPreset GetPreset(EnemyHitSoundType type)
        {
            return GetPresetInternal(type);
        }

        private static EnemyHitPreset GetPresetInternal(EnemyHitSoundType type)
        {
            EnemyHitPreset p = new EnemyHitPreset();

            switch (type)
            {
                case EnemyHitSoundType.PlasmaImpact:
                    p.duration = 0.18f;
                    p.impactFreq = 150f;
                    p.impactAmount = 0.5f;
                    p.impactDecay = 20f;
                    p.bodyFreq = 280f;
                    p.bodyAmount = 0.4f;
                    p.bodyDecay = 15f;
                    p.highFreq = 2200f;
                    p.highAmount = 0.3f;
                    p.highDecay = 25f;
                    p.noiseAmount = 0.35f;
                    p.noiseDecay = 18f;
                    p.noiseCutoff = 3500f;
                    p.noiseColor = 0.3f;
                    p.hasWet = true;
                    p.wetAmount = 0.25f;
                    p.hasDistortion = false;
                    p.hasFlutter = false;
                    break;

                case EnemyHitSoundType.VoidBurst:
                    p.duration = 0.22f;
                    p.impactFreq = 80f;
                    p.impactAmount = 0.6f;
                    p.impactDecay = 12f;
                    p.bodyFreq = 160f;
                    p.bodyAmount = 0.45f;
                    p.bodyDecay = 10f;
                    p.highFreq = 1800f;
                    p.highAmount = 0.25f;
                    p.highDecay = 20f;
                    p.noiseAmount = 0.4f;
                    p.noiseDecay = 14f;
                    p.noiseCutoff = 2500f;
                    p.noiseColor = 0.6f;
                    p.hasWet = false;
                    p.hasDistortion = true;
                    p.distortionAmount = 0.3f;
                    p.hasFlutter = true;
                    p.flutterRate = 30f;
                    break;

                case EnemyHitSoundType.SwarmImpact:
                    p.duration = 0.15f;
                    p.impactFreq = 220f;
                    p.impactAmount = 0.4f;
                    p.impactDecay = 30f;
                    p.bodyFreq = 400f;
                    p.bodyAmount = 0.35f;
                    p.bodyDecay = 25f;
                    p.highFreq = 3500f;
                    p.highAmount = 0.35f;
                    p.highDecay = 35f;
                    p.noiseAmount = 0.45f;
                    p.noiseDecay = 30f;
                    p.noiseCutoff = 5000f;
                    p.noiseColor = 0.1f;
                    p.hasWet = false;
                    p.hasDistortion = false;
                    p.hasFlutter = true;
                    p.flutterRate = 60f;
                    break;

                case EnemyHitSoundType.CorruptedHit:
                    p.duration = 0.2f;
                    p.impactFreq = 120f;
                    p.impactAmount = 0.55f;
                    p.impactDecay = 18f;
                    p.bodyFreq = 240f;
                    p.bodyAmount = 0.4f;
                    p.bodyDecay = 15f;
                    p.highFreq = 2800f;
                    p.highAmount = 0.3f;
                    p.highDecay = 22f;
                    p.noiseAmount = 0.5f;
                    p.noiseDecay = 20f;
                    p.noiseCutoff = 4000f;
                    p.noiseColor = 0.4f;
                    p.hasWet = false;
                    p.hasDistortion = true;
                    p.distortionAmount = 0.5f;
                    p.hasFlutter = true;
                    p.flutterRate = 45f;
                    break;

                case EnemyHitSoundType.AcidSplash:
                    p.duration = 0.25f;
                    p.impactFreq = 100f;
                    p.impactAmount = 0.45f;
                    p.impactDecay = 15f;
                    p.bodyFreq = 200f;
                    p.bodyAmount = 0.35f;
                    p.bodyDecay = 12f;
                    p.highFreq = 1500f;
                    p.highAmount = 0.4f;
                    p.highDecay = 10f;
                    p.noiseAmount = 0.5f;
                    p.noiseDecay = 12f;
                    p.noiseCutoff = 3000f;
                    p.noiseColor = 0.5f;
                    p.hasWet = true;
                    p.wetAmount = 0.45f;
                    p.hasDistortion = false;
                    p.hasFlutter = false;
                    break;
            }

            return p;
        }

        public void PlayHitSound()
        {
            PlayHitSound(soundType);
        }

        public void PlayHitSound(EnemyHitSoundType type)
        {
            EnsureStaticInitialized();

            // Use cached clip if available
            AudioClip clip;
            if (cachedClips.TryGetValue(type, out clip) && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
                return;
            }

            // Fallback: generate with randomization
            EnemyHitPreset preset = GetPreset(type);

            // Apply randomization
            float randMult = 1f + Random.Range(-randomization, randomization);
            preset.impactFreq *= randMult;
            preset.bodyFreq *= randMult;
            preset.highFreq *= Mathf.Lerp(1f, randMult, 0.6f);

            clip = GenerateHitClip(preset);
            audioSource.PlayOneShot(clip, volume);
        }
    }
}
