using System;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>
    /// The naming convention that splits a level into one scene of game content and one scene
    /// of rendering data per pipeline.
    ///
    /// A level called <c>Foo</c> is authored as <c>Foo_Common</c>, which holds everything the
    /// game is -- geometry, gameplay objects, navigation, audio, interface -- plus
    /// <c>Foo_URP</c> and <c>Foo_HDRP</c>, which hold nothing but rendering data: volumes,
    /// their profiles, reflection probes, and light settings in that pipeline's units. Only
    /// the common scene and the running pipeline's scene are ever loaded together, so the
    /// pipelines cannot see each other's data and neither can drift into gameplay.
    /// </summary>
    public static class RenderingSceneNames
    {
        /// <summary>The suffix on the scene holding a level's game content.</summary>
        public const string CommonSuffix = "_Common";

        /// <summary>The suffix on Universal's rendering scene.</summary>
        public const string UniversalSuffix = "_URP";

        /// <summary>The suffix on High Definition's rendering scene.</summary>
        public const string HighDefinitionSuffix = "_HDRP";

        /// <summary>
        /// The suffix belonging to <paramref name="pipeline"/>, or null when it has no
        /// rendering scene of its own.
        /// </summary>
        public static string SuffixFor(RenderPipelineKind pipeline) =>
            pipeline switch
            {
                RenderPipelineKind.Universal => UniversalSuffix,
                RenderPipelineKind.HighDefinition => HighDefinitionSuffix,
                _ => null,
            };

        /// <summary>
        /// The level name behind <paramref name="sceneName"/>, with any of the three suffixes
        /// removed. A scene that carries none is already the level name.
        /// </summary>
        public static string LevelOf(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return sceneName;

            foreach (string suffix in new[] { CommonSuffix, UniversalSuffix, HighDefinitionSuffix })
            {
                if (sceneName.EndsWith(suffix, StringComparison.Ordinal))
                    return sceneName.Substring(0, sceneName.Length - suffix.Length);
            }

            return sceneName;
        }

        /// <summary>
        /// The rendering scene <paramref name="pipeline"/> should load alongside
        /// <paramref name="sceneName"/>, or null when that pipeline has none. Accepts either
        /// the level name or the common scene's name, so a caller does not have to know which
        /// one it is holding.
        /// </summary>
        public static string RenderingSceneFor(string sceneName, RenderPipelineKind pipeline)
        {
            string suffix = SuffixFor(pipeline);
            if (suffix == null || string.IsNullOrEmpty(sceneName))
                return null;
            return LevelOf(sceneName) + suffix;
        }

        /// <summary>The common scene's name for <paramref name="sceneName"/>.</summary>
        public static string CommonSceneFor(string sceneName) =>
            string.IsNullOrEmpty(sceneName) ? sceneName : LevelOf(sceneName) + CommonSuffix;
    }
}
