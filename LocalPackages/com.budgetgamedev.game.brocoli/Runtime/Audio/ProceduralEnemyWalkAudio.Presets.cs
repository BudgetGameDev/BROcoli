using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralEnemyWalkAudio
    {
        private WalkPreset GetPreset(EnemyWalkSoundType type)
        {
            WalkPreset p = new WalkPreset();

            switch (type)
            {
                case EnemyWalkSoundType.Skitter:
                    // Fast, light, insectoid - high frequency clicks
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
                    // Heavy, slow stomping - medium-low impact (raised from very low to avoid rumble)
                    p.duration = 0.12f;
                    p.impactFreq = 90f; // Raised from 45Hz to avoid sub-bass rumble
                    p.impactAmount = 0.6f; // Reduced from 0.8
                    p.impactDecay = 10f;
                    p.bodyFreq = 120f; // Raised from 70Hz
                    p.bodyAmount = 0.45f; // Reduced from 0.6
                    p.bodyDecay = 8f;
                    p.secondaryFreq = 180f; // Raised from 120Hz
                    p.secondaryDelay = 0.025f;
                    p.secondaryAmount = 0.3f;
                    p.noiseAmount = 0.25f; // Reduced from 0.35
                    p.noiseDecay = 12f;
                    p.noiseCutoff = 800f; // Raised from 600Hz
                    p.noiseColor = 0.7f; // Reduced from 0.9 (less brown noise)
                    p.hasClick = false;
                    p.clickFreq = 0f;
                    p.clickAmount = 0f;
                    p.hasWet = false;
                    p.wetAmount = 0f;
                    break;

                case EnemyWalkSoundType.Slither:
                    // Wet, sliding movement - noise-heavy with squelch
                    p.duration = 0.12f;
                    p.impactFreq = 120f; // Raised from 90Hz
                    p.impactAmount = 0.2f; // Reduced from 0.25
                    p.impactDecay = 14f;
                    p.bodyFreq = 100f; // Raised from 60Hz
                    p.bodyAmount = 0.25f; // Reduced from 0.3
                    p.bodyDecay = 10f;
                    p.secondaryFreq = 200f; // Raised from 150Hz
                    p.secondaryDelay = 0.02f;
                    p.secondaryAmount = 0.15f;
                    p.noiseAmount = 0.5f; // Reduced from 0.7
                    p.noiseDecay = 18f;
                    p.noiseCutoff = 1500f; // Raised from 1200Hz
                    p.noiseColor = 0.5f; // Reduced from 0.7
                    p.hasClick = false;
                    p.clickFreq = 0f;
                    p.clickAmount = 0f;
                    p.hasWet = true;
                    p.wetAmount = 0.3f; // Reduced from 0.4
                    break;

                case EnemyWalkSoundType.Shuffle:
                    // Shambling, zombie-like - dragging sound
                    p.duration = 0.15f;
                    p.impactFreq = 100f; // Raised from 65Hz
                    p.impactAmount = 0.3f; // Reduced from 0.4
                    p.impactDecay = 9f;
                    p.bodyFreq = 130f; // Raised from 85Hz
                    p.bodyAmount = 0.25f; // Reduced from 0.35
                    p.bodyDecay = 8f;
                    p.secondaryFreq = 160f; // Raised from 110Hz
                    p.secondaryDelay = 0.05f;
                    p.secondaryAmount = 0.2f;
                    p.noiseAmount = 0.4f; // Reduced from 0.55
                    p.noiseDecay = 10f;
                    p.noiseCutoff = 1100f; // Raised from 900Hz
                    p.noiseColor = 0.6f; // Reduced from 0.8
                    p.hasClick = false;
                    p.clickFreq = 0f;
                    p.clickAmount = 0f;
                    p.hasWet = false;
                    p.wetAmount = 0f;
                    break;

                case EnemyWalkSoundType.Clatter:
                    // Bony, skeletal rattling - multiple high clicks
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

        void Update()
        {
            if (rb == null)
                return;

            float speed = rb.GroundVelocity().magnitude;
            lastSpeed = speed;

            if (speed < minSpeedForSound)
            {
                return;
            }

            // Scale step interval by speed (faster = more frequent steps)
            float speedFactor = Mathf.Clamp(speed / 5f, 0.5f, 2f);
            float currentInterval = baseStepInterval / speedFactor;

            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                PlayStep();
                // Add slight randomization to timing
                stepTimer = currentInterval * Random.Range(0.85f, 1.15f);
            }
        }
    }
}
