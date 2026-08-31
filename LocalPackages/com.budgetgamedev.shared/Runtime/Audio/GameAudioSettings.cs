using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// Persistent three-bus audio settings. Existing and runtime-created sources are
    /// automatically routed so gameplay code does not need per-source volume logic.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class GameAudioSettings : MonoBehaviour
    {
        public const float DefaultMasterVolume = 1f;
        public const float DefaultAmbienceVolume = 0.35f;
        public const float DefaultSfxVolume = 1f;

        private const string MasterKey = "Audio.MasterVolume";
        private const string AmbienceKey = "Audio.AmbienceVolume";
        private const string SfxKey = "Audio.SfxVolume";

        private const string MasterParameter = "MasterVolume";
        private const string AmbienceParameter = "AmbienceVolume";
        private const string SfxParameter = "SfxVolume";

        private const float MutedDecibels = -80f;

        /// <summary>
        /// Resources path of the active game's AudioMixer, and the name of the scene
        /// that counts as its menu. The hub applies the selected game's values before
        /// its first scene loads, so this shared component never names a title.
        /// </summary>
        public static void Configure(string mixerResourcePath, string menuSceneName)
        {
            MixerResourcePath = mixerResourcePath;
            MenuSceneName = menuSceneName;
        }

        public static string MixerResourcePath { get; private set; }
        public static string MenuSceneName { get; private set; }

        internal static GameAudioSettings instance;
        private static bool valuesLoaded;
        private static bool pauseMenuOpen;
        private static float masterVolume;
        private static float ambienceVolume;
        private static float sfxVolume;

        private AudioMixer mixer;
        private AudioMixerGroup ambienceGroup;
        private AudioMixerGroup sfxGroup;
        internal float nextSourceScan;

        public static event Action ValuesChanged;

        public static float MasterVolume
        {
            get
            {
                LoadValues();
                return masterVolume;
            }
        }

        public static float AmbienceVolume
        {
            get
            {
                LoadValues();
                return ambienceVolume;
            }
        }

        public static float SfxVolume
        {
            get
            {
                LoadValues();
                return sfxVolume;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            instance = null;
            valuesLoaded = false;
            pauseMenuOpen = false;
            AudioListener.pause = false;
            ValuesChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Bootstrap()
        {
            LoadValues();
            if (instance != null)
                return;

            GameObject root = new("Game Audio Settings");
            instance = root.AddComponent<GameAudioSettings>();
            if (Application.isPlaying)
                DontDestroyOnLoad(root); // Refused outside play mode.
        }

        internal void Awake()
        {
            if (instance != null && instance != this)
            {
                if (Application.isPlaying)
                    Destroy(gameObject);
                else
                    DestroyImmediate(gameObject); // Destroy is refused in the editor.
                return;
            }

            instance = this;
            LoadValues();
            if (string.IsNullOrEmpty(MixerResourcePath))
                return;

            mixer = Resources.Load<AudioMixer>(MixerResourcePath);
            if (mixer == null)
            {
                Debug.LogError($"[Audio Settings] Missing Resources/{MixerResourcePath}.mixer");
                return;
            }

            ambienceGroup = FindGroup("Ambience");
            sfxGroup = FindGroup("SFX");
            ApplyMixerVolumes();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        internal void OnDestroy()
        {
            if (instance == this)
                SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        internal void LateUpdate()
        {
            if (Time.unscaledTime < nextSourceScan)
                return;

            nextSourceScan = Time.unscaledTime + 0.1f;
            ApplyMixerVolumes();
            RouteAllSources();
        }

        internal void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            pauseMenuOpen = false;
            AudioListener.pause = false;
            ApplyMixerVolumes();
            RouteAllSources();
        }

        public static void SetPauseMenuOpen(bool isOpen)
        {
            pauseMenuOpen = isOpen;
            AudioListener.pause = isOpen;
            instance?.ApplyMixerVolumes();
        }

        public static void SetMasterVolume(float value) =>
            SetVolume(ref masterVolume, value, MasterKey);

        public static void SetAmbienceVolume(float value) =>
            SetVolume(ref ambienceVolume, value, AmbienceKey);

        public static void SetSfxVolume(float value) => SetVolume(ref sfxVolume, value, SfxKey);

        public static void ResetToDefaults()
        {
            LoadValues();
            masterVolume = DefaultMasterVolume;
            ambienceVolume = DefaultAmbienceVolume;
            sfxVolume = DefaultSfxVolume;
            SaveValues();
            instance?.ApplyMixerVolumes();
            ValuesChanged?.Invoke();
        }

        private static void SetVolume(ref float field, float value, string key)
        {
            LoadValues();
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(field, value))
                return;

            field = value;
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            instance?.ApplyMixerVolumes();
            ValuesChanged?.Invoke();
        }

        private static void LoadValues()
        {
            if (valuesLoaded)
                return;

            masterVolume = PlayerPrefs.GetFloat(MasterKey, DefaultMasterVolume);
            ambienceVolume = PlayerPrefs.GetFloat(AmbienceKey, DefaultAmbienceVolume);
            sfxVolume = PlayerPrefs.GetFloat(SfxKey, DefaultSfxVolume);
            valuesLoaded = true;
        }

        private static void SaveValues()
        {
            PlayerPrefs.SetFloat(MasterKey, masterVolume);
            PlayerPrefs.SetFloat(AmbienceKey, ambienceVolume);
            PlayerPrefs.SetFloat(SfxKey, sfxVolume);
            PlayerPrefs.Save();
        }

        private AudioMixerGroup FindGroup(string groupName)
        {
            AudioMixerGroup[] matches = mixer.FindMatchingGroups(groupName);
            if (matches.Length > 0)
                return matches[0];

            Debug.LogError($"[Audio Settings] Mixer group '{groupName}' was not found.");
            return null;
        }

        private void ApplyMixerVolumes()
        {
            if (mixer == null)
                return;

            mixer.SetFloat(MasterParameter, LinearToDecibels(masterVolume));
            float ambienceDecibels = ShouldSuppressAmbience(SceneManager.GetActiveScene().name)
                ? MutedDecibels
                : LinearToDecibels(ambienceVolume);
            mixer.SetFloat(AmbienceParameter, ambienceDecibels);
            mixer.SetFloat(SfxParameter, LinearToDecibels(sfxVolume));
        }

        /// <summary>
        /// Whether the ambience bus is muted: while the pause menu is up, and while
        /// the configured menu scene is the active one.
        /// </summary>
        internal static bool ShouldSuppressAmbience(string activeSceneName) =>
            pauseMenuOpen
            || (!string.IsNullOrEmpty(MenuSceneName) && activeSceneName == MenuSceneName);

        private void RouteAllSources()
        {
            if (ambienceGroup == null || sfxGroup == null)
                return;

            AudioSource[] sources = FindObjectsByType<AudioSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
            foreach (AudioSource source in sources)
            {
                if (
                    source.outputAudioMixerGroup == ambienceGroup
                    || source.outputAudioMixerGroup == sfxGroup
                )
                    continue;

                AudioMixerGroup destination = IsAmbience(source) ? ambienceGroup : sfxGroup;
                source.outputAudioMixerGroup = destination;
            }
        }

        internal static bool IsAmbience(AudioSource source)
        {
            string identity = source.gameObject.name;
            if (source.clip != null)
                identity += " " + source.clip.name;
            if (source.transform.parent != null)
                identity += " " + source.transform.parent.name;

            identity = identity.ToLowerInvariant();
            return identity.Contains("ambient")
                || identity.Contains("ambience")
                || identity.Contains("music")
                || identity.Contains("wind")
                || identity.Contains("lava")
                || identity.Contains("nature")
                || identity.Contains("rain")
                || identity.Contains("umhv");
        }

        internal static float LinearToDecibels(float value)
        {
            return value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;
        }
    }
}
