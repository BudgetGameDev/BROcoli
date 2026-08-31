using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralGunAudio
    {
        public void PlayGunSound()
        {
            AudioClip clip;
            // Use cached clip if available (from PrewarmAll), otherwise generate with randomization
            if (cachedClips != null && cachedClips.TryGetValue(soundType, out clip))
            {
                // Use prewarmed clip (no randomization, but avoids first-shot hitch)
            }
            else
            {
                // Fallback: generate new clip with randomization
                currentPreset = GetPreset(soundType);
                clip = GenerateGunClip();
            }
            audioSource.PlayOneShot(clip, volume);
        }

        public void PlayGunSound(float volumeMultiplier)
        {
            AudioClip clip;
            // Use cached clip if available (from PrewarmAll), otherwise generate with randomization
            if (cachedClips != null && cachedClips.TryGetValue(soundType, out clip))
            {
                // Use prewarmed clip (no randomization, but avoids first-shot hitch)
            }
            else
            {
                // Fallback: generate new clip with randomization
                currentPreset = GetPreset(soundType);
                clip = GenerateGunClip();
            }
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
            float phase1 = 0f,
                phase2 = 0f;
            float phaseSub = 0f,
                phaseMid = 0f,
                phaseMech = 0f;
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
                        transientEnv +=
                            GetTransientEnvelope(t - clickTime, p.transientDecay * 1.5f) * 0.6f;
                    }

                    float tf1 =
                        p.transientFreq1 * freqOffset1 * pitchMod * (1f - normalizedT * 0.3f);
                    float tf2 =
                        p.transientFreq2 * freqOffset2 * pitchMod * (1f - normalizedT * 0.4f);

                    phase1 += tf1 / sampleRate;
                    phase2 += tf2 / sampleRate;

                    float trans1 = Mathf.Sin(phase1 * Mathf.PI * 2f);
                    float trans2 = Mathf.Sin(phase2 * Mathf.PI * 2f);

                    // Ring mod for metallic character (less for energy weapons)
                    float ringAmount = p.hasPitchSweep ? 0.15f : 0.4f;
                    float transRing = trans1 * trans2 * ringAmount;

                    float transient =
                        (trans1 + trans2 * 0.7f + transRing) * transientEnv * p.transientAmount;

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

                    float noiseLow = LowpassFilter(
                        pinkish,
                        p.noiseLowCutoff * (1f - normalizedT * 0.5f),
                        0
                    );
                    float noiseMid = BandpassFilter(
                        whiteNoise,
                        p.noiseMidCutoff * (1f - normalizedT * 0.3f),
                        2.5f,
                        2
                    );
                    float noiseHigh =
                        HighpassFilter(whiteNoise, p.noiseHighCutoff, 0)
                        * (1f - normalizedT * 0.7f)
                        * p.brightness;

                    float noise =
                        (noiseLow * 0.5f + noiseMid * 0.35f + noiseHigh * 0.25f)
                        * noiseEnv
                        * p.noiseAmount;

                    // ========== LAYER 5: AIR ==========
                    float airEnv = GetAirEnvelope(t, dur);
                    float airNoise = LowpassFilter(
                        Random.Range(-1f, 1f),
                        350f + 150f * (1f - normalizedT),
                        1
                    );
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
    }
}
