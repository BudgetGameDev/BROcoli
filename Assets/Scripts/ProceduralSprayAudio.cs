using UnityEngine;

/// <summary>
/// Procedural spray audio for the sanitizer weapon.
/// Generates a realistic aerosol spray sound with shaped noise,
/// pressure release, and can resonance.
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
    private int sampleRate;
    private float[] audioBuffer;
    
    // Filter states for colored noise
    private float lpState1, lpState2;
    private float hpState1, hpState2;
    private float bpState1, bpState2;
    
    // For continuous spray
    private bool isSpraying = false;
    private AudioClip sprayClip;
    private AudioClip sprayLoopClip;
    private AudioClip sprayEndClip;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        sampleRate = AudioSettings.outputSampleRate;
        
        // Pre-generate spray sounds
        GenerateSprayClips();
    }

    private void GenerateSprayClips()
    {
        // Generate start burst
        sprayClip = GenerateSprayBurst(0.15f);
        
        // Generate looping spray
        sprayLoopClip = GenerateSprayLoop(0.3f);
        
        // Generate end release
        sprayEndClip = GenerateSprayEnd(0.2f);
    }

    /// <summary>
    /// Play a single spray burst (for short attacks)
    /// </summary>
    public void PlaySprayBurst()
    {
        PlaySprayBurst(1f);
    }

    public void PlaySprayBurst(float volumeMultiplier)
    {
        if (audioSource == null) return;
        
        // Regenerate with slight variation
        float variation = 1f + Random.Range(-randomization, randomization);
        AudioClip clip = GenerateSprayBurst(sprayDuration * variation);
        
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
        
        // Play start burst then loop
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
        
        // Play end release sound
        audioSource.PlayOneShot(sprayEndClip, volume);
    }

    public bool IsSpraying => isSpraying;

    private AudioClip GenerateSprayBurst(float duration)
    {
        int totalSamples = Mathf.CeilToInt(duration * sampleRate);
        audioBuffer = new float[totalSamples];
        
        // Reset filter states
        ResetFilters();
        
        float baseHiss = hissFrequency * (1f + Random.Range(-randomization, randomization));
        float pressure = pressureIntensity;
        
        // Use pink noise approach - multiple filtered layers
        float pinkAccum = 0f;
        float[] pinkState = new float[7];
        
        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float normalizedT = t / duration;
            
            // Generate pink-ish noise (more natural than white)
            float white = Random.Range(-1f, 1f);
            
            // Paul Kellet's pink noise approximation
            pinkState[0] = 0.99886f * pinkState[0] + white * 0.0555179f;
            pinkState[1] = 0.99332f * pinkState[1] + white * 0.0750759f;
            pinkState[2] = 0.96900f * pinkState[2] + white * 0.1538520f;
            pinkState[3] = 0.86650f * pinkState[3] + white * 0.3104856f;
            pinkState[4] = 0.55000f * pinkState[4] + white * 0.5329522f;
            pinkState[5] = -0.7616f * pinkState[5] - white * 0.0168980f;
            float pinkNoise = (pinkState[0] + pinkState[1] + pinkState[2] + pinkState[3] + pinkState[4] + pinkState[5] + pinkState[6] + white * 0.5362f) * 0.11f;
            pinkState[6] = white * 0.115926f;
            
            // Shaped noise through multiple filters
            float sample = 0f;
            
            // 1. Bandpass for the main "shhh" spray character
            float bandpassNoise = ApplyBandpass(pinkNoise, baseHiss, 1.2f) * 0.7f;
            
            // 2. Subtle high shimmer (very light)
            float highShimmer = ApplyHighpass(pinkNoise, baseHiss * 1.8f) * 0.08f * airiness;
            
            // 3. Low "body" of the spray (pressure sound)
            float lowBody = ApplyLowpass(pinkNoise, 1200f) * 0.15f;
            
            // Combine with emphasis on the shaped bandpass
            sample = bandpassNoise + highShimmer + lowBody;
            
            // Apply envelope - sharp attack, sustain, quick release
            float envelope = GetSprayEnvelope(normalizedT, pressure);
            sample *= envelope;
            
            // Add subtle can resonance modulation (metallic vibration)
            float modulation = 1f + Mathf.Sin(t * 180f * Mathf.PI * 2f) * 0.02f;
            modulation *= 1f + Mathf.Sin(t * 45f * Mathf.PI * 2f) * 0.03f;
            sample *= modulation;
            
            // Gentle soft clipping
            sample = SoftClip(sample * 0.9f);
            
            audioBuffer[i] = sample;
        }
        
        // Normalize to a reasonable level
        NormalizeBuffer(audioBuffer, 0.6f);
        
        AudioClip clip = AudioClip.Create("SprayBurst", totalSamples, 1, sampleRate, false);
        clip.SetData(audioBuffer, 0);
        return clip;
    }

    private AudioClip GenerateSprayLoop(float duration)
    {
        int totalSamples = Mathf.CeilToInt(duration * sampleRate);
        audioBuffer = new float[totalSamples];
        
        ResetFilters();
        
        float baseHiss = hissFrequency;
        
        // Create seamless loop with crossfade
        int crossfadeSamples = Mathf.CeilToInt(0.02f * sampleRate);
        
        // Pink noise state
        float[] pinkState = new float[7];
        
        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            
            // Pink noise generation
            float white = Random.Range(-1f, 1f);
            pinkState[0] = 0.99886f * pinkState[0] + white * 0.0555179f;
            pinkState[1] = 0.99332f * pinkState[1] + white * 0.0750759f;
            pinkState[2] = 0.96900f * pinkState[2] + white * 0.1538520f;
            pinkState[3] = 0.86650f * pinkState[3] + white * 0.3104856f;
            pinkState[4] = 0.55000f * pinkState[4] + white * 0.5329522f;
            pinkState[5] = -0.7616f * pinkState[5] - white * 0.0168980f;
            float pinkNoise = (pinkState[0] + pinkState[1] + pinkState[2] + pinkState[3] + pinkState[4] + pinkState[5] + pinkState[6] + white * 0.5362f) * 0.11f;
            pinkState[6] = white * 0.115926f;
            
            float bandpassNoise = ApplyBandpass(pinkNoise, baseHiss, 1.0f) * 0.65f;
            float highShimmer = ApplyHighpass(pinkNoise, baseHiss * 1.6f) * 0.06f * airiness;
            float lowBody = ApplyLowpass(pinkNoise, 1000f) * 0.12f;
            
            float sample = bandpassNoise + highShimmer + lowBody;
            
            // Subtle variation in the loop
            float variation = 1f + Mathf.Sin(t * 2.5f * Mathf.PI * 2f) * 0.06f;
            sample *= variation * pressureIntensity;
            
            // Can resonance
            float resonance = 1f + Mathf.Sin(t * 90f * Mathf.PI * 2f) * 0.015f;
            sample *= resonance;
            
            sample = SoftClip(sample * 0.8f);
            
            audioBuffer[i] = sample;
        }
        
        // Crossfade for seamless loop
        for (int i = 0; i < crossfadeSamples; i++)
        {
            float fade = (float)i / crossfadeSamples;
            int endIndex = totalSamples - crossfadeSamples + i;
            audioBuffer[i] = audioBuffer[i] * fade + audioBuffer[endIndex] * (1f - fade);
        }
        
        NormalizeBuffer(audioBuffer, 0.5f);
        
        AudioClip clip = AudioClip.Create("SprayLoop", totalSamples, 1, sampleRate, false);
        clip.SetData(audioBuffer, 0);
        return clip;
    }

    private AudioClip GenerateSprayEnd(float duration)
    {
        int totalSamples = Mathf.CeilToInt(duration * sampleRate);
        audioBuffer = new float[totalSamples];
        
        ResetFilters();
        
        float baseHiss = hissFrequency * 0.9f; // Slightly lower as pressure drops
        
        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float normalizedT = t / duration;
            
            float whiteNoise = Random.Range(-1f, 1f);
            
            // Frequency drops as pressure releases
            float freqDrop = Mathf.Lerp(1f, 0.6f, normalizedT);
            float bandpassNoise = ApplyBandpass(whiteNoise, baseHiss * freqDrop, 0.5f);
            float highHiss = ApplyHighpass(whiteNoise, baseHiss * freqDrop) * 0.2f;
            
            float sample = bandpassNoise * 0.4f + highHiss;
            
            // Exponential decay envelope
            float envelope = Mathf.Exp(-normalizedT * 5f);
            sample *= envelope;
            
            // Add some sputtering at the end
            if (normalizedT > 0.5f)
            {
                float sputter = Mathf.Sin(t * 40f * Mathf.PI * 2f) * (normalizedT - 0.5f) * 0.3f;
                sample *= (1f + sputter);
            }
            
            sample = SoftClip(sample);
            
            audioBuffer[i] = sample;
        }
        
        NormalizeBuffer(audioBuffer, 0.7f);
        
        AudioClip clip = AudioClip.Create("SprayEnd", totalSamples, 1, sampleRate, false);
        clip.SetData(audioBuffer, 0);
        return clip;
    }

    private float GetSprayEnvelope(float t, float pressure)
    {
        // Fast attack (0-5%), sustain (5-85%), release (85-100%)
        float attack = 0.05f;
        float release = 0.15f;
        
        if (t < attack)
        {
            // Quick attack with slight overshoot
            float attackT = t / attack;
            return Mathf.Sin(attackT * Mathf.PI * 0.5f) * (1f + 0.1f * pressure);
        }
        else if (t < 1f - release)
        {
            // Sustain with subtle variation
            return 1f + Mathf.Sin(t * 20f) * 0.05f * pressure;
        }
        else
        {
            // Release
            float releaseT = (t - (1f - release)) / release;
            return Mathf.Exp(-releaseT * 4f);
        }
    }

    private void ResetFilters()
    {
        lpState1 = lpState2 = 0f;
        hpState1 = hpState2 = 0f;
        bpState1 = bpState2 = 0f;
    }

    private float ApplyLowpass(float input, float cutoff)
    {
        float rc = 1f / (2f * Mathf.PI * cutoff);
        float dt = 1f / sampleRate;
        float alpha = dt / (rc + dt);
        
        lpState1 += alpha * (input - lpState1);
        lpState2 += alpha * (lpState1 - lpState2);
        
        return lpState2;
    }

    private float ApplyHighpass(float input, float cutoff)
    {
        float rc = 1f / (2f * Mathf.PI * cutoff);
        float dt = 1f / sampleRate;
        float alpha = rc / (rc + dt);
        
        float output = alpha * (hpState2 + input - hpState1);
        hpState1 = input;
        hpState2 = output;
        
        return output;
    }

    private float ApplyBandpass(float input, float centerFreq, float q)
    {
        float w0 = 2f * Mathf.PI * centerFreq / sampleRate;
        float alpha = Mathf.Sin(w0) / (2f * q);
        
        float a0 = 1f + alpha;
        float b1 = Mathf.Sin(w0) / a0;
        float a1 = -2f * Mathf.Cos(w0) / a0;
        float a2 = (1f - alpha) / a0;
        
        float output = b1 * input - a1 * bpState1 - a2 * bpState2;
        bpState2 = bpState1;
        bpState1 = output;
        
        return output;
    }

    private float SoftClip(float x)
    {
        if (x > 1f) return 1f - Mathf.Exp(-(x - 1f) * 2f) * 0.3f + 0.7f;
        if (x < -1f) return -1f + Mathf.Exp(-(-x - 1f) * 2f) * 0.3f - 0.7f;
        
        // Subtle saturation in normal range
        return x - x * x * x * 0.15f;
    }

    private void NormalizeBuffer(float[] buffer, float targetPeak)
    {
        float maxAmp = 0f;
        for (int i = 0; i < buffer.Length; i++)
            maxAmp = Mathf.Max(maxAmp, Mathf.Abs(buffer[i]));
        
        if (maxAmp > 0.001f)
        {
            float scale = targetPeak / maxAmp;
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] *= scale;
        }
    }
}
