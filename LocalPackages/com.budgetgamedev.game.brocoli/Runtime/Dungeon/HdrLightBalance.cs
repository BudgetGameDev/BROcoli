using BudgetGameDev.Shared;
using BudgetGameDev.Shared.Rendering;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Rebalances one light independently for the rendering pipeline and native HDR output.
    /// HDRP can shorten and dim broad fill without changing the nearby character light or
    /// emissive flames. Native HDR balancing then applies on top of that pipeline baseline.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class HdrLightBalance : MonoBehaviour
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip("What this light's authored intensity is multiplied by while HDR output is on.")]
        private float hdrIntensityScale = 1f;

        [SerializeField, Min(0f)]
        [Tooltip("Additional intensity scale in HDRP, for both SDR and HDR displays.")]
        private float highDefinitionIntensityScale = 1f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip(
            "HDRP range relative to the authored range. Shortens broad fill without changing local highlight lights."
        )]
        private float highDefinitionRangeScale = 1f;

        private Light balancedLight;
        private float authoredIntensity;
        private float authoredRange;

        private void Awake() => Cache();

        private void OnEnable()
        {
            GameDisplaySettings.ValuesChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameDisplaySettings.ValuesChanged -= Refresh;
            SetBalance(false, false);
        }

        private void Refresh()
        {
            Cache();
            SetHdrBalance(GameDisplaySettings.HdrEnabled && GameDisplaySettings.IsHdrActive);
        }

        private void Cache()
        {
            if (balancedLight != null)
                return;

            balancedLight = GetComponent<Light>();
            if (balancedLight != null)
            {
                authoredIntensity = balancedLight.intensity;
                authoredRange = balancedLight.range;
            }
        }

        /// <summary>Test seam: adopts <paramref name="light"/> and its authored intensity.</summary>
        internal void Bind(
            Light light,
            float scale,
            float pipelineScale = 1f,
            float rangeScale = 1f
        )
        {
            balancedLight = light;
            authoredIntensity = light == null ? 0f : light.intensity;
            authoredRange = light == null ? 0f : light.range;
            hdrIntensityScale = Mathf.Max(scale, 0f);
            highDefinitionIntensityScale = Mathf.Max(pipelineScale, 0f);
            highDefinitionRangeScale = Mathf.Clamp(rangeScale, 0.01f, 1f);
        }

        internal void SetHdrBalance(bool hdrActive)
        {
            SetBalance(hdrActive, RenderPipelineProbe.Current == RenderPipelineKind.HighDefinition);
        }

        internal void SetBalance(bool hdrActive, bool highDefinition)
        {
            if (balancedLight == null)
                return;

            balancedLight.intensity =
                authoredIntensity
                * (hdrActive ? Mathf.Max(hdrIntensityScale, 0f) : 1f)
                * (highDefinition ? Mathf.Max(highDefinitionIntensityScale, 0f) : 1f);
            balancedLight.range =
                authoredRange
                * (highDefinition ? Mathf.Clamp(highDefinitionRangeScale, 0.01f, 1f) : 1f);
        }
    }
}
