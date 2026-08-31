using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralEnemyGunAudio
    {
        private EnemyGunPreset GetPreset(EnemyGunSoundType type)
        {
            EnemyGunPreset p = new EnemyGunPreset();

            switch (type)
            {
                case EnemyGunSoundType.PlasmaSpitter:
                    // Organic, wet, splattery - low frequency, gurgly
                    p.duration = 0.25f;
                    p.roomSize = 0.15f;
                    p.transientFreq1 = 800f;
                    p.transientFreq2 = 1200f;
                    p.transientDecay = 8f;
                    p.transientAmount = 0.3f;
                    p.subFreq = 65f;
                    p.subAmount = 0.5f;
                    p.midFreq = 220f;
                    p.midAmount = 0.6f;
                    p.bodyDecay = 6f;
                    p.modFreq = 12f;
                    p.modDepth = 0.4f;
                    p.resonanceFreq = 350f;
                    p.resonanceQ = 4f;
                    p.resonanceAmount = 0.5f;
                    p.noiseColor = 0.8f;
                    p.noiseCutoff = 600f;
                    p.noiseAmount = 0.45f;
                    p.noiseDecay = 7f;
                    p.distortion = 0.3f;
                    p.pitchBend = -0.3f;
                    p.hasChorus = true;
                    p.hasGlitch = false;
                    break;

                case EnemyGunSoundType.VoidCannon:
                    // Deep, resonant, ominous - sub-bass heavy
                    p.duration = 0.35f;
                    p.roomSize = 0.4f;
                    p.transientFreq1 = 600f;
                    p.transientFreq2 = 900f;
                    p.transientDecay = 5f;
                    p.transientAmount = 0.4f;
                    p.subFreq = 30f;
                    p.subAmount = 0.9f;
                    p.midFreq = 80f;
                    p.midAmount = 0.7f;
                    p.bodyDecay = 4f;
                    p.modFreq = 3f;
                    p.modDepth = 0.2f;
                    p.resonanceFreq = 120f;
                    p.resonanceQ = 6f;
                    p.resonanceAmount = 0.6f;
                    p.noiseColor = 0.9f;
                    p.noiseCutoff = 300f;
                    p.noiseAmount = 0.3f;
                    p.noiseDecay = 5f;
                    p.distortion = 0.5f;
                    p.pitchBend = -0.5f;
                    p.hasChorus = false;
                    p.hasGlitch = false;
                    break;

                case EnemyGunSoundType.SwarmShot:
                    // Buzzing, insectoid - high frequency modulation
                    p.duration = 0.2f;
                    p.roomSize = 0.1f;
                    p.transientFreq1 = 2200f;
                    p.transientFreq2 = 3500f;
                    p.transientDecay = 12f;
                    p.transientAmount = 0.35f;
                    p.subFreq = 90f;
                    p.subAmount = 0.25f;
                    p.midFreq = 380f;
                    p.midAmount = 0.4f;
                    p.bodyDecay = 10f;
                    p.modFreq = 85f;
                    p.modDepth = 0.6f;
                    p.resonanceFreq = 550f;
                    p.resonanceQ = 8f;
                    p.resonanceAmount = 0.55f;
                    p.noiseColor = 0.3f;
                    p.noiseCutoff = 2500f;
                    p.noiseAmount = 0.35f;
                    p.noiseDecay = 9f;
                    p.distortion = 0.2f;
                    p.pitchBend = 0.2f;
                    p.hasChorus = true;
                    p.hasGlitch = false;
                    break;

                case EnemyGunSoundType.CorruptedBlaster:
                    // Distorted, glitchy - digital artifacts
                    p.duration = 0.22f;
                    p.roomSize = 0.2f;
                    p.transientFreq1 = 1500f;
                    p.transientFreq2 = 2400f;
                    p.transientDecay = 10f;
                    p.transientAmount = 0.5f;
                    p.subFreq = 55f;
                    p.subAmount = 0.4f;
                    p.midFreq = 280f;
                    p.midAmount = 0.5f;
                    p.bodyDecay = 8f;
                    p.modFreq = 45f;
                    p.modDepth = 0.35f;
                    p.resonanceFreq = 420f;
                    p.resonanceQ = 5f;
                    p.resonanceAmount = 0.45f;
                    p.noiseColor = 0.5f;
                    p.noiseCutoff = 1800f;
                    p.noiseAmount = 0.4f;
                    p.noiseDecay = 8f;
                    p.distortion = 0.7f;
                    p.pitchBend = 0f;
                    p.hasChorus = false;
                    p.hasGlitch = true;
                    break;

                case EnemyGunSoundType.AcidLauncher:
                    // Hissing, corrosive - white noise heavy
                    p.duration = 0.28f;
                    p.roomSize = 0.25f;
                    p.transientFreq1 = 1100f;
                    p.transientFreq2 = 1800f;
                    p.transientDecay = 7f;
                    p.transientAmount = 0.35f;
                    p.subFreq = 50f;
                    p.subAmount = 0.35f;
                    p.midFreq = 180f;
                    p.midAmount = 0.45f;
                    p.bodyDecay = 6f;
                    p.modFreq = 8f;
                    p.modDepth = 0.25f;
                    p.resonanceFreq = 280f;
                    p.resonanceQ = 3f;
                    p.resonanceAmount = 0.4f;
                    p.noiseColor = 0.2f;
                    p.noiseCutoff = 3500f;
                    p.noiseAmount = 0.6f;
                    p.noiseDecay = 6f;
                    p.distortion = 0.25f;
                    p.pitchBend = -0.15f;
                    p.hasChorus = true;
                    p.hasGlitch = false;
                    break;

                case EnemyGunSoundType.Sneeze:
                    // Soft muffled cough/spit - subtle and non-intrusive
                    p.duration = 0.18f; // Short, quick sound
                    p.roomSize = 0.05f; // Minimal reverb
                    p.transientFreq1 = 400f; // Low soft thump
                    p.transientFreq2 = 800f; // Muffled pop
                    p.transientDecay = 12f; // Very fast decay
                    p.transientAmount = 0.2f; // Subtle transient
                    p.subFreq = 80f; // Soft bass
                    p.subAmount = 0.15f; // Minimal sub
                    p.midFreq = 250f; // Muffled mid
                    p.midAmount = 0.25f; // Low mid presence
                    p.bodyDecay = 10f; // Quick fade
                    p.modFreq = 8f; // Gentle wobble
                    p.modDepth = 0.15f; // Subtle modulation
                    p.resonanceFreq = 350f; // Low resonance
                    p.resonanceQ = 2f; // Soft Q
                    p.resonanceAmount = 0.2f; // Minimal resonance
                    p.noiseColor = 0.7f; // Darker noise (pink/brown)
                    p.noiseCutoff = 1200f; // Low-passed breath
                    p.noiseAmount = 0.25f; // Subtle breath
                    p.noiseDecay = 8f; // Fast noise decay
                    p.distortion = 0.05f; // Almost no distortion
                    p.pitchBend = -0.1f; // Slight pitch drop
                    p.hasChorus = false;
                    p.hasGlitch = false;
                    break;
            }

            return p;
        }

        private float GetDistanceAttenuation()
        {
            if (playerTransform == null)
                return 1f;

            float dist = GroundPlane.GroundDistance(transform.position, playerTransform.position);
            if (dist > MAX_AUDIBLE_DISTANCE)
                return 0f;

            float attenuation = Mathf.InverseLerp(MAX_AUDIBLE_DISTANCE, MIN_AUDIBLE_DISTANCE, dist);
            return Mathf.Sqrt(attenuation);
        }
    }
}
