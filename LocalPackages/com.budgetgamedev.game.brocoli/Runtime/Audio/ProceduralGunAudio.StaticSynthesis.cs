using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralGunAudio
    {
        /// <summary>
        /// Static clip generator for prewarming. Uses default parameters without randomization.
        /// </summary>
        private static AudioClip GenerateGunClipStatic(GunSoundType type)
        {
            int sampleRate = AudioSettings.outputSampleRate;
            GunPreset p = GetPresetStatic(type);

            int numSamples = Mathf.CeilToInt(p.duration * sampleRate);
            int totalSamples = Mathf.CeilToInt((p.duration + p.roomSize * 0.5f) * sampleRate);
            float[] buffer = new float[totalSamples];

            // Phase accumulators
            float phase1 = 0f,
                phase2 = 0f;
            float phaseSub = 0f,
                phaseMid = 0f,
                phaseMech = 0f;
            float noiseState = 0f;
            float lpState = 0f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float normalizedT = Mathf.Clamp01(t / p.duration);
                float sample = 0f;

                if (i < numSamples)
                {
                    float pitchMod = p.hasPitchSweep ? 1f + p.pitchSweepAmount * normalizedT : 1f;

                    // Transient layer
                    float transientEnv = Mathf.Exp(-t * p.transientDecay * 50f);
                    float tf1 = p.transientFreq1 * pitchMod * (1f - normalizedT * 0.3f);
                    float tf2 = p.transientFreq2 * pitchMod * (1f - normalizedT * 0.4f);
                    phase1 += tf1 / sampleRate;
                    phase2 += tf2 / sampleRate;
                    float trans1 = Mathf.Sin(phase1 * Mathf.PI * 2f);
                    float trans2 = Mathf.Sin(phase2 * Mathf.PI * 2f);
                    float transient = (trans1 + trans2 * 0.7f) * transientEnv * p.transientAmount;

                    // Body layer
                    float bodyEnv = Mathf.Exp(-t * p.bodyDecay);
                    phaseSub += p.subFreq * pitchMod / sampleRate;
                    phaseMid += p.midFreq * pitchMod / sampleRate;
                    float sub = Mathf.Sin(phaseSub * Mathf.PI * 2f);
                    float mid = Mathf.Sin(phaseMid * Mathf.PI * 2f);
                    float body = (sub * p.subAmount + mid * p.midAmount) * bodyEnv;

                    // Mechanical layer
                    float mechEnv = Mathf.Exp(-t * 15f) * (1f - normalizedT);
                    phaseMech += p.mechFreq * pitchMod / sampleRate;
                    float mech = Mathf.Sin(phaseMech * Mathf.PI * 2f) * mechEnv * p.mechAmount;

                    // Noise layer
                    float whiteNoise = UnityEngine.Random.Range(-1f, 1f);
                    noiseState = noiseState * 0.97f + whiteNoise * 0.03f;
                    float noiseEnv = Mathf.Exp(-t * p.noiseDecay);
                    float noise = (noiseState + whiteNoise * 0.3f) * noiseEnv * p.noiseAmount;

                    sample = transient + body + mech + noise;

                    // Simple saturation
                    sample = Mathf.Clamp(sample * p.saturation, -1f, 1f);
                }

                // Simple lowpass for tail smoothing
                lpState = lpState * 0.95f + sample * 0.05f;
                sample = sample * 0.7f + lpState * 0.3f * p.roomSize;

                buffer[i] = sample;
            }

            // Fade out
            int fadeOutSamples = Mathf.Min(totalSamples / 6, sampleRate / 15);
            for (int i = 0; i < fadeOutSamples; i++)
            {
                int idx = totalSamples - 1 - i;
                float fade = (float)i / fadeOutSamples;
                buffer[idx] *= fade * fade;
            }

            // Normalize
            float maxAmp = 0f;
            for (int i = 0; i < totalSamples; i++)
                maxAmp = Mathf.Max(maxAmp, Mathf.Abs(buffer[i]));
            if (maxAmp > 0.01f)
            {
                float normalize = 0.92f / maxAmp;
                for (int i = 0; i < totalSamples; i++)
                    buffer[i] *= normalize;
            }

            AudioClip clip = AudioClip.Create(
                "GunShot_" + type,
                totalSamples,
                1,
                sampleRate,
                false
            );
            clip.SetData(buffer, 0);
            return clip;
        }

        private static GunPreset GetPresetStatic(GunSoundType type)
        {
            GunPreset p = new GunPreset();
            switch (type)
            {
                case GunSoundType.AssaultRifle:
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
                    p.noiseAmount = 0.35f;
                    p.noiseDecay = 10f;
                    p.punch = 0.8f;
                    p.saturation = 1.2f;
                    break;
                case GunSoundType.Shotgun:
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
                    p.noiseAmount = 0.6f;
                    p.noiseDecay = 5f;
                    p.punch = 1f;
                    p.saturation = 2f;
                    p.hasDoubleClick = true;
                    break;
                case GunSoundType.HandCannon:
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
                    p.noiseAmount = 0.45f;
                    p.noiseDecay = 6f;
                    p.punch = 0.95f;
                    p.saturation = 1.6f;
                    break;
                case GunSoundType.EnergyBlaster:
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
                    p.noiseAmount = 0.2f;
                    p.noiseDecay = 12f;
                    p.punch = 0.6f;
                    p.saturation = 0.8f;
                    p.hasPitchSweep = true;
                    p.pitchSweepAmount = -0.5f;
                    break;
                case GunSoundType.HeavyMachineGun:
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
                    p.noiseAmount = 0.5f;
                    p.noiseDecay = 7f;
                    p.punch = 0.9f;
                    p.saturation = 1.8f;
                    break;
            }
            return p;
        }

        [Header("Sound Type")]
        [SerializeField]
        private GunSoundType soundType = GunSoundType.AssaultRifle;

        [Header("Volume")]
        [Range(0f, 1f)]
        [SerializeField]
        private float volume = 1.0f;

        [Header("Variation")]
        [Range(0f, 0.2f)]
        [SerializeField]
        private float randomization = 0.1f;
    }
}
