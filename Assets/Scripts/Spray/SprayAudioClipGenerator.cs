using UnityEngine;

/// <summary>
/// Generates AudioClip assets for spray sounds (burst, loop, end).
/// Uses filtered noise to create realistic aerosol spray audio.
/// </summary>
public class SprayAudioClipGenerator
{
    private readonly int sampleRate;
    private readonly float hissFrequency;
    private readonly float pressureIntensity;
    private readonly float airiness;
    private readonly float randomization;
    
    private SprayAudioFilters filters;
    private float[] audioBuffer;

    public SprayAudioClipGenerator(int sampleRate, float hissFrequency, 
        float pressureIntensity, float airiness, float randomization)
    {
        this.sampleRate = sampleRate;
        this.hissFrequency = hissFrequency;
        this.pressureIntensity = pressureIntensity;
        this.airiness = airiness;
        this.randomization = randomization;
        
        filters = new SprayAudioFilters(sampleRate);
    }

    /// <summary>
    /// Generate a spray burst sound effect
    /// </summary>
    public AudioClip GenerateSprayBurst(float duration)
    {
        int totalSamples = Mathf.CeilToInt(duration * sampleRate);
        audioBuffer = new float[totalSamples];
        
        filters.ResetFilters();
        
        float baseHiss = hissFrequency * (1f + Random.Range(-randomization, randomization));
        float pressure = pressureIntensity;
        float[] pinkState = new float[7];
        
        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float normalizedT = t / duration;
            
            float white = Random.Range(-1f, 1f);
            float pinkNoise = SprayAudioFilters.GeneratePinkNoise(white, pinkState);
            
            // Shaped noise through multiple filters
            float bandpassNoise = filters.ApplyBandpass(pinkNoise, baseHiss, 1.2f) * 0.7f;
            float highShimmer = filters.ApplyHighpass(pinkNoise, baseHiss * 1.8f) * 0.08f * airiness;
            float lowBody = filters.ApplyLowpass(pinkNoise, 1200f) * 0.15f;
            
            float sample = bandpassNoise + highShimmer + lowBody;
            
            // Apply envelope
            float envelope = SprayAudioFilters.GetSprayEnvelope(normalizedT, pressure);
            sample *= envelope;
            
            // Add can resonance modulation
            float modulation = 1f + Mathf.Sin(t * 180f * Mathf.PI * 2f) * 0.02f;
            modulation *= 1f + Mathf.Sin(t * 45f * Mathf.PI * 2f) * 0.03f;
            sample *= modulation;
            
            audioBuffer[i] = SprayAudioFilters.SoftClip(sample * 0.9f);
        }
        
        SprayAudioFilters.NormalizeBuffer(audioBuffer, 0.6f);
        
        AudioClip clip = AudioClip.Create("SprayBurst", totalSamples, 1, sampleRate, false);
        clip.SetData(audioBuffer, 0);
        return clip;
    }

    /// <summary>
    /// Generate a looping spray sound effect
    /// </summary>
    public AudioClip GenerateSprayLoop(float duration)
    {
        int totalSamples = Mathf.CeilToInt(duration * sampleRate);
        audioBuffer = new float[totalSamples];
        
        filters.ResetFilters();
        
        int crossfadeSamples = Mathf.CeilToInt(0.02f * sampleRate);
        float[] pinkState = new float[7];
        
        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            
            float white = Random.Range(-1f, 1f);
            float pinkNoise = SprayAudioFilters.GeneratePinkNoise(white, pinkState);
            
            float bandpassNoise = filters.ApplyBandpass(pinkNoise, hissFrequency, 1.0f) * 0.65f;
            float highShimmer = filters.ApplyHighpass(pinkNoise, hissFrequency * 1.6f) * 0.06f * airiness;
            float lowBody = filters.ApplyLowpass(pinkNoise, 1000f) * 0.12f;
            
            float sample = bandpassNoise + highShimmer + lowBody;
            
            // Subtle variation in the loop
            float variation = 1f + Mathf.Sin(t * 2.5f * Mathf.PI * 2f) * 0.06f;
            sample *= variation * pressureIntensity;
            
            // Can resonance
            float resonance = 1f + Mathf.Sin(t * 90f * Mathf.PI * 2f) * 0.015f;
            sample *= resonance;
            
            audioBuffer[i] = SprayAudioFilters.SoftClip(sample * 0.8f);
        }
        
        // Crossfade for seamless loop
        for (int i = 0; i < crossfadeSamples; i++)
        {
            float fade = (float)i / crossfadeSamples;
            int endIndex = totalSamples - crossfadeSamples + i;
            audioBuffer[i] = audioBuffer[i] * fade + audioBuffer[endIndex] * (1f - fade);
        }
        
        SprayAudioFilters.NormalizeBuffer(audioBuffer, 0.5f);
        
        AudioClip clip = AudioClip.Create("SprayLoop", totalSamples, 1, sampleRate, false);
        clip.SetData(audioBuffer, 0);
        return clip;
    }

    /// <summary>
    /// Generate a spray end/release sound effect
    /// </summary>
    public AudioClip GenerateSprayEnd(float duration)
    {
        int totalSamples = Mathf.CeilToInt(duration * sampleRate);
        audioBuffer = new float[totalSamples];
        
        filters.ResetFilters();
        
        float baseHiss = hissFrequency * 0.9f; // Slightly lower as pressure drops
        
        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float normalizedT = t / duration;
            
            float whiteNoise = Random.Range(-1f, 1f);
            
            // Frequency drops as pressure releases
            float freqDrop = Mathf.Lerp(1f, 0.6f, normalizedT);
            float bandpassNoise = filters.ApplyBandpass(whiteNoise, baseHiss * freqDrop, 0.5f);
            float highHiss = filters.ApplyHighpass(whiteNoise, baseHiss * freqDrop) * 0.2f;
            
            float sample = bandpassNoise * 0.4f + highHiss;
            
            // Exponential decay envelope
            float envelope = Mathf.Exp(-normalizedT * 5f);
            sample *= envelope;
            
            // Add sputtering at the end
            if (normalizedT > 0.5f)
            {
                float sputter = Mathf.Sin(t * 40f * Mathf.PI * 2f) * (normalizedT - 0.5f) * 0.3f;
                sample *= (1f + sputter);
            }
            
            audioBuffer[i] = SprayAudioFilters.SoftClip(sample);
        }
        
        SprayAudioFilters.NormalizeBuffer(audioBuffer, 0.7f);
        
        AudioClip clip = AudioClip.Create("SprayEnd", totalSamples, 1, sampleRate, false);
        clip.SetData(audioBuffer, 0);
        return clip;
    }
}
