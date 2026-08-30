using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public static partial class SprayMaterialCreator
    {
        public enum BlendMode
        {
            Alpha,
            Additive,
            SoftAdditive,
            Multiply,
        }

        private static void ConfigureParticleBlending(Material mat, BlendMode mode)
        {
            switch (mode)
            {
                case BlendMode.Alpha:
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    break;
                case BlendMode.Additive:
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    break;
                case BlendMode.SoftAdditive:
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusDstColor);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    break;
                case BlendMode.Multiply:
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.DstColor);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    break;
            }

            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000; // Transparent queue
        }

        private static void SetMaterialColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            if (mat.HasProperty("_TintColor"))
                mat.SetColor("_TintColor", color);
        }

        private static void EnableSoftParticles(Material mat, float distance)
        {
            if (mat.HasProperty("_SoftParticlesEnabled"))
                mat.SetFloat("_SoftParticlesEnabled", 1f);
            if (mat.HasProperty("_SoftParticleFadeParams"))
                mat.SetVector("_SoftParticleFadeParams", new Vector4(0, distance, 0, 0));
            if (mat.HasProperty("_SoftParticlesNearFadeDistance"))
                mat.SetFloat("_SoftParticlesNearFadeDistance", 0f);
            if (mat.HasProperty("_SoftParticlesFarFadeDistance"))
                mat.SetFloat("_SoftParticlesFarFadeDistance", distance);
        }
    }
}
