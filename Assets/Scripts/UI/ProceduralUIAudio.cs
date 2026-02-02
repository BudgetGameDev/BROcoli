using UnityEngine;

/// <summary>
/// Procedural audio generator for UI navigation sounds.
/// Provides hover (navigation) and select (confirm) sounds.
/// </summary>
public static class ProceduralUIAudio
{
    private static AudioSource sharedAudioSource;
    private static int sampleRate;
    private static float[] audioBuffer;
    
    private static AudioClip hoverClip;
    private static AudioClip selectClip;
    private static AudioClip levelUpSelectClip;
    
    private const float HoverVolume = 0.35f;
    private const float SelectVolume = 0.5f;
    private const float LevelUpSelectVolume = 0.65f;
    
    private static void EnsureInitialized()
    {
        if (sharedAudioSource == null)
        {
            GameObject audioObj = new GameObject("UIAudio");
            Object.DontDestroyOnLoad(audioObj);
            sharedAudioSource = audioObj.AddComponent<AudioSource>();
            sharedAudioSource.playOnAwake = false;
            sharedAudioSource.spatialBlend = 0f;
            
            sampleRate = AudioSettings.outputSampleRate;
            int maxSamples = Mathf.CeilToInt(0.3f * sampleRate);
            audioBuffer = new float[maxSamples];
            
            // Pre-generate clips
            hoverClip = GenerateHoverSound();
            selectClip = GenerateSelectSound();
            levelUpSelectClip = GenerateLevelUpSelectSound();
        }
    }
    
    /// <summary>
    /// Play a subtle tick/blip sound when hovering over a UI element
    /// </summary>
    public static void PlayHover()
    {
        EnsureInitialized();
        if (hoverClip != null)
        {
            sharedAudioSource.PlayOneShot(hoverClip, HoverVolume);
        }
    }
    
    /// <summary>
    /// Play a satisfying confirm sound when selecting a UI element
    /// </summary>
    public static void PlaySelect()
    {
        EnsureInitialized();
        if (selectClip != null)
        {
            sharedAudioSource.PlayOneShot(selectClip, SelectVolume);
        }
    }
    
    /// <summary>
    /// Play a hyped up sound for level-up stat selection - more epic!
    /// </summary>
    public static void PlayLevelUpSelect()
    {
        EnsureInitialized();
        if (levelUpSelectClip != null)
        {
            sharedAudioSource.PlayOneShot(levelUpSelectClip, LevelUpSelectVolume);
        }
    }
    
    /// <summary>
    /// Generates a short, subtle tick sound for navigation
    /// </summary>
    private static AudioClip GenerateHoverSound()
    {
        float duration = 0.06f;
        int numSamples = Mathf.CeilToInt(duration * sampleRate);
        float[] samples = new float[numSamples];
        
        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float norm = t / duration;
            
            // Quick high-pitched tick
            float freq = 1800f * (1f - norm * 0.3f);  // Slight pitch drop
            float phase = t * freq * Mathf.PI * 2f;
            
            // Main tone - soft sine
            float tone = Mathf.Sin(phase) * 0.6f;
            
            // Add subtle click transient at start
            float click = 0f;
            if (norm < 0.15f)
            {
                click = (1f - norm / 0.15f) * 0.4f;
                click *= Mathf.Sin(t * 3500f * Mathf.PI * 2f);
            }
            
            // Fast envelope - quick attack, quick decay
            float envelope;
            if (norm < 0.1f)
                envelope = norm / 0.1f;
            else
                envelope = 1f - (norm - 0.1f) / 0.9f;
            envelope = envelope * envelope;  // Exponential decay
            
            samples[i] = (tone + click) * envelope;
        }
        
        // Normalize
        NormalizeSamples(samples, 0.7f);
        
        AudioClip clip = AudioClip.Create("UIHover", numSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
    
    /// <summary>
    /// Generates a satisfying confirm/select sound
    /// </summary>
    private static AudioClip GenerateSelectSound()
    {
        float duration = 0.15f;
        int numSamples = Mathf.CeilToInt(duration * sampleRate);
        float[] samples = new float[numSamples];
        
        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float norm = t / duration;
            
            // Two-tone confirm: quick rising pitch
            float freq1 = 600f + norm * 400f;  // Rising from 600 to 1000
            float freq2 = 900f + norm * 600f;  // Rising from 900 to 1500
            
            float phase1 = t * freq1 * Mathf.PI * 2f;
            float phase2 = t * freq2 * Mathf.PI * 2f;
            
            // Harmonious blend
            float tone1 = Mathf.Sin(phase1) * 0.5f;
            float tone2 = Mathf.Sin(phase2) * 0.35f;
            
            // Add brightness with high harmonic
            float brightness = Mathf.Sin(phase2 * 2f) * 0.15f * (1f - norm);
            
            // Envelope - quick attack, smooth decay
            float envelope;
            if (norm < 0.05f)
                envelope = norm / 0.05f;
            else
                envelope = Mathf.Pow(1f - (norm - 0.05f) / 0.95f, 1.5f);
            
            samples[i] = (tone1 + tone2 + brightness) * envelope;
        }
        
        // Normalize
        NormalizeSamples(samples, 0.8f);
        
        AudioClip clip = AudioClip.Create("UISelect", numSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
    
    /// <summary>
    /// Generates an epic, hyped sound for level-up stat selection
    /// Three-tone ascending arpeggio with shimmer
    /// </summary>
    private static AudioClip GenerateLevelUpSelectSound()
    {
        float duration = 0.28f;
        int numSamples = Mathf.CeilToInt(duration * sampleRate);
        float[] samples = new float[numSamples];
        
        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float norm = t / duration;
            
            // Three-note ascending arpeggio (C5, E5, G5 - major chord)
            float note1 = 523f;  // C5
            float note2 = 659f;  // E5
            float note3 = 784f;  // G5
            float note4 = 1047f; // C6 (octave)
            
            // Staggered note timing
            float tone1 = 0f, tone2 = 0f, tone3 = 0f, tone4 = 0f;
            
            // First note: 0-100%
            if (norm < 1.0f)
            {
                float env1 = (norm < 0.05f) ? norm / 0.05f : Mathf.Max(0, 1f - (norm - 0.05f) / 0.5f);
                tone1 = Mathf.Sin(t * note1 * Mathf.PI * 2f) * 0.4f * env1;
            }
            
            // Second note: 15-100%
            if (norm >= 0.12f)
            {
                float n2 = (norm - 0.12f) / 0.88f;
                float env2 = (n2 < 0.08f) ? n2 / 0.08f : Mathf.Max(0, 1f - (n2 - 0.08f) / 0.5f);
                tone2 = Mathf.Sin(t * note2 * Mathf.PI * 2f) * 0.35f * env2;
            }
            
            // Third note: 28-100%
            if (norm >= 0.25f)
            {
                float n3 = (norm - 0.25f) / 0.75f;
                float env3 = (n3 < 0.1f) ? n3 / 0.1f : Mathf.Max(0, 1f - (n3 - 0.1f) / 0.6f);
                tone3 = Mathf.Sin(t * note3 * Mathf.PI * 2f) * 0.35f * env3;
            }
            
            // Final high note: 40-100% (the epic finish!)
            if (norm >= 0.4f)
            {
                float n4 = (norm - 0.4f) / 0.6f;
                float env4 = (n4 < 0.15f) ? n4 / 0.15f : Mathf.Max(0, 1f - (n4 - 0.15f) / 0.85f);
                tone4 = Mathf.Sin(t * note4 * Mathf.PI * 2f) * 0.4f * env4;
            }
            
            // Add shimmer/sparkle effect throughout
            float shimmer = Mathf.Sin(t * 2500f * Mathf.PI * 2f) * 0.08f;
            shimmer *= Mathf.Sin(t * 37f * Mathf.PI * 2f) * 0.5f + 0.5f; // Modulation
            float shimmerEnv = Mathf.Max(0, 1f - norm * 0.8f);
            shimmer *= shimmerEnv;
            
            // Master envelope
            float masterEnv = 1f;
            if (norm > 0.85f)
                masterEnv = (1f - norm) / 0.15f;
            
            samples[i] = (tone1 + tone2 + tone3 + tone4 + shimmer) * masterEnv;
        }
        
        NormalizeSamples(samples, 0.85f);
        
        AudioClip clip = AudioClip.Create("UILevelUpSelect", numSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
    
    private static void NormalizeSamples(float[] samples, float targetPeak)
    {
        float maxAmp = 0f;
        for (int i = 0; i < samples.Length; i++)
            maxAmp = Mathf.Max(maxAmp, Mathf.Abs(samples[i]));
        
        if (maxAmp > 0.01f)
        {
            float normalize = targetPeak / maxAmp;
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= normalize;
        }
    }
}
