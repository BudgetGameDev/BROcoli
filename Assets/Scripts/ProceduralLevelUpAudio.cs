using UnityEngine;

/// <summary>
/// Procedural audio for level up fanfare - triumphant ascending tones with sparkle.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ProceduralLevelUpAudio : MonoBehaviour
{
    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.8f;

    private AudioSource audioSource;
    private int sampleRate;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        sampleRate = AudioSettings.outputSampleRate;
    }

    public void PlayLevelUpSound()
    {
        AudioClip clip = GenerateLevelUpClip();
        audioSource.PlayOneShot(clip, volume);
    }

    private AudioClip GenerateLevelUpClip()
    {
        float duration = 0.8f;
        int numSamples = Mathf.CeilToInt(duration * sampleRate);
        float[] audioBuffer = new float[numSamples];

        // Ascending arpeggio frequencies (C5 -> E5 -> G5 -> C6)
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f };
        float noteLength = 0.15f;
        float overlapTime = 0.05f;

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            for (int n = 0; n < notes.Length; n++)
            {
                float noteStart = n * (noteLength - overlapTime);
                float noteEnd = noteStart + noteLength + 0.2f;

                if (t >= noteStart && t < noteEnd)
                {
                    float noteT = t - noteStart;
                    float envelope = GetNoteEnvelope(noteT, noteLength + 0.2f);

                    // Main tone with harmonics
                    float freq = notes[n];
                    float phase = noteT * freq * Mathf.PI * 2f;
                    float tone = Mathf.Sin(phase) * 0.6f;
                    tone += Mathf.Sin(phase * 2f) * 0.25f;
                    tone += Mathf.Sin(phase * 3f) * 0.1f;
                    tone += Mathf.Sin(phase * 4f) * 0.05f;

                    sample += tone * envelope;
                }
            }

            // Add sparkle/shimmer layer
            float sparkleEnv = Mathf.Exp(-t * 3f) * Mathf.Max(0f, 1f - t * 1.5f);
            float sparkle = 0f;
            float sparkleFreq1 = 2500f + Mathf.Sin(t * 15f) * 200f;
            float sparkleFreq2 = 3200f + Mathf.Sin(t * 12f) * 300f;
            sparkle += Mathf.Sin(t * sparkleFreq1 * Mathf.PI * 2f) * 0.08f;
            sparkle += Mathf.Sin(t * sparkleFreq2 * Mathf.PI * 2f) * 0.06f;
            sample += sparkle * sparkleEnv;

            // Subtle sub bass punch at start
            if (t < 0.15f)
            {
                float subEnv = Mathf.Exp(-t * 20f);
                sample += Mathf.Sin(t * 80f * Mathf.PI * 2f) * subEnv * 0.3f;
            }

            audioBuffer[i] = sample;
        }

        // Normalize
        float maxAmp = 0f;
        for (int i = 0; i < numSamples; i++)
            maxAmp = Mathf.Max(maxAmp, Mathf.Abs(audioBuffer[i]));

        if (maxAmp > 0.01f)
        {
            float normalize = 0.85f / maxAmp;
            for (int i = 0; i < numSamples; i++)
                audioBuffer[i] *= normalize;
        }

        // Fade out last 10%
        int fadeStart = (int)(numSamples * 0.9f);
        for (int i = fadeStart; i < numSamples; i++)
        {
            float fade = 1f - (float)(i - fadeStart) / (numSamples - fadeStart);
            audioBuffer[i] *= fade * fade;
        }

        AudioClip clip = AudioClip.Create("LevelUp", numSamples, 1, sampleRate, false);
        clip.SetData(audioBuffer, 0);
        return clip;
    }

    private float GetNoteEnvelope(float t, float duration)
    {
        float attack = 0.01f;
        float decay = 0.1f;
        float sustainLevel = 0.7f;
        float release = duration - attack - decay;

        if (t < attack)
            return t / attack;
        else if (t < attack + decay)
            return 1f - (1f - sustainLevel) * (t - attack) / decay;
        else
        {
            float releaseT = (t - attack - decay) / release;
            return sustainLevel * Mathf.Exp(-releaseT * 4f);
        }
    }
}
