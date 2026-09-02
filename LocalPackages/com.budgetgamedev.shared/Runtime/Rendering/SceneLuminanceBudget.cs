using UnityEngine;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>
    /// The luminance ladder a scene is authored against, in nits. Both front ends grade
    /// towards these numbers, so a surface reads at the same brightness whether Universal
    /// or High Definition renders it.
    ///
    /// Two separate things are described here and they must not be conflated. A flame's
    /// <em>emissive</em> luminance is how bright the fire itself looks; the torch light's
    /// intensity is how much illumination reaches the cobblestones. A flame can sit at 300
    /// nits while lighting stone to 30, and pushing one has no business dragging the other
    /// along.
    /// </summary>
    public readonly struct SceneLuminanceBudget
    {
        /// <summary>Pitch-black recesses the eye should read as absence of light.</summary>
        public float RecessNits { get; }

        /// <summary>Distant cobblestones at the edge of visibility.</summary>
        public float DistantSurfaceNits { get; }

        /// <summary>The shadow side of objects close to the camera.</summary>
        public float ShadowSideNits { get; }

        /// <summary>Stone standing in a torch's pool of light.</summary>
        public float TorchLitStoneNits { get; }

        /// <summary>An ordinary brightly lit diffuse surface: the scene's diffuse white.</summary>
        public float DiffuseWhiteNits { get; }

        /// <summary>The body of a flame, well above diffuse white but not the peak.</summary>
        public float FlameBodyNits { get; }

        /// <summary>The hottest core, a specular glint, or a spark. Clipped by the display.</summary>
        public float FlamePeakNits { get; }

        public SceneLuminanceBudget(
            float recessNits,
            float distantSurfaceNits,
            float shadowSideNits,
            float torchLitStoneNits,
            float diffuseWhiteNits,
            float flameBodyNits,
            float flamePeakNits
        )
        {
            RecessNits = recessNits;
            DistantSurfaceNits = distantSurfaceNits;
            ShadowSideNits = shadowSideNits;
            TorchLitStoneNits = torchLitStoneNits;
            DiffuseWhiteNits = diffuseWhiteNits;
            FlameBodyNits = flameBodyNits;
            FlamePeakNits = flamePeakNits;
        }

        /// <summary>
        /// The dungeon's ladder. Torchlight carries the whole scene, so almost everything
        /// sits below diffuse white and the flames are the only things above it.
        /// </summary>
        public static SceneLuminanceBudget Dungeon { get; } =
            new SceneLuminanceBudget(
                recessNits: 0.05f,
                distantSurfaceNits: 1.75f,
                shadowSideNits: 6f,
                torchLitStoneNits: 30f,
                diffuseWhiteNits: 85f,
                flameBodyNits: 300f,
                flamePeakNits: 800f
            );

        /// <summary>
        /// Converts an authored nit value into the scene-linear number a shader writes,
        /// given where the grade puts diffuse white. Scene-linear 1.0 is paper white by
        /// definition, so a flame body at 300 nits against 200-nit paper white is 1.5.
        /// </summary>
        public static float NitsToSceneLinear(float nits, float paperWhiteNits) =>
            paperWhiteNits <= 0f ? 0f : Mathf.Max(0f, nits) / paperWhiteNits;

        /// <summary>The inverse of <see cref="NitsToSceneLinear"/>.</summary>
        public static float SceneLinearToNits(float sceneLinear, float paperWhiteNits) =>
            Mathf.Max(0f, sceneLinear) * Mathf.Max(0f, paperWhiteNits);

        /// <summary>
        /// The fixed exposure, in EV100, that puts <see cref="DiffuseWhiteNits"/> where the
        /// display expects it. High Definition meters the scene physically, so a pipeline
        /// told the dungeon's lights in lumens still needs to be told what counts as a
        /// correct exposure for them; left at zero it renders the whole dungeon nine stops
        /// hot and pure white.
        ///
        /// This is the standard photometric relation, EV100 = log2(L * S / K), with the
        /// film speed S at 100 and the reflected-light meter constant K at 12.5.
        /// </summary>
        public float FixedExposureEv100 => Ev100For(DiffuseWhiteNits);

        /// <summary>The exposure that renders a surface of <paramref name="nits"/> correctly.</summary>
        public static float Ev100For(float nits) =>
            Mathf.Log(Mathf.Max(nits, 1e-4f) * ReflectedLightMeterSpeed, 2f);

        /// <summary>Film speed over meter constant, 100 / 12.5, folded into one number.</summary>
        private const float ReflectedLightMeterSpeed = 8f;
    }
}
