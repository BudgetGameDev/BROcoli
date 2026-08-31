using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Procedural melee attack audio for enemies.
    /// Generates distinct sounds for different melee attack types.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public partial class ProceduralEnemyMeleeAudio : MonoBehaviour
    {
        public enum MeleeSoundType
        {
            Slash, // Quick slashing attack
            Bite, // Biting/chomping attack
            Slam, // Heavy impact slam
            Swipe, // Wide sweeping attack
            Stinger, // Piercing/stabbing attack
        }

        // Static caching for prewarmed clips
        private static System.Collections.Generic.Dictionary<MeleeSoundType, AudioClip> cachedClips;
        private static bool isPrewarmed = false;
        private static int staticSampleRate;
        private static float[] staticAudioBuffer;
        private static float[] staticLpState;
        private static float[] staticHpState;

        [Header("Sound Type")]
        [SerializeField]
        private MeleeSoundType soundType = MeleeSoundType.Slash;

        [Header("Volume")]
        [Range(0f, 1f)]
        [SerializeField]
        private float volume = 0.7f;

        [Header("Variation")]
        [Range(0f, 0.25f)]
        [SerializeField]
        private float randomization = 0.12f;

        private struct MeleePreset
        {
            public float duration;

            // Whoosh/movement
            public float whooshFreqStart;
            public float whooshFreqEnd;
            public float whooshAmount;
            public float whooshDecay;

            // Impact
            public float impactDelay;
            public float impactFreq;
            public float impactAmount;
            public float impactDecay;

            // Body resonance
            public float bodyFreq;
            public float bodyAmount;
            public float bodyDecay;

            // Noise characteristics
            public float noiseBurst;
            public float noiseDecay;
            public float noiseCutoff;

            // Character
            public bool hasMetallic;
            public float metallicFreq;
            public float metallicAmount;
        }

        private MeleePreset currentPreset;
        private AudioSource audioSource;
        private int sampleRate;
        private float[] audioBuffer;

        private float[] lpState = new float[4];
        private float[] hpState = new float[2];

        // Distance-based volume attenuation
        private static Transform playerTransform;
        private const float MAX_AUDIBLE_DISTANCE = 25f;
        private const float MIN_AUDIBLE_DISTANCE = 2f;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;

            sampleRate = AudioSettings.outputSampleRate;
            int maxSamples = Mathf.CeilToInt(0.8f * sampleRate);
            audioBuffer = new float[maxSamples];

            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerTransform = player.transform;
            }
        }
    }
}
