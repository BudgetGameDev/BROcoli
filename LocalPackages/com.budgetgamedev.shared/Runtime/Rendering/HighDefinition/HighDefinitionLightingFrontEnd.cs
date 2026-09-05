using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace BudgetGameDev.Shared.Rendering.HighDefinition
{
    /// <summary>
    /// Realizes a light spec in High Definition's physical units. The spec is already stated
    /// photometrically, so this is the front end that needs no fudge factor: the candela go
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

            // Initialize HDRP data first: component initialization is allowed to change the
            // Light's defaults. The runtime intensity is candela even when the inspector is
            // displaying lumens (LightUnit only selects the inspector's unit).
            if (light.GetComponent<HDAdditionalLightData>() == null)
                light.gameObject.AddComponent<HDAdditionalLightData>();

            light.lightUnit = LightUnit.Candela;
            light.intensity = spec.LuminousIntensityCandela;
        }
    }
}
