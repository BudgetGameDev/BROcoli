using UnityEngine;

/// <summary>
/// Creates PBR-style materials for realistic spray particle effects.
/// Handles reflection, refraction simulation, and lighting interaction.
/// Materials are cached after first creation to avoid shader compilation hitches.
/// Call PrewarmAll() during loading to avoid runtime stutters.
/// </summary>
public static partial class SprayMaterialCreator
{
    private const string LicensedWaterSprayMaterialPath = "Integration/LicensedWaterSpray";

    // Cached materials
    private static Material _sprayCoreMaterial;
    private static Material _sprayMistMaterial;
    private static Material _sprayDropletMaterial;
    private static Material _sprayGlowMaterial;

    // Cached textures
    private static Texture2D _softCircleTexture;
    private static Texture2D _dropletTexture;

    /// <summary>
    /// Prewarm all materials and textures to avoid runtime shader compilation.
    /// Call this during scene loading or startup.
    /// </summary>
    public static void PrewarmAll()
    {
        GetSprayCoreMaterial();
        GetSprayMistMaterial();
        GetSprayDropletMaterial();
        GetSprayGlowMaterial();
        GetSoftCircleTexture();
        GetDropletTexture();
    }

    /// <summary>
    /// Get cached soft circle texture (creates if needed)
    /// </summary>
    public static Texture2D GetSoftCircleTexture(int size = 64)
    {
        if (_softCircleTexture == null)
            _softCircleTexture = CreateSoftCircleTexture(size);
        return _softCircleTexture;
    }

    /// <summary>
    /// Get cached droplet texture (creates if needed)
    /// </summary>
    public static Texture2D GetDropletTexture(int size = 32)
    {
        if (_dropletTexture == null)
            _dropletTexture = CreateDropletTexture(size);
        return _dropletTexture;
    }

    /// <summary>
    /// Get or create the main spray core material (dense center spray)
    /// </summary>
    public static Material GetSprayCoreMaterial()
    {
        if (_sprayCoreMaterial != null)
            return _sprayCoreMaterial;

        // Keep the gameplay-tuned particle system, but render its dense core with the
        // acquired Stylized Water Effect Pack shader when licensed assets are present.
        Material licensedTemplate = Resources.Load<Material>(LicensedWaterSprayMaterialPath);
        if (licensedTemplate != null)
        {
            _sprayCoreMaterial = new Material(licensedTemplate);
            _sprayCoreMaterial.name = "SprayCoreMaterial (Stylized Water Effect Pack)";
            return _sprayCoreMaterial;
        }

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
        if (_sprayMistMaterial != null)
            return _sprayMistMaterial;

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
        if (_sprayDropletMaterial != null)
            return _sprayDropletMaterial;

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
        if (_sprayGlowMaterial != null)
            return _sprayGlowMaterial;

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
}
