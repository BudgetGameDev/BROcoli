using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Organic flame flicker for the dungeon's torch lights: Perlin-noise
    /// intensity wavering with a slight positional wobble, each torch offset so
    /// a room of torches never pulses in sync.
    /// </summary>
    [DisallowMultipleComponent]
    public class TorchFlicker : MonoBehaviour
    {
        [SerializeField]
        private Light torchLight;

        [SerializeField, Range(0f, 1f)]
        private float flickerAmount = 0.35f;

        [SerializeField, Min(0.1f)]
        private float flickerSpeed = 7f;

        private float baseIntensity;
        private Vector3 basePosition;
        private float noiseSeed;

        private void Awake()
        {
            if (torchLight == null)
                torchLight = GetComponentInChildren<Light>();
            if (torchLight != null)
            {
                baseIntensity = torchLight.intensity;
                basePosition = torchLight.transform.localPosition;
            }
            noiseSeed = Random.value * 100f;
        }

        private void Update()
        {
            if (torchLight == null)
                return;

            float t = Time.time * flickerSpeed;
            float noise = Mathf.PerlinNoise(t, noiseSeed);
            torchLight.intensity =
                baseIntensity * (1f - flickerAmount * 0.5f + flickerAmount * noise);
            torchLight.transform.localPosition =
                basePosition
                + new Vector3(
                    (Mathf.PerlinNoise(t, noiseSeed + 10f) - 0.5f) * 0.08f,
                    (Mathf.PerlinNoise(t, noiseSeed + 20f) - 0.5f) * 0.05f,
                    (Mathf.PerlinNoise(t, noiseSeed + 30f) - 0.5f) * 0.08f
                );
        }
    }
}
