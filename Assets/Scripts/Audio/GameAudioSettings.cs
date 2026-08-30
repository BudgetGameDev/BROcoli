using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

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
    private const string MixerResourcePath = "Audio/BrocoliAudioMixer";
    private const string MasterParameter = "MasterVolume";
    private const string AmbienceParameter = "AmbienceVolume";
    private const string SfxParameter = "SfxVolume";
    private const string MainMenuSceneName = "MainMenuScene";
    private const float MutedDecibels = -80f;

    private static GameAudioSettings instance;
    private static bool valuesLoaded;
    private static bool pauseMenuOpen;
    private static float masterVolume;
    private static float ambienceVolume;
    private static float sfxVolume;

    private AudioMixer mixer;
    private AudioMixerGroup ambienceGroup;
    private AudioMixerGroup sfxGroup;
    private float nextSourceScan;

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
    private static void ResetStatics()
    {
        instance = null;
        valuesLoaded = false;
        pauseMenuOpen = false;
        ValuesChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        LoadValues();
        if (instance != null)
            return;

        GameObject root = new("Game Audio Settings");
        instance = root.AddComponent<GameAudioSettings>();
        DontDestroyOnLoad(root);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        LoadValues();
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

    private void OnDestroy()
    {
        if (instance == this)
            SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextSourceScan)
            return;

        nextSourceScan = Time.unscaledTime + 0.1f;
        ApplyMixerVolumes();
        RouteAllSources();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        pauseMenuOpen = false;
        ApplyMixerVolumes();
        RouteAllSources();
    }

    public static void SetPauseMenuOpen(bool isOpen)
    {
        pauseMenuOpen = isOpen;
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
        float ambienceDecibels = ShouldSuppressAmbience()
            ? MutedDecibels
            : LinearToDecibels(ambienceVolume);
        mixer.SetFloat(AmbienceParameter, ambienceDecibels);
        mixer.SetFloat(SfxParameter, LinearToDecibels(sfxVolume));
    }

    private static bool ShouldSuppressAmbience() =>
        pauseMenuOpen || SceneManager.GetActiveScene().name == MainMenuSceneName;

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

    private static bool IsAmbience(AudioSource source)
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

    private static float LinearToDecibels(float value)
    {
        return value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;
    }
}
