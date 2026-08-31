using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralEnemyWalkAudio
    {
        public void PlayStep()
        {
            // Distance-based attenuation
            float distanceVolume = 1f;
            if (playerTransform != null)
            {
                float dist = GroundPlane.GroundDistance(
                    transform.position,
                    playerTransform.position
                );
                if (dist > MAX_AUDIBLE_DISTANCE)
                {
                    return; // Too far, don't play
                }
                distanceVolume = Mathf.InverseLerp(
                    MAX_AUDIBLE_DISTANCE,
                    MIN_AUDIBLE_DISTANCE,
                    dist
                );
                distanceVolume = Mathf.Sqrt(distanceVolume); // Smoother falloff
            }

            // Reduce volume when many enemies are active
            float crowdAttenuation = 1f / (1f + activeEnemyCount * 0.1f);

            // Use cached clip if available, otherwise generate dynamically
            AudioClip clip;
            if (
                _isPrewarmed
                && _clipCache != null
                && _clipCache.TryGetValue(soundType, out var clips)
            )
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
                noiseState =
                    noiseState * (0.9f + p.noiseColor * 0.09f)
                    + whiteNoise * (0.1f - p.noiseColor * 0.09f);
                float coloredNoise = noiseState * p.noiseColor + whiteNoise * (1f - p.noiseColor);
                float noise = LowpassFilter(
                    coloredNoise,
                    p.noiseCutoff * (1f - normalizedT * 0.4f),
                    0
                );
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
                    float wetNoise = LowpassFilter(
                        Random.Range(-1f, 1f),
                        400f + 800f * (1f - normalizedT),
                        1
                    );
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
                float normalize = 0.65f / maxAmp; // Leave more headroom
                for (int i = 0; i < totalSamples; i++)
                    audioBuffer[i] *= normalize;
            }

            AudioClip clip = AudioClip.Create("EnemyStep", totalSamples, 1, sampleRate, false);
            float[] clipData = new float[totalSamples];
            System.Array.Copy(audioBuffer, clipData, totalSamples);
            clip.SetData(clipData, 0);

            return clip;
        }
    }
}
