using UnityEngine;

/// <summary>
/// Creates PBR-style materials for realistic spray particle effects.
/// Handles reflection, refraction simulation, and lighting interaction.
/// </summary>
public static class SprayMaterialCreator
{
    // Cached materials
    private static Material _sprayCoreMaterial;
    private static Material _sprayMistMaterial;
    private static Material _sprayDropletMaterial;
    private static Material _sprayGlowMaterial;
    
    /// <summary>
    /// Get or create the main spray core material (dense center spray)
    /// </summary>
    public static Material GetSprayCoreMaterial()
    {
        if (_sprayCoreMaterial != null) return _sprayCoreMaterial;
        
        // Try to use URP Lit particle shader for PBR, fallback to standard
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        
        _sprayCoreMaterial = new Material(shader);
        _sprayCoreMaterial.name = "SprayCoreMaterial";
        
        // Configure for additive blending with transparency
        ConfigureParticleBlending(_sprayCoreMaterial, BlendMode.Additive);
        
        // Set base color - bright white-blue core
        Color coreColor = new Color(0.9f, 0.95f, 1f, 0.7f);
        SetMaterialColor(_sprayCoreMaterial, coreColor);
        
        // Enable soft particles for depth blending
        EnableSoftParticles(_sprayCoreMaterial, 0.5f);
        
        return _sprayCoreMaterial;
    }
    
    /// <summary>
    /// Get or create the mist/fog material (outer spray cloud)
    /// </summary>
    public static Material GetSprayMistMaterial()
    {
        if (_sprayMistMaterial != null) return _sprayMistMaterial;
        
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        
        _sprayMistMaterial = new Material(shader);
        _sprayMistMaterial.name = "SprayMistMaterial";
        
        // Soft additive for fog effect
        ConfigureParticleBlending(_sprayMistMaterial, BlendMode.SoftAdditive);
        
        // Softer, more transparent mist
        Color mistColor = new Color(0.8f, 0.9f, 1f, 0.3f);
        SetMaterialColor(_sprayMistMaterial, mistColor);
        
        EnableSoftParticles(_sprayMistMaterial, 1f);
        
        return _sprayMistMaterial;
    }
    
    /// <summary>
    /// Get or create the droplet material (individual visible droplets)
    /// </summary>
    public static Material GetSprayDropletMaterial()
    {
        if (_sprayDropletMaterial != null) return _sprayDropletMaterial;
        
        // Try to get Lit shader for PBR reflections
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Surface");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        
        _sprayDropletMaterial = new Material(shader);
        _sprayDropletMaterial.name = "SprayDropletMaterial";
        
        // Alpha blend for solid droplets
        ConfigureParticleBlending(_sprayDropletMaterial, BlendMode.Alpha);
        
        // Brighter droplets that catch light
        Color dropletColor = new Color(1f, 1f, 1f, 0.85f);
        SetMaterialColor(_sprayDropletMaterial, dropletColor);
        
        // Configure metallic/smoothness for reflections
        if (_sprayDropletMaterial.HasProperty("_Metallic"))
            _sprayDropletMaterial.SetFloat("_Metallic", 0.1f);
        if (_sprayDropletMaterial.HasProperty("_Smoothness"))
            _sprayDropletMaterial.SetFloat("_Smoothness", 0.95f);
        if (_sprayDropletMaterial.HasProperty("_Glossiness"))
            _sprayDropletMaterial.SetFloat("_Glossiness", 0.95f);
        
        EnableSoftParticles(_sprayDropletMaterial, 0.3f);
        
        return _sprayDropletMaterial;
    }
    
    /// <summary>
    /// Get or create the glow/highlight material (bright center highlights)
    /// </summary>
    public static Material GetSprayGlowMaterial()
    {
        if (_sprayGlowMaterial != null) return _sprayGlowMaterial;
        
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        
        _sprayGlowMaterial = new Material(shader);
        _sprayGlowMaterial.name = "SprayGlowMaterial";
        
        // Strong additive for glow
        ConfigureParticleBlending(_sprayGlowMaterial, BlendMode.Additive);
        
        // Bright white glow
        Color glowColor = new Color(1f, 1f, 1f, 0.5f);
        SetMaterialColor(_sprayGlowMaterial, glowColor);
        
        // Extra HDR intensity for bloom
        if (_sprayGlowMaterial.HasProperty("_EmissionColor"))
        {
            _sprayGlowMaterial.EnableKeyword("_EMISSION");
            _sprayGlowMaterial.SetColor("_EmissionColor", glowColor * 2f);
        }
        
        EnableSoftParticles(_sprayGlowMaterial, 0.8f);
        
        return _sprayGlowMaterial;
    }
    
    public enum BlendMode
    {
        Alpha,
        Additive,
        SoftAdditive,
        Multiply
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
