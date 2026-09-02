using UnityEngine;

namespace BudgetGameDev.Shared.Rendering.Universal
{
    /// <summary>
    /// Realizes a light spec in Universal's units, which are arbitrary and anchored on where
    /// the grade puts diffuse white.
    /// </summary>
    public sealed class UniversalLightingFrontEnd : ILightingFrontEnd
    {
        public RenderPipelineKind Pipeline => RenderPipelineKind.Universal;

        public void ConfigurePunctual(Light light, in PunctualLightSpec spec, float paperWhiteNits)
        {
            if (light == null)
                return;

            light.type = LightType.Point;
            light.color = spec.Color;
            light.range = spec.RangeMeters;
            light.useColorTemperature = false;
            light.intensity = spec.UniversalIntensity(paperWhiteNits);
        }
    }
}
