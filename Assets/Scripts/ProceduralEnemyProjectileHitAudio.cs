using UnityEngine;

/// <summary>
/// Procedural impact sound generator for enemy projectile hits.
/// Creates distinct alien/organic impact sounds different from player projectiles.
/// </summary>
public class ProceduralEnemyProjectileHitAudio : MonoBehaviour
{
    public enum EnemyHitSoundType
    {
        PlasmaImpact,       // Acidic plasma splatter
        VoidBurst,          // Dark energy dissipation
        SwarmImpact,        // Multiple small impacts
        CorruptedHit,       // Glitchy, corrupted impact
        AcidSplash          // Wet, caustic splash
    }

    [Header("Sound Type")]
    [SerializeField] private EnemyHitSoundType soundType = EnemyHitSoundType.PlasmaImpact;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.45f;

    [Header("Variation")]
    [Range(0f, 0.3f)]
    [SerializeField] private float randomization = 0.18f;

    private struct EnemyHitPreset
    {
        public float duration;
        
        // Impact
        public float impactFreq;
        public float impactAmount;
        public float impactDecay;
        
        // Body
        public float bodyFreq;
        public float bodyAmount;
        public float bodyDecay;
        
        // High frequency component
        public float highFreq;
        public float highAmount;
        public float highDecay;
        
        // Noise
        public float noiseAmount;
        public float noiseDecay;
        public float noiseCutoff;
        public float noiseColor; // 0 = white, 1 = brown
        
        // Special effects
        public bool hasWet;
        public float wetAmount;
        public bool hasDistortion;
        public float distortionAmount;
        public bool hasFlutter;
        public float flutterRate;
    }

    private AudioSource audioSource;
    private int sampleRate;
    private float[] audioBuffer;
    private float[] lpState = new float[4];
    private float[] hpState = new float[2];

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
            
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        sampleRate = AudioSettings.outputSampleRate;
        int maxSamples = Mathf.CeilToInt(0.35f * sampleRate);
        audioBuffer = new float[maxSamples];
    }

    private EnemyHitPreset GetPreset(EnemyHitSoundType type)
    {
        EnemyHitPreset p = new EnemyHitPreset();

        switch (type)
        {
            case EnemyHitSoundType.PlasmaImpact:
                p.duration = 0.18f;
                p.impactFreq = 150f;
                p.impactAmount = 0.5f;
                p.impactDecay = 20f;
                p.bodyFreq = 280f;
                p.bodyAmount = 0.4f;
                p.bodyDecay = 15f;
                p.highFreq = 2200f;
                p.highAmount = 0.3f;
                p.highDecay = 25f;
                p.noiseAmount = 0.35f;
                p.noiseDecay = 18f;
                p.noiseCutoff = 3500f;
                p.noiseColor = 0.3f;
                p.hasWet = true;
                p.wetAmount = 0.25f;
                p.hasDistortion = false;
                p.hasFlutter = false;
                break;

            case EnemyHitSoundType.VoidBurst:
                p.duration = 0.22f;
                p.impactFreq = 80f;
                p.impactAmount = 0.6f;
                p.impactDecay = 12f;
                p.bodyFreq = 160f;
                p.bodyAmount = 0.45f;
                p.bodyDecay = 10f;
                p.highFreq = 1800f;
                p.highAmount = 0.25f;
                p.highDecay = 20f;
                p.noiseAmount = 0.4f;
                p.noiseDecay = 14f;
                p.noiseCutoff = 2500f;
                p.noiseColor = 0.6f;
                p.hasWet = false;
                p.hasDistortion = true;
                p.distortionAmount = 0.3f;
                p.hasFlutter = true;
                p.flutterRate = 30f;
                break;

            case EnemyHitSoundType.SwarmImpact:
                p.duration = 0.15f;
                p.impactFreq = 220f;
                p.impactAmount = 0.4f;
                p.impactDecay = 30f;
                p.bodyFreq = 400f;
                p.bodyAmount = 0.35f;
                p.bodyDecay = 25f;
                p.highFreq = 3500f;
                p.highAmount = 0.35f;
                p.highDecay = 35f;
                p.noiseAmount = 0.45f;
                p.noiseDecay = 30f;
                p.noiseCutoff = 5000f;
                p.noiseColor = 0.1f;
                p.hasWet = false;
                p.hasDistortion = false;
                p.hasFlutter = true;
                p.flutterRate = 60f;
                break;

            case EnemyHitSoundType.CorruptedHit:
                p.duration = 0.2f;
                p.impactFreq = 120f;
                p.impactAmount = 0.55f;
                p.impactDecay = 18f;
                p.bodyFreq = 240f;
                p.bodyAmount = 0.4f;
                p.bodyDecay = 15f;
                p.highFreq = 2800f;
                p.highAmount = 0.3f;
                p.highDecay = 22f;
                p.noiseAmount = 0.5f;
                p.noiseDecay = 20f;
                p.noiseCutoff = 4000f;
                p.noiseColor = 0.4f;
                p.hasWet = false;
                p.hasDistortion = true;
                p.distortionAmount = 0.5f;
                p.hasFlutter = true;
                p.flutterRate = 45f;
                break;

            case EnemyHitSoundType.AcidSplash:
                p.duration = 0.25f;
                p.impactFreq = 100f;
                p.impactAmount = 0.45f;
                p.impactDecay = 15f;
                p.bodyFreq = 200f;
                p.bodyAmount = 0.35f;
                p.bodyDecay = 12f;
                p.highFreq = 1500f;
                p.highAmount = 0.4f;
                p.highDecay = 10f;
                p.noiseAmount = 0.5f;
                p.noiseDecay = 12f;
                p.noiseCutoff = 3000f;
                p.noiseColor = 0.5f;
                p.hasWet = true;
                p.wetAmount = 0.45f;
                p.hasDistortion = false;
                p.hasFlutter = false;
                break;
        }

        return p;
    }

    public void PlayHitSound()
    {
        PlayHitSound(soundType);
    }

    public void PlayHitSound(EnemyHitSoundType type)
    {
        EnemyHitPreset preset = GetPreset(type);
        
        // Apply randomization
        float randMult = 1f + Random.Range(-randomization, randomization);
        preset.impactFreq *= randMult;
        preset.bodyFreq *= randMult;
        preset.highFreq *= Mathf.Lerp(1f, randMult, 0.6f);

        AudioClip clip = GenerateHitClip(preset);
        audioSource.PlayOneShot(clip, volume);
    }

    private AudioClip GenerateHitClip(EnemyHitPreset p)
    {
        int samples = Mathf.CeilToInt(p.duration * sampleRate);
        samples = Mathf.Min(samples, audioBuffer.Length);

        // Reset filter states
        for (int i = 0; i < lpState.Length; i++) lpState[i] = 0;
        for (int i = 0; i < hpState.Length; i++) hpState[i] = 0;

        float phase1 = 0f, phase2 = 0f, phase3 = 0f;
        float wetPhase = Random.Range(0f, Mathf.PI * 2f);

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            // Flutter modulation
            float flutter = 1f;
            if (p.hasFlutter)
            {
                flutter = 1f + 0.2f * Mathf.Sin(t * p.flutterRate * 2f * Mathf.PI);
            }

            // Impact thump (pitch drops)
            float impactEnv = Mathf.Exp(-t * p.impactDecay);
            float impactFreq = p.impactFreq * (1f - t * 0.5f) * flutter; // Pitch drops
            phase1 += impactFreq * 2f * Mathf.PI / sampleRate;
            sample += Mathf.Sin(phase1) * p.impactAmount * impactEnv;

            // Body resonance
            float bodyEnv = Mathf.Exp(-t * p.bodyDecay);
            phase2 += p.bodyFreq * flutter * 2f * Mathf.PI / sampleRate;
            sample += Mathf.Sin(phase2) * p.bodyAmount * bodyEnv;

            // High frequency component
            float highEnv = Mathf.Exp(-t * p.highDecay);
            phase3 += p.highFreq * 2f * Mathf.PI / sampleRate;
            sample += Mathf.Sin(phase3) * p.highAmount * highEnv;

            // Noise (colored)
            float noiseEnv = Mathf.Exp(-t * p.noiseDecay);
            float noise = Random.Range(-1f, 1f);
            
            // Apply color (brown noise = low pass filtered)
            if (p.noiseColor > 0)
            {
                noise = LowPassFilter(noise, Mathf.Lerp(8000f, 500f, p.noiseColor), 0);
            }
            noise = LowPassFilter(noise, p.noiseCutoff, 1);
            sample += noise * p.noiseAmount * noiseEnv;

            // Wet/splatter effect
            if (p.hasWet)
            {
                float wetEnv = Mathf.Exp(-t * 8f) * (1f - Mathf.Exp(-t * 50f));
                wetPhase += (600f + Random.Range(-100f, 100f)) * 2f * Mathf.PI / sampleRate;
                float wet = Mathf.Sin(wetPhase) * 0.5f + Random.Range(-0.5f, 0.5f);
                wet = LowPassFilter(wet, 2000f, 2);
                sample += wet * p.wetAmount * wetEnv;
            }

            // Distortion
            if (p.hasDistortion)
            {
                sample = ApplyDistortion(sample, p.distortionAmount);
            }

            // Soft clip
            sample = SoftClip(sample * 0.9f);

            audioBuffer[i] = sample;
        }

        AudioClip clip = AudioClip.Create("EnemyProjectileHit", samples, 1, sampleRate, false);
        float[] finalBuffer = new float[samples];
        System.Array.Copy(audioBuffer, finalBuffer, samples);
        clip.SetData(finalBuffer, 0);
        return clip;
    }

    private float LowPassFilter(float input, float cutoff, int stateIndex)
    {
        float rc = 1f / (2f * Mathf.PI * cutoff);
        float dt = 1f / sampleRate;
        float alpha = dt / (rc + dt);
        lpState[stateIndex] = lpState[stateIndex] + alpha * (input - lpState[stateIndex]);
        return lpState[stateIndex];
    }

    private float ApplyDistortion(float x, float amount)
    {
        // Waveshaping distortion
        float k = 2f * amount / (1f - amount + 0.001f);
        return (1f + k) * x / (1f + k * Mathf.Abs(x));
    }

    private float SoftClip(float x)
    {
        if (x > 1f) return 1f - Mathf.Exp(-(x - 1f));
        if (x < -1f) return -1f + Mathf.Exp(-(-x - 1f));
        return x;
    }

    // Static helper to play hit sound at position
    public static void PlayHit(Vector3 position, EnemyHitSoundType type = EnemyHitSoundType.PlasmaImpact, float vol = 0.45f)
    {
        GameObject temp = new GameObject("EnemyProjectileHitSound");
        temp.transform.position = position;
        
        AudioSource source = temp.AddComponent<AudioSource>();
        source.spatialBlend = 0.5f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.maxDistance = 30f;
        
        ProceduralEnemyProjectileHitAudio hitAudio = temp.AddComponent<ProceduralEnemyProjectileHitAudio>();
        hitAudio.volume = vol;
        hitAudio.soundType = type;
        hitAudio.PlayHitSound();
        
        Destroy(temp, 0.5f);
    }
}
