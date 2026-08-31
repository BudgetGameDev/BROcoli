using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Shared enemy-death SFX player. A small voice pool lets nearby deaths overlap
    /// without allocating a temporary GameObject for every kill.
    /// </summary>
    public static class EnemyDeathAudio
    {
        private const string ClipResourcePath = "Brocoli/Audio/SFX/EnemyDeathSplat";
        private const int VoiceCount = 6;
        private const float BaseVolume = 0.3f;
        private const float BurstWindow = 0.08f;

        private static AudioClip deathClip;
        private static AudioSource[] voices;
        private static int nextVoice;
        private static float lastPlayTime = float.NegativeInfinity;
        private static int burstCount;
        private static bool warnedAboutMissingClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            deathClip = null;
            voices = null;
            nextVoice = 0;
            lastPlayTime = float.NegativeInfinity;
            burstCount = 0;
            warnedAboutMissingClip = false;
        }

        /// <summary>
        /// Loads the acquired clip and creates the reusable voices during the
        /// loading screen, avoiding a hitch on the first kill.
        /// </summary>
        public static void PrewarmAll()
        {
            EnsureInitialized();
        }

        public static void Play(Vector3 worldPosition, bool isElite) =>
            Play(worldPosition, isElite, () => Resources.Load<AudioClip>(ClipResourcePath));

        internal static void Play(
            Vector3 worldPosition,
            bool isElite,
            System.Func<AudioClip> loadClip
        )
        {
            EnsureInitialized(loadClip);
            if (deathClip == null || voices == null)
                return;

            float now = Time.unscaledTime;
            if (now - lastPlayTime <= BurstWindow)
                burstCount = Mathf.Min(burstCount + 1, VoiceCount - 1);
            else
                burstCount = 0;
            lastPlayTime = now;

            // Layered deaths still read as a group, but do not become a loud wall
            // of identical splats during an area-of-effect multi-kill.
            float crowdAttenuation = 1f / Mathf.Sqrt(1f + burstCount * 0.55f);
            float distanceAttenuation = CalculateDistanceAttenuation(worldPosition);
            float volume = BaseVolume * crowdAttenuation * distanceAttenuation;
            if (isElite)
                volume *= 1.2f;

            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;

            voice.Stop();
            voice.pitch = Random.Range(0.94f, 1.06f) * (isElite ? 0.88f : 1f);
            voice.PlayOneShot(deathClip, Mathf.Clamp01(volume));
        }

        private static void EnsureInitialized() =>
            EnsureInitialized(() => Resources.Load<AudioClip>(ClipResourcePath));

        internal static void EnsureInitialized(System.Func<AudioClip> loadClip)
        {
            if (deathClip == null)
                deathClip = loadClip();

            if (deathClip == null)
            {
                if (!warnedAboutMissingClip)
                {
                    Debug.LogWarning(
                        $"Enemy death SFX is missing at Resources/{ClipResourcePath}."
                    );
                    warnedAboutMissingClip = true;
                }
                return;
            }

            if (voices != null && voices.Length == VoiceCount && voices[0] != null)
                return;

            GameObject root = new GameObject("Enemy Death Audio");
            Object.DontDestroyOnLoad(root);
            voices = new AudioSource[VoiceCount];

            for (int i = 0; i < voices.Length; i++)
            {
                AudioSource voice = root.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.loop = false;
                voice.spatialBlend = 0f;
                voice.dopplerLevel = 0f;
                voices[i] = voice;
            }
        }

        private static float CalculateDistanceAttenuation(Vector3 worldPosition)
        {
            Transform player = GameContext.Instance?.PlayerTransform;
            if (player == null)
                return 1f;

            float distance = GroundPlane.GroundDistance(worldPosition, player.position);
            return Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(4f, 22f, distance));
        }
    }
}
