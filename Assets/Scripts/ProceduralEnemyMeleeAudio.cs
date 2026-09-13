using UnityEngine;

/// <summary>
/// Procedural melee attack audio for enemies.
/// Generates distinct sounds for different melee attack types.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ProceduralEnemyMeleeAudio : MonoBehaviour
{
    public enum MeleeSoundType
    {
        Slash,          // Quick slashing attack
        Bite,           // Biting/chomping attack
        Slam,           // Heavy impact slam
        Swipe,          // Wide sweeping attack
        Stinger         // Piercing/stabbing attack
    }

    [Header("Sound Type")]
    [SerializeField] private MeleeSoundType soundType = MeleeSoundType.Slash;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.7f;

    [Header("Variation")]
    [Range(0f, 0.25f)]
    [SerializeField] private float randomization = 0.12f;

    private struct MeleePreset
    {
        public float duration;
        
        // Whoosh/movement
        public float whooshFreqStart;
        public float whooshFreqEnd;
        public float whooshAmount;
        public float whooshDecay;
        
        // Impact
        public float impactDelay;
        public float impactFreq;
        public float impactAmount;
        public float impactDecay;
        
        // Body resonance
        public float bodyFreq;
        public float bodyAmount;
        public float bodyDecay;
        
        // Noise characteristics
        public float noiseBurst;
        public float noiseDecay;
        public float noiseCutoff;
        
        // Character
        public bool hasMetallic;
        public float metallicFreq;
        public float metallicAmount;
    }

    private MeleePreset currentPreset;
    private AudioSource audioSource;
    private int sampleRate;
    private float[] audioBuffer;

    private float[] lpState = new float[4];
    private float[] hpState = new float[2];
    
    // Distance-based volume attenuation
    private static Transform playerTransform;
    private const float MAX_AUDIBLE_DISTANCE = 25f;
    private const float MIN_AUDIBLE_DISTANCE = 2f;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        sampleRate = AudioSettings.outputSampleRate;
        int maxSamples = Mathf.CeilToInt(0.8f * sampleRate);
        audioBuffer = new float[maxSamples];
        
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }
    }

    private MeleePreset GetPreset(MeleeSoundType type)
    {
        MeleePreset p = new MeleePreset();

        switch (type)
        {
            case MeleeSoundType.Slash:
                // Quick, sharp whoosh with slight metallic edge
                p.duration = 0.18f;
                p.whooshFreqStart = 1800f;
                p.whooshFreqEnd = 400f;
                p.whooshAmount = 0.5f;
                p.whooshDecay = 10f;
                p.impactDelay = 0.08f;
                p.impactFreq = 180f;
                p.impactAmount = 0.3f;
                p.impactDecay = 12f;
                p.bodyFreq = 120f;
                p.bodyAmount = 0.25f;
                p.bodyDecay = 8f;
                p.noiseBurst = 0.6f;
                p.noiseDecay = 15f;
                p.noiseCutoff = 3500f;
                p.hasMetallic = true;
                p.metallicFreq = 2800f;
                p.metallicAmount = 0.15f;
                break;

            case MeleeSoundType.Bite:
                // Wet, crunchy chomp sound
                p.duration = 0.15f;
                p.whooshFreqStart = 600f;
                p.whooshFreqEnd = 200f;
                p.whooshAmount = 0.2f;
                p.whooshDecay = 12f;
                p.impactDelay = 0.02f;
                p.impactFreq = 250f;
                p.impactAmount = 0.6f;
                p.impactDecay = 18f;
                p.bodyFreq = 90f;
                p.bodyAmount = 0.5f;
                p.bodyDecay = 10f;
                p.noiseBurst = 0.7f;
                p.noiseDecay = 20f;
                p.noiseCutoff = 1200f;
                p.hasMetallic = false;
                p.metallicFreq = 0f;
                p.metallicAmount = 0f;
                break;

            case MeleeSoundType.Slam:
                // Heavy, booming impact
                p.duration = 0.3f;
                p.whooshFreqStart = 500f;
                p.whooshFreqEnd = 100f;
                p.whooshAmount = 0.35f;
                p.whooshDecay = 6f;
                p.impactDelay = 0.05f;
                p.impactFreq = 60f;
                p.impactAmount = 0.9f;
                p.impactDecay = 5f;
                p.bodyFreq = 45f;
                p.bodyAmount = 0.8f;
                p.bodyDecay = 4f;
                p.noiseBurst = 0.5f;
                p.noiseDecay = 8f;
                p.noiseCutoff = 800f;
                p.hasMetallic = false;
                p.metallicFreq = 0f;
                p.metallicAmount = 0f;
                break;

            case MeleeSoundType.Swipe:
                // Wide, sweeping whoosh
                p.duration = 0.22f;
                p.whooshFreqStart = 2200f;
                p.whooshFreqEnd = 300f;
                p.whooshAmount = 0.65f;
                p.whooshDecay = 8f;
                p.impactDelay = 0.12f;
                p.impactFreq = 150f;
                p.impactAmount = 0.25f;
                p.impactDecay = 10f;
                p.bodyFreq = 100f;
                p.bodyAmount = 0.2f;
                p.bodyDecay = 7f;
                p.noiseBurst = 0.75f;
                p.noiseDecay = 12f;
                p.noiseCutoff = 4500f;
                p.hasMetallic = true;
                p.metallicFreq = 3200f;
                p.metallicAmount = 0.12f;
                break;

            case MeleeSoundType.Stinger:
                // Sharp, piercing thrust
                p.duration = 0.12f;
                p.whooshFreqStart = 3500f;
                p.whooshFreqEnd = 1200f;
                p.whooshAmount = 0.4f;
                p.whooshDecay = 18f;
                p.impactDelay = 0.04f;
                p.impactFreq = 320f;
                p.impactAmount = 0.5f;
                p.impactDecay = 15f;
                p.bodyFreq = 200f;
                p.bodyAmount = 0.3f;
                p.bodyDecay = 12f;
                p.noiseBurst = 0.45f;
                p.noiseDecay = 18f;
                p.noiseCutoff = 5000f;
                p.hasMetallic = true;
                p.metallicFreq = 4200f;
                p.metallicAmount = 0.25f;
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
        return Mathf.Sqrt(attenuation); // Smoother falloff
    }

    public void PlayMeleeSound()
    {
        float distAtten = GetDistanceAttenuation();
        if (distAtten < 0.01f) return;
        
        currentPreset = GetPreset(soundType);
        AudioClip clip = GenerateMeleeClip();
        audioSource.PlayOneShot(clip, volume * distAtten);
    }

    public void PlayMeleeSound(float volumeMultiplier)
    {
        float distAtten = GetDistanceAttenuation();
        if (distAtten < 0.01f) return;
        
        currentPreset = GetPreset(soundType);
        AudioClip clip = GenerateMeleeClip();
        audioSource.PlayOneShot(clip, volume * volumeMultiplier * distAtten);
    }

    private AudioClip GenerateMeleeClip()
    {
        MeleePreset p = currentPreset;
        float rnd = randomization;

        float dur = p.duration * (1f + Random.Range(-rnd * 0.3f, rnd * 0.3f));
        int totalSamples = Mathf.CeilToInt(dur * sampleRate);
        totalSamples = Mathf.Min(totalSamples, audioBuffer.Length);

        System.Array.Clear(lpState, 0, lpState.Length);
        System.Array.Clear(hpState, 0, hpState.Length);

        float phaseImpact = 0f;
        float phaseBody = 0f;
        float phaseMetallic = 0f;
        float noiseState = 0f;

        float freqOffset = Random.Range(0.92f, 1.08f);
        float impactDelayRnd = p.impactDelay * (1f + Random.Range(-rnd, rnd));

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float normalizedT = Mathf.Clamp01(t / dur);

            float sample = 0f;

            // ===== WHOOSH (filtered noise sweep) =====
            float whooshEnv = GetWhooshEnvelope(t, dur, p.whooshDecay);
            float whooshFreq = Mathf.Lerp(p.whooshFreqStart, p.whooshFreqEnd, normalizedT) * freqOffset;
            
            float whooshNoise = Random.Range(-1f, 1f);
            float whoosh = LowpassFilter(whooshNoise, whooshFreq, 0);
            whoosh = HighpassFilter(whoosh, whooshFreq * 0.3f, 0);
            whoosh *= whooshEnv * p.whooshAmount;

            // ===== IMPACT =====
            float impactSample = 0f;
            if (t >= impactDelayRnd)
            {
                float impactT = t - impactDelayRnd;
                float impactEnv = GetImpactEnvelope(impactT, p.impactDecay);
                
                float impactF = p.impactFreq * freqOffset;
                phaseImpact += impactF / sampleRate;
                impactSample = Mathf.Sin(phaseImpact * Mathf.PI * 2f);
                impactSample += Mathf.Sin(phaseImpact * Mathf.PI * 4f) * 0.4f;
                impactSample *= impactEnv * p.impactAmount;
            }

            // ===== BODY RESONANCE =====
            float bodyEnv = GetBodyEnvelope(t, dur, p.bodyDecay);
            float bodyF = p.bodyFreq * freqOffset;
            phaseBody += bodyF / sampleRate;
            float body = Mathf.Sin(phaseBody * Mathf.PI * 2f);
            body += Mathf.Sin(phaseBody * Mathf.PI * 3f) * 0.3f;
            body *= bodyEnv * p.bodyAmount;

            // ===== NOISE BURST =====
            float noiseEnv = GetNoiseBurstEnvelope(t, dur, p.noiseDecay);
            float whiteNoise = Random.Range(-1f, 1f);
            noiseState = noiseState * 0.85f + whiteNoise * 0.15f;
            float noiseBurst = LowpassFilter(noiseState + whiteNoise * 0.5f, p.noiseCutoff * (1f - normalizedT * 0.5f), 1);
            noiseBurst *= noiseEnv * p.noiseBurst;

            // ===== METALLIC (optional) =====
            float metallic = 0f;
            if (p.hasMetallic && t < dur * 0.4f)
            {
                float metallicEnv = Mathf.Exp(-t * 20f);
                phaseMetallic += p.metallicFreq * freqOffset / sampleRate;
                metallic = Mathf.Sin(phaseMetallic * Mathf.PI * 2f);
                metallic *= metallicEnv * p.metallicAmount;
            }

            // ===== COMBINE =====
            sample = whoosh + impactSample + body + noiseBurst + metallic;

            // Soft clipping
            sample = SoftClip(sample);

            audioBuffer[i] = sample;
        }

        // Fade out
        int fadeOutSamples = Mathf.Min(totalSamples / 6, sampleRate / 20);
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
            float normalize = 0.85f / maxAmp;
            for (int i = 0; i < totalSamples; i++)
                audioBuffer[i] *= normalize;
        }

        AudioClip clip = AudioClip.Create("EnemyMelee", totalSamples, 1, sampleRate, false);
        float[] clipData = new float[totalSamples];
        System.Array.Copy(audioBuffer, clipData, totalSamples);
        clip.SetData(clipData, 0);

        return clip;
    }

    // =============== ENVELOPES ===============

    private float GetWhooshEnvelope(float t, float duration, float decayRate)
    {
        float attack = 0.008f;
        float decay = duration * 0.9f;

        if (t < attack)
            return Mathf.Sqrt(t / attack);
        else
        {
            float dt = (t - attack) / decay;
            return Mathf.Exp(-dt * decayRate);
        }
    }

    private float GetImpactEnvelope(float t, float decayRate)
    {
        float attack = 0.001f;

        if (t < attack)
            return t / attack;
        else
        {
            return Mathf.Exp(-(t - attack) * decayRate);
        }
    }

    private float GetBodyEnvelope(float t, float duration, float decayRate)
    {
        float attack = 0.003f;
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

    private float GetNoiseBurstEnvelope(float t, float duration, float decayRate)
    {
        float attack = 0.002f;

        if (t < attack)
            return t / attack;
        else
        {
            return Mathf.Exp(-(t - attack) * decayRate);
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

    private float SoftClip(float x)
    {
        if (Mathf.Abs(x) < 0.7f)
            return x;
        else if (x > 0)
            return 0.7f + (1f - 0.7f) * (float)System.Math.Tanh((x - 0.7f) * 3f);
        else
            return -0.7f + (-1f + 0.7f) * (float)System.Math.Tanh((x + 0.7f) * 3f);
    }
}
