using UnityEngine;

/// <summary>
/// Procedural spray audio for the sanitizer weapon.
/// Generates a realistic aerosol spray sound with shaped noise,
/// pressure release, and can resonance.
/// 
/// Uses SprayAudioClipGenerator for sound generation and 
/// SprayAudioFilters for audio processing.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ProceduralSprayAudio : MonoBehaviour
{
    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.18f;

    [Header("Spray Characteristics")]
    [Range(0.1f, 2f)]
    [SerializeField] private float sprayDuration = 0.4f;
    
    [Range(0f, 0.15f)]
    [SerializeField] private float randomization = 0.05f;

    [Header("Sound Settings")]
    [Range(3000f, 10000f)]
    [SerializeField] private float hissFrequency = 6000f;
    
    [Range(0.5f, 2f)]
    [SerializeField] private float pressureIntensity = 0.8f;
    
    [Range(0.1f, 1f)]
    [SerializeField] private float airiness = 0.6f;

    private AudioSource audioSource;
    private SprayAudioClipGenerator clipGenerator;
    
    // For continuous spray
    private bool isSpraying = false;
    private AudioClip sprayLoopClip;
    private AudioClip sprayEndClip;

    public bool IsSpraying => isSpraying;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        
        int sampleRate = AudioSettings.outputSampleRate;
        clipGenerator = new SprayAudioClipGenerator(
            sampleRate, hissFrequency, pressureIntensity, airiness, randomization
        );
        
        GenerateSprayClips();
    }

    private void GenerateSprayClips()
    {
        sprayLoopClip = clipGenerator.GenerateSprayLoop(0.3f);
        sprayEndClip = clipGenerator.GenerateSprayEnd(0.2f);
    }

    /// <summary>
    /// Play a single spray burst (for short attacks)
    /// </summary>
    public void PlaySprayBurst()
    {
        PlaySprayBurst(1f);
    }

    /// <summary>
    /// Play a single spray burst with volume multiplier
    /// </summary>
    public void PlaySprayBurst(float volumeMultiplier)
    {
        if (audioSource == null) return;
        
        float variation = 1f + Random.Range(-randomization, randomization);
        AudioClip clip = clipGenerator.GenerateSprayBurst(sprayDuration * variation);
        
        audioSource.pitch = 1f + Random.Range(-0.05f, 0.05f);
        audioSource.PlayOneShot(clip, volume * volumeMultiplier);
    }

    /// <summary>
    /// Start continuous spraying
    /// </summary>
    public void StartSpray()
    {
        if (isSpraying) return;
        isSpraying = true;
        
        audioSource.clip = sprayLoopClip;
        audioSource.loop = true;
        audioSource.volume = volume;
        audioSource.Play();
    }

    /// <summary>
    /// Stop continuous spraying
    /// </summary>
    public void StopSpray()
    {
        if (!isSpraying) return;
        isSpraying = false;
        
        audioSource.Stop();
        audioSource.loop = false;
        
        audioSource.PlayOneShot(sprayEndClip, volume);
    }
}
