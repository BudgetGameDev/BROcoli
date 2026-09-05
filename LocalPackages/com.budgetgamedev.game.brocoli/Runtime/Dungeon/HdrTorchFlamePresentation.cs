using System.Collections.Generic;
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
    internal sealed partial class HdrTorchFlamePresentation : MonoBehaviour
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
        private readonly Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient> authoredFades =
            new();

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
                    RestoreFade(flameRenderer.GetComponent<ParticleSystem>());
                    continue;
                }

                ParticleSystem particles = flameRenderer.GetComponent<ParticleSystem>();
                Color color = MaterialColorForPeak(peak, PeakParticleAlpha(particles));
                flameRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, color);
                propertyBlock.SetColor(ColorId, color);
                flameRenderer.SetPropertyBlock(propertyBlock);
                SteepenFade(particles, BoostOver(flameRenderer.sharedMaterial, color));
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
            PeakAlpha(gradient, gradient.mode);

        internal static float PeakAlpha(
            ParticleSystem.MinMaxGradient gradient,
            ParticleSystemGradientMode mode
        ) =>
            mode switch
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

        internal static bool TryReadGradientForTests(
            ParticleSystem.MinMaxGradient source,
            out Gradient gradient
        ) => TryReadGradient(source, out gradient);

        private static float PeakAlpha(Gradient gradient)
        {
            if (gradient == null)
                return 1f;

            float alpha = 0f;
            foreach (GradientAlphaKey key in gradient.alphaKeys)
                alpha = Mathf.Max(alpha, key.alpha);
            return alpha;
        }

        /// <summary>
        /// How much brighter than its authored self the flame material has been driven. The whole
        /// plume is one colour times each particle's alpha, so this boost lands on the faded
        /// particles at the edge as much as on the young ones at the core.
        /// </summary>
        internal static float BoostOver(Material authored, Color boosted)
        {
            if (authored == null || !authored.HasProperty(BaseColorId))
                return 1f;

            Color source = authored.GetColor(BaseColorId);
            float from = Mathf.Max(source.r, Mathf.Max(source.g, source.b));
            float to = Mathf.Max(boosted.r, Mathf.Max(boosted.g, boosted.b));
            return from <= 0f ? 1f : Mathf.Max(to / from, 1f);
        }

        /// <summary>
        /// Bends the particle's fade so the boost reaches only the hottest particles. Without it
        /// the faded tail of the plume is lifted just as far as the core and reads as a lit orb
        /// hanging around the flame; the core is what should be spending the display's range.
        /// </summary>
        internal void SteepenFade(ParticleSystem particles, float boost)
        {
            if (particles == null)
                return;
            if (boost <= 1f)
            {
                RestoreFade(particles);
                return;
            }

            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            if (!fade.enabled)
                return;

            // Always start from the original fade. Calibration changes can refresh this
            // repeatedly; bending the previously bent curve progressively erases the flame.
            if (!authoredFades.TryGetValue(particles, out var original))
                original = SnapshotFade(fade.color);
            if (!TryReadGradient(original, out Gradient authored))
                return;
            authoredFades[particles] = original;

            // Cancelling the boost by the time a particle is half faded keeps the tail at the
            // brightness SDR draws it at while the young particles keep every bit of the boost.
            float exponent = 1f + (Mathf.Log(boost) / Mathf.Log(2f));
            fade.color = new ParticleSystem.MinMaxGradient(Steepen(authored, exponent));
        }

        private static ParticleSystem.MinMaxGradient SnapshotFade(
            ParticleSystem.MinMaxGradient source
        )
        {
            // MinMaxGradient is a struct but its Gradient values can reference the native
            // particle module. Copy the curves before assigning a new fade to that module.
            source.gradientMin = SnapshotGradient(source.gradientMin);
            source.gradientMax = SnapshotGradient(source.gradientMax);
            return source;
        }

        private static Gradient SnapshotGradient(Gradient source)
        {
            if (source == null)
                return null;
            Gradient copy = new() { mode = source.mode };
            copy.SetKeys(source.colorKeys, source.alphaKeys);
            return copy;
        }

        private void RestoreFade(ParticleSystem particles)
        {
            if (particles == null || !authoredFades.TryGetValue(particles, out var authored))
                return;

            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.color = authored;
            authoredFades.Remove(particles);
        }

        private void CacheRenderers()
        {
            flameRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
            propertyBlock ??= new MaterialPropertyBlock();
        }
    }
}
