using UnityEngine;

public static partial class SprayMaterialCreator
{
    /// <summary>
    /// Create a procedural soft circle texture for particles
    /// </summary>
    public static Texture2D CreateSoftCircleTexture(int size = 64)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        float maxDist = center;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float normalizedDist = dist / maxDist;

                // Soft falloff with bright center
                float alpha = 1f - Mathf.Pow(normalizedDist, 1.5f);
                alpha = Mathf.Clamp01(alpha);

                // Add slight rim brightening for refraction effect
                float rim = Mathf.Pow(normalizedDist, 3f) * 0.3f;
                float brightness = 1f + rim;

                pixels[y * size + x] = new Color(brightness, brightness, brightness, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Create a droplet texture with highlight for refraction look
    /// </summary>
    public static Texture2D CreateDropletTexture(int size = 32)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        float maxDist = center * 0.9f;

        // Offset for highlight
        float highlightX = center - size * 0.15f;
        float highlightY = center + size * 0.15f;
        float highlightRadius = size * 0.2f;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > maxDist)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }

                // Base droplet with soft edge
                float edgeDist = (maxDist - dist) / (maxDist * 0.2f);
                float alpha = Mathf.Clamp01(edgeDist);

                // Highlight (refraction simulation)
                float hdx = x - highlightX;
                float hdy = y - highlightY;
                float hDist = Mathf.Sqrt(hdx * hdx + hdy * hdy);
                float highlight = 1f - Mathf.Clamp01(hDist / highlightRadius);
                highlight = Mathf.Pow(highlight, 2f) * 0.8f;

                // Slight darkening at edges (Fresnel-like)
                float normalizedDist = dist / maxDist;
                float fresnel = Mathf.Pow(normalizedDist, 2f) * 0.3f;

                float brightness = 0.9f + highlight - fresnel;
                pixels[y * size + x] = new Color(brightness, brightness, brightness, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
