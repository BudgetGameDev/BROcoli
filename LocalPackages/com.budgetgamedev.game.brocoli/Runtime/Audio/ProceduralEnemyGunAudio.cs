using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Procedural gun audio for enemies - distinct from player weapon sounds.
    /// Features alien/organic/corrupted weapon sounds with different tonal characteristics.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public partial class ProceduralEnemyGunAudio : MonoBehaviour
    {
        public enum EnemyGunSoundType
        {
            PlasmaSpitter, // Organic, wet, splattery
            VoidCannon, // Deep, resonant, ominous
            SwarmShot, // Buzzing, insectoid
            CorruptedBlaster, // Distorted, glitchy
            AcidLauncher, // Hissing, corrosive
            Sneeze, // Corona sneeze attack
        }

        // Static caching for prewarmed clips
        private static System.Collections.Generic.Dictionary<
            EnemyGunSoundType,
            AudioClip
        > cachedClips;
        private static bool isPrewarmed = false;
        private static int staticSampleRate;
        private static float[] staticAudioBuffer;
        private static float[] staticLpState;
        private static float[] staticHpState;
        private static float[] staticBpState;
        private static float[][] staticAllpassBuffers;
        private static int[] staticAllpassIndices;
        private static float[][] staticCombBuffers;
        private static int[] staticCombIndices;

        [Header("Sound Type")]
        [SerializeField]
        private EnemyGunSoundType soundType = EnemyGunSoundType.PlasmaSpitter;

        [Header("Volume")]
        [Range(0f, 1f)]
        [SerializeField]
        private float volume = 0.0675f;

        [Header("Variation")]
        [Range(0f, 0.25f)]
        [SerializeField]
        private float randomization = 0.15f;

        private struct EnemyGunPreset
        {
            public float duration;
            public float roomSize;

            // Transient
            public float transientFreq1;
            public float transientFreq2;
            public float transientDecay;
            public float transientAmount;

            // Body
            public float subFreq;
            public float subAmount;
            public float midFreq;
            public float midAmount;
            public float bodyDecay;

            // Character layers
            public float modFreq;
            public float modDepth;
            public float resonanceFreq;
            public float resonanceQ;
            public float resonanceAmount;

            // Noise
            public float noiseColor; // 0 = white, 1 = pink/brown
            public float noiseCutoff;
            public float noiseAmount;
            public float noiseDecay;

            // Special effects
            public float distortion;
            public float pitchBend;
            public bool hasChorus;
            public bool hasGlitch;
        }

        private EnemyGunPreset currentPreset;
        private AudioSource audioSource;
        private int sampleRate;
        private float[] audioBuffer;

        // Filter states
        private float[] lpState = new float[4];
        private float[] hpState = new float[2];
        private float[] bpState = new float[4];

        // Reverb
        private float[][] allpassBuffers;
        private int[] allpassIndices;
        private float[][] combBuffers;
        private int[] combIndices;

        // Distance-based volume attenuation
        private static Transform playerTransform;
        private const float MAX_AUDIBLE_DISTANCE = 30f;
        private const float MIN_AUDIBLE_DISTANCE = 3f;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;

            sampleRate = AudioSettings.outputSampleRate;
            int maxSamples = Mathf.CeilToInt(1.2f * sampleRate);
            audioBuffer = new float[maxSamples];

            InitializeReverb();

            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerTransform = player.transform;
            }
        }

        private void InitializeReverb()
        {
            int[] allpassDelays = { 281, 89, 31, 47 };
            int[] combDelays = { 1423, 1361, 1847, 1993, 1531, 1721 };

            allpassBuffers = new float[allpassDelays.Length][];
            allpassIndices = new int[allpassDelays.Length];
            for (int i = 0; i < allpassDelays.Length; i++)
            {
                allpassBuffers[i] = new float[allpassDelays[i]];
                allpassIndices[i] = 0;
            }

            combBuffers = new float[combDelays.Length][];
            combIndices = new int[combDelays.Length];
            for (int i = 0; i < combDelays.Length; i++)
            {
                combBuffers[i] = new float[combDelays[i]];
                combIndices[i] = 0;
            }
        }

        private void ClearReverb()
        {
            for (int i = 0; i < allpassBuffers.Length; i++)
                System.Array.Clear(allpassBuffers[i], 0, allpassBuffers[i].Length);
            for (int i = 0; i < combBuffers.Length; i++)
                System.Array.Clear(combBuffers[i], 0, combBuffers[i].Length);
        }
    }
}
