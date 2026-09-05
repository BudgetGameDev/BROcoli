using UnityEngine;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>
    /// A light stated by what it does to the surfaces around it, rather than in either
    /// pipeline's intensity units. Universal counts in arbitrary numbers and High Definition
    /// counts in candela, so a torch authored for one is wrong on the other; both can, however,
    /// answer the same question -- how bright does this make the stone two metres away.
    ///
    /// This is deliberately not the flame's own brightness. The fire is emissive geometry and
    /// says how bright the fire looks; this says how much light reaches the cobblestones. They
    /// are tuned against each other but neither is derived from the other, so a flame can be
    /// pushed to read hotter without washing out the floor, and the floor can be lit further
    /// without the fire turning into a white blob.
    /// </summary>
    public readonly struct PunctualLightSpec
    {
        /// <summary>
        /// The albedo the target luminance is quoted against. The dungeon's cobblestone is
        /// dark, and quoting against a white surface would make every torch far too dim.
        /// </summary>
        public const float ReferenceAlbedo = 0.18f;

        /// <summary>
        /// The luminance a Lambertian surface of <see cref="ReferenceAlbedo"/>, facing the
        /// light square on, should reach at <see cref="ReferenceDistanceMeters"/>.
        /// </summary>
        public float TargetLuminanceNits { get; }

        /// <summary>Where <see cref="TargetLuminanceNits"/> is measured, in metres.</summary>
        public float ReferenceDistanceMeters { get; }

        /// <summary>How far the light reaches before it is cut off, in metres.</summary>
        public float RangeMeters { get; }

        /// <summary>The light's hue.</summary>
        public Color Color { get; }

        public PunctualLightSpec(
            float targetLuminanceNits,
            float referenceDistanceMeters,
            float rangeMeters,
            Color color
        )
        {
            TargetLuminanceNits = targetLuminanceNits;
            ReferenceDistanceMeters = referenceDistanceMeters;
            RangeMeters = rangeMeters;
            Color = color;
        }

        /// <summary>
        /// A torch: warm, close-range, and sized so the stone around it lands on the ladder's
        /// torch-lit step rather than on diffuse white.
        /// </summary>
        public static PunctualLightSpec Torch(SceneLuminanceBudget budget) =>
            new PunctualLightSpec(
                targetLuminanceNits: budget.TorchLitStoneNits,
                referenceDistanceMeters: 2f,
                rangeMeters: 9f,
                color: new Color(1f, 0.62f, 0.28f)
            );

        /// <summary>
        /// The illuminance this light puts on a surface at its reference distance, in lux.
        /// A Lambertian surface of albedo p under illuminance E has luminance E*p/pi, so the
        /// wanted luminance inverts to E = pi*L/p.
        /// </summary>
        public float ReferenceIlluminanceLux =>
            Mathf.PI * Mathf.Max(0f, TargetLuminanceNits) / ReferenceAlbedo;

        /// <summary>
        /// The luminous intensity that produces <see cref="ReferenceIlluminanceLux"/> at the
        /// reference distance, in candela. Illuminance falls with the square of distance, so
        /// I = E * d^2.
        /// </summary>
        public float LuminousIntensityCandela =>
            ReferenceIlluminanceLux * ReferenceDistanceMeters * ReferenceDistanceMeters;

        /// <summary>
        /// The same light as a total luminous flux, in lumens, for inspection. Unity's runtime
        /// Light.intensity remains candela; a point source radiates over the whole sphere, so the flux
        /// is its intensity times 4*pi steradians.
        /// </summary>
        public float LuminousFluxLumens => LuminousIntensityCandela * 4f * Mathf.PI;

        /// <summary>
        /// The same light in Universal's units, given the fixed scene authoring white.
        /// Universal's Lambert term is albedo * intensity * attenuation, with the 1/pi folded
        /// into its intensity convention and with scene-linear 1.0 meaning authoring white; so an
        /// intensity of L*d^2/(p*paperWhite) lands the reference surface on L nits.
        /// </summary>
        public float UniversalIntensity(float paperWhiteNits)
        {
            if (paperWhiteNits <= 0f)
                return 0f;

            return TargetLuminanceNits
                * ReferenceDistanceMeters
                * ReferenceDistanceMeters
                / (ReferenceAlbedo * paperWhiteNits);
        }
    }
}
