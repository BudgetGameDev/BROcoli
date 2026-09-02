using System;
using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Rendering
{
    /// <summary>
    /// Every shader the game creates a material from, named once. Nothing else in the game
    /// spells a shader name.
    ///
    /// The shaders behind these names are BROcoli's own dual-target Shader Graphs: one asset
    /// per look, compiled for both Universal and High Definition. That is what lets the same
    /// gameplay code build the same material on the web build and the Windows one. Naming a
    /// pipeline's stock shader here instead -- "Universal Render Pipeline/Lit" -- would bind
    /// the game to one front end, which is exactly what these graphs exist to prevent.
    /// </summary>
    public static class BrocoliShaders
    {
        /// <summary>Opaque lit surfaces: props, walls, floors, pickups, the weapon.</summary>
        public const string Surface = "BROcoli/Surface";

        /// <summary>Lit particles that receive the dungeon's torchlight.</summary>
        public const string ParticleLit = "BROcoli/Particle Lit";

        /// <summary>Unlit particles, blended or additive: spray, mist, sparks.</summary>
        public const string ParticleUnlit = "BROcoli/Particle Unlit";

        /// <summary>
        /// Fire. Emissive only, authored in nits, and deliberately separate from whatever light
        /// the torch casts on the stone around it.
        /// </summary>
        public const string Flame = "BROcoli/Flame";

        /// <summary>The dungeon's standing water.</summary>
        public const string WaterVolume = "BROcoli/Water Volume";

        /// <summary>Ground fog and the drifting volumetric haze.</summary>
        public const string Fog = "BROcoli/Fog";

        /// <summary>Walls that fade out when they stand between the camera and the player.</summary>
        public const string DungeonOcclusionFade = "BROcoli/Dungeon Occlusion Fade";

        /// <summary>The experience orb's glow.</summary>
        public const string XpEnergyGlow = "BROcoli/XP Energy Glow";

        /// <summary>
        /// Where each shader lives under the game's own <c>Resources</c> tree. Resolution goes
        /// through the resource path first so an unloaded game cannot leave a name resolving to
        /// another package's shader, then falls back to the name for the edit-time case where
        /// the resource has not been imported yet.
        /// </summary>
        private static readonly Dictionary<string, string> ResourcePaths = new()
        {
            [Surface] = "Brocoli/Shaders/Surface",
            [ParticleLit] = "Brocoli/Shaders/ParticleLit",
            [ParticleUnlit] = "Brocoli/Shaders/ParticleUnlit",
            [Flame] = "Brocoli/Shaders/Flame",
            [WaterVolume] = "Brocoli/Shaders/WaterVolume",
            [Fog] = "Brocoli/Shaders/Fog",
            [DungeonOcclusionFade] = "Brocoli/Shaders/DungeonOcclusionFade",
            [XpEnergyGlow] = "Brocoli/Shaders/XpEnergyGlow",
        };

        private static readonly Dictionary<string, Shader> Cache = new();

        /// <summary>Every shader in the catalog, for the preloader to warm.</summary>
        public static IEnumerable<string> All => ResourcePaths.Keys;

        /// <summary>
        /// The shader behind <paramref name="shaderName"/>, or null when it is missing. A null
        /// return is worth reporting rather than silently substituting: these graphs are the
        /// game's own assets, so a missing one is a broken build, not a platform difference.
        /// </summary>
        public static Shader Resolve(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName))
                return null;
            if (Cache.TryGetValue(shaderName, out Shader cached) && cached != null)
                return cached;

            Shader shader = ResolveUncached(shaderName, Resources.Load<Shader>, Shader.Find);
            if (shader != null)
                Cache[shaderName] = shader;
            return shader;
        }

        /// <summary>
        /// <see cref="Resolve(string)"/> with its two lookups injected and without the cache,
        /// so tests can exercise the fallback order without an asset database and without one
        /// test's result leaking into the next.
        /// </summary>
        internal static Shader ResolveUncached(
            string shaderName,
            Func<string, Shader> load,
            Func<string, Shader> find
        )
        {
            if (string.IsNullOrEmpty(shaderName))
                return null;

            Shader shader = null;
            if (ResourcePaths.TryGetValue(shaderName, out string resourcePath))
                shader = load(resourcePath);
            return shader != null ? shader : find(shaderName);
        }

        /// <summary>Drops resolved shaders, so a test starts from a known state.</summary>
        internal static void ClearCache() => Cache.Clear();
    }
}
