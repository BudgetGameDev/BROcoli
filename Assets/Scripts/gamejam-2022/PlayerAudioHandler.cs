using UnityEngine;

/// <summary>
/// Handles all player audio: SFX and ambient sounds.
/// Loads AudioClips from Resources and creates AudioSources dynamically.
/// </summary>
public class PlayerAudioHandler : MonoBehaviour
{
    // Resource paths for audio clips
    private const string WalkSoundPath = "Sprites/ggj-2023/sfx/skref";
    private const string DamageSoundPath = "Sprites/ggj-2023/sfx/damage";
    private const string CollisionSoundPath = "Sprites/ggj-2023/sfx/damage";
    private const string PickupSoundPath = "Sprites/ggj-2023/sfx/pickup sound";
    private const string GameOverSoundPath = "Sprites/ggj-2023/sfx/game over2";
    private const string GrowSoundPath = "Sprites/ggj-2023/sfx/pickup sound";
    private const string ShrinkSoundPath = "Sprites/ggj-2023/sfx/damage";
    private const string Ambient1Path = "Sprites/ggj-2023/sfx/ambient/ambient-náttúra";
    private const string Ambient2Path = "Sprites/ggj-2023/sfx/ambient/ambient-náttúra";
    private const string WindAmbientPath = "Sprites/ggj-2023/sfx/ambient/vindur-langt uppi.x.metrar+";
    private const string LavaAmbientPath = "Sprites/ggj-2023/sfx/ambient/gos-tætt-við";

    // Audio clips (loaded from Resources)
    private AudioClip _walkClip;
    private AudioClip _damageClip;
    private AudioClip _collisionClip;
    private AudioClip _pickupClip;
    private AudioClip _gameOverClip;
    private AudioClip _growClip;
    private AudioClip _shrinkClip;
    private AudioClip _ambient1Clip;
    private AudioClip _ambient2Clip;
    private AudioClip _windAmbientClip;
    private AudioClip _lavaAmbientClip;

    // Audio sources (created dynamically or found on GameObject)
    private AudioSource _sfxSource;
    private AudioSource _sfxSource2;
    private AudioSource _ambientSource1;
    private AudioSource _ambientSource2;
    private AudioSource _windSource;
    private AudioSource _lavaSource;
    private AudioSource _gameOverSource;

    private void Awake()
    {
        LoadAudioClips();
        SetupAudioSources();
    }

    private void LoadAudioClips()
    {
        _walkClip = LoadClip(WalkSoundPath);
        _damageClip = LoadClip(DamageSoundPath);
        _collisionClip = LoadClip(CollisionSoundPath);
        _pickupClip = LoadClip(PickupSoundPath);
        _gameOverClip = LoadClip(GameOverSoundPath);
        _growClip = LoadClip(GrowSoundPath);
        _shrinkClip = LoadClip(ShrinkSoundPath);
        _ambient1Clip = LoadClip(Ambient1Path);
        _ambient2Clip = LoadClip(Ambient2Path);
        _windAmbientClip = LoadClip(WindAmbientPath);
        _lavaAmbientClip = LoadClip(LavaAmbientPath);
    }

    private AudioClip LoadClip(string path)
    {
        AudioClip clip = Resources.Load<AudioClip>(path);
        if (clip == null)
        {
            Debug.LogWarning($"PlayerAudioHandler: Could not load audio clip from '{path}'");
        }
        return clip;
    }

    private void SetupAudioSources()
    {
        // Get existing AudioSources or create new ones
        AudioSource[] existingSources = GetComponents<AudioSource>();

        if (existingSources.Length >= 2)
        {
            _sfxSource = existingSources[0];
            _sfxSource2 = existingSources[1];
        }
        else
        {
            _sfxSource = GetOrAddAudioSource(0);
            _sfxSource2 = GetOrAddAudioSource(1);
        }

        // Create dedicated sources for ambient sounds
        _ambientSource1 = CreateAmbientSource("AmbientSource1", _ambient1Clip, true, 0.3f);
        _ambientSource2 = CreateAmbientSource("AmbientSource2", _ambient2Clip, true, 0.3f);
        _windSource = CreateAmbientSource("WindSource", _windAmbientClip, true, 0.2f);
        _lavaSource = CreateAmbientSource("LavaSource", _lavaAmbientClip, true, 0f);
        _gameOverSource = CreateAmbientSource("GameOverSource", _gameOverClip, false, 1f);
    }

    private AudioSource GetOrAddAudioSource(int index)
    {
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length > index)
        {
            return sources[index];
        }
        return gameObject.AddComponent<AudioSource>();
    }

    private AudioSource CreateAmbientSource(string name, AudioClip clip, bool loop, float volume)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(transform);
        child.transform.localPosition = Vector3.zero;

        AudioSource source = child.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.volume = volume;
        source.playOnAwake = loop;
        source.spatialBlend = 0f; // 2D sound

        if (loop && clip != null)
        {
            source.Play();
        }

        return source;
    }

    /// <summary>
    /// Play the walk/footstep sound.
    /// </summary>
    public void PlayWalkSound()
    {
        PlayOneShot(_sfxSource, _walkClip);
    }

    /// <summary>
    /// Play the damage/hurt sound.
    /// </summary>
    public void PlayDamageSound()
    {
        PlayClip(_sfxSource2, _damageClip);
    }

    /// <summary>
    /// Play the collision sound.
    /// </summary>
    public void PlayCollisionSound()
    {
        PlayClip(_sfxSource2, _collisionClip);
    }

    /// <summary>
    /// Play the pickup/experience sound.
    /// </summary>
    public void PlayPickupSound()
    {
        PlayClip(_sfxSource2, _pickupClip);
    }

    /// <summary>
    /// Play the game over sound.
    /// </summary>
    public void PlayGameOverSound()
    {
        if (_gameOverSource != null && _gameOverClip != null)
        {
            _gameOverSource.Play();
        }
    }

    /// <summary>
    /// Play the grow/level up sound.
    /// </summary>
    public void PlayGrowSound()
    {
        PlayOneShot(_sfxSource, _growClip);
    }

    /// <summary>
    /// Play the shrink sound.
    /// </summary>
    public void PlayShrinkSound()
    {
        PlayOneShot(_sfxSource, _shrinkClip);
    }

    /// <summary>
    /// Update the lava ambient volume based on distance.
    /// </summary>
    /// <param name="distance">Distance from lava (higher = louder up to a point).</param>
    public void UpdateLavaAmbient(float distance)
    {
        if (_lavaSource != null)
        {
            _lavaSource.volume = Mathf.Clamp01(Mathf.Abs(distance) / 100f);
        }
    }

    /// <summary>
    /// Stop all ambient audio sources.
    /// </summary>
    public void StopAllAmbient()
    {
        SetAmbientVolume(_ambientSource1, 0f);
        SetAmbientVolume(_ambientSource2, 0f);
        SetAmbientVolume(_windSource, 0f);
        SetAmbientVolume(_lavaSource, 0f);
    }

    private void SetAmbientVolume(AudioSource source, float volume)
    {
        if (source != null)
        {
            source.volume = volume;
        }
    }

    private void PlayOneShot(AudioSource source, AudioClip clip)
    {
        if (source != null && clip != null)
        {
            source.PlayOneShot(clip);
        }
    }

    private void PlayClip(AudioSource source, AudioClip clip)
    {
        if (source != null && clip != null)
        {
            source.clip = clip;
            source.Play();
        }
    }

    private void OnDestroy()
    {
        // Clean up dynamically created child GameObjects
        if (_ambientSource1 != null) Destroy(_ambientSource1.gameObject);
        if (_ambientSource2 != null) Destroy(_ambientSource2.gameObject);
        if (_windSource != null) Destroy(_windSource.gameObject);
        if (_lavaSource != null) Destroy(_lavaSource.gameObject);
        if (_gameOverSource != null) Destroy(_gameOverSource.gameObject);
    }
}
