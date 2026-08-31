using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralProjectileHitAudio
    {
        private static HitPreset GetPresetStatic(HitSoundType type)
        {
            HitPreset p = new HitPreset();

            switch (type)
            {
                case HitSoundType.Energy:
                    p.duration = 0.15f;
                    p.impactFreq = 180f;
                    p.impactAmount = 0.6f;
                    p.impactDecay = 25f;
                    p.bodyFreq = 320f;
                    p.bodyAmount = 0.35f;
                    p.bodyDecay = 18f;
                    p.sizzleFreq = 2800f;
                    p.sizzleAmount = 0.25f;
                    p.sizzleDecay = 30f;
                    p.noiseAmount = 0.3f;
                    p.noiseDecay = 35f;
                    p.noiseCutoff = 4000f;
                    p.thumpFreq = 60f;
                    p.thumpAmount = 0.4f;
                    break;

                case HitSoundType.Ballistic:
                    p.duration = 0.12f;
                    p.impactFreq = 140f;
                    p.impactAmount = 0.7f;
                    p.impactDecay = 35f;
                    p.bodyFreq = 250f;
                    p.bodyAmount = 0.3f;
                    p.bodyDecay = 25f;
                    p.sizzleFreq = 3500f;
                    p.sizzleAmount = 0.15f;
                    p.sizzleDecay = 50f;
                    p.noiseAmount = 0.45f;
                    p.noiseDecay = 40f;
                    p.noiseCutoff = 6000f;
                    p.thumpFreq = 50f;
                    p.thumpAmount = 0.5f;
                    break;

                case HitSoundType.Plasma:
                    p.duration = 0.2f;
                    p.impactFreq = 200f;
                    p.impactAmount = 0.5f;
                    p.impactDecay = 20f;
                    p.bodyFreq = 400f;
                    p.bodyAmount = 0.4f;
                    p.bodyDecay = 12f;
                    p.sizzleFreq = 3200f;
                    p.sizzleAmount = 0.4f;
                    p.sizzleDecay = 15f;
                    p.noiseAmount = 0.35f;
                    p.noiseDecay = 20f;
                    p.noiseCutoff = 5000f;
                    p.thumpFreq = 70f;
                    p.thumpAmount = 0.35f;
                    break;

                case HitSoundType.Laser:
                    p.duration = 0.1f;
                    p.impactFreq = 280f;
                    p.impactAmount = 0.55f;
                    p.impactDecay = 40f;
                    p.bodyFreq = 600f;
                    p.bodyAmount = 0.3f;
                    p.bodyDecay = 35f;
                    p.sizzleFreq = 4500f;
                    p.sizzleAmount = 0.35f;
                    p.sizzleDecay = 45f;
                    p.noiseAmount = 0.2f;
                    p.noiseDecay = 50f;
                    p.noiseCutoff = 7000f;
                    p.thumpFreq = 90f;
                    p.thumpAmount = 0.25f;
                    break;

                case HitSoundType.Explosive:
                    p.duration = 0.25f;
                    p.impactFreq = 100f;
                    p.impactAmount = 0.7f;
                    p.impactDecay = 15f;
                    p.bodyFreq = 180f;
                    p.bodyAmount = 0.5f;
                    p.bodyDecay = 10f;
                    p.sizzleFreq = 2000f;
                    p.sizzleAmount = 0.3f;
                    p.sizzleDecay = 12f;
                    p.noiseAmount = 0.55f;
                    p.noiseDecay = 18f;
                    p.noiseCutoff = 3500f;
                    p.thumpFreq = 40f;
                    p.thumpAmount = 0.6f;
                    break;
            }

            return p;
        }

        private HitPreset GetPreset(HitSoundType type)
        {
            return GetPresetStatic(type);
        }

        public void PlayHitSound()
        {
            PlayHitSound(soundType);
        }

        public void PlayHitSound(HitSoundType type)
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
            HitPreset preset = GetPreset(type);

            // Apply randomization
            float randMult = 1f + Random.Range(-randomization, randomization);
            preset.impactFreq *= randMult;
            preset.bodyFreq *= randMult;
            preset.sizzleFreq *= Mathf.Lerp(1f, randMult, 0.5f);

            clip = GenerateHitClip(preset);
            audioSource.PlayOneShot(clip, volume);
        }
    }
}
