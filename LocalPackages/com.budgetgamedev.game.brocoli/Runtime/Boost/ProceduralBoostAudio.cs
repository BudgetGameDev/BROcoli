using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Procedural audio generator for boost pickups.
    /// </summary>
    public partial class ProceduralBoostAudio : MonoBehaviour
    {
        public enum BoostSoundType
        {
            Health, // Warm, healing chime
            Damage, // Powerful impact punch
            AttackSpeed, // Rapid staccato tones
            MovementSpeed, // Whooshing wind sound
            Experience, // Bright ascending sparkle
            DetectionRadius, // Radar ping/sonar
            SprayRange, // Extending reach sound
            SprayWidth, // Spreading expansion sound
            Magnet, // Magnetic pull whoosh
            TimeSlow, // Descending clock-like chime
        }

        [Header("Volume")]
        [Range(0f, 1f)]
        [SerializeField]
        private float volume = 0.6f;

        private static AudioSource sharedAudioSource;
        private static int sampleRate;
        private static float[] audioBuffer;
        private static float[] filterState = new float[8];

        // Cached clips for each boost type
        private static System.Collections.Generic.Dictionary<BoostSoundType, AudioClip> cachedClips;
        private static bool isPrewarmed = false;

        void Awake()
        {
            EnsureInitialized();
        }

        private static void EnsureInitialized()
        {
            if (sharedAudioSource == null)
            {
                GameObject audioObj = new GameObject("BoostPickupAudio");
                if (Application.isPlaying)
                    DontDestroyOnLoad(audioObj);
                sharedAudioSource = audioObj.AddComponent<AudioSource>();
                sharedAudioSource.playOnAwake = false;
                sharedAudioSource.spatialBlend = 0f;
                // Reward feedback must remain audible when combat fills the voice pool.
                sharedAudioSource.priority = 32;

                sampleRate = Mathf.Max(22050, AudioSettings.outputSampleRate);
                int maxSamples = Mathf.CeilToInt(0.8f * sampleRate);
                audioBuffer = new float[maxSamples];
                cachedClips = new System.Collections.Generic.Dictionary<
                    BoostSoundType,
                    AudioClip
                >();
                isPrewarmed = false;
            }
        }

        /// <summary>
        /// Pre-generate all boost sound clips to avoid hitches on first pickup.
        /// Call this during loading screen.
        /// </summary>
        public static void PrewarmAll()
        {
            EnsureInitialized();
            if (isPrewarmed)
                return;

            foreach (BoostSoundType type in System.Enum.GetValues(typeof(BoostSoundType)))
            {
                if (!cachedClips.ContainsKey(type))
                {
                    cachedClips[type] = GenerateClip(type);
                }
            }
            isPrewarmed = true;
        }

        public void PlayBoostSound(BoostSoundType type)
        {
            PlaySound(type, volume);
        }

        public static void PlaySound(BoostSoundType type, float vol = 0.6f)
        {
            EnsureInitialized();

            // Use cached clip if available, otherwise generate
            AudioClip clip;
            if (cachedClips.TryGetValue(type, out clip) && clip != null)
            {
                // Use cached
            }
            else
            {
                clip = GenerateClip(type);
                cachedClips[type] = clip;
            }
            sharedAudioSource.PlayOneShot(clip, vol);
        }
    }
}
