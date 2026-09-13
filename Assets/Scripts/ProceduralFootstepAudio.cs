using UnityEngine;

/// <summary>
/// Procedural footstep sound generator using synthesized audio.
/// Generates soft "thump" sounds when the character lands.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ProceduralFootstepAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShuffleWalkVisual hopVisual;
    
    [Header("Sound Parameters")]
    [Range(40f, 120f)]
    [SerializeField] private float baseFrequency = 65f;
    
    [Range(0.02f, 0.2f)]
    [SerializeField] private float duration = 0.08f;
    
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1.0f;
    
    [Range(0f, 0.5f)]
    [SerializeField] private float frequencyVariation = 0.15f;
    
    [Range(0f, 0.3f)]
    [SerializeField] private float volumeVariation = 0.1f;
    
    [Header("Noise Mix")]
    [Range(0f, 1f)]
    [SerializeField] private float noiseMix = 0.4f;
    
    [Header("Filter")]
    [Range(100f, 2000f)]
    [SerializeField] private float lowPassCutoff = 400f;

    private AudioSource audioSource;
    private ShuffleWalkVisual.HopState lastState;
    private float[] audioBuffer;
    private int sampleRate;
    
    // Simple low-pass filter state
    private float filterState;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        
        sampleRate = AudioSettings.outputSampleRate;
        
        // Pre-allocate buffer for max duration
        int maxSamples = Mathf.CeilToInt(0.3f * sampleRate);
        audioBuffer = new float[maxSamples];
    }

    void Update()
    {
        if (hopVisual == null) return;
        
        ShuffleWalkVisual.HopState currentState = hopVisual.State;
        
        // Play sound when landing (transitioning from Airborne to BhopBounce, Landing, or Stopping)
        if (lastState == ShuffleWalkVisual.HopState.Airborne && 
            (currentState == ShuffleWalkVisual.HopState.BhopBounce ||
             currentState == ShuffleWalkVisual.HopState.Landing ||
             currentState == ShuffleWalkVisual.HopState.Stopping))
        {
            PlayFootstep();
        }
        
        lastState = currentState;
    }

    public void PlayFootstep()
    {
        AudioClip clip = GenerateFootstepClip();
        audioSource.PlayOneShot(clip, volume);
    }

    private AudioClip GenerateFootstepClip()
    {
        // Add variation
        float freq = baseFrequency * (1f + Random.Range(-frequencyVariation, frequencyVariation));
        float vol = 1f - Random.Range(0f, volumeVariation);
        float dur = duration * Random.Range(0.85f, 1.15f);
        
        int numSamples = Mathf.CeilToInt(dur * sampleRate);
        numSamples = Mathf.Min(numSamples, audioBuffer.Length);
        
        // Reset filter
        filterState = 0f;
        
        // Low-pass filter coefficient (simple one-pole)
        float rc = 1f / (2f * Mathf.PI * lowPassCutoff);
        float dt = 1f / sampleRate;
        float alpha = dt / (rc + dt);
        
        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = GetEnvelope(t, dur);
            
            // Base tone (sine wave with slight frequency decay for "thump")
            float freqDecay = freq * Mathf.Exp(-t * 15f); // Frequency drops quickly
            float phase = 2f * Mathf.PI * freqDecay * t;
            float tone = Mathf.Sin(phase);
            
            // Add some harmonics for body
            tone += 0.3f * Mathf.Sin(phase * 2f);
            tone += 0.1f * Mathf.Sin(phase * 3f);
            
            // Noise component for texture
            float noise = Random.Range(-1f, 1f);
            
            // Mix tone and noise
            float sample = Mathf.Lerp(tone, noise, noiseMix);
            
            // Apply envelope
            sample *= envelope * vol;
            
            // Simple low-pass filter
            filterState += alpha * (sample - filterState);
            sample = filterState;
            
            // Soft clip to prevent harsh peaks
            sample = SoftClip(sample);
            
            audioBuffer[i] = sample;
        }
        
        // Create AudioClip from buffer
        AudioClip clip = AudioClip.Create("Footstep", numSamples, 1, sampleRate, false);
        
        // Copy only the samples we need
        float[] clipData = new float[numSamples];
        System.Array.Copy(audioBuffer, clipData, numSamples);
        clip.SetData(clipData, 0);
        
        return clip;
    }

    private float GetEnvelope(float time, float totalDuration)
    {
        // Quick attack, exponential decay - like a soft impact
        float attackTime = 0.005f;
        
        if (time < attackTime)
        {
            // Quick attack
            return time / attackTime;
        }
        else
        {
            // Exponential decay
            float decayTime = time - attackTime;
            float decayDuration = totalDuration - attackTime;
            return Mathf.Exp(-decayTime / (decayDuration * 0.25f));
        }
    }

    private float SoftClip(float x)
    {
        // Soft saturation using tanh-like function
        if (x > 1f) return 1f;
        if (x < -1f) return -1f;
        return x - (x * x * x) / 3f;
    }
}
