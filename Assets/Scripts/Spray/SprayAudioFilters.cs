using UnityEngine;

/// <summary>
/// Audio filter utilities for procedural sound generation.
/// Provides lowpass, highpass, and bandpass filters along with helper functions.
/// </summary>
public class SprayAudioFilters
{
    private int sampleRate;
    
    // Filter states for colored noise
    private float lpState1, lpState2;
    private float hpState1, hpState2;
    private float bpState1, bpState2;

    public SprayAudioFilters(int sampleRate)
    {
        this.sampleRate = sampleRate;
        ResetFilters();
    }

    /// <summary>
    /// Reset all filter states to zero
    /// </summary>
    public void ResetFilters()
    {
        lpState1 = lpState2 = 0f;
        hpState1 = hpState2 = 0f;
        bpState1 = bpState2 = 0f;
    }

    /// <summary>
    /// Apply a second-order lowpass filter
    /// </summary>
    public float ApplyLowpass(float input, float cutoff)
    {
        float rc = 1f / (2f * Mathf.PI * cutoff);
        float dt = 1f / sampleRate;
        float alpha = dt / (rc + dt);
        
        lpState1 += alpha * (input - lpState1);
        lpState2 += alpha * (lpState1 - lpState2);
        
        return lpState2;
    }

    /// <summary>
    /// Apply a first-order highpass filter
    /// </summary>
    public float ApplyHighpass(float input, float cutoff)
    {
        float rc = 1f / (2f * Mathf.PI * cutoff);
        float dt = 1f / sampleRate;
        float alpha = rc / (rc + dt);
        
        float output = alpha * (hpState2 + input - hpState1);
        hpState1 = input;
        hpState2 = output;
        
        return output;
    }

    /// <summary>
    /// Apply a bandpass filter with adjustable Q factor
    /// </summary>
    public float ApplyBandpass(float input, float centerFreq, float q)
    {
        float w0 = 2f * Mathf.PI * centerFreq / sampleRate;
        float alpha = Mathf.Sin(w0) / (2f * q);
        
        float a0 = 1f + alpha;
        float b1 = Mathf.Sin(w0) / a0;
        float a1 = -2f * Mathf.Cos(w0) / a0;
        float a2 = (1f - alpha) / a0;
        
        float output = b1 * input - a1 * bpState1 - a2 * bpState2;
        bpState2 = bpState1;
        bpState1 = output;
        
        return output;
    }

    /// <summary>
    /// Apply soft clipping for gentle saturation
    /// </summary>
    public static float SoftClip(float x)
    {
        if (x > 1f) return 1f - Mathf.Exp(-(x - 1f) * 2f) * 0.3f + 0.7f;
        if (x < -1f) return -1f + Mathf.Exp(-(-x - 1f) * 2f) * 0.3f - 0.7f;
        
        // Subtle saturation in normal range
        return x - x * x * x * 0.15f;
    }

    /// <summary>
    /// Normalize audio buffer to target peak level
    /// </summary>
    public static void NormalizeBuffer(float[] buffer, float targetPeak)
    {
        float maxAmp = 0f;
        for (int i = 0; i < buffer.Length; i++)
            maxAmp = Mathf.Max(maxAmp, Mathf.Abs(buffer[i]));
        
        if (maxAmp > 0.001f)
        {
            float scale = targetPeak / maxAmp;
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] *= scale;
        }
    }

    /// <summary>
    /// Generate pink noise using Paul Kellet's approximation
    /// </summary>
    public static float GeneratePinkNoise(float white, float[] pinkState)
    {
        if (pinkState.Length < 7) return white;
        
        pinkState[0] = 0.99886f * pinkState[0] + white * 0.0555179f;
        pinkState[1] = 0.99332f * pinkState[1] + white * 0.0750759f;
        pinkState[2] = 0.96900f * pinkState[2] + white * 0.1538520f;
        pinkState[3] = 0.86650f * pinkState[3] + white * 0.3104856f;
        pinkState[4] = 0.55000f * pinkState[4] + white * 0.5329522f;
        pinkState[5] = -0.7616f * pinkState[5] - white * 0.0168980f;
        
        float pinkNoise = (pinkState[0] + pinkState[1] + pinkState[2] + 
                          pinkState[3] + pinkState[4] + pinkState[5] + 
                          pinkState[6] + white * 0.5362f) * 0.11f;
        
        pinkState[6] = white * 0.115926f;
        
        return pinkNoise;
    }

    /// <summary>
    /// Get spray envelope for attack/sustain/release shaping
    /// </summary>
    /// <param name="t">Normalized time (0-1)</param>
    /// <param name="pressure">Pressure intensity modifier</param>
    public static float GetSprayEnvelope(float t, float pressure)
    {
        const float attack = 0.05f;
        const float release = 0.15f;
        
        if (t < attack)
        {
            // Quick attack with slight overshoot
            float attackT = t / attack;
            return Mathf.Sin(attackT * Mathf.PI * 0.5f) * (1f + 0.1f * pressure);
        }
        else if (t < 1f - release)
        {
            // Sustain with subtle variation
            return 1f + Mathf.Sin(t * 20f) * 0.05f * pressure;
        }
        else
        {
            // Release
            float releaseT = (t - (1f - release)) / release;
            return Mathf.Exp(-releaseT * 4f);
        }
    }
}
