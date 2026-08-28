using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class CameraOcclusionFader
{
    private static void ApplyVisibility(FadeState state)
    {
        for (int i = 0; i < state.FadedMaterials.Length; i++)
        {
            Material material = state.FadedMaterials[i];
            if (material == null)
                continue;

            if (material.HasProperty(OcclusionFadeId))
            {
                material.SetFloat(OcclusionFadeId, 1f - state.Visibility);
                continue;
            }

            Color color = state.BaseColors[i];
            color.a *= state.Visibility;
            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, color);
            else if (material.HasProperty(ColorId))
                material.SetColor(ColorId, color);
        }
    }

    private static Color ReadColor(Material material)
    {
        if (material.HasProperty(BaseColorId))
            return material.GetColor(BaseColorId);
        return material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
    }

    private static void ConfigureTransparent(Material material)
    {
        SetFloatIfPresent(material, "_Surface", 1f);
        SetFloatIfPresent(material, "_Blend", 0f);
        SetFloatIfPresent(material, "_AlphaClip", 0f);
        SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfPresent(material, "_SrcBlendAlpha", (float)BlendMode.One);
        SetFloatIfPresent(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        SetFloatIfPresent(material, "_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetShaderPassEnabled("ShadowCaster", true);
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property))
            material.SetFloat(property, value);
    }

    private static void DestroyFadedMaterials(FadeState state)
    {
        foreach (Material material in state.FadedMaterials)
        {
            if (material != null)
                Destroy(material);
        }
    }
}
