using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>
    /// Finds the active pipeline's front end, and is the only place gameplay and shared code
    /// look one up. Each pipeline ships its front end in its own assembly; this picks the one
    /// whose pipeline is actually rendering, so nothing outside those assemblies names a
    /// pipeline.
    ///
    /// A build renders through one pipeline for its whole life, but the Editor does not: both
    /// pipelines' assemblies are loaded and the quality level decides which one renders, so
    /// the answer changes when the level does. The lookup is therefore remembered against the
    /// pipeline it was made for and made again when that changes, rather than once forever --
    /// caching it outright leaves High Definition lighting its torches through Universal's
    /// front end, which states intensities in the wrong units.
    ///
    /// Front ends announce themselves through <see cref="Register(IHdrGradeFrontEnd)"/> as the
    /// runtime starts. They have to: nothing in the game references either front-end assembly
    /// by name -- that is the point of the seam -- so a player build strips both, and searching
    /// the loaded assemblies finds nothing. A dungeon lit by a front end that was never there
    /// keeps the intensities its prefabs were authored with, which on High Definition are
    /// lumens read as candela: a black room with the flames still burning in it.
    /// </summary>
    public static class RenderPipelineFrontEnd
    {
        private static readonly Dictionary<RenderPipelineKind, IHdrGradeFrontEnd> registeredGrades =
            new();
        private static readonly Dictionary<RenderPipelineKind, ILightingFrontEnd> registeredLighting =
            new();

        private static IHdrGradeFrontEnd hdrGrade;
        private static RenderPipelineKind searchedFor;
        private static bool hdrGradeOverridden;
        private static ILightingFrontEnd lighting;
        private static RenderPipelineKind searchedLightingFor;
        private static bool lightingOverridden;

        /// <summary>
        /// Announces a pipeline's HDR grade. Called from that pipeline's own assembly as the
        /// runtime starts; registering the same pipeline twice replaces the earlier entry, so a
        /// domain reload leaves one of each rather than a pile.
        /// </summary>
        public static void Register(IHdrGradeFrontEnd frontEnd)
        {
            if (frontEnd == null)
                return;

            registeredGrades[frontEnd.Pipeline] = frontEnd;
            searchedFor = RenderPipelineKind.Unknown;
        }

        /// <summary>Same, for a pipeline's lighting.</summary>
        public static void Register(ILightingFrontEnd frontEnd)
        {
            if (frontEnd == null)
                return;

            registeredLighting[frontEnd.Pipeline] = frontEnd;
            searchedLightingFor = RenderPipelineKind.Unknown;
        }

        /// <summary>What has announced itself so far, for a startup line in the log.</summary>
        public static string RegisteredSummary =>
            $"grade: [{string.Join(", ", registeredGrades.Keys)}], "
            + $"lighting: [{string.Join(", ", registeredLighting.Keys)}]";

        /// <summary>
        /// Writes down what is rendering and what answered for it. A player that lost its front
        /// ends still runs, and looks like a dungeon someone forgot to light rather than like a
        /// failure, so the log is where that has to be visible.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen
        )]
        internal static void LogWhatIsRendering() =>
            UnityEngine.Debug.Log(
                $"[Rendering] {RenderPipelineProbe.Current} is rendering; front ends -- "
                    + RegisteredSummary
            );

        /// <summary>
        /// The active pipeline's HDR grade, or null when the build's pipeline supplies none.
        /// Callers treat null as "no grade to drive" rather than as an error.
        /// </summary>
        public static IHdrGradeFrontEnd HdrGrade
        {
            get
            {
                if (hdrGradeOverridden)
                    return hdrGrade;

                RenderPipelineKind active = RenderPipelineProbe.Current;
                if (searchedFor != active)
                {
                    searchedFor = active;
                    hdrGrade =
                        Registered(registeredGrades, active)
                        ?? Discover<IHdrGradeFrontEnd>(front => front.Pipeline, active);
                }

                return hdrGrade;
            }
        }

        /// <summary>
        /// The active pipeline's lighting, or null when the build's pipeline supplies none.
        /// </summary>
        public static ILightingFrontEnd Lighting
        {
            get
            {
                if (lightingOverridden)
                    return lighting;

                RenderPipelineKind active = RenderPipelineProbe.Current;
                if (searchedLightingFor != active)
                {
                    searchedLightingFor = active;
                    lighting =
                        Registered(registeredLighting, active)
                        ?? Discover<ILightingFrontEnd>(front => front.Pipeline, active);
                }

                return lighting;
            }
        }

        /// <summary>
        /// Forces <paramref name="frontEnd"/> in, bypassing discovery. Tests use this to drive
        /// the grade without standing up a pipeline asset; passing null restores discovery.
        /// </summary>
        internal static void OverrideForTests(IHdrGradeFrontEnd frontEnd)
        {
            hdrGrade = frontEnd;
            hdrGradeOverridden = frontEnd != null;
            searchedFor = RenderPipelineKind.Unknown;
        }

        /// <summary>Same, for the lighting front end.</summary>
        internal static void OverrideForTests(ILightingFrontEnd frontEnd)
        {
            lighting = frontEnd;
            lightingOverridden = frontEnd != null;
            searchedLightingFor = RenderPipelineKind.Unknown;
        }

        /// <summary>What announced itself for <paramref name="active"/>, if anything did.</summary>
        private static T Registered<T>(Dictionary<RenderPipelineKind, T> registry, RenderPipelineKind active)
            where T : class =>
            registry.TryGetValue(active, out T frontEnd) ? frontEnd : null;

        /// <summary>
        /// The single implementation of <typeparamref name="T"/> whose pipeline is the active
        /// one, found by looking through what is loaded. This is the Editor's path: outside play
        /// mode nothing has registered yet, and both pipelines' assemblies are loaded anyway. In
        /// a player it finds nothing, and registration is what answers instead.
        /// </summary>
        private static T Discover<T>(
            Func<T, RenderPipelineKind> pipelineOf,
            RenderPipelineKind active
        )
            where T : class
        {
            if (active == RenderPipelineKind.Unknown)
                return null;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException loadFailure)
                {
                    // A pipeline package can be present but not fully loadable. Its front end
                    // is simply not a candidate; the other pipeline's still is.
                    types = loadFailure.Types.Where(type => type != null).ToArray();
                }

                foreach (Type type in types)
                {
                    if (type.IsAbstract || type.IsInterface || !typeof(T).IsAssignableFrom(type))
                        continue;
                    if (type.GetConstructor(Type.EmptyTypes) == null)
                        continue;

                    var candidate = (T)Activator.CreateInstance(type);
                    if (pipelineOf(candidate) == active)
                        return candidate;
                }
            }

            return null;
        }
    }
}
