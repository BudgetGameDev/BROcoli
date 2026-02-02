using UnityEngine;

/// <summary>
/// Procedural impact sound generator for player projectile hits.
/// Creates satisfying, punchy impact sounds when projectiles hit enemies.
/// </summary>
public class ProceduralProjectileHitAudio : MonoBehaviour
{
    public enum HitSoundType
    {
        Energy,         // Sci-fi energy weapon hit
        Ballistic,      // Traditional bullet impact
        Plasma,         // Hot plasma sizzle
        Laser,          // Sharp laser hit
        Explosive       // Small explosion on impact
    }

    [Header("Sound Type")]
    [SerializeField] private HitSoundType soundType = HitSoundType.Energy;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.5f;

    [Header("Variation")]
    [Range(0f, 0.3f)]
    [SerializeField] private float randomization = 0.15f;

    private struct HitPreset
    {
        public float duration;
        
        // Impact pop
        public float impactFreq;
        public float impactAmount;
        public float impactDecay;
        
        // Body/resonance
        public float bodyFreq;
        public float bodyAmount;
        public float bodyDecay;
        
        // High sizzle
        public float sizzleFreq;
        public float sizzleAmount;
        public float sizzleDecay;
        
        // Noise burst
        public float noiseAmount;
        public float noiseDecay;
        public float noiseCutoff;
        
        // Thump
        public float thumpFreq;
        public float thumpAmount;
    }

    private AudioSource audioSource;
    private int sampleRate;
    private float[] audioBuffer;
    private float[] lpState = new float[4];

    private static ProceduralProjectileHitAudio instance;
    
    // Static caching for prewarmed clips
    private static System.Collections.Generic.Dictionary<HitSoundType, AudioClip> cachedClips;
    private static bool isPrewarmed = false;
    private static int staticSampleRate;
    private static float[] staticAudioBuffer;
    private static float[] staticLpState = new float[4];

    void Awake()
    {
        // Allow multiple instances but keep reference for static access
        if (instance == null)
            instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
            
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // 2D sound

        sampleRate = AudioSettings.outputSampleRate;
        int maxSamples = Mathf.CeilToInt(0.3f * sampleRate);
        audioBuffer = new float[maxSamples];
    }

    private static void EnsureStaticInitialized()
    {
        if (staticAudioBuffer == null)
        {
            staticSampleRate = AudioSettings.outputSampleRate;
            int maxSamples = Mathf.CeilToInt(0.3f * staticSampleRate);
            staticAudioBuffer = new float[maxSamples];
            cachedClips = new System.Collections.Generic.Dictionary<HitSoundType, AudioClip>();
        }
    }

    /// <summary>
    /// Pre-generate all hit sound clips to avoid hitches during gameplay.
    /// Call this during loading screen.
    /// </summary>
    public static void PrewarmAll()
    {
        EnsureStaticInitialized();
        if (isPrewarmed) return;

        foreach (HitSoundType type in System.Enum.GetValues(typeof(HitSoundType)))
        {
            if (!cachedClips.ContainsKey(type))
            {
                HitPreset preset = GetPresetStatic(type);
                cachedClips[type] = GenerateStaticHitClip(preset);
            }
        }
        isPrewarmed = true;
    }

    private static HitPreset GetPresetStatic(HitSoundType type)
    {
        HitPreset p = new HitPreset();

        switch (type)
        {
            case HitSoundType.Energy:
                p.duration = 0.15f;
                p.impactFreq = 180f;
                p.impactAmount = 0.6f;
                p.impactDecay = 25f;
                p.bodyFreq = 320f;
                p.bodyAmount = 0.35f;
                p.bodyDecay = 18f;
                p.sizzleFreq = 2800f;
                p.sizzleAmount = 0.25f;
                p.sizzleDecay = 30f;
                p.noiseAmount = 0.3f;
                p.noiseDecay = 35f;
                p.noiseCutoff = 4000f;
                p.thumpFreq = 60f;
                p.thumpAmount = 0.4f;
                break;

            case HitSoundType.Ballistic:
                p.duration = 0.12f;
                p.impactFreq = 140f;
                p.impactAmount = 0.7f;
                p.impactDecay = 35f;
                p.bodyFreq = 250f;
                p.bodyAmount = 0.3f;
                p.bodyDecay = 25f;
                p.sizzleFreq = 3500f;
                p.sizzleAmount = 0.15f;
                p.sizzleDecay = 50f;
                p.noiseAmount = 0.45f;
                p.noiseDecay = 40f;
                p.noiseCutoff = 6000f;
                p.thumpFreq = 50f;
                p.thumpAmount = 0.5f;
                break;

            case HitSoundType.Plasma:
                p.duration = 0.2f;
                p.impactFreq = 200f;
                p.impactAmount = 0.5f;
                p.impactDecay = 20f;
                p.bodyFreq = 400f;
                p.bodyAmount = 0.4f;
                p.bodyDecay = 12f;
                p.sizzleFreq = 3200f;
                p.sizzleAmount = 0.4f;
                p.sizzleDecay = 15f;
                p.noiseAmount = 0.35f;
                p.noiseDecay = 20f;
                p.noiseCutoff = 5000f;
                p.thumpFreq = 70f;
                p.thumpAmount = 0.35f;
                break;

            case HitSoundType.Laser:
                p.duration = 0.1f;
                p.impactFreq = 280f;
                p.impactAmount = 0.55f;
                p.impactDecay = 40f;
                p.bodyFreq = 600f;
                p.bodyAmount = 0.3f;
                p.bodyDecay = 35f;
                p.sizzleFreq = 4500f;
                p.sizzleAmount = 0.35f;
                p.sizzleDecay = 45f;
                p.noiseAmount = 0.2f;
                p.noiseDecay = 50f;
                p.noiseCutoff = 7000f;
                p.thumpFreq = 90f;
                p.thumpAmount = 0.25f;
                break;

            case HitSoundType.Explosive:
                p.duration = 0.25f;
                p.impactFreq = 100f;
                p.impactAmount = 0.7f;
                p.impactDecay = 15f;
                p.bodyFreq = 180f;
                p.bodyAmount = 0.5f;
                p.bodyDecay = 10f;
                p.sizzleFreq = 2000f;
                p.sizzleAmount = 0.3f;
                p.sizzleDecay = 12f;
                p.noiseAmount = 0.55f;
                p.noiseDecay = 18f;
                p.noiseCutoff = 3500f;
                p.thumpFreq = 40f;
                p.thumpAmount = 0.6f;
                break;
        }

        return p;
    }

    private HitPreset GetPreset(HitSoundType type)
    {
        return GetPresetStatic(type);
    }

    public void PlayHitSound()
    {
        PlayHitSound(soundType);
    }

    public void PlayHitSound(HitSoundType type)
    {
        EnsureStaticInitialized();
        
        // Use cached clip if available
        AudioClip clip;
        if (cachedClips.TryGetValue(type, out clip) && clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
            return;
        }
        
        // Fallback: generate with randomization
        HitPreset preset = GetPreset(type);
        
        // Apply randomization
        float randMult = 1f + Random.Range(-randomization, randomization);
        preset.impactFreq *= randMult;
        preset.bodyFreq *= randMult;
        preset.sizzleFreq *= Mathf.Lerp(1f, randMult, 0.5f);

        clip = GenerateHitClip(preset);
        audioSource.PlayOneShot(clip, volume);
    }

    private AudioClip GenerateHitClip(HitPreset p)
    {
        int samples = Mathf.CeilToInt(p.duration * sampleRate);
        samples = Mathf.Min(samples, audioBuffer.Length);

        // Reset filter state
        for (int i = 0; i < lpState.Length; i++) lpState[i] = 0;

        float phase1 = 0f, phase2 = 0f, phase3 = 0f, phase4 = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            // Initial thump (sub bass)
            float thumpEnv = Mathf.Exp(-t * 40f);
            phase1 += p.thumpFreq * 2f * Mathf.PI / sampleRate;
            sample += Mathf.Sin(phase1) * p.thumpAmount * thumpEnv;

            // Impact pop
            float impactEnv = Mathf.Exp(-t * p.impactDecay);
            float impactFreq = p.impactFreq * (1f + t * 2f); // Slight pitch rise
            phase2 += impactFreq * 2f * Mathf.PI / sampleRate;
            sample += Mathf.Sin(phase2) * p.impactAmount * impactEnv;

            // Body resonance
            float bodyEnv = Mathf.Exp(-t * p.bodyDecay);
            phase3 += p.bodyFreq * 2f * Mathf.PI / sampleRate;
            sample += Mathf.Sin(phase3) * p.bodyAmount * bodyEnv;

            // High sizzle
            float sizzleEnv = Mathf.Exp(-t * p.sizzleDecay);
            phase4 += p.sizzleFreq * 2f * Mathf.PI / sampleRate;
            sample += Mathf.Sin(phase4) * p.sizzleAmount * sizzleEnv;
            // Add some harmonics to sizzle
            sample += Mathf.Sin(phase4 * 2.5f) * p.sizzleAmount * 0.3f * sizzleEnv;

            // Noise burst
            float noiseEnv = Mathf.Exp(-t * p.noiseDecay);
            float noise = Random.Range(-1f, 1f);
            noise = LowPassFilter(noise, p.noiseCutoff, 0);
            sample += noise * p.noiseAmount * noiseEnv;

            // Soft clip for punch
            sample = SoftClip(sample);

            audioBuffer[i] = sample;
        }

        AudioClip clip = AudioClip.Create("PlayerHit", samples, 1, sampleRate, false);
        float[] finalBuffer = new float[samples];
        System.Array.Copy(audioBuffer, finalBuffer, samples);
        clip.SetData(finalBuffer, 0);
        return clip;
    }

    /// <summary>
    /// Static version of GenerateHitClip for prewarming (doesn't use instance fields).
    /// </summary>
    private static AudioClip GenerateStaticHitClip(HitPreset p)
    {
        int samples = Mathf.CeilToInt(p.duration * staticSampleRate);
        samples = Mathf.Min(samples, staticAudioBuffer.Length);

        // Reset filter state
        for (int i = 0; i < staticLpState.Length; i++) staticLpState[i] = 0;

        float phase1 = 0f, phase2 = 0f, phase3 = 0f, phase4 = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / staticSampleRate;
            float sample = 0f;

            // Initial thump (sub bass)
            float thumpEnv = Mathf.Exp(-t * 40f);
            phase1 += p.thumpFreq * 2f * Mathf.PI / staticSampleRate;
            sample += Mathf.Sin(phase1) * p.thumpAmount * thumpEnv;

            // Impact pop
            float impactEnv = Mathf.Exp(-t * p.impactDecay);
            float impactFreq = p.impactFreq * (1f + t * 2f); // Slight pitch rise
            phase2 += impactFreq * 2f * Mathf.PI / staticSampleRate;
            sample += Mathf.Sin(phase2) * p.impactAmount * impactEnv;

            // Body resonance
            float bodyEnv = Mathf.Exp(-t * p.bodyDecay);
            phase3 += p.bodyFreq * 2f * Mathf.PI / staticSampleRate;
            sample += Mathf.Sin(phase3) * p.bodyAmount * bodyEnv;

            // High sizzle
            float sizzleEnv = Mathf.Exp(-t * p.sizzleDecay);
            phase4 += p.sizzleFreq * 2f * Mathf.PI / staticSampleRate;
            sample += Mathf.Sin(phase4) * p.sizzleAmount * sizzleEnv;
            // Add some harmonics to sizzle
            sample += Mathf.Sin(phase4 * 2.5f) * p.sizzleAmount * 0.3f * sizzleEnv;

            // Noise burst
            float noiseEnv = Mathf.Exp(-t * p.noiseDecay);
            float noise = Random.Range(-1f, 1f);
            noise = StaticLowPassFilter(noise, p.noiseCutoff, 0);
            sample += noise * p.noiseAmount * noiseEnv;

            // Soft clip for punch
            sample = SoftClip(sample);

            staticAudioBuffer[i] = sample;
        }

        AudioClip clip = AudioClip.Create("PlayerHit", samples, 1, staticSampleRate, false);
        float[] finalBuffer = new float[samples];
        System.Array.Copy(staticAudioBuffer, finalBuffer, samples);
        clip.SetData(finalBuffer, 0);
        return clip;
    }

    private static float StaticLowPassFilter(float input, float cutoff, int stateIndex)
    {
        float rc = 1f / (2f * Mathf.PI * cutoff);
        float dt = 1f / staticSampleRate;
        float alpha = dt / (rc + dt);
        staticLpState[stateIndex] = staticLpState[stateIndex] + alpha * (input - staticLpState[stateIndex]);
        return staticLpState[stateIndex];
    }

    private static float SoftClip(float x)
    {
        if (x > 1f) return 1f - Mathf.Exp(-(x - 1f));
        if (x < -1f) return -1f + Mathf.Exp(-(-x - 1f));
        return x;
    }

    private float LowPassFilter(float input, float cutoff, int stateIndex)
    {
        float rc = 1f / (2f * Mathf.PI * cutoff);
        float dt = 1f / sampleRate;
        float alpha = dt / (rc + dt);
        lpState[stateIndex] = lpState[stateIndex] + alpha * (input - lpState[stateIndex]);
        return lpState[stateIndex];
    }

    // Static helper to play hit sound from anywhere
    public static void PlayHit(Vector3 position, HitSoundType type = HitSoundType.Energy, float vol = 0.5f)
    {
        EnsureStaticInitialized();
        
        // Use cached clip directly if available (avoids GameObject/Component overhead)
        AudioClip clip;
        if (cachedClips.TryGetValue(type, out clip) && clip != null)
        {
            // Play directly using AudioSource.PlayClipAtPoint for minimal overhead
            AudioSource.PlayClipAtPoint(clip, position, vol);
            return;
        }
        
        // Fallback: Create temporary audio source (shouldn't happen after prewarming)
        GameObject temp = new GameObject("ProjectileHitSound");
        temp.transform.position = position;
        
        AudioSource source = temp.AddComponent<AudioSource>();
        source.spatialBlend = 0.5f; // Partial 3D
        source.rolloffMode = AudioRolloffMode.Linear;
        source.maxDistance = 30f;
        
        ProceduralProjectileHitAudio hitAudio = temp.AddComponent<ProceduralProjectileHitAudio>();
        hitAudio.volume = vol;
        hitAudio.soundType = type;
        hitAudio.PlayHitSound();
        
        Destroy(temp, 0.5f);
    }
}
