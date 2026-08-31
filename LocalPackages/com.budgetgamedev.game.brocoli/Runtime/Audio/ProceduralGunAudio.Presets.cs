using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralGunAudio
    {
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
            public bool hasDoubleClick; // For shotgun pump action
            public bool hasPitchSweep; // For energy weapons
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
    }
}
