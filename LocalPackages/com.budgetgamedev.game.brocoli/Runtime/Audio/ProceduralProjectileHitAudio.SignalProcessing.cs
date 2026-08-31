using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class ProceduralProjectileHitAudio
    {
        private static float StaticLowPassFilter(float input, float cutoff, int stateIndex)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / staticSampleRate;
            float alpha = dt / (rc + dt);
            staticLpState[stateIndex] =
                staticLpState[stateIndex] + alpha * (input - staticLpState[stateIndex]);
            return staticLpState[stateIndex];
        }

        private static float SoftClip(float x)
        {
            if (x > 1f)
                return 1f - Mathf.Exp(-(x - 1f));
            if (x < -1f)
                return -1f + Mathf.Exp(-(-x - 1f));
            return x;
        }

        private float LowPassFilter(float input, float cutoff, int stateIndex)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / sampleRate;
            float alpha = dt / (rc + dt);
            lpState[stateIndex] = lpState[stateIndex] + alpha * (input - lpState[stateIndex]);
            return lpState[stateIndex];
        }

        // Static helper to play hit sound from anywhere
        public static void PlayHit(
            Vector3 position,
            HitSoundType type = HitSoundType.Energy,
            float vol = 0.5f
        )
        {
            EnsureStaticInitialized();

            // Use cached clip directly if available (avoids GameObject/Component overhead)
            AudioClip clip;
            if (cachedClips.TryGetValue(type, out clip) && clip != null)
            {
                // Play directly using AudioSource.PlayClipAtPoint for minimal overhead
                AudioSource.PlayClipAtPoint(clip, position, vol);
                return;
            }

            // Fallback: Create temporary audio source (shouldn't happen after prewarming)
            GameObject temp = new GameObject("ProjectileHitSound");
            temp.transform.position = position;

            AudioSource source = temp.AddComponent<AudioSource>();
            source.spatialBlend = 0.5f; // Partial 3D
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = 30f;

            ProceduralProjectileHitAudio hitAudio =
                temp.AddComponent<ProceduralProjectileHitAudio>();
            hitAudio.volume = vol;
            hitAudio.soundType = type;
            hitAudio.PlayHitSound();

            Destroy(temp, 0.5f);
        }
    }
}
