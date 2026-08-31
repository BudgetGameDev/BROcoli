using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Procedural walk/movement sound generator for enemies.
    /// Generates distinct alien/monster footstep sounds different from the player.
    /// Triggers based on movement velocity rather than hop state.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public partial class ProceduralEnemyWalkAudio : MonoBehaviour
    {
        public enum EnemyWalkSoundType
        {
            Skitter, // Fast, light, insectoid
            Thud, // Heavy, slow stomping
            Slither, // Wet, sliding movement
            Shuffle, // Shambling, zombie-like
            Clatter, // Bony, skeletal rattling
        }

        [Header("Sound Type")]
        [SerializeField]
        private EnemyWalkSoundType soundType = EnemyWalkSoundType.Skitter;

        [Header("Volume")]
        [Range(0f, 1f)]
        [SerializeField]
        private float volume = 0.25f;

        [Header("Variation")]
        [Range(0f, 0.3f)]
        [SerializeField]
        private float randomization = 0.15f;

        [Header("Step Timing")]
        [SerializeField]
        private float baseStepInterval = 0.3f; // Time between steps at max speed

        [SerializeField]
        private float minSpeedForSound = 0.5f; // Minimum velocity to trigger sounds

        private struct WalkPreset
        {
            public float duration;

            // Impact
            public float impactFreq;
            public float impactAmount;
            public float impactDecay;

            // Body/resonance
            public float bodyFreq;
            public float bodyAmount;
            public float bodyDecay;

            // Secondary sound
            public float secondaryFreq;
            public float secondaryDelay;
            public float secondaryAmount;

            // Noise
            public float noiseAmount;
            public float noiseDecay;
            public float noiseCutoff;
            public float noiseColor; // 0=white, 1=brown

            // Character
            public bool hasClick;
            public float clickFreq;
            public float clickAmount;

            public bool hasWet;
            public float wetAmount;
        }

        private WalkPreset currentPreset;
        private AudioSource audioSource;
        private Rigidbody rb;
        private int sampleRate;
        private float[] audioBuffer;

        private float[] lpState = new float[4];
        private float[] hpState = new float[4]; // High-pass filter state
        private float stepTimer;
        private float lastSpeed;

        // Distance-based volume attenuation
        private static Transform playerTransform;
        private static int activeEnemyCount = 0;
        private const float MAX_AUDIBLE_DISTANCE = 20f;
        private const float MIN_AUDIBLE_DISTANCE = 3f;

        // Static caching for prewarmed clips (multiple clips per type for variation)
        private static System.Collections.Generic.Dictionary<
            EnemyWalkSoundType,
            AudioClip[]
        > _clipCache;
        private static bool _isPrewarmed = false;
        private static int _staticSampleRate;
        private static float[] _staticAudioBuffer;
        private static float[] _staticLpState;
        private static float[] _staticHpState;
        private const int ClipsPerType = 3; // Pre-generate multiple clips for variation

        /// <summary>
        /// Pre-generates and caches audio clips for all walk sound types.
        /// Call this during game initialization to eliminate first-use hitches.
        /// </summary>
        public static void PrewarmAll()
        {
            if (_isPrewarmed)
                return;

            _staticSampleRate = AudioSettings.outputSampleRate;
            int maxSamples = Mathf.CeilToInt(0.4f * _staticSampleRate);
            _staticAudioBuffer = new float[maxSamples];
            _staticLpState = new float[4];
            _staticHpState = new float[4];

            _clipCache = new System.Collections.Generic.Dictionary<
                EnemyWalkSoundType,
                AudioClip[]
            >();

            // Generate multiple clips per type for variation
            foreach (EnemyWalkSoundType type in System.Enum.GetValues(typeof(EnemyWalkSoundType)))
            {
                AudioClip[] clips = new AudioClip[ClipsPerType];
                for (int i = 0; i < ClipsPerType; i++)
                {
                    clips[i] = GenerateStepClipStatic(type, i);
                }
                _clipCache[type] = clips;
            }

            _isPrewarmed = true;
            Debug.Log("[ProceduralEnemyWalkAudio] Pre-warmed all walk sound types");
        }
    }
}
