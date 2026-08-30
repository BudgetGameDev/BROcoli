using UnityEngine;

public sealed partial class CameraOcclusionFader
{
    /// <summary>
    /// One renderer's runtime fade, and the material copies that carry it. The
    /// shared material is never touched, so nothing a fade does outlives the
    /// object it was applied to.
    ///
    /// The cutoff is measured from the piece through
    /// <see cref="OcclusionFadeProfile"/>. Every occluder is treated alike:
    /// a wall run, an arch, and a prop nobody had thought of when this was
    /// written all keep the same fraction of their own height and fade what is
    /// above it.
    /// </summary>
    private sealed class FadeState
    {
        public readonly Renderer Renderer;
        public readonly Material[] OriginalMaterials;
        public readonly Material[] FadedMaterials;
        public readonly Color[] BaseColors;
        public readonly OcclusionFadeProfile Profile;
        public float Visibility = 1f;
        public bool UsingFadedMaterials;

        public FadeState(
            Renderer renderer,
            Shader fadeShader,
            float featherFraction,
            float visibleBaseFraction,
            float characterHeight
        )
        {
            Renderer = renderer;
            OriginalMaterials = renderer.sharedMaterials;
            FadedMaterials = new Material[OriginalMaterials.Length];
            BaseColors = new Color[OriginalMaterials.Length];
            Profile = ProfileFor(renderer, featherFraction, visibleBaseFraction, characterHeight);

            for (int i = 0; i < OriginalMaterials.Length; i++)
            {
                Material original = OriginalMaterials[i];
                if (original == null)
                    continue;

                Material faded;
                if (fadeShader != null)
                {
                    faded = new Material(fadeShader);
                    faded.CopyMatchingPropertiesFromMaterial(original);
                    faded.renderQueue = original.renderQueue;
                    faded.enableInstancing = original.enableInstancing;
                    faded.SetFloat(FadeStartYId, Profile.StartY);
                    faded.SetFloat(FadeFeatherId, Profile.Feather);
                    faded.SetFloat(OcclusionFadeId, 0f);
                    faded.SetShaderPassEnabled("ShadowCaster", true);
                }
                else
                {
                    faded = new Material(original);
                    ConfigureTransparent(faded);
                }
                faded.name = $"{original.name} (Occlusion Fade)";
                faded.hideFlags = HideFlags.DontSave;
                FadedMaterials[i] = faded;
                BaseColors[i] = ReadColor(original);
            }
        }

        /// <summary>
        /// The cutoff for one renderer. The occluder it belongs to gets to say
        /// what the piece is measured against - an arch borrows the wall run's
        /// reference so the two give way together - and otherwise the piece is
        /// measured by its own bounds.
        /// </summary>
        private static OcclusionFadeProfile ProfileFor(
            Renderer renderer,
            float featherFraction,
            float visibleBaseFraction,
            float characterHeight
        )
        {
            DungeonOccluder occluder = renderer.GetComponentInParent<DungeonOccluder>();
            if (
                occluder != null
                && occluder.TryGetFadeReference(renderer, out float minimumY, out float height)
            )
                return OcclusionFadeProfile.For(
                    minimumY,
                    height,
                    characterHeight,
                    visibleBaseFraction,
                    featherFraction
                );

            return OcclusionFadeProfile.For(
                renderer.bounds,
                characterHeight,
                visibleBaseFraction,
                featherFraction
            );
        }
    }
}
