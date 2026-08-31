using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralEnemyMeleeAudio
    {
        private MeleePreset GetPreset(MeleeSoundType type)
        {
            MeleePreset p = new MeleePreset();

            switch (type)
            {
                case MeleeSoundType.Slash:
                    // Quick, sharp whoosh with slight metallic edge
                    p.duration = 0.18f;
                    p.whooshFreqStart = 1800f;
                    p.whooshFreqEnd = 400f;
                    p.whooshAmount = 0.5f;
                    p.whooshDecay = 10f;
                    p.impactDelay = 0.08f;
                    p.impactFreq = 180f;
                    p.impactAmount = 0.3f;
                    p.impactDecay = 12f;
                    p.bodyFreq = 120f;
                    p.bodyAmount = 0.25f;
                    p.bodyDecay = 8f;
                    p.noiseBurst = 0.6f;
                    p.noiseDecay = 15f;
                    p.noiseCutoff = 3500f;
                    p.hasMetallic = true;
                    p.metallicFreq = 2800f;
                    p.metallicAmount = 0.15f;
                    break;

                case MeleeSoundType.Bite:
                    // Wet, crunchy chomp sound
                    p.duration = 0.15f;
                    p.whooshFreqStart = 600f;
                    p.whooshFreqEnd = 200f;
                    p.whooshAmount = 0.2f;
                    p.whooshDecay = 12f;
                    p.impactDelay = 0.02f;
                    p.impactFreq = 250f;
                    p.impactAmount = 0.6f;
                    p.impactDecay = 18f;
                    p.bodyFreq = 90f;
                    p.bodyAmount = 0.5f;
                    p.bodyDecay = 10f;
                    p.noiseBurst = 0.7f;
                    p.noiseDecay = 20f;
                    p.noiseCutoff = 1200f;
                    p.hasMetallic = false;
                    p.metallicFreq = 0f;
                    p.metallicAmount = 0f;
                    break;

                case MeleeSoundType.Slam:
                    // Heavy, booming impact
                    p.duration = 0.3f;
                    p.whooshFreqStart = 500f;
                    p.whooshFreqEnd = 100f;
                    p.whooshAmount = 0.35f;
                    p.whooshDecay = 6f;
                    p.impactDelay = 0.05f;
                    p.impactFreq = 60f;
                    p.impactAmount = 0.9f;
                    p.impactDecay = 5f;
                    p.bodyFreq = 45f;
                    p.bodyAmount = 0.8f;
                    p.bodyDecay = 4f;
                    p.noiseBurst = 0.5f;
                    p.noiseDecay = 8f;
                    p.noiseCutoff = 800f;
                    p.hasMetallic = false;
                    p.metallicFreq = 0f;
                    p.metallicAmount = 0f;
                    break;

                case MeleeSoundType.Swipe:
                    // Wide, sweeping whoosh
                    p.duration = 0.22f;
                    p.whooshFreqStart = 2200f;
                    p.whooshFreqEnd = 300f;
                    p.whooshAmount = 0.65f;
                    p.whooshDecay = 8f;
                    p.impactDelay = 0.12f;
                    p.impactFreq = 150f;
                    p.impactAmount = 0.25f;
                    p.impactDecay = 10f;
                    p.bodyFreq = 100f;
                    p.bodyAmount = 0.2f;
                    p.bodyDecay = 7f;
                    p.noiseBurst = 0.75f;
                    p.noiseDecay = 12f;
                    p.noiseCutoff = 4500f;
                    p.hasMetallic = true;
                    p.metallicFreq = 3200f;
                    p.metallicAmount = 0.12f;
                    break;

                case MeleeSoundType.Stinger:
                    // Sharp, piercing thrust
                    p.duration = 0.12f;
                    p.whooshFreqStart = 3500f;
                    p.whooshFreqEnd = 1200f;
                    p.whooshAmount = 0.4f;
                    p.whooshDecay = 18f;
                    p.impactDelay = 0.04f;
                    p.impactFreq = 320f;
                    p.impactAmount = 0.5f;
                    p.impactDecay = 15f;
                    p.bodyFreq = 200f;
                    p.bodyAmount = 0.3f;
                    p.bodyDecay = 12f;
                    p.noiseBurst = 0.45f;
                    p.noiseDecay = 18f;
                    p.noiseCutoff = 5000f;
                    p.hasMetallic = true;
                    p.metallicFreq = 4200f;
                    p.metallicAmount = 0.25f;
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
            return Mathf.Sqrt(attenuation); // Smoother falloff
        }

        /// <summary>
        /// Pre-generates and caches audio clips for all melee sound types.
        /// Call this during game initialization to eliminate first-use hitches.
        /// </summary>
        public static void PrewarmAll()
        {
            if (isPrewarmed)
                return;

            staticSampleRate = AudioSettings.outputSampleRate;
            int maxSamples = Mathf.CeilToInt(0.8f * staticSampleRate);
            staticAudioBuffer = new float[maxSamples];
            staticLpState = new float[4];
            staticHpState = new float[2];

            cachedClips = new System.Collections.Generic.Dictionary<MeleeSoundType, AudioClip>();
            cachedClips[MeleeSoundType.Slash] = GenerateMeleeClipStatic(MeleeSoundType.Slash);
            cachedClips[MeleeSoundType.Bite] = GenerateMeleeClipStatic(MeleeSoundType.Bite);
            cachedClips[MeleeSoundType.Slam] = GenerateMeleeClipStatic(MeleeSoundType.Slam);
            cachedClips[MeleeSoundType.Swipe] = GenerateMeleeClipStatic(MeleeSoundType.Swipe);
            cachedClips[MeleeSoundType.Stinger] = GenerateMeleeClipStatic(MeleeSoundType.Stinger);

            isPrewarmed = true;
        }

        private static MeleePreset GetPresetStatic(MeleeSoundType type)
        {
            MeleePreset p = new MeleePreset();

            switch (type)
            {
                case MeleeSoundType.Slash:
                    p.duration = 0.18f;
                    p.whooshFreqStart = 1800f;
                    p.whooshFreqEnd = 400f;
                    p.whooshAmount = 0.5f;
                    p.whooshDecay = 10f;
                    p.impactDelay = 0.08f;
                    p.impactFreq = 180f;
                    p.impactAmount = 0.3f;
                    p.impactDecay = 12f;
                    p.bodyFreq = 120f;
                    p.bodyAmount = 0.25f;
                    p.bodyDecay = 8f;
                    p.noiseBurst = 0.6f;
                    p.noiseDecay = 15f;
                    p.noiseCutoff = 3500f;
                    p.hasMetallic = true;
                    p.metallicFreq = 2800f;
                    p.metallicAmount = 0.15f;
                    break;
                case MeleeSoundType.Bite:
                    p.duration = 0.15f;
                    p.whooshFreqStart = 600f;
                    p.whooshFreqEnd = 200f;
                    p.whooshAmount = 0.2f;
                    p.whooshDecay = 12f;
                    p.impactDelay = 0.02f;
                    p.impactFreq = 250f;
                    p.impactAmount = 0.6f;
                    p.impactDecay = 18f;
                    p.bodyFreq = 90f;
                    p.bodyAmount = 0.5f;
                    p.bodyDecay = 10f;
                    p.noiseBurst = 0.7f;
                    p.noiseDecay = 20f;
                    p.noiseCutoff = 1200f;
                    p.hasMetallic = false;
                    p.metallicFreq = 0f;
                    p.metallicAmount = 0f;
                    break;
                case MeleeSoundType.Slam:
                    p.duration = 0.3f;
                    p.whooshFreqStart = 500f;
                    p.whooshFreqEnd = 100f;
                    p.whooshAmount = 0.35f;
                    p.whooshDecay = 6f;
                    p.impactDelay = 0.05f;
                    p.impactFreq = 60f;
                    p.impactAmount = 0.9f;
                    p.impactDecay = 5f;
                    p.bodyFreq = 45f;
                    p.bodyAmount = 0.8f;
                    p.bodyDecay = 4f;
                    p.noiseBurst = 0.5f;
                    p.noiseDecay = 8f;
                    p.noiseCutoff = 800f;
                    p.hasMetallic = false;
                    p.metallicFreq = 0f;
                    p.metallicAmount = 0f;
                    break;
                case MeleeSoundType.Swipe:
                    p.duration = 0.22f;
                    p.whooshFreqStart = 2200f;
                    p.whooshFreqEnd = 300f;
                    p.whooshAmount = 0.65f;
                    p.whooshDecay = 8f;
                    p.impactDelay = 0.12f;
                    p.impactFreq = 150f;
                    p.impactAmount = 0.25f;
                    p.impactDecay = 10f;
                    p.bodyFreq = 100f;
                    p.bodyAmount = 0.2f;
                    p.bodyDecay = 7f;
                    p.noiseBurst = 0.75f;
                    p.noiseDecay = 12f;
                    p.noiseCutoff = 4500f;
                    p.hasMetallic = true;
                    p.metallicFreq = 3200f;
                    p.metallicAmount = 0.12f;
                    break;
                case MeleeSoundType.Stinger:
                    p.duration = 0.12f;
                    p.whooshFreqStart = 3500f;
                    p.whooshFreqEnd = 1200f;
                    p.whooshAmount = 0.4f;
                    p.whooshDecay = 18f;
                    p.impactDelay = 0.04f;
                    p.impactFreq = 320f;
                    p.impactAmount = 0.5f;
                    p.impactDecay = 15f;
                    p.bodyFreq = 200f;
                    p.bodyAmount = 0.3f;
                    p.bodyDecay = 12f;
                    p.noiseBurst = 0.45f;
                    p.noiseDecay = 18f;
                    p.noiseCutoff = 5000f;
                    p.hasMetallic = true;
                    p.metallicFreq = 4200f;
                    p.metallicAmount = 0.25f;
                    break;
            }
            return p;
        }
    }
}
