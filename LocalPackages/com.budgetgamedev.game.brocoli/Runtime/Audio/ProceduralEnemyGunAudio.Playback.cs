using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralEnemyGunAudio
    {
        public void PlayGunSound()
        {
            float distAtten = GetDistanceAttenuation();
            if (distAtten < 0.01f)
                return;

            AudioClip clip;
            if (cachedClips != null && cachedClips.TryGetValue(soundType, out clip))
            {
                audioSource.PlayOneShot(clip, volume * distAtten);
            }
            else
            {
                currentPreset = GetPreset(soundType);
                clip = GenerateGunClip();
                audioSource.PlayOneShot(clip, volume * distAtten);
            }
        }

        public void PlayGunSound(float volumeMultiplier)
        {
            float distAtten = GetDistanceAttenuation();
            if (distAtten < 0.01f)
                return;

            AudioClip clip;
            if (cachedClips != null && cachedClips.TryGetValue(soundType, out clip))
            {
                audioSource.PlayOneShot(clip, volume * volumeMultiplier * distAtten);
            }
            else
            {
                currentPreset = GetPreset(soundType);
                clip = GenerateGunClip();
                audioSource.PlayOneShot(clip, volume * volumeMultiplier * distAtten);
            }
        }

        public void PlayGunSound(EnemyGunSoundType overrideSoundType)
        {
            float distAtten = GetDistanceAttenuation();
            if (distAtten < 0.01f)
                return;

            AudioClip clip;
            if (cachedClips != null && cachedClips.TryGetValue(overrideSoundType, out clip))
            {
                audioSource.PlayOneShot(clip, volume * distAtten);
            }
            else
            {
                currentPreset = GetPreset(overrideSoundType);
                clip = GenerateGunClip();
                audioSource.PlayOneShot(clip, volume * distAtten);
            }
        }

        public void PlayGunSound(EnemyGunSoundType overrideSoundType, float volumeMultiplier)
        {
            float distAtten = GetDistanceAttenuation();
            if (distAtten < 0.01f)
                return;

            AudioClip clip;
            if (cachedClips != null && cachedClips.TryGetValue(overrideSoundType, out clip))
            {
                audioSource.PlayOneShot(clip, volume * volumeMultiplier * distAtten);
            }
            else
            {
                currentPreset = GetPreset(overrideSoundType);
                clip = GenerateGunClip();
                audioSource.PlayOneShot(clip, volume * volumeMultiplier * distAtten);
            }
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

            float phase1 = 0f,
                phase2 = 0f;
            float phaseSub = 0f,
                phaseMid = 0f;
            float phaseMod = 0f,
                phaseRes = 0f;
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
                        if (
                            (t > glitchTime1 && t < glitchTime1 + 0.008f)
                            || (t > glitchTime2 && t < glitchTime2 + 0.012f)
                        )
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
                    float transient =
                        (trans1 * 0.6f + trans2 * 0.4f) * transientEnv * p.transientAmount;

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
                    noiseState =
                        noiseState * (0.95f + p.noiseColor * 0.04f)
                        + whiteNoise * (0.05f - p.noiseColor * 0.04f);
                    float coloredNoise =
                        noiseState * p.noiseColor + whiteNoise * (1f - p.noiseColor);

                    float noise = LowpassFilter(
                        coloredNoise,
                        p.noiseCutoff * (1f - normalizedT * 0.4f),
                        0
                    );
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

            // Normalize with headroom to prevent clipping
            float maxAmp = 0f;
            for (int i = 0; i < totalSamples; i++)
                maxAmp = Mathf.Max(maxAmp, Mathf.Abs(audioBuffer[i]));

            if (maxAmp > 0.01f)
            {
                float normalize = 0.7f / maxAmp; // Leave more headroom
                for (int i = 0; i < totalSamples; i++)
                    audioBuffer[i] *= normalize;
            }

            AudioClip clip = AudioClip.Create("EnemyGunShot", totalSamples, 1, sampleRate, false);
            float[] clipData = new float[totalSamples];
            System.Array.Copy(audioBuffer, clipData, totalSamples);
            clip.SetData(clipData, 0);

            return clip;
        }
    }
}
