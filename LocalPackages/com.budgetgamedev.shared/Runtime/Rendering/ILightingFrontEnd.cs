using UnityEngine;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>
    /// One render pipeline's way of realizing a <see cref="PunctualLightSpec"/>. Gameplay
    /// spawns torches by saying what they should light, and the active front end turns that
    /// into whatever units its pipeline counts in.
    /// </summary>
    public interface ILightingFrontEnd
    {
        /// <summary>The pipeline this front end lights for.</summary>
        RenderPipelineKind Pipeline { get; }

        /// <summary>
        /// Configures <paramref name="light"/> to match <paramref name="spec"/>.
        /// <paramref name="paperWhiteNits"/> is where the grade puts diffuse white, which is
        /// what ties the spec's nits to a pipeline that has no physical units.
        /// </summary>
        void ConfigurePunctual(Light light, in PunctualLightSpec spec, float paperWhiteNits);
    }
}
