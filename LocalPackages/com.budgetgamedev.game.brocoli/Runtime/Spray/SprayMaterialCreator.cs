using System;
using BudgetGameDev.Games.Brocoli.Rendering;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Creates PBR-style materials for realistic spray particle effects.
    /// Handles reflection, refraction simulation, and lighting interaction.
    /// Materials are cached after first creation to avoid shader compilation hitches.
    /// Call PrewarmAll() during loading to avoid runtime stutters.
    /// </summary>
    public static partial class SprayMaterialCreator
    {
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
        public static Texture2D GetSoftCircleTexture(int size = 96)
        {
            if (_softCircleTexture == null)
                _softCircleTexture = CreateSoftCircleTexture(size);
            return _softCircleTexture;
        }

        /// <summary>
        /// Get cached droplet texture (creates if needed)
        /// </summary>
        public static Texture2D GetDropletTexture(int size = 64)
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

            // The spray needs a conventional particle shader that multiplies the
            // procedural soft-circle alpha. The licensed water Shader Graph is designed
            // for its own flipbook and renders these runtime billboards as hard squares.
            Shader shader = ResolveShader(BrocoliShaders.ParticleUnlit);

            _sprayCoreMaterial = new Material(shader);
            _sprayCoreMaterial.name = "SprayCoreMaterial";

            ConfigureParticleBlending(_sprayCoreMaterial, BlendMode.SoftAdditive);

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

            Shader shader = ResolveShader(BrocoliShaders.ParticleUnlit);

            _sprayMistMaterial = new Material(shader);
            _sprayMistMaterial.name = "SprayMistMaterial";

            // Soft additive for fog effect
            ConfigureParticleBlending(_sprayMistMaterial, BlendMode.SoftAdditive);

            // Softer, more transparent mist
            Color mistColor = new Color(0.8f, 0.9f, 1f, 0.5f);
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
            Shader shader = ResolveShader(BrocoliShaders.ParticleLit);

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

            Shader shader = ResolveShader(BrocoliShaders.ParticleUnlit);

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

        /// <summary>
        /// One of BROcoli's own particle graphs, which compile for both pipelines.
        /// <c>Sprites/Default</c> is the last resort: an engine builtin that resolves under
        /// either pipeline, so a missing graph costs the spray its look rather than replacing
        /// it with magenta mid-fight.
        /// </summary>
        internal static Shader ResolveShader(string shaderName) =>
            ResolveShader(shaderName, BrocoliShaders.Resolve, Shader.Find);

        /// <summary><see cref="ResolveShader(string)"/> with both lookups injected, for tests.</summary>
        internal static Shader ResolveShader(
            string shaderName,
            Func<string, Shader> resolve,
            Func<string, Shader> find
        ) => resolve(shaderName) ?? find("Sprites/Default");
    }
}
