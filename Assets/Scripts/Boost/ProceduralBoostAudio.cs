using UnityEngine;

/// <summary>
/// Procedural audio generator for boost pickups.
/// Each boost type has a unique, thematic sound that matches its effect.
/// </summary>
public class ProceduralBoostAudio : MonoBehaviour
{
    public enum BoostSoundType
    {
        Health,         // Warm, healing chime
        Damage,         // Powerful impact punch
        AttackSpeed,    // Rapid staccato tones
        MovementSpeed,  // Whooshing wind sound
        Experience,     // Bright ascending sparkle
        DetectionRadius,// Radar ping/sonar
        SprayRange,     // Extending reach sound
        SprayWidth,     // Spreading expansion sound
        Magnet          // Magnetic pull whoosh
    }

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.6f;

    private static AudioSource sharedAudioSource;
    private static int sampleRate;
    private static float[] audioBuffer;
    private static float[] filterState = new float[8];

    void Awake()
    {
        EnsureInitialized();
    }

    private static void EnsureInitialized()
    {
        if (sharedAudioSource == null)
        {
            GameObject audioObj = new GameObject("BoostPickupAudio");
            DontDestroyOnLoad(audioObj);
            sharedAudioSource = audioObj.AddComponent<AudioSource>();
            sharedAudioSource.playOnAwake = false;
            sharedAudioSource.spatialBlend = 0f;
            
            sampleRate = AudioSettings.outputSampleRate;
            int maxSamples = Mathf.CeilToInt(0.8f * sampleRate);
            audioBuffer = new float[maxSamples];
        }
    }

    public void PlayBoostSound(BoostSoundType type)
    {
        PlaySound(type, volume);
    }

    public static void PlaySound(BoostSoundType type, float vol = 0.6f)
    {
        EnsureInitialized();
        AudioClip clip = GenerateClip(type);
        sharedAudioSource.PlayOneShot(clip, vol);
    }

    private static AudioClip GenerateClip(BoostSoundType type)
    {
        System.Array.Clear(filterState, 0, filterState.Length);
        
        switch (type)
        {
            case BoostSoundType.Health:
                return GenerateHealthSound();
            case BoostSoundType.Damage:
                return GenerateDamageSound();
            case BoostSoundType.AttackSpeed:
                return GenerateAttackSpeedSound();
            case BoostSoundType.MovementSpeed:
                return GenerateMovementSpeedSound();
            case BoostSoundType.Experience:
                return GenerateExperienceSound();
            case BoostSoundType.DetectionRadius:
                return GenerateDetectionRadiusSound();
            case BoostSoundType.SprayRange:
                return GenerateSprayRangeSound();
            case BoostSoundType.SprayWidth:
                return GenerateSprayWidthSound();
            case BoostSoundType.Magnet:
                return GenerateMagnetSound();
            default:
                return GenerateHealthSound();
        }
    }

    /// <summary>
    /// Health boost - Warm, soothing healing chime with soft harmonics.
    /// Evokes restoration and comfort.
    /// </summary>
    private static AudioClip GenerateHealthSound()
    {
        float duration = 0.4f;
        int numSamples = Mathf.CeilToInt(duration * sampleRate);
        numSamples = Mathf.Min(numSamples, audioBuffer.Length);

        // Warm minor 7th chord frequencies (healing feel)
        float[] freqs = { 392f, 466.16f, 587.33f, 698.46f }; // G4, Bb4, D5, F5

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            // Staggered warm tones
            for (int n = 0; n < freqs.Length; n++)
            {
                float noteDelay = n * 0.04f;
                float noteT = t - noteDelay;
                if (noteT < 0f) continue;

                float env = GetHealEnvelope(noteT, duration - noteDelay);
                float phase = noteT * freqs[n] * Mathf.PI * 2f;
                
                // Soft sine with gentle harmonics
                float tone = Mathf.Sin(phase) * 0.6f;
                tone += Mathf.Sin(phase * 2f) * 0.2f;
                tone += Mathf.Sin(phase * 0.5f) * 0.15f; // Sub-harmonic warmth
                
                sample += tone * env * 0.3f;
            }

            // Soft shimmer overlay
            float shimmerEnv = Mathf.Exp(-t * 6f) * Mathf.Sin(t * 8f) * 0.5f + 0.5f;
            float shimmer = Mathf.Sin(t * 1200f * Mathf.PI * 2f) * shimmerEnv * 0.08f;
            sample += shimmer;

            // Gentle whoosh
            float whooshEnv = Mathf.Exp(-t * 4f);
            float noise = Lowpass(Random.Range(-1f, 1f), 800f, 0);
            sample += noise * whooshEnv * 0.06f;

            audioBuffer[i] = SoftClip(sample);
        }

        return FinalizeClip("HealthBoost", numSamples, duration);
    }

    /// <summary>
    /// Damage boost - Powerful impact with bass punch and metallic edge.
    /// Evokes strength and power.
    /// </summary>
    private static AudioClip GenerateDamageSound()
    {
        float duration = 0.3f;
        int numSamples = Mathf.CeilToInt(duration * sampleRate);
        numSamples = Mathf.Min(numSamples, audioBuffer.Length);

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            // Heavy sub-bass punch
            float punchEnv = Mathf.Exp(-t * 25f);
            float punch = Mathf.Sin(t * 60f * Mathf.PI * 2f) * punchEnv * 0.5f;
            punch += Mathf.Sin(t * 90f * Mathf.PI * 2f) * punchEnv * 0.3f;
            sample += punch;

            // Aggressive mid attack
            float attackEnv = Mathf.Exp(-t * 15f);
            float attack = Mathf.Sin(t * 200f * Mathf.PI * 2f) * attackEnv * 0.4f;
            attack += Mathf.Sin(t * 350f * Mathf.PI * 2f) * attackEnv * 0.25f;
            sample += attack;

            // Metallic transient
            if (t < 0.05f)
            {
                float transientEnv = Mathf.Exp(-t * 80f);
                float transient = Mathf.Sin(t * 2500f * Mathf.PI * 2f) * transientEnv * 0.3f;
                transient += Mathf.Sin(t * 3800f * Mathf.PI * 2f) * transientEnv * 0.15f;
                sample += transient;
            }

            // Distorted noise burst
            float noiseEnv = Mathf.Exp(-t * 30f);
            float noise = Lowpass(Random.Range(-1f, 1f), 3000f, 0);
            sample += noise * noiseEnv * 0.15f;

            // Power chord undertone
            float chordEnv = Mathf.Exp(-t * 8f);
            sample += Mathf.Sin(t * 110f * Mathf.PI * 2f) * chordEnv * 0.2f; // A2
            sample += Mathf.Sin(t * 165f * Mathf.PI * 2f) * chordEnv * 0.15f; // E3

            audioBuffer[i] = HardClip(sample * 1.2f);
        }

        return FinalizeClip("DamageBoost", numSamples, duration);
    }

    /// <summary>
    /// Attack Speed boost - Rapid staccato tones accelerating upward.
    /// Evokes quickness and rapid-fire.
    /// </summary>
    private static AudioClip GenerateAttackSpeedSound()
    {
        float duration = 0.35f;
        int numSamples = Mathf.CeilToInt(duration * sampleRate);
        numSamples = Mathf.Min(numSamples, audioBuffer.Length);

        int numClicks = 6;
        float clickDuration = 0.025f;

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            // Accelerating clicks
            for (int c = 0; c < numClicks; c++)
            {
                // Exponentially decreasing gaps (accelerating)
                float clickTime = c * (0.06f - c * 0.008f);
                float clickT = t - clickTime;
                
                if (clickT >= 0f && clickT < clickDuration)
                {
                    float clickEnv = Mathf.Exp(-clickT * 100f);
                    float freq = 1200f + c * 150f; // Rising pitch
                    float click = Mathf.Sin(clickT * freq * Mathf.PI * 2f) * clickEnv;
                    click += Mathf.Sin(clickT * freq * 2f * Mathf.PI * 2f) * clickEnv * 0.3f;
                    sample += click * 0.4f;
                }
            }

            // Fast arpeggio sweep
            float sweepEnv = Mathf.Exp(-t * 10f);
            float sweepFreq = 800f + t * 3000f; // Rising sweep
            sample += Mathf.Sin(t * sweepFreq * Mathf.PI * 2f) * sweepEnv * 0.2f;

            // Mechanical whir
            float whirEnv = Mathf.Sin(t * 30f) * 0.5f + 0.5f;
            whirEnv *= Mathf.Exp(-t * 8f);
            sample += Mathf.Sin(t * 400f * Mathf.PI * 2f) * whirEnv * 0.1f;

            audioBuffer[i] = SoftClip(sample);
        }

        return FinalizeClip("AttackSpeedBoost", numSamples, duration);
    }

    /// <summary>
    /// Movement Speed boost - Whooshing wind with doppler-like effect.
    /// Evokes motion and velocity.
    /// </summary>
    private static AudioClip GenerateMovementSpeedSound()
    {
        float duration = 0.4f;
        int numSamples = Mathf.CeilToInt(duration * sampleRate);
        numSamples = Mathf.Min(numSamples, audioBuffer.Length);

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            // Main whoosh - filtered noise with sweeping cutoff
            float whooshEnv = GetWhooshEnvelope(t, duration);
            float cutoff = 800f + 2500f * Mathf.Sin(t * 4f * Mathf.PI);
            cutoff = Mathf.Max(cutoff, 400f);
            
            float noise = Random.Range(-1f, 1f);
            float whoosh = Lowpass(noise, cutoff, 0);
            whoosh = Highpass(whoosh, 200f, 1);
            sample += whoosh * whooshEnv * 0.4f;

            // Wind whistle harmonics
            float whistleEnv = Mathf.Exp(-Mathf.Abs(t - 0.15f) * 8f);
            float whistleFreq = 2000f + Mathf.Sin(t * 20f) * 300f;
            sample += Mathf.Sin(t * whistleFreq * Mathf.PI * 2f) * whistleEnv * 0.1f;

            // Speed streaks (high frequency bursts)
            for (int s = 0; s < 3; s++)
            {
                float streakT = t - s * 0.1f;
                if (streakT > 0f && streakT < 0.08f)
                {
                    float streakEnv = Mathf.Exp(-streakT * 40f);
                    float streakFreq = 3000f + s * 500f;
                    sample += Mathf.Sin(streakT * streakFreq * Mathf.PI * 2f) * streakEnv * 0.08f;
                }
            }

            // Low rumble undertone
            float rumbleEnv = Mathf.Exp(-t * 5f);
            sample += Mathf.Sin(t * 80f * Mathf.PI * 2f) * rumbleEnv * 0.15f;

            audioBuffer[i] = SoftClip(sample);
        }

        return FinalizeClip("MovementSpeedBoost", numSamples, duration);
    }

    /// <summary>
    /// Experience boost - Bright, sparkling ascending tones.
    /// Evokes growth and enlightenment.
    /// </summary>
    private static AudioClip GenerateExperienceSound()
    {
        float duration = 0.35f;
        int numSamples = Mathf.CeilToInt(duration * sampleRate);
        numSamples = Mathf.Min(numSamples, audioBuffer.Length);

        // Ascending major scale snippet
        float[] notes = { 523.25f, 659.25f, 783.99f, 880f, 1046.5f }; // C5, E5, G5, A5, C6

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            // Quick ascending arpeggio
            for (int n = 0; n < notes.Length; n++)
            {
                float noteStart = n * 0.035f;
                float noteT = t - noteStart;
                if (noteT < 0f) continue;

                float noteEnv = Mathf.Exp(-noteT * 12f);
                float phase = noteT * notes[n] * Mathf.PI * 2f;
                
                float tone = Mathf.Sin(phase) * 0.5f;
                tone += Mathf.Sin(phase * 2f) * 0.2f;
                tone += Mathf.Sin(phase * 3f) * 0.1f;
                
                sample += tone * noteEnv * 0.25f;
            }

            // Sparkle layer
            float sparkleEnv = Mathf.Exp(-t * 8f);
            float sparkleFreq = 2800f + Mathf.Sin(t * 25f) * 400f;
            sample += Mathf.Sin(t * sparkleFreq * Mathf.PI * 2f) * sparkleEnv * 0.12f;

            // Bright shimmer
            float shimmerMod = Mathf.Sin(t * 40f) * 0.5f + 0.5f;
            sample += Mathf.Sin(t * 4000f * Mathf.PI * 2f) * shimmerMod * Mathf.Exp(-t * 15f) * 0.06f;

            audioBuffer[i] = SoftClip(sample);
        }

        return FinalizeClip("ExperienceBoost", numSamples, duration);
    }

    /// <summary>
    /// Detection Radius boost - Radar ping with sonar-like resonance.
    /// Evokes scanning and awareness.
    /// </summary>
    private static AudioClip GenerateDetectionRadiusSound()
    {
        float duration = 0.5f;
        int numSamples = Mathf.CeilToInt(duration * sampleRate);
        numSamples = Mathf.Min(numSamples, audioBuffer.Length);

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            // Main sonar ping
            float pingEnv = Mathf.Exp(-t * 6f);
            float pingFreq = 1400f;
            float ping = Mathf.Sin(t * pingFreq * Mathf.PI * 2f) * pingEnv * 0.5f;
            
            // Resonant harmonics (sonar character)
            ping += Mathf.Sin(t * pingFreq * 2f * Mathf.PI * 2f) * pingEnv * 0.2f;
            ping += Mathf.Sin(t * pingFreq * 0.5f * Mathf.PI * 2f) * pingEnv * 0.15f;
            sample += ping;

            // Expanding wave effect (echo)
            for (int e = 1; e <= 3; e++)
            {
                float echoT = t - e * 0.1f;
                if (echoT > 0f)
                {
                    float echoEnv = Mathf.Exp(-echoT * 8f) * (1f - e * 0.25f);
                    float echoFreq = pingFreq * (1f - e * 0.05f); // Slight pitch drop
                    sample += Mathf.Sin(echoT * echoFreq * Mathf.PI * 2f) * echoEnv * 0.2f;
                }
            }

            // Electronic blip at start
            if (t < 0.03f)
            {
                float blipEnv = Mathf.Exp(-t * 150f);
                sample += Mathf.Sin(t * 3000f * Mathf.PI * 2f) * blipEnv * 0.25f;
            }

            // Subtle static hum
            float humEnv = Mathf.Exp(-t * 4f);
            float noise = Lowpass(Random.Range(-1f, 1f), 600f, 0);
            sample += noise * humEnv * 0.04f;

            audioBuffer[i] = SoftClip(sample);
        }

        return FinalizeClip("DetectionRadiusBoost", numSamples, duration);
    }

    /// <summary>
    /// Spray Range boost - Extending reach sound with stretching quality.
    /// Evokes distance and projection.
    /// </summary>
    private static AudioClip GenerateSprayRangeSound()
    {
        float duration = 0.4f;
        int numSamples = Mathf.CeilToInt(duration * sampleRate);
        numSamples = Mathf.Min(numSamples, audioBuffer.Length);

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            // Extending tone (pitch rises then settles)
            float extendEnv = GetExtendEnvelope(t, duration);
            float pitchCurve = 1f + 0.3f * Mathf.Exp(-t * 8f); // Starts high, settles
            float baseFreq = 600f * pitchCurve;
            
            sample += Mathf.Sin(t * baseFreq * Mathf.PI * 2f) * extendEnv * 0.35f;
            sample += Mathf.Sin(t * baseFreq * 1.5f * Mathf.PI * 2f) * extendEnv * 0.2f;

            // Spray hiss (extends outward)
            float hissEnv = Mathf.Max(0f, 1f - t * 3f) * Mathf.Exp(-t * 4f);
            float hissCutoff = 4000f + 2000f * t; // Opening up
            float hiss = Lowpass(Random.Range(-1f, 1f), hissCutoff, 0);
            hiss = Highpass(hiss, 1500f, 1);
            sample += hiss * hissEnv * 0.2f;

            // Pressure release burst at start
            if (t < 0.05f)
            {
                float burstEnv = Mathf.Exp(-t * 60f);
                float burst = Lowpass(Random.Range(-1f, 1f), 5000f, 2);
                sample += burst * burstEnv * 0.25f;
            }

            // Reaching tone
            float reachEnv = Mathf.Exp(-Mathf.Abs(t - 0.1f) * 10f);
            sample += Mathf.Sin(t * 1000f * Mathf.PI * 2f) * reachEnv * 0.15f;

            audioBuffer[i] = SoftClip(sample);
        }

        return FinalizeClip("SprayRangeBoost", numSamples, duration);
    }

    /// <summary>
    /// Spray Width boost - Spreading expansion sound with widening quality.
    /// Evokes spreading and coverage.
    /// </summary>
    private static AudioClip GenerateSprayWidthSound()
    {
        float duration = 0.4f;
        int numSamples = Mathf.CeilToInt(duration * sampleRate);
        numSamples = Mathf.Min(numSamples, audioBuffer.Length);

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            // Spreading chord (notes diverge from center)
            float spreadEnv = GetSpreadEnvelope(t, duration);
            float centerFreq = 700f;
            float spread = Mathf.Min(t * 3f, 1f) * 200f; // Frequencies spread apart
            
            sample += Mathf.Sin(t * centerFreq * Mathf.PI * 2f) * spreadEnv * 0.25f;
            sample += Mathf.Sin(t * (centerFreq + spread) * Mathf.PI * 2f) * spreadEnv * 0.2f;
            sample += Mathf.Sin(t * (centerFreq - spread * 0.8f) * Mathf.PI * 2f) * spreadEnv * 0.2f;
            sample += Mathf.Sin(t * (centerFreq + spread * 1.5f) * Mathf.PI * 2f) * spreadEnv * 0.15f;

            // Fan-out hiss
            float fanEnv = Mathf.Exp(-t * 5f);
            float fanCutoff = 2000f + 3000f * Mathf.Min(t * 4f, 1f);
            float fan = Lowpass(Random.Range(-1f, 1f), fanCutoff, 0);
            sample += fan * fanEnv * 0.15f;

            // Opening whoosh
            if (t < 0.15f)
            {
                float openEnv = Mathf.Sin(t / 0.15f * Mathf.PI);
                float openNoise = Lowpass(Random.Range(-1f, 1f), 3000f, 1);
                sample += openNoise * openEnv * 0.2f;
            }

            // Wide shimmer
            float shimmerEnv = Mathf.Exp(-t * 6f);
            float shimmerMod = Mathf.Sin(t * 15f) * 0.5f + 0.5f;
            sample += Mathf.Sin(t * 2200f * Mathf.PI * 2f) * shimmerEnv * shimmerMod * 0.08f;

            audioBuffer[i] = SoftClip(sample);
        }

        return FinalizeClip("SprayWidthBoost", numSamples, duration);
    }

    #region Helper Methods

    private static float GetHealEnvelope(float t, float duration)
    {
        float attack = 0.02f;
        float sustain = 0.3f;
        
        if (t < attack)
            return t / attack;
        else if (t < sustain)
            return 1f;
        else
            return Mathf.Exp(-(t - sustain) * 6f);
    }

    private static float GetWhooshEnvelope(float t, float duration)
    {
        float peak = 0.12f;
        if (t < peak)
            return Mathf.Sin(t / peak * Mathf.PI * 0.5f);
        else
            return Mathf.Exp(-(t - peak) * 5f);
    }

    private static float GetExtendEnvelope(float t, float duration)
    {
        float attack = 0.01f;
        if (t < attack)
            return t / attack;
        else
            return Mathf.Exp(-(t - attack) * 4f);
    }

    private static float GetSpreadEnvelope(float t, float duration)
    {
        float attack = 0.03f;
        float hold = 0.1f;
        
        if (t < attack)
            return t / attack;
        else if (t < attack + hold)
            return 1f;
        else
            return Mathf.Exp(-(t - attack - hold) * 5f);
    }

    private static float Lowpass(float input, float cutoff, int stateIdx)
    {
        float rc = 1f / (2f * Mathf.PI * cutoff);
        float dt = 1f / sampleRate;
        float alpha = Mathf.Clamp01(dt / (rc + dt));
        
        filterState[stateIdx] += alpha * (input - filterState[stateIdx]);
        return filterState[stateIdx];
    }

    private static float Highpass(float input, float cutoff, int stateIdx)
    {
        float rc = 1f / (2f * Mathf.PI * cutoff);
        float dt = 1f / sampleRate;
        float alpha = Mathf.Clamp01(rc / (rc + dt));
        
        float output = alpha * (filterState[stateIdx + 4] + input - filterState[stateIdx]);
        filterState[stateIdx] = input;
        filterState[stateIdx + 4] = output;
        return output;
    }

    private static float SoftClip(float x)
    {
        if (x > 1f) return 1f;
        if (x < -1f) return -1f;
        return x - (x * x * x) / 3f;
    }

    private static float HardClip(float x)
    {
        return Mathf.Clamp(x, -0.95f, 0.95f);
    }

    private static AudioClip FinalizeClip(string name, int numSamples, float duration)
    {
        // Normalize
        float maxAmp = 0f;
        for (int i = 0; i < numSamples; i++)
            maxAmp = Mathf.Max(maxAmp, Mathf.Abs(audioBuffer[i]));

        if (maxAmp > 0.01f)
        {
            float normalize = 0.8f / maxAmp;
            for (int i = 0; i < numSamples; i++)
                audioBuffer[i] *= normalize;
        }

        // Fade out
        int fadeOut = Mathf.Min(numSamples / 5, sampleRate / 20);
        for (int i = 0; i < fadeOut; i++)
        {
            int idx = numSamples - 1 - i;
            float fade = (float)i / fadeOut;
            audioBuffer[idx] *= fade * fade;
        }

        AudioClip clip = AudioClip.Create(name, numSamples, 1, sampleRate, false);
        float[] clipData = new float[numSamples];
        System.Array.Copy(audioBuffer, clipData, numSamples);
        clip.SetData(clipData, 0);
        return clip;
    }
    
    /// <summary>
    /// Magnet boost - Magnetic pull whoosh with swirling resonance.
    /// Evokes attraction and gathering.
    /// </summary>
    private static AudioClip GenerateMagnetSound()
    {
        float duration = 0.45f;
        int numSamples = Mathf.CeilToInt(duration * sampleRate);
        
        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float norm = t / duration;
            
            // Swirling magnetic pull effect - frequency rises then falls
            float freqCurve = Mathf.Sin(norm * Mathf.PI);  // Peaks at middle
            float baseFreq = Mathf.Lerp(150f, 400f, freqCurve);
            float phase = t * baseFreq * Mathf.PI * 2f;
            
            // Magnetic hum with modulation
            float hum = Mathf.Sin(phase);
            float modulation = 1f + 0.3f * Mathf.Sin(t * 25f * Mathf.PI * 2f);
            hum *= modulation;
            
            // Swirling overtones
            float swirl1 = Mathf.Sin(phase * 1.5f + t * 8f) * 0.3f;
            float swirl2 = Mathf.Sin(phase * 2.01f - t * 12f) * 0.2f;
            
            // Whoosh component - filtered noise
            float noise = Mathf.PerlinNoise(t * 50f, 0f) * 2f - 1f;
            noise *= freqCurve * 0.4f;
            
            // Envelope - quick attack, sustain, smooth decay
            float envelope;
            if (norm < 0.1f)
                envelope = norm / 0.1f;
            else if (norm < 0.7f)
                envelope = 1f;
            else
                envelope = (1f - norm) / 0.3f;
            
            audioBuffer[i] = (hum * 0.5f + swirl1 + swirl2 + noise) * envelope;
        }
        
        return FinalizeClip("MagnetBoost", numSamples, duration);
    }

    #endregion
}
