using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Reserves HDR highlight energy for the visible flame silhouette. The shared materials
    /// retain their authored SDR values; HDR only boosts the compact primary particle layer.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class HdrTorchFlamePresentation : MonoBehaviour
    {
        internal const string PrimaryMaterialName = "DungeonTorchFirePrimary";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color HdrPrimaryColor = new(30f, 15f, 1.5f, 1f);

        private ParticleSystemRenderer[] flameRenderers;
        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            CacheRenderers();
        }

        private void OnEnable()
        {
            GameDisplaySettings.ValuesChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameDisplaySettings.ValuesChanged -= Refresh;
            SetHdrPresentation(false);
        }

        private void Refresh()
        {
            SetHdrPresentation(GameDisplaySettings.HdrEnabled && GameDisplaySettings.IsHdrActive);
        }

        internal void SetHdrPresentation(bool hdrActive)
        {
            if (flameRenderers == null)
                CacheRenderers();

            foreach (ParticleSystemRenderer flameRenderer in flameRenderers)
            {
                if (flameRenderer == null || !IsPrimaryFlame(flameRenderer.sharedMaterial))
                    continue;

                if (!hdrActive)
                {
                    flameRenderer.SetPropertyBlock(null);
                    continue;
                }

                flameRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, HdrPrimaryColor);
                propertyBlock.SetColor(ColorId, HdrPrimaryColor);
                flameRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        internal static bool IsPrimaryFlame(Material material)
        {
            return material != null && material.name == PrimaryMaterialName;
        }

        private void CacheRenderers()
        {
            flameRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
            propertyBlock ??= new MaterialPropertyBlock();
        }
    }
}
