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
    [SerializeField] private float volume = 0.5f;

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
    private float stepTimer;
    private bool isMoving;
    private float lastSpeed;
    
    // Distance-based volume attenuation
    private static Transform playerTransform;
    private static int activeEnemyCount = 0;
    private const float MAX_AUDIBLE_DISTANCE = 20f;
    private const float MIN_AUDIBLE_DISTANCE = 3f;

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
                // Heavy, slow stomping - deep bass impact
                p.duration = 0.15f;
                p.impactFreq = 45f;
                p.impactAmount = 0.8f;
                p.impactDecay = 8f;
                p.bodyFreq = 70f;
                p.bodyAmount = 0.6f;
                p.bodyDecay = 6f;
                p.secondaryFreq = 120f;
                p.secondaryDelay = 0.03f;
                p.secondaryAmount = 0.4f;
                p.noiseAmount = 0.35f;
                p.noiseDecay = 10f;
                p.noiseCutoff = 600f;
                p.noiseColor = 0.9f;
                p.hasClick = false;
                p.clickFreq = 0f;
                p.clickAmount = 0f;
                p.hasWet = false;
                p.wetAmount = 0f;
                break;

            case EnemyWalkSoundType.Slither:
                // Wet, sliding movement - noise-heavy with squelch
                p.duration = 0.12f;
                p.impactFreq = 90f;
                p.impactAmount = 0.25f;
                p.impactDecay = 12f;
                p.bodyFreq = 60f;
                p.bodyAmount = 0.3f;
                p.bodyDecay = 8f;
                p.secondaryFreq = 150f;
                p.secondaryDelay = 0.02f;
                p.secondaryAmount = 0.2f;
                p.noiseAmount = 0.7f;
                p.noiseDecay = 15f;
                p.noiseCutoff = 1200f;
                p.noiseColor = 0.7f;
                p.hasClick = false;
                p.clickFreq = 0f;
                p.clickAmount = 0f;
                p.hasWet = true;
                p.wetAmount = 0.4f;
                break;

            case EnemyWalkSoundType.Shuffle:
                // Shambling, zombie-like - dragging sound
                p.duration = 0.18f;
                p.impactFreq = 65f;
                p.impactAmount = 0.4f;
                p.impactDecay = 7f;
                p.bodyFreq = 85f;
                p.bodyAmount = 0.35f;
                p.bodyDecay = 6f;
                p.secondaryFreq = 110f;
                p.secondaryDelay = 0.06f;
                p.secondaryAmount = 0.3f;
                p.noiseAmount = 0.55f;
                p.noiseDecay = 8f;
                p.noiseCutoff = 900f;
                p.noiseColor = 0.8f;
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
        
        currentPreset = GetPreset(soundType);
        AudioClip clip = GenerateStepClip();
        
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

        // Normalize
        float maxAmp = 0f;
        for (int i = 0; i < totalSamples; i++)
            maxAmp = Mathf.Max(maxAmp, Mathf.Abs(audioBuffer[i]));

        if (maxAmp > 0.01f)
        {
            float normalize = 0.8f / maxAmp;
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

    private float SoftClip(float x)
    {
        if (x > 1f) return 1f;
        if (x < -1f) return -1f;
        return x - (x * x * x) / 3f;
    }
}
