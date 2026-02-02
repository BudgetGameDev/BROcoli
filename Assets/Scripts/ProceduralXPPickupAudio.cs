using UnityEngine;

/// <summary>
/// Procedural XP pickup sound - satisfying, dopamine-inducing collect sound.
/// Combines a bright chime with a soft whoosh for that rewarding feel.
/// </summary>
public class ProceduralXPPickupAudio : MonoBehaviour
{
    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.5f;

    [Header("Pitch Variation")]
    [Range(0f, 0.3f)]
    [SerializeField] private float pitchVariation = 0.1f;
    
    [Header("Pitch Scaling")]
    [SerializeField] private bool scaleWithCombo = true;
    [SerializeField] private float maxPitchBoost = 0.5f;

    private static AudioSource sharedAudioSource;
    private static int sampleRate;
    private static float[] audioBuffer;
    private static float[] lpState = new float[4];
    
    // Cached base clip for instant playback
    private static AudioClip cachedBaseClip;
    
    // Combo tracking for pitch scaling
    private static float lastPickupTime;
    private static int comboCount;
    private const float COMBO_WINDOW = 0.5f;
    private const int MAX_COMBO = 10;

    void Awake()
    {
        EnsureInitialized();
    }

    private static void EnsureInitialized()
    {
        if (sharedAudioSource == null)
        {
            GameObject audioObj = new GameObject("XPPickupAudio");
            DontDestroyOnLoad(audioObj);
            sharedAudioSource = audioObj.AddComponent<AudioSource>();
            sharedAudioSource.playOnAwake = false;
            sharedAudioSource.spatialBlend = 0f; // 2D sound
            
            sampleRate = AudioSettings.outputSampleRate;
            int maxSamples = Mathf.CeilToInt(0.5f * sampleRate);
            audioBuffer = new float[maxSamples];
        }
    }
    
    /// <summary>
    /// Pre-generate the base XP pickup sound clip to avoid hitches on first pickup.
    /// Call this during loading screen.
    /// </summary>
    public static void PrewarmAll()
    {
        EnsureInitialized();
        if (cachedBaseClip == null)
        {
            cachedBaseClip = GeneratePickupClip(1f);
        }
    }

    public void PlayPickupSound()
    {
        PlayPickupSoundInternal(volume, pitchVariation, scaleWithCombo, maxPitchBoost);
    }

    public static void PlayPickup(float vol = 0.5f, float pitchVar = 0.1f, bool useCombo = true, float maxPitch = 0.5f)
    {
        EnsureInitialized();
        PlayPickupSoundInternal(vol, pitchVar, useCombo, maxPitch);
    }

    private static void PlayPickupSoundInternal(float vol, float pitchVar, bool useCombo, float maxPitchBoost)
    {
        // Update combo
        float currentTime = Time.time;
        if (currentTime - lastPickupTime < COMBO_WINDOW)
        {
            comboCount = Mathf.Min(comboCount + 1, MAX_COMBO);
        }
        else
        {
            comboCount = 0;
        }
        lastPickupTime = currentTime;

        // Calculate pitch multiplier based on combo
        float comboPitch = 1f;
        if (useCombo && comboCount > 0)
        {
            comboPitch = 1f + (maxPitchBoost * (float)comboCount / MAX_COMBO);
        }

        // Random pitch variation
        float pitchMult = comboPitch * (1f + Random.Range(-pitchVar, pitchVar));

        AudioClip clip = GeneratePickupClip(pitchMult);
        sharedAudioSource.PlayOneShot(clip, vol);
    }

    private static AudioClip GeneratePickupClip(float pitchMult)
    {
        float duration = 0.25f;
        int totalSamples = Mathf.CeilToInt(duration * sampleRate);
        totalSamples = Mathf.Min(totalSamples, audioBuffer.Length);

        System.Array.Clear(lpState, 0, lpState.Length);

        // Base frequencies for a pleasant major chord arpeggio feel
        float baseFreq = 880f * pitchMult;  // A5
        float freq2 = baseFreq * 1.25f;      // C#6 (major third)
        float freq3 = baseFreq * 1.5f;       // E6 (perfect fifth)
        float freq4 = baseFreq * 2f;         // A6 (octave)

        float phase1 = 0f, phase2 = 0f, phase3 = 0f, phase4 = 0f;
        float shimmerPhase = 0f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float normalizedT = t / duration;

            float sample = 0f;

            // === MAIN CHIME (bright sine with harmonics) ===
            float chimeEnv = GetChimeEnvelope(t, duration);
            
            // Staggered entry for arpeggio effect
            float t1 = t;
            float t2 = Mathf.Max(0f, t - 0.015f);
            float t3 = Mathf.Max(0f, t - 0.03f);
            float t4 = Mathf.Max(0f, t - 0.045f);

            phase1 += baseFreq / sampleRate;
            float tone1 = Mathf.Sin(phase1 * Mathf.PI * 2f);
            tone1 += Mathf.Sin(phase1 * Mathf.PI * 4f) * 0.3f; // 2nd harmonic
            tone1 *= GetStaggeredEnv(t1, duration) * 0.4f;

            phase2 += freq2 / sampleRate;
            float tone2 = Mathf.Sin(phase2 * Mathf.PI * 2f);
            tone2 += Mathf.Sin(phase2 * Mathf.PI * 4f) * 0.25f;
            tone2 *= GetStaggeredEnv(t2, duration) * 0.35f;

            phase3 += freq3 / sampleRate;
            float tone3 = Mathf.Sin(phase3 * Mathf.PI * 2f);
            tone3 += Mathf.Sin(phase3 * Mathf.PI * 4f) * 0.2f;
            tone3 *= GetStaggeredEnv(t3, duration) * 0.3f;

            phase4 += freq4 / sampleRate;
            float tone4 = Mathf.Sin(phase4 * Mathf.PI * 2f);
            tone4 *= GetStaggeredEnv(t4, duration) * 0.25f;

            float chime = (tone1 + tone2 + tone3 + tone4) * chimeEnv;

            // === SHIMMER (high frequency sparkle) ===
            float shimmerEnv = Mathf.Exp(-t * 15f);
            shimmerPhase += (3500f * pitchMult) / sampleRate;
            float shimmer = Mathf.Sin(shimmerPhase * Mathf.PI * 2f);
            shimmer *= shimmerEnv * 0.15f;

            // === SOFT WHOOSH (filtered noise) ===
            float whooshEnv = GetWhooshEnvelope(t, duration);
            float noise = Random.Range(-1f, 1f);
            float whoosh = LowpassFilter(noise, 2000f + 3000f * (1f - normalizedT), 0);
            whoosh = LowpassFilter(whoosh, 4000f, 1); // Extra smoothing
            whoosh *= whooshEnv * 0.12f;

            // === ATTACK CLICK ===
            float click = 0f;
            if (t < 0.008f)
            {
                float clickEnv = Mathf.Exp(-t * 400f);
                click = Mathf.Sin(t * 6000f * Mathf.PI * 2f) * clickEnv * 0.2f;
            }

            // === COMBINE ===
            sample = chime + shimmer + whoosh + click;

            // Soft clip
            sample = SoftClip(sample);

            audioBuffer[i] = sample;
        }

        // Fade out
        int fadeOutSamples = Mathf.Min(totalSamples / 4, sampleRate / 20);
        for (int i = 0; i < fadeOutSamples; i++)
        {
            int idx = totalSamples - 1 - i;
            float fade = (float)i / fadeOutSamples;
            fade = fade * fade;
            audioBuffer[idx] *= fade;
        }

        // Normalize with headroom
        float maxAmp = 0f;
        for (int i = 0; i < totalSamples; i++)
            maxAmp = Mathf.Max(maxAmp, Mathf.Abs(audioBuffer[i]));

        if (maxAmp > 0.01f)
        {
            float normalize = 0.7f / maxAmp;
            for (int i = 0; i < totalSamples; i++)
                audioBuffer[i] *= normalize;
        }

        AudioClip clip = AudioClip.Create("XPPickup", totalSamples, 1, sampleRate, false);
        float[] clipData = new float[totalSamples];
        System.Array.Copy(audioBuffer, clipData, totalSamples);
        clip.SetData(clipData, 0);

        return clip;
    }

    private static float GetChimeEnvelope(float t, float duration)
    {
        float attack = 0.005f;
        float decay = 0.08f;
        float sustainLevel = 0.6f;

        if (t < attack)
            return t / attack;
        else if (t < attack + decay)
        {
            float dt = (t - attack) / decay;
            return 1f - (1f - sustainLevel) * dt;
        }
        else
        {
            float remaining = duration - attack - decay;
            float dt = (t - attack - decay) / remaining;
            return sustainLevel * Mathf.Exp(-dt * 4f);
        }
    }

    private static float GetStaggeredEnv(float t, float duration)
    {
        if (t <= 0f) return 0f;
        
        float attack = 0.003f;
        if (t < attack)
            return t / attack;
        else
            return Mathf.Exp(-(t - attack) * 8f);
    }

    private static float GetWhooshEnvelope(float t, float duration)
    {
        // Quick rise, medium decay
        float attack = 0.01f;
        float peak = 0.03f;

        if (t < attack)
            return t / attack;
        else if (t < peak)
            return 1f;
        else
            return Mathf.Exp(-(t - peak) * 12f);
    }

    private static float LowpassFilter(float input, float cutoff, int stateIndex)
    {
        float rc = 1f / (2f * Mathf.PI * cutoff);
        float dt = 1f / sampleRate;
        float alpha = dt / (rc + dt);
        alpha = Mathf.Clamp01(alpha);

        lpState[stateIndex] += alpha * (input - lpState[stateIndex]);
        return lpState[stateIndex];
    }

    private static float SoftClip(float x)
    {
        if (x > 1f) return 1f;
        if (x < -1f) return -1f;
        return x - (x * x * x) / 3f;
    }
}
