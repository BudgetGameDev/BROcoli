using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>Applies per-renderer colors without instantiating or modifying materials.</summary>
    internal static class EnemyRendererColor
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        internal static Color Get(Renderer renderer, MaterialPropertyBlock properties)
        {
            renderer.GetPropertyBlock(properties);
            if (properties.HasColor(BaseColorId))
                return properties.GetColor(BaseColorId);
            if (properties.HasColor(ColorId))
                return properties.GetColor(ColorId);

            Material sharedMaterial = renderer.sharedMaterial;
            if (sharedMaterial == null)
                return Color.white;
            if (sharedMaterial.HasProperty(BaseColorId))
                return sharedMaterial.GetColor(BaseColorId);
            if (sharedMaterial.HasProperty(ColorId))
                return sharedMaterial.GetColor(ColorId);

            return Color.white;
        }

        internal static void Set(Renderer renderer, MaterialPropertyBlock properties, Color color)
        {
            renderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColorId, color);
            properties.SetColor(ColorId, color);
            renderer.SetPropertyBlock(properties);
        }
    }
}
