using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Procedural impact sound generator for player projectile hits.
    /// Creates satisfying, punchy impact sounds when projectiles hit enemies.
    /// </summary>
    public partial class ProceduralProjectileHitAudio : MonoBehaviour
    {
        public enum HitSoundType
        {
            Energy, // Sci-fi energy weapon hit
            Ballistic, // Traditional bullet impact
            Plasma, // Hot plasma sizzle
            Laser, // Sharp laser hit
            Explosive, // Small explosion on impact
        }

        [Header("Sound Type")]
        [SerializeField]
        private HitSoundType soundType = HitSoundType.Energy;

        [Header("Volume")]
        [Range(0f, 1f)]
        [SerializeField]
        private float volume = 0.5f;

        [Header("Variation")]
        [Range(0f, 0.3f)]
        [SerializeField]
        private float randomization = 0.15f;

        private struct HitPreset
        {
            public float duration;

            // Impact pop
            public float impactFreq;
            public float impactAmount;
            public float impactDecay;

            // Body/resonance
            public float bodyFreq;
            public float bodyAmount;
            public float bodyDecay;

            // High sizzle
            public float sizzleFreq;
            public float sizzleAmount;
            public float sizzleDecay;

            // Noise burst
            public float noiseAmount;
            public float noiseDecay;
            public float noiseCutoff;

            // Thump
            public float thumpFreq;
            public float thumpAmount;
        }

        private AudioSource audioSource;
        private int sampleRate;
        private float[] audioBuffer;
        private float[] lpState = new float[4];

        private static ProceduralProjectileHitAudio instance;

        // Static caching for prewarmed clips
        private static System.Collections.Generic.Dictionary<HitSoundType, AudioClip> cachedClips;
        private static bool isPrewarmed = false;
        private static int staticSampleRate;
        private static float[] staticAudioBuffer;
        private static float[] staticLpState = new float[4];

        void Awake()
        {
            // Allow multiple instances but keep reference for static access
            if (instance == null)
                instance = this;

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f; // 2D sound

            sampleRate = AudioSettings.outputSampleRate;
            int maxSamples = Mathf.CeilToInt(0.3f * sampleRate);
            audioBuffer = new float[maxSamples];
        }

        private static void EnsureStaticInitialized()
        {
            if (staticAudioBuffer == null)
            {
                staticSampleRate = AudioSettings.outputSampleRate;
                int maxSamples = Mathf.CeilToInt(0.3f * staticSampleRate);
                staticAudioBuffer = new float[maxSamples];
                cachedClips = new System.Collections.Generic.Dictionary<HitSoundType, AudioClip>();
            }
        }

        /// <summary>
        /// Pre-generate all hit sound clips to avoid hitches during gameplay.
        /// Call this during loading screen.
        /// </summary>
        public static void PrewarmAll()
        {
            EnsureStaticInitialized();
            if (isPrewarmed)
                return;

            foreach (HitSoundType type in System.Enum.GetValues(typeof(HitSoundType)))
            {
                if (!cachedClips.ContainsKey(type))
                {
                    HitPreset preset = GetPresetStatic(type);
                    cachedClips[type] = GenerateStaticHitClip(preset);
                }
            }
            isPrewarmed = true;
        }
    }
}
