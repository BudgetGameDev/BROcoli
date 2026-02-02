using UnityEngine;

/// <summary>
/// Procedural walk/movement sound generator for enemies.
/// Generates distinct alien/monster footstep sounds different from the player.
/// Triggers based on movement velocity rather than hop state.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ProceduralEnemyWalkAudio : MonoBehaviour
{
    public enum EnemyWalkSoundType
    {
        Skitter,        // Fast, light, insectoid
        Thud,           // Heavy, slow stomping
        Slither,        // Wet, sliding movement
        Shuffle,        // Shambling, zombie-like
        Clatter         // Bony, skeletal rattling
    }

    [Header("Sound Type")]
    [SerializeField] private EnemyWalkSoundType soundType = EnemyWalkSoundType.Skitter;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.25f;

    [Header("Variation")]
    [Range(0f, 0.3f)]
    [SerializeField] private float randomization = 0.15f;

    [Header("Step Timing")]
    [SerializeField] private float baseStepInterval = 0.3f;  // Time between steps at max speed
    [SerializeField] private float minSpeedForSound = 0.5f;  // Minimum velocity to trigger sounds

    private struct WalkPreset
    {
        public float duration;
        
        // Impact
        public float impactFreq;
        public float impactAmount;
        public float impactDecay;
        
        // Body/resonance
        public float bodyFreq;
        public float bodyAmount;
        public float bodyDecay;
        
        // Secondary sound
        public float secondaryFreq;
        public float secondaryDelay;
        public float secondaryAmount;
        
        // Noise
        public float noiseAmount;
        public float noiseDecay;
        public float noiseCutoff;
        public float noiseColor;  // 0=white, 1=brown
        
        // Character
        public bool hasClick;
        public float clickFreq;
        public float clickAmount;
        
        public bool hasWet;
        public float wetAmount;
    }

    private WalkPreset currentPreset;
    private AudioSource audioSource;
    private Rigidbody2D rb;
    private int sampleRate;
    private float[] audioBuffer;

    private float[] lpState = new float[4];
    private float[] hpState = new float[4];  // High-pass filter state
    private float stepTimer;
    private bool isMoving;
    private float lastSpeed;
    
    // Distance-based volume attenuation
    private static Transform playerTransform;
    private static int activeEnemyCount = 0;
    private const float MAX_AUDIBLE_DISTANCE = 20f;
    private const float MIN_AUDIBLE_DISTANCE = 3f;

    // Static caching for prewarmed clips (multiple clips per type for variation)
    private static System.Collections.Generic.Dictionary<EnemyWalkSoundType, AudioClip[]> _clipCache;
    private static bool _isPrewarmed = false;
    private static int _staticSampleRate;
    private static float[] _staticAudioBuffer;
    private static float[] _staticLpState;
    private static float[] _staticHpState;
    private const int ClipsPerType = 3; // Pre-generate multiple clips for variation

    /// <summary>
    /// Pre-generates and caches audio clips for all walk sound types.
    /// Call this during game initialization to eliminate first-use hitches.
    /// </summary>
    public static void PrewarmAll()
    {
        if (_isPrewarmed) return;

        _staticSampleRate = AudioSettings.outputSampleRate;
        int maxSamples = Mathf.CeilToInt(0.4f * _staticSampleRate);
        _staticAudioBuffer = new float[maxSamples];
        _staticLpState = new float[4];
        _staticHpState = new float[4];

        _clipCache = new System.Collections.Generic.Dictionary<EnemyWalkSoundType, AudioClip[]>();
        
        // Generate multiple clips per type for variation
        foreach (EnemyWalkSoundType type in System.Enum.GetValues(typeof(EnemyWalkSoundType)))
        {
            AudioClip[] clips = new AudioClip[ClipsPerType];
            for (int i = 0; i < ClipsPerType; i++)
            {
                clips[i] = GenerateStepClipStatic(type, i);
            }
            _clipCache[type] = clips;
        }

        _isPrewarmed = true;
        Debug.Log("[ProceduralEnemyWalkAudio] Pre-warmed all walk sound types");
    }

    private static WalkPreset GetPresetStatic(EnemyWalkSoundType type)
    {
        WalkPreset p = new WalkPreset();
        switch (type)
        {
            case EnemyWalkSoundType.Skitter:
                p.duration = 0.06f;
                p.impactFreq = 280f; p.impactAmount = 0.3f; p.impactDecay = 25f;
                p.bodyFreq = 180f; p.bodyAmount = 0.2f; p.bodyDecay = 20f;
                p.secondaryFreq = 420f; p.secondaryDelay = 0.015f; p.secondaryAmount = 0.25f;
                p.noiseAmount = 0.5f; p.noiseDecay = 30f; p.noiseCutoff = 4000f; p.noiseColor = 0.2f;
                p.hasClick = true; p.clickFreq = 3500f; p.clickAmount = 0.35f;
                p.hasWet = false; p.wetAmount = 0f;
                break;
            case EnemyWalkSoundType.Thud:
                p.duration = 0.12f;
                p.impactFreq = 90f; p.impactAmount = 0.6f; p.impactDecay = 10f;
                p.bodyFreq = 120f; p.bodyAmount = 0.45f; p.bodyDecay = 8f;
                p.secondaryFreq = 180f; p.secondaryDelay = 0.025f; p.secondaryAmount = 0.3f;
                p.noiseAmount = 0.25f; p.noiseDecay = 12f; p.noiseCutoff = 800f; p.noiseColor = 0.7f;
                p.hasClick = false; p.clickFreq = 0f; p.clickAmount = 0f;
                p.hasWet = false; p.wetAmount = 0f;
                break;
            case EnemyWalkSoundType.Slither:
                p.duration = 0.12f;
                p.impactFreq = 120f; p.impactAmount = 0.2f; p.impactDecay = 14f;
                p.bodyFreq = 100f; p.bodyAmount = 0.25f; p.bodyDecay = 10f;
                p.secondaryFreq = 200f; p.secondaryDelay = 0.02f; p.secondaryAmount = 0.15f;
                p.noiseAmount = 0.5f; p.noiseDecay = 18f; p.noiseCutoff = 1500f; p.noiseColor = 0.5f;
                p.hasClick = false; p.clickFreq = 0f; p.clickAmount = 0f;
                p.hasWet = true; p.wetAmount = 0.3f;
                break;
            case EnemyWalkSoundType.Shuffle:
                p.duration = 0.15f;
                p.impactFreq = 100f; p.impactAmount = 0.3f; p.impactDecay = 9f;
                p.bodyFreq = 130f; p.bodyAmount = 0.25f; p.bodyDecay = 8f;
                p.secondaryFreq = 160f; p.secondaryDelay = 0.05f; p.secondaryAmount = 0.2f;
                p.noiseAmount = 0.4f; p.noiseDecay = 10f; p.noiseCutoff = 1100f; p.noiseColor = 0.6f;
                p.hasClick = false; p.clickFreq = 0f; p.clickAmount = 0f;
                p.hasWet = false; p.wetAmount = 0f;
                break;
            case EnemyWalkSoundType.Clatter:
                p.duration = 0.1f;
                p.impactFreq = 220f; p.impactAmount = 0.35f; p.impactDecay = 18f;
                p.bodyFreq = 140f; p.bodyAmount = 0.25f; p.bodyDecay = 15f;
                p.secondaryFreq = 350f; p.secondaryDelay = 0.008f; p.secondaryAmount = 0.3f;
                p.noiseAmount = 0.4f; p.noiseDecay = 22f; p.noiseCutoff = 5500f; p.noiseColor = 0.1f;
                p.hasClick = true; p.clickFreq = 4800f; p.clickAmount = 0.4f;
                p.hasWet = false; p.wetAmount = 0f;
                break;
        }
        return p;
    }

    private static AudioClip GenerateStepClipStatic(EnemyWalkSoundType type, int variationIndex)
    {
        WalkPreset p = GetPresetStatic(type);
        
        // Use variation index to create deterministic but varied clips
        uint rngState = (uint)(type.GetHashCode() * 31 + variationIndex * 17 + 12345);
        
        float dur = p.duration * (1f + ((rngState % 100) / 500f - 0.1f)); // ±10% variation
        int totalSamples = Mathf.CeilToInt(dur * _staticSampleRate);
        totalSamples = Mathf.Min(totalSamples, _staticAudioBuffer.Length);

        System.Array.Clear(_staticLpState, 0, _staticLpState.Length);
        System.Array.Clear(_staticHpState, 0, _staticHpState.Length);

        float phaseImpact = 0f, phaseBody = 0f, phaseSecondary = 0f, phaseClick = 0f;
        float noiseState = 0f;

        rngState = rngState * 1103515245 + 12345;
        float freqOffset = 0.9f + (rngState % 1000) / 5000f; // 0.9 to 1.1
        
        rngState = rngState * 1103515245 + 12345;
        float secondaryDelayRnd = p.secondaryDelay * (0.85f + (rngState % 1000) / 3333f); // ±15%

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / _staticSampleRate;
            float normalizedT = Mathf.Clamp01(t / dur);
            float sample = 0f;

            // Impact
            float impactEnv = GetImpactEnvelopeStatic(t, p.impactDecay);
            float impactF = p.impactFreq * freqOffset * (1f - normalizedT * 0.3f);
            phaseImpact += impactF / _staticSampleRate;
            float impact = Mathf.Sin(phaseImpact * Mathf.PI * 2f);
            impact += Mathf.Sin(phaseImpact * Mathf.PI * 4f) * 0.35f;
            impact *= impactEnv * p.impactAmount;

            // Body
            float bodyEnv = GetBodyEnvelopeStatic(t, dur, p.bodyDecay);
            float bodyF = p.bodyFreq * freqOffset;
            phaseBody += bodyF / _staticSampleRate;
            float body = Mathf.Sin(phaseBody * Mathf.PI * 2f);
            body += Mathf.Sin(phaseBody * Mathf.PI * 3f) * 0.25f;
            body *= bodyEnv * p.bodyAmount;

            // Secondary
            float secondary = 0f;
            if (t >= secondaryDelayRnd)
            {
                float secT = t - secondaryDelayRnd;
                float secEnv = GetSecondaryEnvelopeStatic(secT, dur, p.impactDecay * 1.2f);
                float secF = p.secondaryFreq * freqOffset;
                phaseSecondary += secF / _staticSampleRate;
                secondary = Mathf.Sin(phaseSecondary * Mathf.PI * 2f);
                secondary *= secEnv * p.secondaryAmount;
            }

            // Noise
            float noiseEnv = GetNoiseEnvelopeStatic(t, dur, p.noiseDecay);
            rngState = rngState * 1103515245 + 12345;
            float whiteNoise = ((rngState >> 16) & 0x7FFF) / 16383.5f - 1f;
            noiseState = noiseState * (0.9f + p.noiseColor * 0.09f) + whiteNoise * (0.1f - p.noiseColor * 0.09f);
            float coloredNoise = noiseState * p.noiseColor + whiteNoise * (1f - p.noiseColor);
            float noise = LowpassFilterStatic(coloredNoise, p.noiseCutoff * (1f - normalizedT * 0.4f), 0);
            noise *= noiseEnv * p.noiseAmount;

            // Click
            float click = 0f;
            if (p.hasClick && t < 0.015f)
            {
                float clickEnv = Mathf.Exp(-t * 200f);
                phaseClick += p.clickFreq * freqOffset / _staticSampleRate;
                click = Mathf.Sin(phaseClick * Mathf.PI * 2f);
                click *= clickEnv * p.clickAmount;
            }

            // Wet squelch
            float wet = 0f;
            if (p.hasWet)
            {
                float wetEnv = GetWetEnvelopeStatic(t, dur);
                rngState = rngState * 1103515245 + 12345;
                float wetNoise = ((rngState >> 16) & 0x7FFF) / 16383.5f - 1f;
                wet = LowpassFilterStatic(wetNoise, 400f + 800f * (1f - normalizedT), 1) * wetEnv * p.wetAmount;
            }

            sample = impact + body + secondary + noise + click + wet;
            sample = HighpassFilterStatic(sample, 80f, 0);
            sample = SoftClipStatic(sample);
            _staticAudioBuffer[i] = sample;
        }

        // Fade out
        int fadeOutSamples = Mathf.Min(totalSamples / 5, _staticSampleRate / 25);
        for (int i = 0; i < fadeOutSamples; i++)
        {
            int idx = totalSamples - 1 - i;
            float fade = (float)i / fadeOutSamples;
            _staticAudioBuffer[idx] *= fade * fade;
        }

        // Normalize
        float maxAmp = 0f;
        for (int i = 0; i < totalSamples; i++)
            maxAmp = Mathf.Max(maxAmp, Mathf.Abs(_staticAudioBuffer[i]));
        if (maxAmp > 0.01f)
        {
            float normalize = 0.65f / maxAmp;
            for (int i = 0; i < totalSamples; i++)
                _staticAudioBuffer[i] *= normalize;
        }

        AudioClip clip = AudioClip.Create($"EnemyStep_{type}_{variationIndex}", totalSamples, 1, _staticSampleRate, false);
        float[] clipData = new float[totalSamples];
        System.Array.Copy(_staticAudioBuffer, clipData, totalSamples);
        clip.SetData(clipData, 0);
        return clip;
    }

    private static float GetImpactEnvelopeStatic(float t, float decayRate)
    {
        float attack = 0.002f;
        if (t < attack) return t / attack;
        return Mathf.Exp(-(t - attack) * decayRate);
    }

    private static float GetBodyEnvelopeStatic(float t, float duration, float decayRate)
    {
        float attack = 0.004f;
        float sustain = duration * 0.1f;
        if (t < attack) return t / attack;
        if (t < attack + sustain) return 1f;
        float dt = (t - attack - sustain) / (duration * 0.8f);
        return Mathf.Exp(-dt * decayRate);
    }

    private static float GetSecondaryEnvelopeStatic(float t, float duration, float decayRate)
    {
        float attack = 0.003f;
        if (t < attack) return t / attack;
        return Mathf.Exp(-(t - attack) * decayRate);
    }

    private static float GetNoiseEnvelopeStatic(float t, float duration, float decayRate)
    {
        float attack = 0.001f;
        if (t < attack) return t / attack;
        return Mathf.Exp(-(t - attack) * decayRate);
    }

    private static float GetWetEnvelopeStatic(float t, float duration)
    {
        float delay = 0.02f;
        if (t < delay) return 0f;
        float wetT = t - delay;
        float attack = 0.008f;
        if (wetT < attack) return wetT / attack;
        return Mathf.Exp(-(wetT - attack) * 12f);
    }

    private static float LowpassFilterStatic(float input, float cutoff, int stateIndex)
    {
        float rc = 1f / (2f * Mathf.PI * cutoff);
        float dt = 1f / _staticSampleRate;
        float alpha = Mathf.Clamp01(dt / (rc + dt));
        _staticLpState[stateIndex] += alpha * (input - _staticLpState[stateIndex]);
        return _staticLpState[stateIndex];
    }

    private static float HighpassFilterStatic(float input, float cutoff, int stateIndex)
    {
        float rc = 1f / (2f * Mathf.PI * cutoff);
        float dt = 1f / _staticSampleRate;
        float alpha = rc / (rc + dt);
        float output = alpha * (_staticHpState[stateIndex + 2] + input - _staticHpState[stateIndex]);
        _staticHpState[stateIndex] = input;
        _staticHpState[stateIndex + 2] = output;
        return output;
    }

    private static float SoftClipStatic(float x)
    {
        if (x > 1f) return 1f;
        if (x < -1f) return -1f;
        return x - (x * x * x) / 3f;
    }

    // Track which clip variation to use next for randomization
    private int _clipVariationIndex = 0;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        rb = GetComponent<Rigidbody2D>();

        sampleRate = AudioSettings.outputSampleRate;
        int maxSamples = Mathf.CeilToInt(0.4f * sampleRate);
        audioBuffer = new float[maxSamples];

        stepTimer = Random.Range(0f, baseStepInterval); // Randomize initial offset
        
        activeEnemyCount++;
        
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }
    }
    
    void OnDestroy()
    {
        activeEnemyCount = Mathf.Max(0, activeEnemyCount - 1);
    }

    private WalkPreset GetPreset(EnemyWalkSoundType type)
    {
        WalkPreset p = new WalkPreset();

        switch (type)
        {
            case EnemyWalkSoundType.Skitter:
                // Fast, light, insectoid - high frequency clicks
                p.duration = 0.06f;
                p.impactFreq = 280f;
                p.impactAmount = 0.3f;
                p.impactDecay = 25f;
                p.bodyFreq = 180f;
                p.bodyAmount = 0.2f;
                p.bodyDecay = 20f;
                p.secondaryFreq = 420f;
                p.secondaryDelay = 0.015f;
                p.secondaryAmount = 0.25f;
                p.noiseAmount = 0.5f;
                p.noiseDecay = 30f;
                p.noiseCutoff = 4000f;
                p.noiseColor = 0.2f;
                p.hasClick = true;
                p.clickFreq = 3500f;
                p.clickAmount = 0.35f;
                p.hasWet = false;
                p.wetAmount = 0f;
                break;

            case EnemyWalkSoundType.Thud:
                // Heavy, slow stomping - medium-low impact (raised from very low to avoid rumble)
                p.duration = 0.12f;
                p.impactFreq = 90f;      // Raised from 45Hz to avoid sub-bass rumble
                p.impactAmount = 0.6f;   // Reduced from 0.8
                p.impactDecay = 10f;
                p.bodyFreq = 120f;       // Raised from 70Hz
                p.bodyAmount = 0.45f;    // Reduced from 0.6
                p.bodyDecay = 8f;
                p.secondaryFreq = 180f;  // Raised from 120Hz
                p.secondaryDelay = 0.025f;
                p.secondaryAmount = 0.3f;
                p.noiseAmount = 0.25f;   // Reduced from 0.35
                p.noiseDecay = 12f;
                p.noiseCutoff = 800f;    // Raised from 600Hz
                p.noiseColor = 0.7f;     // Reduced from 0.9 (less brown noise)
                p.hasClick = false;
                p.clickFreq = 0f;
                p.clickAmount = 0f;
                p.hasWet = false;
                p.wetAmount = 0f;
                break;

            case EnemyWalkSoundType.Slither:
                // Wet, sliding movement - noise-heavy with squelch
                p.duration = 0.12f;
                p.impactFreq = 120f;     // Raised from 90Hz
                p.impactAmount = 0.2f;   // Reduced from 0.25
                p.impactDecay = 14f;
                p.bodyFreq = 100f;       // Raised from 60Hz
                p.bodyAmount = 0.25f;    // Reduced from 0.3
                p.bodyDecay = 10f;
                p.secondaryFreq = 200f;  // Raised from 150Hz
                p.secondaryDelay = 0.02f;
                p.secondaryAmount = 0.15f;
                p.noiseAmount = 0.5f;    // Reduced from 0.7
                p.noiseDecay = 18f;
                p.noiseCutoff = 1500f;   // Raised from 1200Hz
                p.noiseColor = 0.5f;     // Reduced from 0.7
                p.hasClick = false;
                p.clickFreq = 0f;
                p.clickAmount = 0f;
                p.hasWet = true;
                p.wetAmount = 0.3f;      // Reduced from 0.4
                break;

            case EnemyWalkSoundType.Shuffle:
                // Shambling, zombie-like - dragging sound
                p.duration = 0.15f;
                p.impactFreq = 100f;     // Raised from 65Hz
                p.impactAmount = 0.3f;   // Reduced from 0.4
                p.impactDecay = 9f;
                p.bodyFreq = 130f;       // Raised from 85Hz
                p.bodyAmount = 0.25f;    // Reduced from 0.35
                p.bodyDecay = 8f;
                p.secondaryFreq = 160f;  // Raised from 110Hz
                p.secondaryDelay = 0.05f;
                p.secondaryAmount = 0.2f;
                p.noiseAmount = 0.4f;    // Reduced from 0.55
                p.noiseDecay = 10f;
                p.noiseCutoff = 1100f;   // Raised from 900Hz
                p.noiseColor = 0.6f;     // Reduced from 0.8
                p.hasClick = false;
                p.clickFreq = 0f;
                p.clickAmount = 0f;
                p.hasWet = false;
                p.wetAmount = 0f;
                break;

            case EnemyWalkSoundType.Clatter:
                // Bony, skeletal rattling - multiple high clicks
                p.duration = 0.1f;
                p.impactFreq = 220f;
                p.impactAmount = 0.35f;
                p.impactDecay = 18f;
                p.bodyFreq = 140f;
                p.bodyAmount = 0.25f;
                p.bodyDecay = 15f;
                p.secondaryFreq = 350f;
                p.secondaryDelay = 0.008f;
                p.secondaryAmount = 0.3f;
                p.noiseAmount = 0.4f;
                p.noiseDecay = 22f;
                p.noiseCutoff = 5500f;
                p.noiseColor = 0.1f;
                p.hasClick = true;
                p.clickFreq = 4800f;
                p.clickAmount = 0.4f;
                p.hasWet = false;
                p.wetAmount = 0f;
                break;
        }

        return p;
    }

    void Update()
    {
        if (rb == null) return;

        float speed = rb.linearVelocity.magnitude;
        lastSpeed = speed;

        if (speed < minSpeedForSound)
        {
            isMoving = false;
            return;
        }

        isMoving = true;

        // Scale step interval by speed (faster = more frequent steps)
        float speedFactor = Mathf.Clamp(speed / 5f, 0.5f, 2f);
        float currentInterval = baseStepInterval / speedFactor;

        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            PlayStep();
            // Add slight randomization to timing
            stepTimer = currentInterval * Random.Range(0.85f, 1.15f);
        }
    }

    public void PlayStep()
    {
        // Distance-based attenuation
        float distanceVolume = 1f;
        if (playerTransform != null)
        {
            float dist = Vector2.Distance(transform.position, playerTransform.position);
            if (dist > MAX_AUDIBLE_DISTANCE)
            {
                return; // Too far, don't play
            }
            distanceVolume = Mathf.InverseLerp(MAX_AUDIBLE_DISTANCE, MIN_AUDIBLE_DISTANCE, dist);
            distanceVolume = Mathf.Sqrt(distanceVolume); // Smoother falloff
        }
        
        // Reduce volume when many enemies are active
        float crowdAttenuation = 1f / (1f + activeEnemyCount * 0.1f);
        
        // Use cached clip if available, otherwise generate dynamically
        AudioClip clip;
        if (_isPrewarmed && _clipCache != null && _clipCache.TryGetValue(soundType, out var clips))
        {
            clip = clips[_clipVariationIndex % clips.Length];
            _clipVariationIndex++;
        }
        else
        {
            currentPreset = GetPreset(soundType);
            clip = GenerateStepClip();
        }
        
        // Scale volume slightly by speed
        float speedVolume = Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(lastSpeed / 5f));
        float finalVolume = volume * speedVolume * distanceVolume * crowdAttenuation;
        
        audioSource.PlayOneShot(clip, finalVolume);
    }

    private AudioClip GenerateStepClip()
    {
        WalkPreset p = currentPreset;
        float rnd = randomization;

        float dur = p.duration * (1f + Random.Range(-rnd * 0.3f, rnd * 0.3f));
        int totalSamples = Mathf.CeilToInt(dur * sampleRate);
        totalSamples = Mathf.Min(totalSamples, audioBuffer.Length);

        System.Array.Clear(lpState, 0, lpState.Length);
        System.Array.Clear(hpState, 0, hpState.Length);

        float phaseImpact = 0f;
        float phaseBody = 0f;
        float phaseSecondary = 0f;
        float phaseClick = 0f;
        float noiseState = 0f;

        float freqOffset = Random.Range(0.9f, 1.1f);
        float secondaryDelayRnd = p.secondaryDelay * (1f + Random.Range(-rnd, rnd));

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float normalizedT = Mathf.Clamp01(t / dur);

            float sample = 0f;

            // ===== IMPACT =====
            float impactEnv = GetImpactEnvelope(t, p.impactDecay);
            float impactF = p.impactFreq * freqOffset * (1f - normalizedT * 0.3f);
            phaseImpact += impactF / sampleRate;
            float impact = Mathf.Sin(phaseImpact * Mathf.PI * 2f);
            impact += Mathf.Sin(phaseImpact * Mathf.PI * 4f) * 0.35f;
            impact *= impactEnv * p.impactAmount;

            // ===== BODY =====
            float bodyEnv = GetBodyEnvelope(t, dur, p.bodyDecay);
            float bodyF = p.bodyFreq * freqOffset;
            phaseBody += bodyF / sampleRate;
            float body = Mathf.Sin(phaseBody * Mathf.PI * 2f);
            body += Mathf.Sin(phaseBody * Mathf.PI * 3f) * 0.25f;
            body *= bodyEnv * p.bodyAmount;

            // ===== SECONDARY =====
            float secondary = 0f;
            if (t >= secondaryDelayRnd)
            {
                float secT = t - secondaryDelayRnd;
                float secEnv = GetSecondaryEnvelope(secT, dur, p.impactDecay * 1.2f);
                float secF = p.secondaryFreq * freqOffset;
                phaseSecondary += secF / sampleRate;
                secondary = Mathf.Sin(phaseSecondary * Mathf.PI * 2f);
                secondary *= secEnv * p.secondaryAmount;
            }

            // ===== NOISE =====
            float noiseEnv = GetNoiseEnvelope(t, dur, p.noiseDecay);
            float whiteNoise = Random.Range(-1f, 1f);
            noiseState = noiseState * (0.9f + p.noiseColor * 0.09f) + whiteNoise * (0.1f - p.noiseColor * 0.09f);
            float coloredNoise = noiseState * p.noiseColor + whiteNoise * (1f - p.noiseColor);
            float noise = LowpassFilter(coloredNoise, p.noiseCutoff * (1f - normalizedT * 0.4f), 0);
            noise *= noiseEnv * p.noiseAmount;

            // ===== CLICK (optional) =====
            float click = 0f;
            if (p.hasClick && t < 0.015f)
            {
                float clickEnv = Mathf.Exp(-t * 200f);
                phaseClick += p.clickFreq * freqOffset / sampleRate;
                click = Mathf.Sin(phaseClick * Mathf.PI * 2f);
                click *= clickEnv * p.clickAmount;
            }

            // ===== WET SQUELCH (optional) =====
            float wet = 0f;
            if (p.hasWet)
            {
                float wetEnv = GetWetEnvelope(t, dur);
                float wetNoise = LowpassFilter(Random.Range(-1f, 1f), 400f + 800f * (1f - normalizedT), 1);
                wet = wetNoise * wetEnv * p.wetAmount;
            }

            // ===== COMBINE =====
            sample = impact + body + secondary + noise + click + wet;

            // High-pass filter to remove low frequency rumble (cutoff ~80Hz)
            sample = HighpassFilter(sample, 80f, 0);

            // Soft clip
            sample = SoftClip(sample);

            audioBuffer[i] = sample;
        }

        // Fade out
        int fadeOutSamples = Mathf.Min(totalSamples / 5, sampleRate / 25);
        for (int i = 0; i < fadeOutSamples; i++)
        {
            int idx = totalSamples - 1 - i;
            float fade = (float)i / fadeOutSamples;
            fade = fade * fade;
            audioBuffer[idx] *= fade;
        }

        // Normalize with headroom to prevent clipping
        float maxAmp = 0f;
        for (int i = 0; i < totalSamples; i++)
            maxAmp = Mathf.Max(maxAmp, Mathf.Abs(audioBuffer[i]));

        if (maxAmp > 0.01f)
        {
            float normalize = 0.65f / maxAmp;  // Leave more headroom
            for (int i = 0; i < totalSamples; i++)
                audioBuffer[i] *= normalize;
        }

        AudioClip clip = AudioClip.Create("EnemyStep", totalSamples, 1, sampleRate, false);
        float[] clipData = new float[totalSamples];
        System.Array.Copy(audioBuffer, clipData, totalSamples);
        clip.SetData(clipData, 0);

        return clip;
    }

    // =============== ENVELOPES ===============

    private float GetImpactEnvelope(float t, float decayRate)
    {
        float attack = 0.002f;

        if (t < attack)
            return t / attack;
        else
            return Mathf.Exp(-(t - attack) * decayRate);
    }

    private float GetBodyEnvelope(float t, float duration, float decayRate)
    {
        float attack = 0.004f;
        float sustain = duration * 0.1f;

        if (t < attack)
            return t / attack;
        else if (t < attack + sustain)
            return 1f;
        else
        {
            float dt = (t - attack - sustain) / (duration * 0.8f);
            return Mathf.Exp(-dt * decayRate);
        }
    }

    private float GetSecondaryEnvelope(float t, float duration, float decayRate)
    {
        float attack = 0.003f;

        if (t < attack)
            return t / attack;
        else
            return Mathf.Exp(-(t - attack) * decayRate);
    }

    private float GetNoiseEnvelope(float t, float duration, float decayRate)
    {
        float attack = 0.001f;

        if (t < attack)
            return t / attack;
        else
            return Mathf.Exp(-(t - attack) * decayRate);
    }

    private float GetWetEnvelope(float t, float duration)
    {
        // Delayed wet squelch
        float delay = 0.02f;
        if (t < delay) return 0f;
        
        float wetT = t - delay;
        float attack = 0.008f;
        
        if (wetT < attack)
            return wetT / attack;
        else
            return Mathf.Exp(-(wetT - attack) * 12f);
    }

    // =============== FILTERS ===============

    private float LowpassFilter(float input, float cutoff, int stateIndex)
    {
        float rc = 1f / (2f * Mathf.PI * cutoff);
        float dt = 1f / sampleRate;
        float alpha = dt / (rc + dt);
        alpha = Mathf.Clamp01(alpha);

        lpState[stateIndex] += alpha * (input - lpState[stateIndex]);
        return lpState[stateIndex];
    }

    private float HighpassFilter(float input, float cutoff, int stateIndex)
    {
        float rc = 1f / (2f * Mathf.PI * cutoff);
        float dt = 1f / sampleRate;
        float alpha = rc / (rc + dt);
        alpha = Mathf.Clamp01(alpha);

        float output = alpha * (hpState[stateIndex + 2] + input - hpState[stateIndex]);
        hpState[stateIndex] = input;
        hpState[stateIndex + 2] = output;
        return output;
    }

    private float SoftClip(float x)
    {
        if (x > 1f) return 1f;
        if (x < -1f) return -1f;
        return x - (x * x * x) / 3f;
    }
}
