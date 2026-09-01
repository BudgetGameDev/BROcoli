using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Drives the visible flame silhouette to the display's calibrated peak brightness while HDR
    /// output is active. The shared materials retain their authored SDR values; only the compact
    /// primary particle layer is re-authored, so the torch is the one thing in the dungeon that
    /// spends the display's highlight range.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class HdrTorchFlamePresentation : MonoBehaviour
    {
        internal const string PrimaryMaterialName = "DungeonTorchFirePrimary";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>
        /// The flame's colour at the peak, chosen so the tone map renders the hottest particles
        /// at the hue the SDR grade renders them at. Only the direction matters; the calibration
        /// decides the length.
        /// </summary>
        private static readonly Color FlameHue = new(1f, 0.451f, 0.053f);

        /// <summary>
        /// Particles that fade out entirely would ask for an unbounded material colour, so the
        /// alpha the flame is authored against is never taken below this.
        /// </summary>
        private const float MinimumParticleAlpha = 0.05f;

        private ParticleSystemRenderer[] flameRenderers;
        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            CacheRenderers();
        }

        private void OnEnable()
        {
            GameDisplaySettings.ValuesChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameDisplaySettings.ValuesChanged -= Refresh;
            SetHdrPresentation(false);
        }

        private void Refresh()
        {
            SetHdrPresentation(GameDisplaySettings.HdrEnabled && GameDisplaySettings.IsHdrActive);
        }

        internal void SetHdrPresentation(bool hdrActive)
        {
            if (flameRenderers == null)
                CacheRenderers();

            Color peak = hdrActive
                ? GameDisplaySettings.HdrSceneColorAtPeakBrightness(FlameHue)
                : Color.black;
            foreach (ParticleSystemRenderer flameRenderer in flameRenderers)
            {
                if (flameRenderer == null || !IsPrimaryFlame(flameRenderer.sharedMaterial))
                    continue;

                if (!hdrActive)
                {
                    flameRenderer.SetPropertyBlock(null);
                    continue;
                }

                Color color = MaterialColorForPeak(
                    peak,
                    PeakParticleAlpha(flameRenderer.GetComponent<ParticleSystem>())
                );
                flameRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, color);
                propertyBlock.SetColor(ColorId, color);
                flameRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        internal static bool IsPrimaryFlame(Material material)
        {
            return material != null && material.name == PrimaryMaterialName;
        }

        /// <summary>
        /// The flame blends additively through the particle alpha, so the material has to be
        /// authored that much brighter for the hottest particles to land on the wanted colour.
        /// </summary>
        internal static Color MaterialColorForPeak(Color peak, float particleAlpha)
        {
            float scale = 1f / Mathf.Max(particleAlpha, MinimumParticleAlpha);
            return new Color(peak.r * scale, peak.g * scale, peak.b * scale, 1f);
        }

        /// <summary>The alpha the brightest particle of a system is drawn with.</summary>
        internal static float PeakParticleAlpha(ParticleSystem particles)
        {
            if (particles == null)
                return 1f;

            float alpha = PeakAlpha(particles.main.startColor);
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            if (fade.enabled)
                alpha *= PeakAlpha(fade.color);
            return Mathf.Clamp(alpha, MinimumParticleAlpha, 1f);
        }

        private static float PeakAlpha(ParticleSystem.MinMaxGradient gradient) =>
            gradient.mode switch
            {
                ParticleSystemGradientMode.Color => gradient.color.a,
                ParticleSystemGradientMode.TwoColors => Mathf.Max(
                    gradient.colorMin.a,
                    gradient.colorMax.a
                ),
                ParticleSystemGradientMode.Gradient or ParticleSystemGradientMode.RandomColor =>
                    PeakAlpha(gradient.gradient),
                ParticleSystemGradientMode.TwoGradients => Mathf.Max(
                    PeakAlpha(gradient.gradientMin),
                    PeakAlpha(gradient.gradientMax)
                ),
                _ => 1f,
            };

        private static float PeakAlpha(Gradient gradient)
        {
            if (gradient == null)
                return 1f;

            float alpha = 0f;
            foreach (GradientAlphaKey key in gradient.alphaKeys)
                alpha = Mathf.Max(alpha, key.alpha);
            return alpha;
        }

        private void CacheRenderers()
        {
            flameRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
            propertyBlock ??= new MaterialPropertyBlock();
        }
    }
}
