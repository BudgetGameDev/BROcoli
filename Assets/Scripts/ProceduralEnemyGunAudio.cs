using UnityEngine;

/// <summary>
/// Procedural gun audio for enemies - distinct from player weapon sounds.
/// Features alien/organic/corrupted weapon sounds with different tonal characteristics.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ProceduralEnemyGunAudio : MonoBehaviour
{
    public enum EnemyGunSoundType
    {
        PlasmaSpitter,      // Organic, wet, splattery
        VoidCannon,         // Deep, resonant, ominous
        SwarmShot,          // Buzzing, insectoid
        CorruptedBlaster,   // Distorted, glitchy
        AcidLauncher        // Hissing, corrosive
    }

    [Header("Sound Type")]
    [SerializeField] private EnemyGunSoundType soundType = EnemyGunSoundType.PlasmaSpitter;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.8f;

    [Header("Variation")]
    [Range(0f, 0.25f)]
    [SerializeField] private float randomization = 0.15f;

    private struct EnemyGunPreset
    {
        public float duration;
        public float roomSize;
        
        // Transient
        public float transientFreq1;
        public float transientFreq2;
        public float transientDecay;
        public float transientAmount;
        
        // Body
        public float subFreq;
        public float subAmount;
        public float midFreq;
        public float midAmount;
        public float bodyDecay;
        
        // Character layers
        public float modFreq;
        public float modDepth;
        public float resonanceFreq;
        public float resonanceQ;
        public float resonanceAmount;
        
        // Noise
        public float noiseColor;  // 0 = white, 1 = pink/brown
        public float noiseCutoff;
        public float noiseAmount;
        public float noiseDecay;
        
        // Special effects
        public float distortion;
        public float pitchBend;
        public bool hasChorus;
        public bool hasGlitch;
    }

    private EnemyGunPreset currentPreset;
    private AudioSource audioSource;
    private int sampleRate;
    private float[] audioBuffer;

    // Filter states
    private float[] lpState = new float[4];
    private float[] hpState = new float[2];
    private float[] bpState = new float[4];

    // Reverb
    private float[][] allpassBuffers;
    private int[] allpassIndices;
    private float[][] combBuffers;
    private int[] combIndices;
    
    // Distance-based volume attenuation
    private static Transform playerTransform;
    private const float MAX_AUDIBLE_DISTANCE = 30f;
    private const float MIN_AUDIBLE_DISTANCE = 3f;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        sampleRate = AudioSettings.outputSampleRate;
        int maxSamples = Mathf.CeilToInt(1.2f * sampleRate);
        audioBuffer = new float[maxSamples];

        InitializeReverb();
        
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }
    }

    private void InitializeReverb()
    {
        int[] allpassDelays = { 281, 89, 31, 47 };
        int[] combDelays = { 1423, 1361, 1847, 1993, 1531, 1721 };

        allpassBuffers = new float[allpassDelays.Length][];
        allpassIndices = new int[allpassDelays.Length];
        for (int i = 0; i < allpassDelays.Length; i++)
        {
            allpassBuffers[i] = new float[allpassDelays[i]];
            allpassIndices[i] = 0;
        }

        combBuffers = new float[combDelays.Length][];
        combIndices = new int[combDelays.Length];
        for (int i = 0; i < combDelays.Length; i++)
        {
            combBuffers[i] = new float[combDelays[i]];
            combIndices[i] = 0;
        }
    }

    private void ClearReverb()
    {
        for (int i = 0; i < allpassBuffers.Length; i++)
            System.Array.Clear(allpassBuffers[i], 0, allpassBuffers[i].Length);
        for (int i = 0; i < combBuffers.Length; i++)
            System.Array.Clear(combBuffers[i], 0, combBuffers[i].Length);
    }

    private EnemyGunPreset GetPreset(EnemyGunSoundType type)
    {
        EnemyGunPreset p = new EnemyGunPreset();

        switch (type)
        {
            case EnemyGunSoundType.PlasmaSpitter:
                // Organic, wet, splattery - low frequency, gurgly
                p.duration = 0.25f;
                p.roomSize = 0.15f;
                p.transientFreq1 = 800f;
                p.transientFreq2 = 1200f;
                p.transientDecay = 8f;
                p.transientAmount = 0.3f;
                p.subFreq = 65f;
                p.subAmount = 0.5f;
                p.midFreq = 220f;
                p.midAmount = 0.6f;
                p.bodyDecay = 6f;
                p.modFreq = 12f;
                p.modDepth = 0.4f;
                p.resonanceFreq = 350f;
                p.resonanceQ = 4f;
                p.resonanceAmount = 0.5f;
                p.noiseColor = 0.8f;
                p.noiseCutoff = 600f;
                p.noiseAmount = 0.45f;
                p.noiseDecay = 7f;
                p.distortion = 0.3f;
                p.pitchBend = -0.3f;
                p.hasChorus = true;
                p.hasGlitch = false;
                break;

            case EnemyGunSoundType.VoidCannon:
                // Deep, resonant, ominous - sub-bass heavy
                p.duration = 0.35f;
                p.roomSize = 0.4f;
                p.transientFreq1 = 600f;
                p.transientFreq2 = 900f;
                p.transientDecay = 5f;
                p.transientAmount = 0.4f;
                p.subFreq = 30f;
                p.subAmount = 0.9f;
                p.midFreq = 80f;
                p.midAmount = 0.7f;
                p.bodyDecay = 4f;
                p.modFreq = 3f;
                p.modDepth = 0.2f;
                p.resonanceFreq = 120f;
                p.resonanceQ = 6f;
                p.resonanceAmount = 0.6f;
                p.noiseColor = 0.9f;
                p.noiseCutoff = 300f;
                p.noiseAmount = 0.3f;
                p.noiseDecay = 5f;
                p.distortion = 0.5f;
                p.pitchBend = -0.5f;
                p.hasChorus = false;
                p.hasGlitch = false;
                break;

            case EnemyGunSoundType.SwarmShot:
                // Buzzing, insectoid - high frequency modulation
                p.duration = 0.2f;
                p.roomSize = 0.1f;
                p.transientFreq1 = 2200f;
                p.transientFreq2 = 3500f;
                p.transientDecay = 12f;
                p.transientAmount = 0.35f;
                p.subFreq = 90f;
                p.subAmount = 0.25f;
                p.midFreq = 380f;
                p.midAmount = 0.4f;
                p.bodyDecay = 10f;
                p.modFreq = 85f;
                p.modDepth = 0.6f;
                p.resonanceFreq = 550f;
                p.resonanceQ = 8f;
                p.resonanceAmount = 0.55f;
                p.noiseColor = 0.3f;
                p.noiseCutoff = 2500f;
                p.noiseAmount = 0.35f;
                p.noiseDecay = 9f;
                p.distortion = 0.2f;
                p.pitchBend = 0.2f;
                p.hasChorus = true;
                p.hasGlitch = false;
                break;

            case EnemyGunSoundType.CorruptedBlaster:
                // Distorted, glitchy - digital artifacts
                p.duration = 0.22f;
                p.roomSize = 0.2f;
                p.transientFreq1 = 1500f;
                p.transientFreq2 = 2400f;
                p.transientDecay = 10f;
                p.transientAmount = 0.5f;
                p.subFreq = 55f;
                p.subAmount = 0.4f;
                p.midFreq = 280f;
                p.midAmount = 0.5f;
                p.bodyDecay = 8f;
                p.modFreq = 45f;
                p.modDepth = 0.35f;
                p.resonanceFreq = 420f;
                p.resonanceQ = 5f;
                p.resonanceAmount = 0.45f;
                p.noiseColor = 0.5f;
                p.noiseCutoff = 1800f;
                p.noiseAmount = 0.4f;
                p.noiseDecay = 8f;
                p.distortion = 0.7f;
                p.pitchBend = 0f;
                p.hasChorus = false;
                p.hasGlitch = true;
                break;

            case EnemyGunSoundType.AcidLauncher:
                // Hissing, corrosive - white noise heavy
                p.duration = 0.28f;
                p.roomSize = 0.25f;
                p.transientFreq1 = 1100f;
                p.transientFreq2 = 1800f;
                p.transientDecay = 7f;
                p.transientAmount = 0.35f;
                p.subFreq = 50f;
                p.subAmount = 0.35f;
                p.midFreq = 180f;
                p.midAmount = 0.45f;
                p.bodyDecay = 6f;
                p.modFreq = 8f;
                p.modDepth = 0.25f;
                p.resonanceFreq = 280f;
                p.resonanceQ = 3f;
                p.resonanceAmount = 0.4f;
                p.noiseColor = 0.2f;
                p.noiseCutoff = 3500f;
                p.noiseAmount = 0.6f;
                p.noiseDecay = 6f;
                p.distortion = 0.25f;
                p.pitchBend = -0.15f;
                p.hasChorus = true;
                p.hasGlitch = false;
                break;
        }

        return p;
    }

    private float GetDistanceAttenuation()
    {
        if (playerTransform == null) return 1f;
        
        float dist = Vector2.Distance(transform.position, playerTransform.position);
        if (dist > MAX_AUDIBLE_DISTANCE) return 0f;
        
        float attenuation = Mathf.InverseLerp(MAX_AUDIBLE_DISTANCE, MIN_AUDIBLE_DISTANCE, dist);
        return Mathf.Sqrt(attenuation);
    }

    public void PlayGunSound()
    {
        float distAtten = GetDistanceAttenuation();
        if (distAtten < 0.01f) return;
        
        currentPreset = GetPreset(soundType);
        AudioClip clip = GenerateGunClip();
        audioSource.PlayOneShot(clip, volume * distAtten);
    }

    public void PlayGunSound(float volumeMultiplier)
    {
        float distAtten = GetDistanceAttenuation();
        if (distAtten < 0.01f) return;
        
        currentPreset = GetPreset(soundType);
        AudioClip clip = GenerateGunClip();
        audioSource.PlayOneShot(clip, volume * volumeMultiplier * distAtten);
    }

    private AudioClip GenerateGunClip()
    {
        EnemyGunPreset p = currentPreset;
        float rnd = randomization;

        float dur = p.duration * (1f + Random.Range(-rnd * 0.3f, rnd * 0.3f));
        float roomR = p.roomSize * (1f + Random.Range(-rnd, rnd));

        int numSamples = Mathf.CeilToInt(dur * sampleRate);
        int totalSamples = Mathf.CeilToInt((dur + roomR * 0.4f) * sampleRate);
        totalSamples = Mathf.Min(totalSamples, audioBuffer.Length);
        numSamples = Mathf.Min(numSamples, totalSamples);

        System.Array.Clear(lpState, 0, lpState.Length);
        System.Array.Clear(hpState, 0, hpState.Length);
        System.Array.Clear(bpState, 0, bpState.Length);
        ClearReverb();

        float phase1 = 0f, phase2 = 0f;
        float phaseSub = 0f, phaseMid = 0f;
        float phaseMod = 0f, phaseRes = 0f;
        float noiseState = 0f;

        float freqOffset1 = Random.Range(0.9f, 1.1f);
        float freqOffset2 = Random.Range(0.88f, 1.12f);

        // Glitch timing
        float glitchTime1 = Random.Range(0.02f, 0.06f);
        float glitchTime2 = Random.Range(0.08f, 0.14f);

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float normalizedT = Mathf.Clamp01(t / dur);

            float sample = 0f;

            if (i < numSamples)
            {
                // Pitch modulation
                float pitchMod = 1f + p.pitchBend * normalizedT;
                
                // LFO for chorus/wobble
                phaseMod += p.modFreq / sampleRate;
                float lfo = Mathf.Sin(phaseMod * Mathf.PI * 2f);

                // Glitch interrupts
                float glitchMod = 1f;
                if (p.hasGlitch)
                {
                    if ((t > glitchTime1 && t < glitchTime1 + 0.008f) ||
                        (t > glitchTime2 && t < glitchTime2 + 0.012f))
                    {
                        glitchMod = Random.Range(0.1f, 0.4f);
                        pitchMod *= Random.Range(0.7f, 1.4f);
                    }
                }

                // ===== TRANSIENT =====
                float transientEnv = GetTransientEnvelope(t, p.transientDecay);
                
                float tf1 = p.transientFreq1 * freqOffset1 * pitchMod;
                float tf2 = p.transientFreq2 * freqOffset2 * pitchMod;
                
                if (p.hasChorus)
                {
                    tf1 *= 1f + lfo * p.modDepth * 0.1f;
                    tf2 *= 1f - lfo * p.modDepth * 0.08f;
                }
                
                phase1 += tf1 / sampleRate;
                phase2 += tf2 / sampleRate;
                
                float trans1 = Mathf.Sin(phase1 * Mathf.PI * 2f);
                float trans2 = Mathf.Sin(phase2 * Mathf.PI * 2f);
                float transient = (trans1 * 0.6f + trans2 * 0.4f) * transientEnv * p.transientAmount;

                // ===== BODY =====
                float bodyEnv = GetBodyEnvelope(t, dur, p.bodyDecay);
                
                float subF = p.subFreq * pitchMod;
                if (p.hasChorus)
                    subF *= 1f + lfo * p.modDepth * 0.05f;
                
                phaseSub += subF / sampleRate;
                float sub = Mathf.Sin(phaseSub * Mathf.PI * 2f);
                sub += Mathf.Sin(phaseSub * Mathf.PI * 3f) * 0.3f;
                
                float midF = p.midFreq * pitchMod;
                phaseMid += midF / sampleRate;
                float mid = Mathf.Sin(phaseMid * Mathf.PI * 2f);
                mid += Mathf.Sin(phaseMid * Mathf.PI * 4f) * 0.25f;
                
                // Apply modulation depth
                mid *= 1f + lfo * p.modDepth;
                
                float body = (sub * p.subAmount + mid * p.midAmount) * bodyEnv;

                // ===== RESONANCE =====
                float resEnv = GetResonanceEnvelope(t, dur);
                
                float resF = p.resonanceFreq * pitchMod;
                phaseRes += resF / sampleRate;
                float res = Mathf.Sin(phaseRes * Mathf.PI * 2f);
                res = BandpassFilter(res, resF, p.resonanceQ, 0);
                res *= resEnv * p.resonanceAmount;

                // ===== NOISE =====
                float noiseEnv = GetNoiseEnvelope(t, dur, p.noiseDecay);
                
                float whiteNoise = Random.Range(-1f, 1f);
                noiseState = noiseState * (0.95f + p.noiseColor * 0.04f) + whiteNoise * (0.05f - p.noiseColor * 0.04f);
                float coloredNoise = noiseState * p.noiseColor + whiteNoise * (1f - p.noiseColor);
                
                float noise = LowpassFilter(coloredNoise, p.noiseCutoff * (1f - normalizedT * 0.4f), 0);
                noise *= noiseEnv * p.noiseAmount;

                // ===== COMBINE =====
                sample = transient + body + res + noise;
                
                // Apply distortion
                sample = Distort(sample, p.distortion);
                
                // Apply glitch
                sample *= glitchMod;
            }

            // Reverb
            float wet = ProcessReverb(sample) * roomR;
            sample = sample * (1f - roomR * 0.3f) + wet;

            // Limit
            sample = FinalLimit(sample);

            audioBuffer[i] = sample;
        }

        // Fade out
        int fadeOutSamples = Mathf.Min(totalSamples / 5, sampleRate / 12);
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
            float normalize = 0.88f / maxAmp;
            for (int i = 0; i < totalSamples; i++)
                audioBuffer[i] *= normalize;
        }

        AudioClip clip = AudioClip.Create("EnemyGunShot", totalSamples, 1, sampleRate, false);
        float[] clipData = new float[totalSamples];
        System.Array.Copy(audioBuffer, clipData, totalSamples);
        clip.SetData(clipData, 0);

        return clip;
    }

    // =============== ENVELOPES ===============

    private float GetTransientEnvelope(float t, float decayRate)
    {
        float attack = 0.001f;
        float decay = 0.025f;

        if (t < attack)
            return t / attack;
        else if (t < attack + decay)
        {
            float dt = (t - attack) / decay;
            return Mathf.Exp(-dt * decayRate);
        }
        return Mathf.Exp(-(t - attack - decay) * 30f) * 0.08f;
    }

    private float GetBodyEnvelope(float t, float duration, float decayRate)
    {
        float attack = 0.003f;
        float sustain = duration * 0.15f;
        float decay = duration * 0.85f;

        if (t < attack)
            return t / attack;
        else if (t < attack + sustain)
            return 1f - (t - attack) / sustain * 0.1f;
        else
        {
            float dt = (t - attack - sustain) / decay;
            return 0.9f * Mathf.Exp(-dt * decayRate);
        }
    }

    private float GetResonanceEnvelope(float t, float duration)
    {
        float delay = 0.002f;
        float attack = 0.005f;
        float decay = duration * 0.7f;

        if (t < delay) return 0f;
        t -= delay;

        if (t < attack)
            return t / attack;
        else
        {
            float dt = (t - attack) / decay;
            return Mathf.Exp(-dt * 5f);
        }
    }

    private float GetNoiseEnvelope(float t, float duration, float decayRate)
    {
        float attack = 0.001f;
        float hold = 0.01f;
        float decay = duration * 0.75f;

        if (t < attack)
            return t / attack;
        else if (t < attack + hold)
            return 1f;
        else
        {
            float dt = (t - attack - hold) / decay;
            return Mathf.Exp(-dt * decayRate);
        }
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

    private float BandpassFilter(float input, float centerFreq, float q, int stateIndex)
    {
        float w0 = 2f * Mathf.PI * centerFreq / sampleRate;
        float alpha = Mathf.Sin(w0) / (2f * q);

        float b0 = alpha;
        float a1 = -2f * Mathf.Cos(w0);
        float a2 = 1f - alpha;
        float norm = 1f + alpha;

        b0 /= norm;
        a1 /= norm;
        a2 /= norm;

        float output = b0 * input - a1 * bpState[stateIndex] - a2 * bpState[stateIndex + 1];
        bpState[stateIndex + 1] = bpState[stateIndex];
        bpState[stateIndex] = output;

        return output;
    }

    private float Distort(float x, float amount)
    {
        if (amount <= 0f) return x;
        
        float drive = 1f + amount * 5f;
        x *= drive;
        
        // Asymmetric waveshaping for alien character
        if (x > 0)
            return (1f - Mathf.Exp(-x * 1.8f)) / 1.1f;
        else
            return (-1f + Mathf.Exp(x * 1.4f)) / 1.1f;
    }

    private float ProcessReverb(float input)
    {
        float combOut = 0f;
        float[] combFeedback = { 0.82f, 0.80f, 0.79f, 0.77f, 0.76f, 0.75f };

        for (int i = 0; i < combBuffers.Length; i++)
        {
            int idx = combIndices[i];
            float delayed = combBuffers[i][idx];
            combBuffers[i][idx] = input + delayed * combFeedback[i];
            combIndices[i] = (idx + 1) % combBuffers[i].Length;
            combOut += delayed;
        }
        combOut /= combBuffers.Length;

        float allpassOut = combOut;
        float allpassFeedback = 0.5f;

        for (int i = 0; i < allpassBuffers.Length; i++)
        {
            int idx = allpassIndices[i];
            float delayed = allpassBuffers[i][idx];
            float temp = -allpassFeedback * allpassOut + delayed;
            allpassBuffers[i][idx] = allpassOut + allpassFeedback * temp;
            allpassIndices[i] = (idx + 1) % allpassBuffers[i].Length;
            allpassOut = temp;
        }

        return allpassOut;
    }

    private float FinalLimit(float x)
    {
        float threshold = 0.75f;
        float knee = 0.15f;

        float absX = Mathf.Abs(x);
        if (absX < threshold - knee)
            return x;
        else if (absX < threshold + knee)
        {
            float t = (absX - (threshold - knee)) / (2f * knee);
            float gain = 1f - t * t * 0.35f;
            return Mathf.Sign(x) * absX * gain;
        }
        else
        {
            float over = absX - threshold;
            return Mathf.Sign(x) * (threshold + over * 0.08f);
        }
    }
}
