using UnityEngine;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>
    /// States a light by what it should do to the surfaces around it, and lets the active
    /// pipeline's front end work out the number.
    ///
    /// A torch authored as "intensity 7.5" means one thing to Universal and something else
    /// entirely to High Definition, which counts in candela. Authored as "put a dark stone
    /// two metres away at thirty nits" it means the same thing to both, which is the only
    /// way one dungeon can be lit once and rendered by either.
    ///
    /// This is deliberately not the flame's brightness. The fire is emissive geometry and is
    /// authored separately; nothing here should ever be derived from it, or pushing one will
    /// start dragging the other along.
    ///
    /// It runs early so that anything caching the light's intensity -- a flicker, a fade --
    /// reads the value the front end just wrote rather than whatever the prefab was saved
    /// with.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    [DefaultExecutionOrder(-100)]
    public sealed class PhysicalPunctualLight : MonoBehaviour
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip("Nits a mid-grey surface should reach at the reference distance.")]
        private float targetLuminanceNits = 30f;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Where the target luminance is measured, in metres.")]
        private float referenceDistanceMeters = 2f;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("How far the light reaches before it is cut off, in metres.")]
        private float rangeMeters = 9f;

        [SerializeField]
        [Tooltip("The light's hue. Warm for a torch.")]
        private Color lightColor = new Color(1f, 0.62f, 0.28f);

        private void Awake() => Apply();

        /// <summary>
        /// Pushes this light's spec through the active front end. A build whose pipeline
        /// registers no lighting front end keeps whatever the prefab was authored with,
        /// which is the right fallback: a light of roughly the wrong brightness beats no
        /// light at all.
        /// </summary>
        public void Apply() => Apply(RenderPipelineFrontEnd.Lighting);

        internal void Apply(ILightingFrontEnd lighting)
        {
            if (lighting == null)
                return;

            lighting.ConfigurePunctual(
                GetComponent<Light>(),
                Spec,
                SceneLuminanceBudget.AuthoringPaperWhiteNits
            );
        }

        /// <summary>What this light is being asked to do.</summary>
        public PunctualLightSpec Spec =>
            new PunctualLightSpec(
                targetLuminanceNits,
                referenceDistanceMeters,
                rangeMeters,
                lightColor
            );

        /// <summary>Test seam: sets the spec without a serialized object behind it.</summary>
        internal void Configure(PunctualLightSpec spec)
        {
            targetLuminanceNits = spec.TargetLuminanceNits;
            referenceDistanceMeters = spec.ReferenceDistanceMeters;
            rangeMeters = spec.RangeMeters;
            lightColor = spec.Color;
        }
    }
}
