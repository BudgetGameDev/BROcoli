using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralEnemyGunAudio
    {
        /// <summary>
        /// Pre-generates and caches audio clips for all enemy gun sound types.
        /// Call this during game initialization to eliminate first-use hitches.
        /// </summary>
        public static void PrewarmAll()
        {
            if (isPrewarmed)
                return;

            staticSampleRate = AudioSettings.outputSampleRate;
            int maxSamples = Mathf.CeilToInt(1.2f * staticSampleRate);
            staticAudioBuffer = new float[maxSamples];
            staticLpState = new float[4];
            staticHpState = new float[2];
            staticBpState = new float[4];
            InitializeReverbStatic();

            cachedClips = new System.Collections.Generic.Dictionary<EnemyGunSoundType, AudioClip>();
            cachedClips[EnemyGunSoundType.PlasmaSpitter] = GenerateGunClipStatic(
                EnemyGunSoundType.PlasmaSpitter
            );
            cachedClips[EnemyGunSoundType.VoidCannon] = GenerateGunClipStatic(
                EnemyGunSoundType.VoidCannon
            );
            cachedClips[EnemyGunSoundType.SwarmShot] = GenerateGunClipStatic(
                EnemyGunSoundType.SwarmShot
            );
            cachedClips[EnemyGunSoundType.CorruptedBlaster] = GenerateGunClipStatic(
                EnemyGunSoundType.CorruptedBlaster
            );
            cachedClips[EnemyGunSoundType.AcidLauncher] = GenerateGunClipStatic(
                EnemyGunSoundType.AcidLauncher
            );
            cachedClips[EnemyGunSoundType.Sneeze] = GenerateGunClipStatic(EnemyGunSoundType.Sneeze);

            isPrewarmed = true;
        }

        private static void InitializeReverbStatic()
        {
            int[] allpassDelays = { 281, 89, 31, 47 };
            int[] combDelays = { 1423, 1361, 1847, 1993, 1531, 1721 };

            staticAllpassBuffers = new float[allpassDelays.Length][];
            staticAllpassIndices = new int[allpassDelays.Length];
            for (int i = 0; i < allpassDelays.Length; i++)
            {
                staticAllpassBuffers[i] = new float[allpassDelays[i]];
                staticAllpassIndices[i] = 0;
            }

            staticCombBuffers = new float[combDelays.Length][];
            staticCombIndices = new int[combDelays.Length];
            for (int i = 0; i < combDelays.Length; i++)
            {
                staticCombBuffers[i] = new float[combDelays[i]];
                staticCombIndices[i] = 0;
            }
        }

        private static void ClearReverbStatic()
        {
            for (int i = 0; i < staticAllpassBuffers.Length; i++)
                System.Array.Clear(staticAllpassBuffers[i], 0, staticAllpassBuffers[i].Length);
            for (int i = 0; i < staticCombBuffers.Length; i++)
                System.Array.Clear(staticCombBuffers[i], 0, staticCombBuffers[i].Length);
        }

        private static EnemyGunPreset GetPresetStatic(EnemyGunSoundType type)
        {
            EnemyGunPreset p = new EnemyGunPreset();
            switch (type)
            {
                case EnemyGunSoundType.PlasmaSpitter:
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
                    p.duration = 0.18f;
                    p.roomSize = 0.05f;
                    p.transientFreq1 = 400f;
                    p.transientFreq2 = 800f;
                    p.transientDecay = 12f;
                    p.transientAmount = 0.2f;
                    p.subFreq = 80f;
                    p.subAmount = 0.15f;
                    p.midFreq = 250f;
                    p.midAmount = 0.25f;
                    p.bodyDecay = 10f;
                    p.modFreq = 8f;
                    p.modDepth = 0.15f;
                    p.resonanceFreq = 350f;
                    p.resonanceQ = 2f;
                    p.resonanceAmount = 0.2f;
                    p.noiseColor = 0.7f;
                    p.noiseCutoff = 1200f;
                    p.noiseAmount = 0.25f;
                    p.noiseDecay = 8f;
                    p.distortion = 0.05f;
                    p.pitchBend = -0.1f;
                    p.hasChorus = false;
                    p.hasGlitch = false;
                    break;
            }
            return p;
        }
    }
}
