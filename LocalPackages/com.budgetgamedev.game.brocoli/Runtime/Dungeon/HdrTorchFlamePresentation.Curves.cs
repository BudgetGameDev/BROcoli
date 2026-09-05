using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    internal sealed partial class HdrTorchFlamePresentation
    {
        /// <summary>
        /// Resamples <paramref name="source"/> with its alpha raised to <paramref name="exponent"/>
        /// about its own peak, so the brightest point is untouched and everything dimmer falls
        /// away faster. Gradients hold eight keys, which is enough to carry the curve.
        /// </summary>
        internal static Gradient Steepen(Gradient source, float exponent)
        {
            const int Keys = 8;
            float peak = 0f;
            foreach (GradientAlphaKey key in source.alphaKeys)
                peak = Mathf.Max(peak, key.alpha);
            if (peak <= 0f)
                return source;

            GradientAlphaKey[] alphas = new GradientAlphaKey[Keys];
            float peakTime = 0f;
            foreach (GradientAlphaKey key in source.alphaKeys)
                if (key.alpha >= peak)
                {
                    peakTime = key.time;
                    break;
                }
            int peakIndex =
                peakTime <= 0f ? 0
                : peakTime >= 1f ? Keys - 1
                : Mathf.Clamp(Mathf.RoundToInt(peakTime * (Keys - 1)), 1, Keys - 2);
            for (int index = 0; index < Keys; index++)
            {
                // Keep the exact ignition peak. A uniform resample can miss a short peak
                // between its samples and crush the hot core under a large HDR boost.
                float time = index == peakIndex ? peakTime : index / (float)(Keys - 1);
                float alpha = source.Evaluate(time).a;
                alphas[index] = new GradientAlphaKey(
                    peak * Mathf.Pow(Mathf.Clamp01(alpha / peak), exponent),
                    time
                );
            }

            Gradient steepened = new();
            steepened.SetKeys(source.colorKeys, alphas);
            return steepened;
        }

        private static bool TryReadGradient(
            ParticleSystem.MinMaxGradient source,
            out Gradient gradient
        )
        {
            switch (source.mode)
            {
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.RandomColor:
                    gradient = source.gradient;
                    return gradient != null;
                case ParticleSystemGradientMode.TwoGradients:
                    gradient = source.gradientMax;
                    return gradient != null;
                default:
                    gradient = null;
                    return false;
            }
        }
    }
}
