using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Procedural XP pickup sound - satisfying, dopamine-inducing collect sound.
    /// </summary>
    public partial class ProceduralXPPickupAudio : MonoBehaviour
    {
        [Header("Volume")]
        [Range(0f, 1f)]
        [SerializeField]
        private float volume = 0.5f;

        [Header("Pitch Variation")]
        [Range(0f, 0.3f)]
        [SerializeField]
        private float pitchVariation = 0.1f;

        [Header("Pitch Scaling")]
        [SerializeField]
        private bool scaleWithCombo = true;

        [SerializeField]
        private float maxPitchBoost = 0.5f;

        private static AudioSource sharedAudioSource;
        private static int sampleRate;
        private static float[] audioBuffer;
        private static float[] lpState = new float[4];

        // Cached base clip for instant playback
        private static AudioClip cachedBaseClip;

        // Combo tracking for pitch scaling
        private static float lastPickupTime;
        private static int comboCount;
        private const float COMBO_WINDOW = 0.5f;
        private const int MAX_COMBO = 10;

        void Awake()
        {
            EnsureInitialized();
        }

        private static void EnsureInitialized()
        {
            if (sharedAudioSource == null)
            {
                GameObject audioObj = new GameObject("XPPickupAudio");
                if (Application.isPlaying)
                    DontDestroyOnLoad(audioObj);
                sharedAudioSource = audioObj.AddComponent<AudioSource>();
                sharedAudioSource.playOnAwake = false;
                sharedAudioSource.spatialBlend = 0f; // 2D sound

                sampleRate = AudioSettings.outputSampleRate;
                int maxSamples = Mathf.CeilToInt(0.5f * sampleRate);
                audioBuffer = new float[maxSamples];
            }
        }

        /// <summary>
        /// Pre-generate the base XP pickup sound clip to avoid hitches on first pickup.
        /// Call this during loading screen.
        /// </summary>
        public static void PrewarmAll()
        {
            EnsureInitialized();
            if (cachedBaseClip == null)
            {
                cachedBaseClip = GeneratePickupClip(1f);
            }
        }

        public void PlayPickupSound()
        {
            PlayPickupSoundInternal(volume, pitchVariation, scaleWithCombo, maxPitchBoost);
        }

        public static void PlayPickup(
            float vol = 0.5f,
            float pitchVar = 0.1f,
            bool useCombo = true,
            float maxPitch = 0.5f
        )
        {
            EnsureInitialized();
            PlayPickupSoundInternal(vol, pitchVar, useCombo, maxPitch);
        }

        private static void PlayPickupSoundInternal(
            float vol,
            float pitchVar,
            bool useCombo,
            float maxPitchBoost
        )
        {
            // Update combo
            float currentTime = Time.time;
            if (currentTime - lastPickupTime < COMBO_WINDOW)
            {
                comboCount = Mathf.Min(comboCount + 1, MAX_COMBO);
            }
            else
            {
                comboCount = 0;
            }
            lastPickupTime = currentTime;

            // Calculate pitch multiplier based on combo
            float comboPitch = 1f;
            if (useCombo && comboCount > 0)
            {
                comboPitch = 1f + (maxPitchBoost * (float)comboCount / MAX_COMBO);
            }

            // Random pitch variation
            float pitchMult = comboPitch * (1f + Random.Range(-pitchVar, pitchVar));

            AudioClip clip = GeneratePickupClip(pitchMult);
            sharedAudioSource.PlayOneShot(clip, vol);
        }
    }
}
