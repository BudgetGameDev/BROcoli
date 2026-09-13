using UnityEngine;

/// <summary>
/// High-quality dynamic weapon sound generator with multi-layer synthesis,
/// complex modulation, room simulation, and punch compression.
/// Each gun type has a completely unique sound signature.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ProceduralGunAudio : MonoBehaviour
{
    public enum GunSoundType
    {
        AssaultRifle,
        Shotgun,
        HandCannon,
        EnergyBlaster,
        HeavyMachineGun
    }

    [Header("Sound Type")]
    [SerializeField] private GunSoundType soundType = GunSoundType.AssaultRifle;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1.0f;

    [Header("Variation")]
    [Range(0f, 0.2f)]
    [SerializeField] private float randomization = 0.1f;

    // Internal preset parameters - set per gun type
    private struct GunPreset
    {
        public float duration;
        public float roomSize;
        
        // Transient layer
        public float transientFreq1;
        public float transientFreq2;
        public float transientDecay;
        public float transientAmount;
        
        // Body layer
        public float subFreq;
        public float subAmount;
        public float midFreq;
        public float midAmount;
        public float bodyDecay;
        
        // Mechanical layer
        public float mechFreq;
        public float mechResonance;
        public float mechAmount;
        
        // Noise layer
        public float noiseLowCutoff;
        public float noiseMidCutoff;
        public float noiseHighCutoff;
        public float noiseAmount;
        public float noiseDecay;
        
        // Character
        public float punch;
        public float brightness;
        public float saturation;
        
        // Special
        public bool hasDoubleClick;  // For shotgun pump action
        public bool hasPitchSweep;   // For energy weapons
        public float pitchSweepAmount;
    }

    private GunPreset currentPreset;

    private AudioSource audioSource;
    private int sampleRate;
    private float[] audioBuffer;

    // Multi-stage filter states
    private float[] lpState = new float[4];
    private float[] hpState = new float[2];
    private float[] bpState = new float[4];

    // Allpass delays for reverb
    private float[][] allpassBuffers;
    private int[] allpassIndices;
    private float[][] combBuffers;
    private int[] combIndices;

    // Compressor state
    private float compEnvelope;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        sampleRate = AudioSettings.outputSampleRate;

        int maxSamples = Mathf.CeilToInt(1.5f * sampleRate);
        audioBuffer = new float[maxSamples];

        InitializeReverb();
    }

    private void InitializeReverb()
    {
        int[] allpassDelays = { 347, 113, 37, 59 };
        int[] combDelays = { 1687, 1601, 2053, 2251, 1777, 1949 };

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

    private GunPreset GetPreset(GunSoundType type)
    {
        GunPreset p = new GunPreset();
        
        switch (type)
        {
            case GunSoundType.AssaultRifle:
                // Sharp, snappy, military feel
                p.duration = 0.18f;
                p.roomSize = 0.2f;
                p.transientFreq1 = 4500f;
                p.transientFreq2 = 6500f;
                p.transientDecay = 12f;
                p.transientAmount = 0.5f;
                p.subFreq = 55f;
                p.subAmount = 0.4f;
                p.midFreq = 180f;
                p.midAmount = 0.6f;
                p.bodyDecay = 8f;
                p.mechFreq = 320f;
                p.mechResonance = 6f;
                p.mechAmount = 0.3f;
                p.noiseLowCutoff = 600f;
                p.noiseMidCutoff = 2500f;
                p.noiseHighCutoff = 5000f;
                p.noiseAmount = 0.35f;
                p.noiseDecay = 10f;
                p.punch = 0.8f;
                p.brightness = 0.7f;
                p.saturation = 1.2f;
                p.hasDoubleClick = false;
                p.hasPitchSweep = false;
                p.pitchSweepAmount = 0f;
                break;

            case GunSoundType.Shotgun:
                // Massive, boomy, with a "chunk" mechanical sound
                p.duration = 0.4f;
                p.roomSize = 0.5f;
                p.transientFreq1 = 2200f;
                p.transientFreq2 = 3800f;
                p.transientDecay = 6f;
                p.transientAmount = 0.7f;
                p.subFreq = 35f;
                p.subAmount = 0.9f;
                p.midFreq = 90f;
                p.midAmount = 0.8f;
                p.bodyDecay = 4f;
                p.mechFreq = 180f;
                p.mechResonance = 3f;
                p.mechAmount = 0.5f;
                p.noiseLowCutoff = 400f;
                p.noiseMidCutoff = 1200f;
                p.noiseHighCutoff = 3000f;
                p.noiseAmount = 0.6f;
                p.noiseDecay = 5f;
                p.punch = 1f;
                p.brightness = 0.4f;
                p.saturation = 2f;
                p.hasDoubleClick = true;
                p.hasPitchSweep = false;
                p.pitchSweepAmount = 0f;
                break;

            case GunSoundType.HandCannon:
                // Deep, powerful, reverberant
                p.duration = 0.32f;
                p.roomSize = 0.4f;
                p.transientFreq1 = 3200f;
                p.transientFreq2 = 5000f;
                p.transientDecay = 8f;
                p.transientAmount = 0.65f;
                p.subFreq = 40f;
                p.subAmount = 0.75f;
                p.midFreq = 130f;
                p.midAmount = 0.7f;
                p.bodyDecay = 5f;
                p.mechFreq = 250f;
                p.mechResonance = 5f;
                p.mechAmount = 0.4f;
                p.noiseLowCutoff = 500f;
                p.noiseMidCutoff = 1800f;
                p.noiseHighCutoff = 4000f;
                p.noiseAmount = 0.45f;
                p.noiseDecay = 6f;
                p.punch = 0.95f;
                p.brightness = 0.55f;
                p.saturation = 1.6f;
                p.hasDoubleClick = false;
                p.hasPitchSweep = false;
                p.pitchSweepAmount = 0f;
                break;

            case GunSoundType.EnergyBlaster:
                // Sci-fi, with pitch sweep, more tonal
                p.duration = 0.22f;
                p.roomSize = 0.15f;
                p.transientFreq1 = 1800f;
                p.transientFreq2 = 2800f;
                p.transientDecay = 15f;
                p.transientAmount = 0.4f;
                p.subFreq = 70f;
                p.subAmount = 0.3f;
                p.midFreq = 280f;
                p.midAmount = 0.5f;
                p.bodyDecay = 10f;
                p.mechFreq = 450f;
                p.mechResonance = 12f;
                p.mechAmount = 0.6f;
                p.noiseLowCutoff = 800f;
                p.noiseMidCutoff = 3500f;
                p.noiseHighCutoff = 7000f;
                p.noiseAmount = 0.2f;
                p.noiseDecay = 12f;
                p.punch = 0.6f;
                p.brightness = 0.85f;
                p.saturation = 0.8f;
                p.hasDoubleClick = false;
                p.hasPitchSweep = true;
                p.pitchSweepAmount = -0.5f;
                break;

            case GunSoundType.HeavyMachineGun:
                // Chunky, industrial, rattling
                p.duration = 0.25f;
                p.roomSize = 0.35f;
                p.transientFreq1 = 3000f;
                p.transientFreq2 = 4200f;
                p.transientDecay = 10f;
                p.transientAmount = 0.55f;
                p.subFreq = 45f;
                p.subAmount = 0.85f;
                p.midFreq = 110f;
                p.midAmount = 0.75f;
                p.bodyDecay = 6f;
                p.mechFreq = 200f;
                p.mechResonance = 4f;
                p.mechAmount = 0.55f;
                p.noiseLowCutoff = 450f;
                p.noiseMidCutoff = 1500f;
                p.noiseHighCutoff = 3500f;
                p.noiseAmount = 0.5f;
                p.noiseDecay = 7f;
                p.punch = 0.9f;
                p.brightness = 0.5f;
                p.saturation = 1.8f;
                p.hasDoubleClick = false;
                p.hasPitchSweep = false;
                p.pitchSweepAmount = 0f;
                break;
        }
        
        return p;
    }

    public void PlayGunSound()
    {
        currentPreset = GetPreset(soundType);
        AudioClip clip = GenerateGunClip();
        audioSource.PlayOneShot(clip, volume);
    }

    public void PlayGunSound(float volumeMultiplier)
    {
        currentPreset = GetPreset(soundType);
        AudioClip clip = GenerateGunClip();
        audioSource.PlayOneShot(clip, volume * volumeMultiplier);
    }

    private AudioClip GenerateGunClip()
    {
        GunPreset p = currentPreset;
        float rnd = randomization;
        
        // Apply randomization to key parameters
        float dur = p.duration * (1f + Random.Range(-rnd * 0.3f, rnd * 0.3f));
        float roomR = p.roomSize * (1f + Random.Range(-rnd, rnd));
        
        int numSamples = Mathf.CeilToInt(dur * sampleRate);
        int totalSamples = Mathf.CeilToInt((dur + roomR * 0.5f) * sampleRate);
        totalSamples = Mathf.Min(totalSamples, audioBuffer.Length);
        numSamples = Mathf.Min(numSamples, totalSamples);

        // Clear states
        System.Array.Clear(lpState, 0, lpState.Length);
        System.Array.Clear(hpState, 0, hpState.Length);
        System.Array.Clear(bpState, 0, bpState.Length);
        ClearReverb();
        compEnvelope = 0f;

        // Phase accumulators
        float phase1 = 0f, phase2 = 0f, phase3 = 0f;
        float phaseSub = 0f, phaseMid = 0f, phaseMech = 0f;
        float noiseState = 0f;

        // Randomized offsets for this shot
        float freqOffset1 = Random.Range(0.92f, 1.08f);
        float freqOffset2 = Random.Range(0.90f, 1.10f);
        float mechOffset = Random.Range(0.95f, 1.05f);
        
        // Shotgun double-click timing
        float clickTime = 0.025f + Random.Range(0f, 0.01f);

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float normalizedT = Mathf.Clamp01(t / dur);

            float sample = 0f;

            if (i < numSamples)
            {
                // Pitch sweep for energy weapons
                float pitchMod = 1f;
                if (p.hasPitchSweep)
                {
                    pitchMod = 1f + p.pitchSweepAmount * normalizedT;
                }

                // ========== LAYER 1: TRANSIENT ==========
                float transientEnv = GetTransientEnvelope(t, p.transientDecay);
                
                // Add second click for shotgun
                if (p.hasDoubleClick && t > clickTime)
                {
                    transientEnv += GetTransientEnvelope(t - clickTime, p.transientDecay * 1.5f) * 0.6f;
                }
                
                float tf1 = p.transientFreq1 * freqOffset1 * pitchMod * (1f - normalizedT * 0.3f);
                float tf2 = p.transientFreq2 * freqOffset2 * pitchMod * (1f - normalizedT * 0.4f);
                
                phase1 += tf1 / sampleRate;
                phase2 += tf2 / sampleRate;
                
                float trans1 = Mathf.Sin(phase1 * Mathf.PI * 2f);
                float trans2 = Mathf.Sin(phase2 * Mathf.PI * 2f);
                
                // Ring mod for metallic character (less for energy weapons)
                float ringAmount = p.hasPitchSweep ? 0.15f : 0.4f;
                float transRing = trans1 * trans2 * ringAmount;
                
                float transient = (trans1 + trans2 * 0.7f + transRing) * transientEnv * p.transientAmount;

                // ========== LAYER 2: BODY ==========
                float bodyEnv = GetBodyEnvelope(t, dur, p.bodyDecay);
                
                // Second thump for shotgun
                if (p.hasDoubleClick && t > clickTime)
                {
                    bodyEnv += GetBodyEnvelope(t - clickTime, dur, p.bodyDecay * 1.2f) * 0.5f;
                }
                
                float subF = p.subFreq * pitchMod * (1f - normalizedT * 0.15f);
                phaseSub += subF / sampleRate;
                float sub = Mathf.Sin(phaseSub * Mathf.PI * 2f);
                
                float midF = p.midFreq * pitchMod * (1f - normalizedT * 0.25f);
                phaseMid += midF / sampleRate;
                float mid = Mathf.Sin(phaseMid * Mathf.PI * 2f);
                mid += Mathf.Sin(phaseMid * Mathf.PI * 4f) * 0.35f;
                mid += Mathf.Sin(phaseMid * Mathf.PI * 6f) * 0.15f;
                
                mid = WarmSaturate(mid * p.saturation);
                sub = WarmSaturate(sub * p.saturation * 0.8f);
                
                float body = (sub * p.subAmount + mid * p.midAmount) * bodyEnv;

                // ========== LAYER 3: MECHANICAL ==========
                float mechEnv = GetMechEnvelope(t, dur);
                
                float mechF = p.mechFreq * mechOffset * pitchMod;
                phaseMech += mechF * (1f + normalizedT * 0.3f) / sampleRate;
                float mech = Mathf.Sin(phaseMech * Mathf.PI * 2f);
                mech += Mathf.Sin(phaseMech * Mathf.PI * 3.7f) * 0.25f;
                mech = BandpassFilter(mech, mechF, p.mechResonance, 0);
                mech *= mechEnv * p.mechAmount;

                // ========== LAYER 4: NOISE ==========
                float noiseEnv = GetNoiseEnvelope(t, dur, p.noiseDecay);
                
                float whiteNoise = Random.Range(-1f, 1f);
                noiseState = noiseState * 0.97f + whiteNoise * 0.03f;
                float pinkish = noiseState + whiteNoise * 0.4f;
                
                float noiseLow = LowpassFilter(pinkish, p.noiseLowCutoff * (1f - normalizedT * 0.5f), 0);
                float noiseMid = BandpassFilter(whiteNoise, p.noiseMidCutoff * (1f - normalizedT * 0.3f), 2.5f, 2);
                float noiseHigh = HighpassFilter(whiteNoise, p.noiseHighCutoff, 0) * (1f - normalizedT * 0.7f) * p.brightness;
                
                float noise = (noiseLow * 0.5f + noiseMid * 0.35f + noiseHigh * 0.25f) * noiseEnv * p.noiseAmount;

                // ========== LAYER 5: AIR ==========
                float airEnv = GetAirEnvelope(t, dur);
                float airNoise = LowpassFilter(Random.Range(-1f, 1f), 350f + 150f * (1f - normalizedT), 1);
                float air = airNoise * airEnv * p.subAmount * 0.15f;

                // ========== COMBINE ==========
                sample = transient + body + mech + noise + air;
                
                // ========== COMPRESSION ==========
                sample = PunchCompress(sample, p.punch);
            }

            // ========== REVERB ==========
            float wet = ProcessReverb(sample) * roomR;
            sample = sample * (1f - roomR * 0.25f) + wet;

            // ========== LIMIT ==========
            sample = FinalLimit(sample);

            audioBuffer[i] = sample;
        }

        // Fade out
        int fadeOutSamples = Mathf.Min(totalSamples / 6, sampleRate / 15);
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
            float normalize = 0.92f / maxAmp;
            for (int i = 0; i < totalSamples; i++)
                audioBuffer[i] *= normalize;
        }

        AudioClip clip = AudioClip.Create("GunShot", totalSamples, 1, sampleRate, false);
        float[] clipData = new float[totalSamples];
        System.Array.Copy(audioBuffer, clipData, totalSamples);
        clip.SetData(clipData, 0);

        return clip;
    }

    // =============== ENVELOPES ===============

    private float GetTransientEnvelope(float t, float decayRate)
    {
        float attack = 0.0006f;
        float decay = 0.02f;
        
        if (t < attack)
            return Mathf.Sqrt(t / attack);
        else if (t < attack + decay)
        {
            float dt = (t - attack) / decay;
            return Mathf.Exp(-dt * decayRate);
        }
        return Mathf.Exp(-(t - attack - decay) * 40f) * 0.05f;
    }

    private float GetBodyEnvelope(float t, float duration, float decayRate)
    {
        float attack = 0.002f;
        float sustain = duration * 0.1f;
        float decay = duration * 0.9f;
        
        if (t < attack)
            return t / attack;
        else if (t < attack + sustain)
            return 1f - (t - attack) / sustain * 0.15f;
        else
        {
            float dt = (t - attack - sustain) / decay;
            return 0.85f * Mathf.Exp(-dt * decayRate);
        }
    }

    private float GetMechEnvelope(float t, float duration)
    {
        float delay = 0.003f;
        float attack = 0.004f;
        float decay = duration * 0.6f;
        
        if (t < delay) return 0f;
        t -= delay;
        
        if (t < attack)
            return t / attack;
        else
        {
            float dt = (t - attack) / decay;
            return Mathf.Exp(-dt * 7f) * (1f + Mathf.Sin(dt * 35f) * 0.12f * Mathf.Exp(-dt * 4f));
        }
    }

    private float GetNoiseEnvelope(float t, float duration, float decayRate)
    {
        float attack = 0.0008f;
        float hold = 0.008f;
        float decay = duration * 0.8f;
        
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

    private float GetAirEnvelope(float t, float duration)
    {
        float attack = 0.008f;
        float decay = duration * 0.95f;
        
        if (t < attack)
            return Mathf.Sqrt(t / attack);
        else
        {
            float dt = (t - attack) / decay;
            return Mathf.Exp(-dt * 3.5f);
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

    private float HighpassFilter(float input, float cutoff, int stateIndex)
    {
        float rc = 1f / (2f * Mathf.PI * cutoff);
        float dt = 1f / sampleRate;
        float alpha = rc / (rc + dt);
        
        float output = alpha * (hpState[stateIndex] + input - lpState[stateIndex + 2]);
        lpState[stateIndex + 2] = input;
        hpState[stateIndex] = output;
        return output;
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

    // =============== SATURATION ===============

    private float WarmSaturate(float x)
    {
        // Asymmetric saturation for analog warmth
        if (x > 0)
            return 1f - Mathf.Exp(-x * 1.5f);
        else
            return -1f + Mathf.Exp(x * 1.2f);
    }

    // =============== COMPRESSION ===============

    private float PunchCompress(float input, float punchAmount)
    {
        // Fast attack, medium release compressor for punch
        float attackTime = 0.0005f;
        float releaseTime = 0.05f;
        float threshold = 0.4f;
        float ratio = 4f + punchAmount * 4f;
        float makeupGain = 1f + punchAmount * 0.5f;
        
        float inputLevel = Mathf.Abs(input);
        
        float targetEnv = inputLevel;
        float coeff = inputLevel > compEnvelope ? 
            1f - Mathf.Exp(-1f / (attackTime * sampleRate)) :
            1f - Mathf.Exp(-1f / (releaseTime * sampleRate));
        
        compEnvelope += coeff * (targetEnv - compEnvelope);
        
        float gainReduction = 1f;
        if (compEnvelope > threshold)
        {
            float overDb = 20f * Mathf.Log10(compEnvelope / threshold);
            float reducedDb = overDb / ratio;
            gainReduction = threshold * Mathf.Pow(10f, reducedDb / 20f) / compEnvelope;
        }
        
        return input * gainReduction * makeupGain;
    }

    // =============== REVERB ===============

    private float ProcessReverb(float input)
    {
        // Comb filters in parallel
        float combOut = 0f;
        float[] combFeedback = { 0.84f, 0.82f, 0.81f, 0.79f, 0.78f, 0.77f };
        
        for (int i = 0; i < combBuffers.Length; i++)
        {
            int idx = combIndices[i];
            float delayed = combBuffers[i][idx];
            combBuffers[i][idx] = input + delayed * combFeedback[i];
            combIndices[i] = (idx + 1) % combBuffers[i].Length;
            combOut += delayed;
        }
        combOut /= combBuffers.Length;
        
        // Allpass filters in series
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

    // =============== FINAL LIMITING ===============

    private float FinalLimit(float x)
    {
        // Soft knee limiter
        float threshold = 0.8f;
        float knee = 0.2f;
        
        float absX = Mathf.Abs(x);
        if (absX < threshold - knee)
            return x;
        else if (absX < threshold + knee)
        {
            // Soft knee region
            float t = (absX - (threshold - knee)) / (2f * knee);
            float gain = 1f - t * t * 0.3f;
            return Mathf.Sign(x) * absX * gain;
        }
        else
        {
            // Hard limit with slight compression
            float over = absX - threshold;
            return Mathf.Sign(x) * (threshold + over * 0.1f);
        }
    }
}
