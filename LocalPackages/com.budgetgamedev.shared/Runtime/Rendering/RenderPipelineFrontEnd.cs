using System;
using System.Linq;
using System.Reflection;

namespace BudgetGameDev.Shared.Rendering
{
    /// <summary>
    /// Finds the active pipeline's front end, and is the only place gameplay and shared code
    /// look one up. Each pipeline ships its front end in its own assembly; this picks the one
    /// whose pipeline is actually rendering, so nothing outside those assemblies names a
    /// pipeline.
    /// </summary>
    public static class RenderPipelineFrontEnd
    {
        private static IHdrGradeFrontEnd hdrGrade;
        private static bool searched;
        private static ILightingFrontEnd lighting;
        private static bool searchedLighting;

        /// <summary>
        /// The active pipeline's HDR grade, or null when the build's pipeline supplies none.
        /// Callers treat null as "no grade to drive" rather than as an error.
        /// </summary>
        public static IHdrGradeFrontEnd HdrGrade
        {
            get
            {
                if (hdrGrade == null && !searched)
                {
                    searched = true;
                    hdrGrade = Discover<IHdrGradeFrontEnd>(front => front.Pipeline);
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
                if (lighting == null && !searchedLighting)
                {
                    searchedLighting = true;
                    lighting = Discover<ILightingFrontEnd>(front => front.Pipeline);
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
            searched = frontEnd != null;
        }

        /// <summary>Same, for the lighting front end.</summary>
        internal static void OverrideForTests(ILightingFrontEnd frontEnd)
        {
            lighting = frontEnd;
            searchedLighting = frontEnd != null;
        }

        /// <summary>
        /// The single implementation of <typeparamref name="T"/> whose pipeline is the active
        /// one. Both pipelines' assemblies are present in this project, so the match is made on
        /// what is rendering rather than on what happens to be compiled in.
        /// </summary>
        private static T Discover<T>(Func<T, RenderPipelineKind> pipelineOf)
            where T : class
        {
            RenderPipelineKind active = RenderPipelineProbe.Current;
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
