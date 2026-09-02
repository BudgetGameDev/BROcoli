using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace BudgetGameDev.Shared.Rendering.HighDefinition
{
    /// <summary>
    /// Realizes a light spec in High Definition's physical units. The spec is already stated
    /// photometrically, so this is the front end that needs no fudge factor: the lumens go
    /// straight in, and the pipeline's own exposure decides what they look like.
    /// </summary>
    public sealed class HighDefinitionLightingFrontEnd : ILightingFrontEnd
    {
        public RenderPipelineKind Pipeline => RenderPipelineKind.HighDefinition;

        public void ConfigurePunctual(Light light, in PunctualLightSpec spec, float paperWhiteNits)
        {
            if (light == null)
                return;

            light.type = LightType.Point;
            light.color = spec.Color;
            light.range = spec.RangeMeters;
            light.useColorTemperature = false;

            // Lumens, not the grade's nits: High Definition meters the scene itself, so the
            // light is stated physically and the exposure on the pipeline's volume profile is
            // what pins the result to the same ladder Universal is graded onto.
            light.lightUnit = LightUnit.Lumen;
            light.intensity = spec.LuminousFluxLumens;

            // The pipeline needs its own component on the light before it will honour any of
            // this; adding it here means gameplay can spawn a bare Light and still be lit
            // physically.
            if (light.GetComponent<HDAdditionalLightData>() == null)
                light.gameObject.AddComponent<HDAdditionalLightData>();
        }
    }
}
