using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralFootstepAudio
    {
        public void PlayFootstep()
        {
            // Use cached clip if available, otherwise generate new one
            AudioClip clip = (cachedClip != null) ? cachedClip : GenerateFootstepClip();
            audioSource.PlayOneShot(clip, volume);
        }

        private AudioClip GenerateFootstepClip()
        {
            // Add variation
            float freq =
                baseFrequency * (1f + Random.Range(-frequencyVariation, frequencyVariation));
            float vol = 1f - Random.Range(0f, volumeVariation);
            float dur = duration * Random.Range(0.85f, 1.15f);

            int numSamples = Mathf.CeilToInt(dur * sampleRate);
            numSamples = Mathf.Min(numSamples, audioBuffer.Length);

            // Reset filter
            filterState = 0f;

            // Low-pass filter coefficient (simple one-pole)
            float rc = 1f / (2f * Mathf.PI * lowPassCutoff);
            float dt = 1f / sampleRate;
            float alpha = dt / (rc + dt);

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = GetEnvelope(t, dur);

                // Base tone (sine wave with slight frequency decay for "thump")
                float freqDecay = freq * Mathf.Exp(-t * 15f); // Frequency drops quickly
                float phase = 2f * Mathf.PI * freqDecay * t;
                float tone = Mathf.Sin(phase);

                // Add some harmonics for body
                tone += 0.3f * Mathf.Sin(phase * 2f);
                tone += 0.1f * Mathf.Sin(phase * 3f);

                // Noise component for texture
                float noise = Random.Range(-1f, 1f);

                // Mix tone and noise
                float sample = Mathf.Lerp(tone, noise, noiseMix);

                // Apply envelope
                sample *= envelope * vol;

                // Simple low-pass filter
                filterState += alpha * (sample - filterState);
                sample = filterState;

                // Soft clip to prevent harsh peaks
                sample = SoftClip(sample);

                audioBuffer[i] = sample;
            }

            // Create AudioClip from buffer
            AudioClip clip = AudioClip.Create("Footstep", numSamples, 1, sampleRate, false);

            // Copy only the samples we need
            float[] clipData = new float[numSamples];
            System.Array.Copy(audioBuffer, clipData, numSamples);
            clip.SetData(clipData, 0);

            return clip;
        }

        private float GetEnvelope(float time, float totalDuration)
        {
            // Quick attack, exponential decay - like a soft impact
            float attackTime = 0.005f;

            if (time < attackTime)
            {
                // Quick attack
                return time / attackTime;
            }
            else
            {
                // Exponential decay
                float decayTime = time - attackTime;
                float decayDuration = totalDuration - attackTime;
                return Mathf.Exp(-decayTime / (decayDuration * 0.25f));
            }
        }

        private float SoftClip(float x)
        {
            // Soft saturation using tanh-like function
            if (x > 1f)
                return 1f;
            if (x < -1f)
                return -1f;
            return x - (x * x * x) / 3f;
        }
    }
}
