using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralEnemyWalkAudio
    {
        private static WalkPreset GetPresetStatic(EnemyWalkSoundType type)
        {
            WalkPreset p = new WalkPreset();
            switch (type)
            {
                case EnemyWalkSoundType.Skitter:
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
                    p.duration = 0.12f;
                    p.impactFreq = 90f;
                    p.impactAmount = 0.6f;
                    p.impactDecay = 10f;
                    p.bodyFreq = 120f;
                    p.bodyAmount = 0.45f;
                    p.bodyDecay = 8f;
                    p.secondaryFreq = 180f;
                    p.secondaryDelay = 0.025f;
                    p.secondaryAmount = 0.3f;
                    p.noiseAmount = 0.25f;
                    p.noiseDecay = 12f;
                    p.noiseCutoff = 800f;
                    p.noiseColor = 0.7f;
                    p.hasClick = false;
                    p.clickFreq = 0f;
                    p.clickAmount = 0f;
                    p.hasWet = false;
                    p.wetAmount = 0f;
                    break;
                case EnemyWalkSoundType.Slither:
                    p.duration = 0.12f;
                    p.impactFreq = 120f;
                    p.impactAmount = 0.2f;
                    p.impactDecay = 14f;
                    p.bodyFreq = 100f;
                    p.bodyAmount = 0.25f;
                    p.bodyDecay = 10f;
                    p.secondaryFreq = 200f;
                    p.secondaryDelay = 0.02f;
                    p.secondaryAmount = 0.15f;
                    p.noiseAmount = 0.5f;
                    p.noiseDecay = 18f;
                    p.noiseCutoff = 1500f;
                    p.noiseColor = 0.5f;
                    p.hasClick = false;
                    p.clickFreq = 0f;
                    p.clickAmount = 0f;
                    p.hasWet = true;
                    p.wetAmount = 0.3f;
                    break;
                case EnemyWalkSoundType.Shuffle:
                    p.duration = 0.15f;
                    p.impactFreq = 100f;
                    p.impactAmount = 0.3f;
                    p.impactDecay = 9f;
                    p.bodyFreq = 130f;
                    p.bodyAmount = 0.25f;
                    p.bodyDecay = 8f;
                    p.secondaryFreq = 160f;
                    p.secondaryDelay = 0.05f;
                    p.secondaryAmount = 0.2f;
                    p.noiseAmount = 0.4f;
                    p.noiseDecay = 10f;
                    p.noiseCutoff = 1100f;
                    p.noiseColor = 0.6f;
                    p.hasClick = false;
                    p.clickFreq = 0f;
                    p.clickAmount = 0f;
                    p.hasWet = false;
                    p.wetAmount = 0f;
                    break;
                case EnemyWalkSoundType.Clatter:
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

            float phaseImpact = 0f,
                phaseBody = 0f,
                phaseSecondary = 0f,
                phaseClick = 0f;
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
                noiseState =
                    noiseState * (0.9f + p.noiseColor * 0.09f)
                    + whiteNoise * (0.1f - p.noiseColor * 0.09f);
                float coloredNoise = noiseState * p.noiseColor + whiteNoise * (1f - p.noiseColor);
                float noise = LowpassFilterStatic(
                    coloredNoise,
                    p.noiseCutoff * (1f - normalizedT * 0.4f),
                    0
                );
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
                    wet =
                        LowpassFilterStatic(wetNoise, 400f + 800f * (1f - normalizedT), 1)
                        * wetEnv
                        * p.wetAmount;
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

            AudioClip clip = AudioClip.Create(
                $"EnemyStep_{type}_{variationIndex}",
                totalSamples,
                1,
                _staticSampleRate,
                false
            );
            float[] clipData = new float[totalSamples];
            System.Array.Copy(_staticAudioBuffer, clipData, totalSamples);
            clip.SetData(clipData, 0);
            return clip;
        }
    }
}
