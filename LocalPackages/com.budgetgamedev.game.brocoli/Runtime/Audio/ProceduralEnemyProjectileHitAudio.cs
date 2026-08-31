using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Procedural impact sound generator for enemy projectile hits.
    /// Creates distinct alien/organic impact sounds different from player projectiles.
    /// </summary>
    public partial class ProceduralEnemyProjectileHitAudio : MonoBehaviour
    {
        public enum EnemyHitSoundType
        {
            PlasmaImpact, // Acidic plasma splatter
            VoidBurst, // Dark energy dissipation
            SwarmImpact, // Multiple small impacts
            CorruptedHit, // Glitchy, corrupted impact
            AcidSplash, // Wet, caustic splash
        }

        [Header("Sound Type")]
        [SerializeField]
        private EnemyHitSoundType soundType = EnemyHitSoundType.PlasmaImpact;

        [Header("Volume")]
        [Range(0f, 1f)]
        [SerializeField]
        private float volume = 0.45f;

        [Header("Variation")]
        [Range(0f, 0.3f)]
        [SerializeField]
        private float randomization = 0.18f;

        private struct EnemyHitPreset
        {
            public float duration;

            // Impact
            public float impactFreq;
            public float impactAmount;
            public float impactDecay;

            // Body
            public float bodyFreq;
            public float bodyAmount;
            public float bodyDecay;

            // High frequency component
            public float highFreq;
            public float highAmount;
            public float highDecay;

            // Noise
            public float noiseAmount;
            public float noiseDecay;
            public float noiseCutoff;
            public float noiseColor; // 0 = white, 1 = brown

            // Special effects
            public bool hasWet;
            public float wetAmount;
            public bool hasDistortion;
            public float distortionAmount;
            public bool hasFlutter;
            public float flutterRate;
        }

        private AudioSource audioSource;
        private int sampleRate;
        private float[] audioBuffer;
        private float[] lpState = new float[4];
        private float[] hpState = new float[2];

        // Static caching for prewarmed clips
        private static System.Collections.Generic.Dictionary<
            EnemyHitSoundType,
            AudioClip
        > cachedClips;
        private static bool isPrewarmed = false;
        private static int staticSampleRate;
        private static float[] staticAudioBuffer;
        private static float[] staticLpState = new float[4];

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;

            sampleRate = AudioSettings.outputSampleRate;
            int maxSamples = Mathf.CeilToInt(0.35f * sampleRate);
            audioBuffer = new float[maxSamples];
        }

        private static void EnsureStaticInitialized()
        {
            if (staticAudioBuffer == null)
            {
                staticSampleRate = AudioSettings.outputSampleRate;
                int maxSamples = Mathf.CeilToInt(0.35f * staticSampleRate);
                staticAudioBuffer = new float[maxSamples];
                cachedClips = new System.Collections.Generic.Dictionary<
                    EnemyHitSoundType,
                    AudioClip
                >();
            }
        }

        /// <summary>
        /// Pre-generate all enemy hit sound clips to avoid hitches during gameplay.
        /// Call this during loading screen.
        /// </summary>
        public static void PrewarmAll()
        {
            EnsureStaticInitialized();
            if (isPrewarmed)
                return;

            foreach (EnemyHitSoundType type in System.Enum.GetValues(typeof(EnemyHitSoundType)))
            {
                if (!cachedClips.ContainsKey(type))
                {
                    EnemyHitPreset preset = GetPresetStatic(type);
                    cachedClips[type] = GenerateStaticHitClip(preset);
                }
            }
            isPrewarmed = true;
        }
    }
}
