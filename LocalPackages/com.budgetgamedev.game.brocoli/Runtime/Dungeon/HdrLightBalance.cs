using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Rebalances one light for native HDR output. An HDR display holds its blacks, so the flat
    /// fill an SDR grade needs to keep a dungeon readable only washes the stone out and leaves
    /// less room for the torches to pool colour on it. Scaling fill down and local light up here
    /// leaves the SDR scene exactly as authored.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class HdrLightBalance : MonoBehaviour
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip("What this light's authored intensity is multiplied by while HDR output is on.")]
        private float hdrIntensityScale = 1f;

        private Light balancedLight;
        private float authoredIntensity;

        private void Awake() => Cache();

        private void OnEnable()
        {
            GameDisplaySettings.ValuesChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameDisplaySettings.ValuesChanged -= Refresh;
            SetHdrBalance(false);
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
                authoredIntensity = balancedLight.intensity;
        }

        /// <summary>Test seam: adopts <paramref name="light"/> and its authored intensity.</summary>
        internal void Bind(Light light, float scale)
        {
            balancedLight = light;
            authoredIntensity = light == null ? 0f : light.intensity;
            hdrIntensityScale = Mathf.Max(scale, 0f);
        }

        internal void SetHdrBalance(bool hdrActive)
        {
            if (balancedLight == null)
                return;

            balancedLight.intensity = hdrActive
                ? authoredIntensity * Mathf.Max(hdrIntensityScale, 0f)
                : authoredIntensity;
        }
    }
}
